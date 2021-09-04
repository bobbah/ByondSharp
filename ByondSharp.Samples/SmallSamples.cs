using ByondSharp.FFI;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace ByondSharp.Samples
{
    /// <summary>
    /// Brief examples for use of ByondSharp.
    /// </summary>
    public class SmallSamples
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

        [ByondFFI(Deferrable = true)]
        public static async Task<string> GetBYONDUserAsync(List<string> args)
        {
            if (args.Count == 0)
                return null;

            var ds = new BYONDDataService();
            var data = await ds.GetUserData(args[0], CancellationToken.None);
            return $"CKey: {data.CKey} -- Key: {data.Key} -- Joined: {data.Joined} -- Is member: {data.IsMember} -- Gender: {data.Gender}";
        }

        [ByondFFI]
        public static string AttachDebugger()
        {
#if DEBUG
            if (!Debugger.IsAttached)
            {
                Debugger.Launch();
            }
            return "Debugger launched.";
#else
            return "ByondSharp was built in release mode, the debugger is not available. Please re-build in debug mode.";
#endif
        }
    }
}
