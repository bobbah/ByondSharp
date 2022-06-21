using ByondSharp.FFI;
using ByondSharp.Samples.Util;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ByondSharp.Samples.Deferred;

/// <summary>
/// Flags for timers from /tg/station codebase
/// </summary>
[Flags]
public enum TimerFlag
{
    Unique          = (1 << 0),
    Override        = (1 << 1),
    ClientTime      = (1 << 2),
    Stoppable       = (1 << 3),
    NoHashWait      = (1 << 4),
    Loop            = (1 << 5)
}

/// <summary>
/// Timer subsystem replacement for /tg/-derived codebases.
/// </summary>
public class Timers
{
    private static readonly ConcurrentDictionary<ulong, Timer> TimerLookup = new ConcurrentDictionary<ulong, Timer>();
    private static readonly ConcurrentDictionary<string, Timer> HashLookup = new ConcurrentDictionary<string, Timer>();
    private static readonly PriorityQueue<Timer> TimersQueue = new PriorityQueue<Timer>(new ReverseComparer());
    private static readonly PriorityQueue<Timer> RealtimeTimersQueue = new PriorityQueue<Timer>(new ReverseComparer());
    private static Timer[] _dispatchedSet;
    private static ulong _currentId = 1;

    /// <summary>
    /// Gets the status string of the timer subsystem for the MC tab
    /// </summary>
    /// <returns>A string representing the status of the subsystem</returns>
    [ByondFFI]
    public static string Status() => $"BYOND Timers: {TimersQueue.Count}, RWT: {RealtimeTimersQueue.Count}";

    /// <summary>
    /// Fires the subsystem, returning all timers which are to be handled this tick
    /// </summary>
    /// <param name="args">Single item list, the current world.time</param>
    /// <returns>A semi-colon separated list of timer IDs to run, negative if a looped timer.</returns>
    [ByondFFI]
    public static async Task<string> Fire(List<string> args)
    {
        var currentTime = float.Parse(args[0]);
        var result = new ConcurrentBag<Timer>();
        var tasks = new Task[2];

        // Process normal timers
        tasks[0] = Task.Run(() => CollectCompletedTimers(TimersQueue, result, currentTime, false));

        // Process real-time timers
        tasks[1] = Task.Run(() => CollectCompletedTimers(RealtimeTimersQueue, result, currentTime, true));

        await Task.WhenAll(tasks);
        _dispatchedSet = result.ToArray();

        if (_dispatchedSet.Length == 0)
        {
            return null;
        }

        var toReturn = new StringBuilder($"{(_dispatchedSet[0].Flags.HasFlag(TimerFlag.Loop) ? "-" : "")}{_dispatchedSet[0].Id}");
        foreach (var t in _dispatchedSet)
        {
            toReturn.Append($";{(t.Flags.HasFlag(TimerFlag.Loop) ? "-" : "")}{t.Id}");
        }
        return toReturn.ToString();
    }

    /// <summary>
    /// Collects completed timers from the respective priority queue
    /// </summary>
    /// <param name="timers">The priority queue of timers to collect from</param>
    /// <param name="result">The ConcurrentBag to store the resulting timers in</param>
    /// <param name="currentTime">The current world.time</param>
    /// <param name="isRealtime">Boolean operator dictating if these timers are in real-world time</param>
    private static void CollectCompletedTimers(PriorityQueue<Timer> timers, ConcurrentBag<Timer> result, float currentTime, bool isRealtime)
    {
        while (timers.Count > 0 && (isRealtime ? timers.Peek().RealWorldTTR <= DateTime.UtcNow : timers.Peek().TimeToRun <= currentTime))
        {
            var timer = timers.Take();
            if (timer.Flags.HasFlag(TimerFlag.Loop))
            {
                var copy = timer.Copy();

                // Update looped time
                if (isRealtime)
                {
                    copy.RealWorldTTR = DateTime.UtcNow.AddSeconds(copy.Wait / 10);
                }
                else
                {
                    copy.TimeToRun = currentTime + copy.Wait;
                }

                // Keep track of the new copy
                timers.Add(copy);
                TimerLookup.AddOrUpdate(copy.Id, copy, (_, _) => copy);
                if (copy.Hash != null)
                    HashLookup.AddOrUpdate(copy.Hash, copy, (_, _) => copy);
            }
            else
            {
                TimerLookup.Remove(timer.Id, out _);
                if (timer.Hash != null)
                    HashLookup.Remove(timer.Hash, out _);
            }
            result.Add(timer);
        }
    }

    /// <summary>
    /// Reports timers that were not fired in time back to the timer subsystem
    /// </summary>
    /// <param name="args">The first non-fired timer</param>
    /// <remarks>Due to issues with DNNE, this method requires returning a string to avoid memory access violations.</remarks>
    [ByondFFI]
    public static string ReportIncompleteTimers(List<string> args)
    {
        if (_dispatchedSet == null)
            return null;

        var lastDispatch = _dispatchedSet.AsSpan();
        var lastId = ulong.Parse(args[0]);
        var foundLast = false;
        foreach (var timer in lastDispatch)
        {
            if (!foundLast && timer.Id == lastId)
                foundLast = true;
            else if (!foundLast)
                continue;

            var t = timer;

            // Add to respective queue
            var selectedQueue = t.IsRealTime ? RealtimeTimersQueue : TimersQueue;

            // Remove the previously-added looped timer from the queue if we are re-adding the old timer
            if (t.Flags.HasFlag(TimerFlag.Loop) && TimerLookup.TryGetValue(t.Id, out var prevTimer))
            {
                selectedQueue.Remove(prevTimer);
            }

            if (t.Hash != null)
                HashLookup.AddOrUpdate(t.Hash, t, (_, _) => t);
            TimerLookup.AddOrUpdate(t.Id, t, (_, _) => t);
            selectedQueue.Add(t);
        }

        return null;
    }

