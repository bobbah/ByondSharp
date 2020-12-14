using ByondSharp.FFI;
using ByondSharp.Util;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace ByondSharp.Deferred
{
    [Flags]
    public enum TimerFlag
    {
        Unique      = (1 << 0),
        Override    = (1 << 1),
        ClientTime  = (1 << 2),
        Stoppable   = (1 << 3),
        NoHashWait  = (1 << 4),
        Loop        = (1 << 5)
    }

    public class Timers
    {
        private static readonly PriorityQueue<Timer> _timers = new PriorityQueue<Timer>(new ReverseComparer());
        private static readonly PriorityQueue<Timer> _realtimeTimers = new PriorityQueue<Timer>(new ReverseComparer());
        private static ulong _currentId = 1;

        [ByondFFI]
        public static string Status()
        {
            return $"BYOND Timers: {_timers.Count}, RWT: {_realtimeTimers.Count}";
        }

        [ByondFFI]
        public static string Fire(List<string> args)
        {
            var currentTime = float.Parse(args[0]);
            var result = new List<Timer>();

            // Process normal timers
            while (_timers.Count > 0 && _timers.Peek().TimeToRun <= currentTime)
            {
                var timer = _timers.Take();
                if (timer.Flags.HasFlag(TimerFlag.Loop))
                {
                    timer.TimeToRun = currentTime + timer.Wait;
                    _timers.Add(timer);
                }
                result.Add(timer);
            }
               
            // Process real-time timers
            while (_realtimeTimers.Count > 0 && _realtimeTimers.Peek().RealWorldTTR <= DateTime.UtcNow)
            {
                var timer = _realtimeTimers.Take();
                if (timer.Flags.HasFlag(TimerFlag.Loop))
                {
                    timer.RealWorldTTR = DateTime.UtcNow.AddSeconds(timer.Wait / 10);
                    _realtimeTimers.Add(timer);
                }
                result.Add(timer);
            }
                
            return string.Join(";", result.Select(x => $"{(x.Flags.HasFlag(TimerFlag.Loop) ? 0 : x.ID)}|{x.Callback}"));
        }

        [ByondFFI]
        public static string CreateTimer(List<string> args)
        {
            var timer = new Timer()
            {
                Hash = args[0],
                Callback = args[1],
                TimeToRun = float.Parse(args[2]),
                Wait = float.Parse(args[3]),
                Source = args[4],
                Name = args[5],
                Flags = (TimerFlag)int.Parse(args[6])
            };

            string deletedCallback = null;
            if (timer.Flags.HasFlag(TimerFlag.ClientTime))
            {
                timer.RealWorldTTR = DateTime.UtcNow.AddSeconds(timer.Wait / 10);

                if (timer.Hash != null)
                {
                    var prevTimer = _realtimeTimers.FirstOrDefault(x => x.Hash == timer.Hash);
                    if (prevTimer != null && !timer.Flags.HasFlag(TimerFlag.Override))
                    {
                        return null;
                    }
                    else if (prevTimer != null && timer.Flags.HasFlag(TimerFlag.Unique))
                    {
                        timer.ID = prevTimer.ID;
                        deletedCallback = prevTimer.Callback;
                        _realtimeTimers.Remove(prevTimer);
                    }
                }

                _realtimeTimers.Add(timer);
            }
            else
            {
                if (timer.Hash != null)
                {
                    var prevTimer = _timers.FirstOrDefault(x => x.Hash == timer.Hash);
                    if (prevTimer != null && !timer.Flags.HasFlag(TimerFlag.Override))
                    {
                        return null;
                    }
                    else if (prevTimer != null && timer.Flags.HasFlag(TimerFlag.Unique))
                    {
                        timer.ID = prevTimer.ID;
                        deletedCallback = prevTimer.Callback;
                        _timers.Remove(prevTimer);
                    }
                }

                _timers.Add(timer);
            }

            if (timer.ID == default)
            {
                timer.ID = Interlocked.Increment(ref _currentId);
            }

            return $"{timer.ID}{(deletedCallback != null ? $"|{deletedCallback}" : "")}";
        }

        [ByondFFI]
        public static string DeleteTimerByID(List<string> args)
        {
            var id = ulong.Parse(args[0]);
            var timer = _timers.FirstOrDefault(x => x.ID == id && x.Flags.HasFlag(TimerFlag.Stoppable));
            if (timer != null)
            {
                _timers.Remove(timer);
                return $"{timer.ID}|{timer.Callback}";
            }

            var rwt = _realtimeTimers.FirstOrDefault(x => x.ID == id && x.Flags.HasFlag(TimerFlag.Stoppable));
            if (rwt != null)
            {
                _realtimeTimers.Remove(rwt);
                return $"{rwt.ID}|{rwt.Callback}";
            }

            return null;
        }

        [ByondFFI]
        public static string DeleteTimerByHash(List<string> args)
        {
            var timer = _timers.FirstOrDefault(x => x.Hash == args[0] && x.Flags.HasFlag(TimerFlag.Stoppable));
            if (timer != null)
            {
                _timers.Remove(timer);
                return "1";
            }

            var rwt = _realtimeTimers.FirstOrDefault(x => x.Hash == args[0] && x.Flags.HasFlag(TimerFlag.Stoppable));
            if (rwt != null)
            {
                _realtimeTimers.Remove(rwt);
                return "1";
            }

            return null;
        }

        [ByondFFI]
        public static string TimeLeft(List<string> args)
        {
            var worldTime = float.Parse(args[0]);
            var id = ulong.Parse(args[1]);
            var timer = _timers.FirstOrDefault(x => x.ID == id);
            if (timer != null)
            {
                return (timer.TimeToRun - worldTime).ToString();
            }

            var rwt = _realtimeTimers.FirstOrDefault(x => x.ID == id);
            if (rwt != null)
            {
                return ((rwt.RealWorldTTR.Value - DateTime.UtcNow).TotalSeconds * 10.0).ToString();
            }

            return null;
        }

        [ByondFFI]
        public static string InvokeImmediately(List<string> args)
        {
            var id = ulong.Parse(args[0]);
            var timer = _timers.FirstOrDefault(x => x.ID == id);
            if (timer != null)
            {
                _timers.Remove(timer);
                return $"{timer.ID}|{timer.Callback}";
            }

            var rwt = _realtimeTimers.FirstOrDefault(x => x.ID == id);
            if (rwt != null)
            {
                _realtimeTimers.Remove(rwt);
                return $"{rwt.ID}|{rwt.Callback}";
            }

            return null;
        }
    }

    public record Timer
    {
        public ulong ID;
        public string Hash;
        public string Callback;
        public float Wait;
        public string Source;
        public string Name;
        public TimerFlag Flags;
        public float TimeToRun;
        public DateTime? RealWorldTTR;
    }

    public class ReverseComparer : IComparer<Timer>
    {
        public int Compare(Timer x, Timer y)
        {
            if (x == null || y == null)
                return x == null && y == null ? 0 : (x == null ? -1 : 1);
            if (x.Flags.HasFlag(TimerFlag.ClientTime))
                return x.RealWorldTTR == y.RealWorldTTR ? 0 : (x.RealWorldTTR > y.RealWorldTTR ? -1 : 1);
            return x.TimeToRun == y.TimeToRun ? 0 : (x.TimeToRun > y.TimeToRun ? -1 : 1);
        }
    }
}
