using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ByondSharpGenerator
{
    [Generator]
    public class FFIGenerator : ISourceGenerator
    {
        private static readonly DiagnosticDescriptor NonStaticMethodError = new DiagnosticDescriptor(id: "BSFFIGEN001",
                                                                                                title: "Exported method must be static",
                                                                                                messageFormat: "Method '{0}' must be static to be exported as a FFI for BYOND.",
                                                                                                category: "ByondSharpGenerator",
                                                                                                DiagnosticSeverity.Error,
                                                                                                isEnabledByDefault: true);
        private static readonly DiagnosticDescriptor InvalidReturnTypeError = new DiagnosticDescriptor(id: "BSFFIGEN002",
                                                                                                title: "Invalid return type for exported method",
                                                                                                messageFormat: "Method '{0}' must have a return type of string or void to be exported as a FFI for BYOND.",
                                                                                                category: "ByondSharpGenerator",
                                                                                                DiagnosticSeverity.Error,
                                                                                                isEnabledByDefault: true);
        private static readonly DiagnosticDescriptor TooManyParametersError = new DiagnosticDescriptor(id: "BSFFIGEN003",
                                                                                                title: "Too many parameters provided for exported method",
                                                                                                messageFormat: "Method '{0}' must have zero or one arguments to be exported as a FFI for BYOND.",
                                                                                                category: "ByondSharpGenerator",
                                                                                                DiagnosticSeverity.Error,
                                                                                                isEnabledByDefault: true);
        private static readonly DiagnosticDescriptor InvalidParameterError = new DiagnosticDescriptor(id: "BSFFIGEN004",
                                                                                                title: "Invalid return type for exported method",
                                                                                                messageFormat: "Method '{0}' must have no arguments or a single argument of List<string> to be exported as a FFI for BYOND.",
                                                                                                category: "ByondSharpGenerator",
                                                                                                DiagnosticSeverity.Error,
                                                                                                isEnabledByDefault: true);

        public void Execute(GeneratorExecutionContext context)
        {
            if (!(context.SyntaxReceiver is SyntaxReceiver receiver))
                return;

            // Check that each method is actually tagged with ByondFFIAttribute
            INamedTypeSymbol attrSymbol = context.Compilation.GetTypeByMetadataName("ByondSharp.FFI.ByondFFIAttribute");
            INamedTypeSymbol listSymbol = context.Compilation.GetTypeByMetadataName("System.Collections.Generic.List`1");
            List<IMethodSymbol> symbols = new List<IMethodSymbol>();
            foreach (MethodDeclarationSyntax method in receiver.CandidateFields)
            {
                SemanticModel model = context.Compilation.GetSemanticModel(method.SyntaxTree);
                IMethodSymbol methodSym = model.GetDeclaredSymbol(method);
                if (methodSym.GetAttributes().Any(attr => attr.AttributeClass.Equals(attrSymbol, SymbolEqualityComparer.Default)))
                {
                    // Check the validity of the attribute on this method, first by ensuring it is static
                    if (!methodSym.IsStatic)
                    {
                        context.ReportDiagnostic(Diagnostic.Create(NonStaticMethodError, method.GetLocation(), new[] { methodSym.Name }));
                        continue;
                    }

                    // Check for valid return type
                    if (!methodSym.ReturnsVoid && methodSym.ReturnType.ToDisplayString() != "string")
                    {
                        context.ReportDiagnostic(Diagnostic.Create(InvalidReturnTypeError, method.GetLocation(), new[] { methodSym.Name }));
                        continue;
                    }

                    // Check for empty or 1 param
                    if (method.ParameterList.Parameters.Count != 0)
                    {
                        if (methodSym.Parameters.Count() > 1)
                        {
                            context.ReportDiagnostic(Diagnostic.Create(TooManyParametersError, method.GetLocation(), new[] { methodSym.Name }));
                            continue;
                        }

                        // If there is a param present, it needs to be a List<string>
                        if (method.ParameterList.Parameters[0].Type.ToString() != "List<string>")
                        {
                            context.ReportDiagnostic(Diagnostic.Create(InvalidParameterError, method.GetLocation(), new[] { methodSym.Name }));
                            continue;
                        }
                    }

                    // Add the symbol if it passes all these checks
                    symbols.Add(model.GetDeclaredSymbol(method));
                }
            }

            // Generate additional source code, and add to the compiler
            var source = ProcessMethods(symbols);
            context.AddSource("FFIExports_auto.cs", SourceText.From(source, Encoding.UTF8));
        }

        /// <summary>
        /// Generate wrappers for a collection of methods so that they can be called by BYOND through FFI
        /// </summary>
        /// <param name="methods">The methods to wrap</param>
        /// <returns>The generated source code, including wrappers</returns>
        private static string ProcessMethods(List<IMethodSymbol> methods)
        {
            var source = new StringBuilder($@"
using System;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
");

            source.Append($@"
namespace ByondSharp
{{
    public class _FFIExports
    {{");

            foreach (var method in methods)
            {
                source.Append($@"
        [UnmanagedCallersOnly(CallConvs = new[] {{ typeof(CallConvCdecl) }}, EntryPoint = ""{method.Name}"")]
        public static {(method.ReturnsVoid ? "void" : "IntPtr")} {method.Name}__FFIWrapper(int numArgs, IntPtr argPtr)
        {{");
                if (!method.Parameters.IsEmpty)
                {
                    source.Append($@"
            // Boilerplate to get data passed
            string[] args = new string[numArgs];
            IntPtr[] argPtrs = new IntPtr[numArgs];
            Marshal.Copy(argPtr, argPtrs, 0, numArgs);
            for (var x = 0; x < numArgs; x++)
            {{
                args[x] = Marshal.PtrToStringUTF8(argPtrs[x]);
            }}");
                }


                if (!method.ReturnsVoid)
                {
                    source.Append($@"
            return ByondSharp.FFI.ByondFFI.FFIReturn({method.ContainingSymbol.ToDisplayString()}.{method.Name}({(method.Parameters.IsEmpty ? "" : "args.ToList()")}));");
                }
                else
                {
                    source.Append($@"
            {method.ContainingSymbol.ToDisplayString()}.{method.Name}({(method.Parameters.IsEmpty ? "" : "args.ToList()")});");
                }

                source.Append(@"
        }
");
            }

            source.Append(@"    }
}");

            return source.ToString();
        }

        public void Initialize(GeneratorInitializationContext context)
        {
            context.RegisterForSyntaxNotifications(() => new SyntaxReceiver());
        }
    }

    public class SyntaxReceiver : ISyntaxReceiver
    {
        public List<MethodDeclarationSyntax> CandidateFields { get; } = new List<MethodDeclarationSyntax>();

        public void OnVisitSyntaxNode(SyntaxNode syntaxNode)
        {
            if (syntaxNode is MethodDeclarationSyntax methodDeclarationSyntax
                && methodDeclarationSyntax.AttributeLists.Count > 0)
            {
                CandidateFields.Add(methodDeclarationSyntax);
            }
        }
    }
}