    /// <summary>
    /// Creates a timer from a set of parameters
    /// </summary>
    /// <returns>The ID of the created timer, if created, or null otherwise</returns>
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

        if (timer.Flags.HasFlag(TimerFlag.ClientTime))
            timer.RealWorldTTR = DateTime.UtcNow.AddSeconds(timer.Wait / 10);
        var selectedQueue = timer.IsRealTime ? RealtimeTimersQueue : TimersQueue;

        if (timer.Hash != null)
        {
            if (HashLookup.TryGetValue(timer.Hash, out var prevTimer) && !timer.Flags.HasFlag(TimerFlag.Override))
            {
                return null;
            }
            else if (prevTimer != null && timer.Flags.HasFlag(TimerFlag.Unique))
            {
                timer.Id = prevTimer.Id;
                selectedQueue.Remove(prevTimer);
            }
        }

        if (timer.Id == default)
        {
            timer.Id = Interlocked.Increment(ref _currentId);
        }

        selectedQueue.Add(timer);
        TimerLookup.AddOrUpdate(timer.Id, timer, (_, _) => timer);
        if (timer.Hash != null)
            HashLookup.AddOrUpdate(timer.Hash, timer, (_, _) => timer);
        return $"{timer.Id}";
    }

    /// <summary>
    /// Deletes a timer by ID
    /// </summary>
    /// <returns>The ID of the timer if found and deleted</returns>
    [ByondFFI]
    public static string DeleteTimerById(List<string> args)
    {
        var id = ulong.Parse(args[0]);
        if (TimerLookup.TryGetValue(id, out var timer) && timer.Flags.HasFlag(TimerFlag.Stoppable))
        {
            DequeueTimer(timer);
            return $"{timer.Id}";
        }
        return null;
    }

    /// <summary>
    /// Deletes a timer by hash
    /// </summary>
    /// <returns>The ID of the timer if found and deleted</returns>
    [ByondFFI]
    public static string DeleteTimerByHash(List<string> args)
    {
        if (HashLookup.TryGetValue(args[0], out var timer) && timer.Flags.HasFlag(TimerFlag.Stoppable))
        {
            DequeueTimer(timer);
            return $"{timer.Id}";
        }
        return null;
    }

    /// <summary>
    /// Gets the remaining time for a timer
    /// </summary>
    /// <returns>The time in deciseconds if found, otherwise null</returns>
    [ByondFFI]
    public static string TimeLeft(List<string> args)
    {
        var worldTime = float.Parse(args[0]);
        var id = ulong.Parse(args[1]);
        if (TimerLookup.TryGetValue(id, out var timer))
        {
            return timer.IsRealTime
                ? ((timer.RealWorldTTR.Value - DateTime.UtcNow).TotalSeconds * 10.0).ToString(CultureInfo.InvariantCulture)
                : (timer.TimeToRun - worldTime).ToString(CultureInfo.InvariantCulture);
        }
        return null;
    }

    /// <summary>
    /// Immediately invokes a timer, removing it from the queue and returning the ID to be used to fire the callback.
    /// </summary>
    /// <returns>The ID of the timer if found</returns>
    [ByondFFI]
    public static string InvokeImmediately(List<string> args)
    {
        var id = ulong.Parse(args[0]);
        if (TimerLookup.TryGetValue(id, out var timer))
        {
            DequeueTimer(timer);
            return $"{timer.Id}";
        }
        return null;
    }

    /// <summary>
    /// Removes a timer from its respective priority queue, and removes it from the lookup dictionaries.
    /// </summary>
    /// <param name="timer">The timer to dequeue</param>
    private static void DequeueTimer(Timer timer)
    {
        TimerLookup.Remove(timer.Id, out _);
        if (timer.Hash != null)
            HashLookup.Remove(timer.Hash, out _);
        var selectedQueue = timer.IsRealTime ? RealtimeTimersQueue : TimersQueue;
        selectedQueue.Remove(timer);
    }
}

public record Timer
{
    public ulong Id;
    public string Hash;
    public string Callback;
    public float Wait;
    public string Source;
    public string Name;
    public TimerFlag Flags;
    public float TimeToRun;
    public DateTime? RealWorldTTR;
    public bool IsRealTime => RealWorldTTR.HasValue;
    public Timer Copy() => (Timer)MemberwiseClone();
}

public class ReverseComparer : IComparer<Timer>
{
    public int Compare(Timer x, Timer y)
    {
        if (x is null || y is null)
            return x is null && y is null ? 0 : (x == null ? -1 : 1);
        if (x.Flags.HasFlag(TimerFlag.ClientTime))
            return x.RealWorldTTR == y.RealWorldTTR ? 0 : (x.RealWorldTTR > y.RealWorldTTR ? -1 : 1);
        return x.TimeToRun == y.TimeToRun ? 0 : (x.TimeToRun > y.TimeToRun ? -1 : 1);
    }
}