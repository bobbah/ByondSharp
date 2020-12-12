using ByondSharp.FFI;
using System.Collections.Generic;

namespace ByondSharp
{
    /// <summary>
    /// Brief examples for use of ByondSharp.
    /// </summary>
    public class Samples
    {
        [ByondFFI]
        public static string RepeatMe(List<string> args)
        {
            return $"You said '{string.Join(", ", args)}'";
        }

        [ByondFFI]
        public static void DoNothing()
        {
            // We do nothing!
        }

        [ByondFFI]
        public static string DoNothingButReturnString()
        {
            return "You did it!";
        }
    }
}
