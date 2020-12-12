using ByondSharp.FFI;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

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

        [ByondFFI]
        public static async Task<string> GetBYONDUserAsync(List<string> args)
        {
            if (args.Count == 0)
                return null;

            var ds = new BYONDDataService();
            var data = await ds.GetUserData(args[0], CancellationToken.None);
            return $"CKey: {data.CKey} -- Key: {data.Key} -- Joined: {data.Joined} -- Is member: {data.IsMember} -- Gender: {data.Gender}";
        }
    }
}
