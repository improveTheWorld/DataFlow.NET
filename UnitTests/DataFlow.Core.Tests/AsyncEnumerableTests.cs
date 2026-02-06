using DataFlow.Framework;

using System.Runtime.CompilerServices;

using DataFlow.Extensions;
namespace DataFlow.Core.Tests;
public static class ExtensionsForTests
{
   
    // Helper: delayed async enumerable producing values with per-item delay
    public static async IAsyncEnumerable<int> DelayedRange(
        int start, int count, int delayMs,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        for (int i = 0; i < count; i++)
        {
            ct.ThrowIfCancellationRequested();
            if (delayMs > 0) await Task.Delay(delayMs, ct);
            yield return start + i;
        }
    }

    // Helper that throws at a given index
    public static async IAsyncEnumerable<int> ThrowsAt(
        int countBeforeThrow,
        Exception ex,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        for (int i = 0; i < countBeforeThrow; i++)
        {
            ct.ThrowIfCancellationRequested();
            await Task.Yield();
            yield return i;
        }
        await Task.Yield();
        throw ex;
    }
}

public class AsyncEnumerableTests
{
    [Fact]
    public async Task FirstAvailable_MergesAll_ItemsArriveFromAnySource()
    {
        var options = new UnifyOptions
        {
            Fairness = UnifyFairness.FirstAvailable,
            ErrorMode = UnifyErrorMode.FailFast
        };

        var fast = ExtensionsForTests.DelayedRange(100, 3, 5);
        var slow = ExtensionsForTests.DelayedRange(200, 3, 20);

        var flow = new UnifiedStream<int>(options)
            .Unify(fast, "fast")
            .Unify(slow, "slow");

        var results = new List<int>();
        await foreach (var x in flow)
            results.Add(x);

        // All values from both sources should appear
        Assert.Equal(6, results.Count);
        Assert.True(results.Contains(100));
        Assert.True(results.Contains(101));
        Assert.True(results.Contains(102));
        Assert.True(results.Contains(200));
        Assert.True(results.Contains(201));
        Assert.True(results.Contains(202));

        // Not asserting order strictly because FirstAvailable is non-deterministic across tasks.
    }

    [Fact]
    public async Task RoundRobin_ProvidesFairTurns()
    {
        var options = new UnifyOptions
        {
            Fairness = UnifyFairness.RoundRobin,
            ErrorMode = UnifyErrorMode.FailFast
        };

        // Make one source much faster than the other
        var fast = ExtensionsForTests.DelayedRange(0, 5, 1);
        var slow = ExtensionsForTests.DelayedRange(1000, 5, 10);

        var flow = new UnifiedStream<int>(options)
            .Unify(fast, "fast")
            .Unify(slow, "slow");

        var results = new List<int>();
        await foreach (var x in flow)
            results.Add(x);

        // Expect interleaving pattern approx alternating, though exact timing can vary.
        // At least, the first few should alternate sources rather than all fast first.
        Assert.Equal(10, results.Count);
        // Check that within first 4 items, we have at least one from each source twice
        var first4 = results.Take(4).ToList();
        Assert.Contains(first4, v => v < 1000);
        Assert.Contains(first4, v => v >= 1000);
    }

    [Fact]
    public async Task Predicate_FiltersPerSource()
    {
        var a = Enumerable.Range(0, 5).Async();
        var b = Enumerable.Range(100, 5).Async();

        var flow = new UnifiedStream<int>()
            .Unify(a, "A", x => x % 2 == 0)       // even from A
            .Unify(b, "B", x => (x % 2) == 1);    // odd from B

        var results = new List<int>();
        await foreach (var x in flow)
            results.Add(x);

        Assert.Equal(new[] { 0, 2, 4, 101, 103 }, results.OrderBy(v => v));
    }

