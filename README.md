# ByondSharp

ByondSharp is a C# library providing easy interop with BYOND (DreamMaker).

It abstracts away the complexity of handling FFI with BYOND, and makes it easy for developers with any level of experience to create meaningful interop with BYOND from C#.

### Use

ByondSharp works by allowing a developer to write 'normal' C# code, without references to pointers or much consideration for the FFI aspect of their code, and tag their methods they need to export to BYOND with an attribute, ``ByondFFI``. Once tagged the method will be wrapped using source generation and exposed to CDecl calls, making it easily called from BYOND.

Important things you will need to run ByondSharp in BYOND:
- [.NET 5.0 or greater runtimes](https://dotnet.microsoft.com/download/dotnet/5.0)

That's it. Really.

Once you have .NET 5.0 or greater runtimes, writing the code is pretty straight forward. I would recommend looking at the [samples](https://github.com/bobbahbrown/ByondSharp/blob/master/ByondSharp/Samples.cs), especially the [timer sample](https://github.com/bobbahbrown/ByondSharp/blob/master/ByondSharp/TimerSample.cs), for an essential introduction to the format of these functions.

At a bare minimum, exported functions must:
- Have the ``ByondFFI`` attribute
- Be ``public`` and ``static`` methods
- Return ``void`` or ``string``
- Have zero arguments, or have one argument which is a ``List<string>``

Aside from that you're free to do as you please.

See below for the timer sample as a point of reference.

```csharp
namespace ByondSharp
{
    /// <summary>
    /// Stateful example of operations possible with ByondSharp. With these two calls, one can maintain 
    /// a stopwatch and keep track of the passing of time.
    /// </summary>
    public class TimerSample
    {
        private static Stopwatch _sw;

        [ByondFFI]
        public static void StartStopwatch()
        {
            if (_sw is not null)
            {
                return;
            }

            _sw = new Stopwatch();
            _sw.Start();
        }

        [ByondFFI]
        public static string GetStopwatchStatus()
        {
            return _sw.Elapsed.ToString();
        }
    }
}
```

### How to build

Knowing how to run it and the brief rules for writing code is not very useful without being able to build the library, so how is it done?

Quite simple: in Visual Studio, you will build the ByondSharp project in debug or release configuration **for x86 CPUs** (BYOND is a 32-bit application, a 64-bit compiled DLL will not do much!)

Once this is done, you will generate several files within your ``ByondSharp\bin\x86\[Debug/Release]\net5.0\win-x86`` directory, but there are only three you need to have in your BYOND directory:
- ``ByondSharp.dll`` (this contains the proper .NET library code)
- ``ByondSharpNE.dll`` (this contains the code to allow for native code to call our library)
- ``ByondSharp.runtimeconfig.json`` (this is required for the runtime to initialize properly)

All other files can be discarded, but these three __must__ be present in a directory that BYOND can access.

### Using in BYOND

To use external DLLs in BYOND, simply use the ``call()()`` proc. For example:

```dm
#define byondsharp_startstopwatch(options)		call("byondsharpNE", "StartStopwatch")(options)
#define byondsharp_getstopwatchstatus(options)	call("byondsharpNE", "GetStopwatchStatus")(options)

/world/New()
	byondsharp_startstopwatch(null)
	while (1)
		var/timelog = byondsharp_getstopwatchstatus(null)
		world.log << timelog
		world.log << byondsharp_repeatme(timelog)
		sleep(1)
```

### What about Linux?

I can't see why you wouldn't be able to change the targeted output to be for linux, should be possible as .NET is cross-platform without issue.