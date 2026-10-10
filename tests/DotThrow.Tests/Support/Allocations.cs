#if NET8_0_OR_GREATER
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Tracing;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Threading;
using Xunit;

namespace DotThrow.Tests.Support;

/// <summary>
/// Shared rules for the zero-allocation tests. They hold for an optimized (Release) build of
/// the library: a Debug build turns off JIT optimizations, so generic code boxes value types
/// for null checks. Run them with "dotnet test -c Release".
/// </summary>
public static class Allocations
{
    public static bool LibraryIsOptimized =>
        typeof(Guard<>).Assembly.GetCustomAttribute<DebuggableAttribute>()?.IsJITOptimizerDisabled != true;

    public static void SkipUnlessOptimized() =>
        Assert.SkipUnless(LibraryIsOptimized, "The DotThrow build is not optimized (Debug). Run with -c Release.");

    /// <summary>
    /// Runs the code until tiered compilation has replaced the first, unoptimized code (and the
    /// precompiled ReadyToRun code of the runtime libraries) with optimized code. The runtime
    /// waits about 100 ms after the last new method before it promotes methods, so a short warm
    /// up measures code that will not run in a real app. Real runtimes on Linux showed this:
    /// span searches of the BCL allocated until they were promoted.
    /// </summary>
    /// <param name="action">The code to warm up.</param>
    public static void WarmUp(Action action)
    {
        for (var round = 0; round < 3; round++)
        {
            for (var i = 0; i < 100; i++)
            {
                action();
            }

            Thread.Sleep(250);
        }
    }

    /// <summary>
    /// Returns the bytes this thread allocated for <paramref name="iterations"/> calls: the
    /// smallest of three rounds after a warm-up. While that is more than
    /// <paramref name="allowedPerCall"/> per call, it warms up and measures again (five times at
    /// most), because under load the runtime can still be replacing unoptimized code. An
    /// allocation that every call makes stays in every measurement.
    /// </summary>
    /// <param name="action">The code to measure.</param>
    /// <param name="iterations">The number of calls per round.</param>
    /// <param name="allowedPerCall">The bytes one call may allocate.</param>
    /// <returns>The smallest round, in bytes.</returns>
    public static long Measure(Action action, int iterations, long allowedPerCall = 0)
    {
        var smallest = long.MaxValue;
        for (var attempt = 0; attempt < 5 && smallest > allowedPerCall * iterations; attempt++)
        {
            WarmUp(action);
            for (var round = 0; round < 3; round++)
            {
                var before = GC.GetAllocatedBytesForCurrentThread();
                for (var i = 0; i < iterations; i++)
                {
                    action();
                }

                smallest = Math.Min(smallest, GC.GetAllocatedBytesForCurrentThread() - before);
            }
        }

        return smallest;
    }

    /// <summary>
    /// Fails the test when one call allocates more than <paramref name="allowedPerCall"/> bytes.
    /// Only on failure, the code runs again under a listener for the runtime's GC allocation
    /// events, and the message lists the types that were allocated (see <see cref="Explain"/>).
    /// </summary>
    /// <param name="action">The code to measure.</param>
    /// <param name="iterations">The number of calls per round.</param>
    /// <param name="allowedPerCall">The bytes one call may allocate.</param>
    public static void AssertAtMost(Action action, int iterations, long allowedPerCall = 0)
    {
        var allocated = Measure(action, iterations, allowedPerCall);
        if (allocated <= allowedPerCall * iterations)
        {
            return;
        }

        var perCall = (double)allocated / iterations;
        Assert.Fail(string.Format(
            CultureInfo.InvariantCulture,
            "Allocated {0} bytes over {1} calls ({2:0.##} bytes per call, {3} allowed). {4}",
            allocated,
            iterations,
            perCall,
            allowedPerCall,
            Explain(action)));
    }