    [Fact]
    public async Task FailFast_PropagatesSourceException()
    {
        var ex = new InvalidOperationException("boom");
        var good = Enumerable.Range(0, 3).Async();
        var bad = ExtensionsForTests.ThrowsAt(2, ex);

        var flow = new UnifiedStream<int>(new UnifyOptions
        {
            ErrorMode = UnifyErrorMode.FailFast
        })
        .Unify(good, "good")
        .Unify(bad, "bad");

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await foreach (var _ in flow) { /* iterate */ }
        });

        Assert.Contains("AsyncEnumerable source 'bad' failed.", thrown.Message);
        Assert.IsType<InvalidOperationException>(thrown.InnerException);
        Assert.Equal("boom", thrown.InnerException!.Message);
    }

    [Fact]
    public async Task ContinueOnError_DropsFailingSource_Continues()
    {
        var ex = new InvalidOperationException("bad-source");
        var bad = ExtensionsForTests.ThrowsAt(1, ex);
        var good = Enumerable.Range(100, 3).Async();

        var flow = new UnifiedStream<int>(new UnifyOptions
        {
            ErrorMode = UnifyErrorMode.ContinueOnError
        })
        .Unify(bad, "bad")
        .Unify(good, "good");

        var results = new List<int>();
        await foreach (var x in flow)
            results.Add(x);

        // Should have all goods; bad source dropped after throwing
        Assert.Equal(new[] { 0, 100, 101, 102 }, results.OrderBy(v => v));
    }

    [Fact]
    public async Task Cancellation_StopsEnumeration_PassesToSources()
    {
        using var cts = new CancellationTokenSource();
        var slow = ExtensionsForTests.DelayedRange(0, 10, 50, cts.Token);
        var flow = new UnifiedStream<int>().Unify(slow, "slow");

        var results = new List<int>();
        var enumerating = Task.Run(async () =>
        {
            await foreach (var x in flow.WithCancellation(cts.Token))
            {
                results.Add(x);
                if (results.Count == 2)
                    cts.Cancel(); // cancel mid-stream
            }
        });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await enumerating);
        Assert.InRange(results.Count, 1, 2);
    }

    [Fact]
    public async Task EmptyFlow_CompletesImmediately()
    {
        var flow = new UnifiedStream<int>();
        int count = 0;
        await foreach (var _ in flow) count++;
        Assert.Equal(0, count);
    }

    [Fact]
    public void ListenAndUnlistenBeforeFreeze_AreAllowed()
    {
        var flow = new UnifiedStream<int>();
        flow.Unify(Enumerable.Range(0, 1).Async(), "A");
        flow.Unify(Enumerable.Range(0, 1).Async(), "B");
        Assert.True(flow.Unlisten("A"));
        Assert.False(flow.Unlisten("A")); // already removed
        flow.Unify(Enumerable.Range(0, 1).Async(), "C");
    }

    [Fact]
    public async Task MutatingAfterEnumerationStarts_Throws()
    {
        var flow = new UnifiedStream<int>();
        flow.Unify(ExtensionsForTests.DelayedRange(0, 5, 100), "A");  // Slower source ensures mutation during enumeration

        var started = new TaskCompletionSource();
        var done = new TaskCompletionSource();

        var t = Task.Run(async () =>
        {
            started.SetResult();
            await foreach (var _ in flow) { /* drain */ }
            done.SetResult();
        });

        await started.Task;

        Assert.Throws<InvalidOperationException>(() => flow.Unify(Enumerable.Range(0, 1).Async(), "B"));
        Assert.Throws<InvalidOperationException>(() => flow.Unlisten("A"));

        await done.Task;
    }

    [Fact]
    public async Task MultipleConcurrentEnumerations_AreIndependent()
    {
        var flow = new UnifiedStream<int>();
        flow.Unify(ExtensionsForTests.DelayedRange(0, 3, 10), "S");

        var list1 = new List<int>();
        var list2 = new List<int>();

        var t1 = Task.Run(async () =>
        {
            await foreach (var x in flow) list1.Add(x);
        });

        var t2 = Task.Run(async () =>
        {
            await foreach (var x in flow) list2.Add(x);
        });

        await Task.WhenAll(t1, t2);

        Assert.Equal(new[] { 0, 1, 2 }, list1);
        Assert.Equal(new[] { 0, 1, 2 }, list2);
    }

    [Fact]
    public async Task FirstAvailable_PrefersReadyItems_HighThroughput()
    {
        var fast = ExtensionsForTests.DelayedRange(0, 100, 0);
        var slow = ExtensionsForTests.DelayedRange(1000, 5, 5);

        var flow = new UnifiedStream<int>(new UnifyOptions
        {
            Fairness = UnifyFairness.FirstAvailable
        }).Unify(fast, "fast").Unify(slow, "slow");

        var results = new List<int>();
        await foreach (var x in flow) results.Add(x);

        Assert.Equal(105, results.Count);
        Assert.True(results.Take(20).All(v => v < 1000)); // likely many fast values first
    }

    [Fact]
    public async Task RoundRobin_DoesNotStarveSlowSource()
    {
        var fast = ExtensionsForTests.DelayedRange(0, 5, 0);
        var slow = ExtensionsForTests.DelayedRange(1000, 2, 20);

        var flow = new UnifiedStream<int>(new UnifyOptions
        {
            Fairness = UnifyFairness.RoundRobin
        }).Unify(fast, "fast").Unify(slow, "slow");

        var results = new List<int>();
        await foreach (var x in flow) results.Add(x);

        Assert.Contains(1000, results);
        Assert.Contains(1001, results);
    }

    [Fact]
    public async Task BackgroundEnumerableAdapter_Works()
    {
        // Use runOnBackgroundThread = true adapter to simulate blocking producer
        var blocking = new BlockingEnumerable(200);

        var asyncSrc = blocking.Values.BufferAsync(runOnBackgroundThread: true);
        var flow = new UnifiedStream<int>().Unify(asyncSrc, "bg");

        var results = new List<int>();
        await foreach (var x in flow) results.Add(x);

        Assert.Equal(Enumerable.Range(0, 200).ToArray(), results.ToArray());
    }

    [Fact]
    public async Task ContinueOnError_WhenOneSourceCompletes_NormallyFinishes()
    {
        var a = Enumerable.Range(0, 3).Async();
        var b = Enumerable.Range(100, 2).Async();

        var flow = new UnifiedStream<int>(new UnifyOptions
        {
            ErrorMode = UnifyErrorMode.ContinueOnError
        }).Unify(a, "A").Unify(b, "B");

        var results = new List<int>();
        await foreach (var x in flow) results.Add(x);

        Assert.Equal(5, results.Count);
        Assert.Contains(0, results);
        Assert.Contains(101, results);
    }

    [Fact]
    public async Task SourceThatCancels_PropagatesCancellation()
    {
        using var cts = new CancellationTokenSource();
        var canceling = ExtensionsForTests.DelayedRange(0, 100, 1, cts.Token);
        var flow = new UnifiedStream<int>().Unify(canceling, "C");

        var task = Task.Run(async () =>
        {
            await foreach (var _ in flow) { cts.Cancel(); }
        });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
    }

    [Fact]
    public async Task Unlisten_RemovesByName_BeforeFreeze()
    {
        var flow = new UnifiedStream<int>();
        flow.Unify(ExtensionsForTests.DelayedRange(0, 5, 100), "A");  // Slower source ensures mutation during enumeration
        Assert.True(flow.Unlisten("A"));
        Assert.False(flow.Unlisten("A"));
        flow.Unify(Enumerable.Range(0, 1).Async(), "B");

        var results = new List<int>();
        await foreach (var x in flow) results.Add(x);

        Assert.Equal(new[] { 0 }, results.ToArray());
    }

    [Fact]
    public async Task ThreadSafety_ConcurrentListenUnlistenBeforeEnumeration_NoCorruption()
    {
        // Not thread-safe by contract, but ensure no crashes if sequentially applied before freeze
        var flow = new UnifiedStream<int>();
        var tasks = new List<Task>();
        for (int i = 0; i < 10; i++)
        {
            int idx = i;
            tasks.Add(Task.Run(() =>
            {
                // Sequential critical section could be guarded by caller; here we just simulate mild concurrency
                lock (flow)
                {
                    flow.Unify(Enumerable.Range(idx * 10, 3).Async(), $"S{idx}");
                    if (idx % 2 == 0) flow.Unlisten($"S{idx}");
                }
            }));
        }
        await Task.WhenAll(tasks);

        // Now enumerate
        var results = new List<int>();
        await foreach (var x in flow) results.Add(x);

        // Should only contain sources that weren’t removed (odd indices)
        var expected = Enumerable.Range(0, 10)
                                 .Where(i => i % 2 == 1)
                                 .SelectMany(i => Enumerable.Range(i * 10, 3))
                                 .ToArray();
        Array.Sort(expected);
        results.Sort();
        Assert.Equal(expected, results.ToArray());
    }

    [Fact]
    public async Task ConcurrentEnumerations_FromMultipleThreads_NoSharedStateCorruption()
    {
        var flow = new UnifiedStream<int>()
            .Unify(ExtensionsForTests.DelayedRange(0, 50, 1), "S");

        var c1 = new List<int>();
        var c2 = new List<int>();
        var c3 = new List<int>();

        var t1 = Task.Run(async () => { await foreach (var x in flow) c1.Add(x); });
        var t2 = Task.Run(async () => { await foreach (var x in flow) c2.Add(x); });
        var t3 = Task.Run(async () => { await foreach (var x in flow) c3.Add(x); });

        await Task.WhenAll(t1, t2, t3);

        Assert.Equal(Enumerable.Range(0, 50), c1);
        Assert.Equal(Enumerable.Range(0, 50), c2);
        Assert.Equal(Enumerable.Range(0, 50), c3);
    }

    private sealed class BlockingEnumerable
    {
        private readonly int _count;
        public IEnumerable<int> Values => Enumerate();
        public BlockingEnumerable(int count) { _count = count; }

        private IEnumerable<int> Enumerate()
        {
            for (int i = 0; i < _count; i++)
            {
                // Simulate occasional blocking
                if (i % 50 == 0) Thread.Sleep(5);
                yield return i;
            }
        }
    }
}
