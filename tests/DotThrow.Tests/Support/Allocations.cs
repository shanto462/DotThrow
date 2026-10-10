#if NET8_0_OR_GREATER
using System;
using System.Diagnostics;
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
}
#endif