    /// <summary>
    /// Runs the code under an <see cref="EventListener"/> for the "Microsoft-Windows-DotNETRuntime"
    /// provider (GC keyword, verbose) and summarizes the GCAllocationTick events of this thread:
    /// the runtime raises one about every 100 KB and names the type allocated at that moment, so
    /// the counts show what the code allocates. Used only after a failed measurement.
    /// </summary>
    /// <param name="action">The code that allocates.</param>
    /// <returns>A one-line summary for an assertion message.</returns>
    public static string Explain(Action action)
    {
        using var listener = new AllocationTickListener();

        // Turning on the runtime provider starts an event session; give it a moment.
        Thread.Sleep(200);
        AllocationMarker.Log.Mark();
        var calls = 0;
        var clock = Stopwatch.StartNew();
        while (listener.TicksOnMarkedThread < 20 && calls < 2_000_000 && clock.Elapsed < TimeSpan.FromSeconds(10))
        {
            for (var i = 0; i < 1000; i++)
            {
                action();
            }

            calls += 1000;
        }

        // Runtime events reach an in-process listener a little later than they happen.
        var last = -1;
        var waited = Stopwatch.StartNew();
        while (waited.Elapsed < TimeSpan.FromSeconds(5) && listener.TotalTicks != last)
        {
            last = listener.TotalTicks;
            Thread.Sleep(500);
        }

        return listener.Describe(calls);
    }

    // Written from the measuring thread, so the listener learns that thread's OS id.
    [EventSource(Name = MarkerName)]
    private sealed class AllocationMarker : EventSource
    {
        public const string MarkerName = "DotThrow-Tests-AllocationMarker";

        public static readonly AllocationMarker Log = new();

        [Event(1)]
        public void Mark() => WriteEvent(1);
    }

    private sealed class AllocationTickListener : EventListener
    {
        private const string RuntimeProvider = "Microsoft-Windows-DotNETRuntime";
        private const long GcKeyword = 0x1;
        private readonly object _gate = new();
        private readonly List<(long Thread, string Type)> _ticks = [];
        private long? _markedThread;

        public int TotalTicks
        {
            get
            {
                lock (_gate)
                {
                    return _ticks.Count;
                }
            }
        }

        public int TicksOnMarkedThread
        {
            get
            {
                lock (_gate)
                {
                    return _markedThread is { } thread ? _ticks.Count(t => t.Thread == thread) : 0;
                }
            }
        }

        public string Describe(int calls)
        {
            lock (_gate)
            {
                if (_ticks.Count == 0)
                {
                    return string.Format(
                        CultureInfo.InvariantCulture,
                        "No GCAllocationTick events reached the in-process listener after {0} more calls on {1}; the runtime may not deliver them in process here.",
                        calls,
                        System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription);
                }

                var mine = _markedThread is { } thread ? _ticks.Where(t => t.Thread == thread).ToList() : [];
                var (scope, ticks) = mine.Count > 0 ? ("this thread", mine) : ("all threads, this thread not identified", _ticks);
                var types = string.Join(", ", ticks
                    .GroupBy(t => t.Type)
                    .OrderByDescending(g => g.Count())
                    .Select(g => string.Format(CultureInfo.InvariantCulture, "{0} x{1}", g.Key, g.Count())));
                return string.Format(
                    CultureInfo.InvariantCulture,
                    "GC allocation ticks ({0}, {1} ticks over {2} more calls, one tick per about 100 KB): {3}.",
                    scope,
                    ticks.Count,
                    calls,
                    types);
            }
        }

        // Called from the base constructor too, before the fields above are set, so it uses
        // constants only.
        protected override void OnEventSourceCreated(EventSource eventSource)
        {
            if (eventSource.Name == RuntimeProvider)
            {
                EnableEvents(eventSource, EventLevel.Verbose, (EventKeywords)GcKeyword);
            }
            else if (eventSource.Name == AllocationMarker.MarkerName)
            {
                EnableEvents(eventSource, EventLevel.Verbose);
            }
        }

        protected override void OnEventWritten(EventWrittenEventArgs eventData)
        {
            if (eventData.EventSource.Name == AllocationMarker.MarkerName)
            {
                lock (_gate)
                {
                    _markedThread = eventData.OSThreadId;
                }

                return;
            }

            if (eventData.EventName is null || !eventData.EventName.StartsWith("GCAllocationTick", StringComparison.Ordinal))
            {
                return;
            }

            var index = eventData.PayloadNames?.IndexOf("TypeName") ?? -1;
            var type = index >= 0 ? eventData.Payload?[index] as string : null;
            lock (_gate)
            {
                _ticks.Add((eventData.OSThreadId, string.IsNullOrEmpty(type) ? "(unknown type)" : type));
            }
        }
    }
}
#endif
