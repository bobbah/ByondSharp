using ByondSharp.FFI;
using System.Diagnostics;

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
