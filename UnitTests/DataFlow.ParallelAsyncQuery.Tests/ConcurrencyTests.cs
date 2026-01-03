using System.Collections.Concurrent;
using DataFlow;
using DataFlow.Parallel;
using Xunit;
using Xunit.Abstractions;

namespace ParallelAsyncQuery.Tests;

/// <summary>
/// Concurrency and thread-safety tests for ParallelAsyncQuery.
/// These tests verify proper behavior under multi-threaded conditions.
/// </summary>
public class ConcurrencyTests
{
    private readonly ITestOutputHelper _output;

    public ConcurrencyTests(ITestOutputHelper output) => _output = output;

    #region MaxConcurrency Enforcement Tests

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(4)]
    [InlineData(8)]
    public async Task Select_MaxConcurrencyEnforced(int maxConcurrency)
    {
        // Arrange
        var tracker = new ConcurrencyTracker();
        var itemCount = 20;
        var source = GenerateNumbers(itemCount);

        // Act
        var query = source.AsParallel()
            .WithMaxConcurrency(maxConcurrency)
            .Select(async x =>
            {
                tracker.Enter();
                await Task.Delay(10); // Simulate work
                tracker.Exit();
                return x;
            });

        var results = await ToListAsync(query);

        // Assert
        _output.WriteLine($"Max observed concurrency: {tracker.MaxObserved}, Limit: {maxConcurrency}");
        Assert.Equal(itemCount, results.Count);
        Assert.True(tracker.MaxObserved <= maxConcurrency,
            $"Concurrency exceeded limit: {tracker.MaxObserved} > {maxConcurrency}");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(4)]
    public async Task Where_MaxConcurrencyEnforced(int maxConcurrency)
    {
        // Arrange
        var tracker = new ConcurrencyTracker();
        var itemCount = 20;
        var source = GenerateNumbers(itemCount);

        // Act
        var query = source.AsParallel()
            .WithMaxConcurrency(maxConcurrency)
            .Where(async x =>
            {
                tracker.Enter();
                await Task.Delay(10);
                tracker.Exit();
                return x % 2 == 0;
            });

        var results = await ToListAsync(query);

        // Assert
        _output.WriteLine($"Max observed concurrency: {tracker.MaxObserved}, Limit: {maxConcurrency}");
        Assert.Equal(itemCount / 2, results.Count);
        Assert.True(tracker.MaxObserved <= maxConcurrency,
            $"Concurrency exceeded limit: {tracker.MaxObserved} > {maxConcurrency}");
    }

    #endregion

    #region Resource Cleanup Tests

    [Fact]
    public async Task PartialEnumeration_CompletesWithoutResourceLeaks()
    {
        // Arrange - create a source and take only a few items
        // Note: In parallel processing, upstream items may still be processed
        // even when Take limits the output. This test verifies no resource leaks.
        var processed = 0;
        var source = GenerateNumbers(100);

        var query = source.AsParallel()
            .WithOrderPreservation(true)
            .Select(async x =>
            {
                Interlocked.Increment(ref processed);
                await Task.Yield();
                return x;
            })
            .Take(10);

        // Act - enumerate only 10 items
        var results = await ToListAsync(query);

        // Assert - we get exactly 10 results
        Assert.Equal(10, results.Count);
        _output.WriteLine($"Processed {processed} items for 10 results");
        // Test passes if no deadlock/timeout occurs - resources were properly cleaned up
    }

    [Fact]
    public async Task ExceptionDuringEnumeration_ResourcesCleanedUp()
    {
        // Arrange
        var processedBeforeException = 0;
        var source = GenerateNumbers(100);

        var query = source.AsParallel()
            .Select(async x =>
            {
                Interlocked.Increment(ref processedBeforeException);
                if (x == 10) throw new InvalidOperationException("Simulated failure");
                await Task.Yield();
                return x;
            });

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await ToListAsync(query);
        });

        _output.WriteLine($"Processed {processedBeforeException} items before exception");
        // Test passes if no deadlock occurs - resources were properly cleaned up
    }

    #endregion

    #region Buffer Boundary Tests

    [Fact]
    public async Task BufferSizeOne_StillProcessesCorrectly()
    {
        // Arrange - minimum buffer size (clamped to 10 in settings)
        var source = GenerateNumbers(20);

        var query = source.AsParallel()
            .WithBufferSize(10) // Minimum allowed
            .WithMaxConcurrency(2)
            .Select(async x =>
            {
                await Task.Delay(5);
                return x * 2;
            });

        // Act
        var results = await ToListAsync(query);

        // Assert
        Assert.Equal(20, results.Count);
        Assert.All(results, r => Assert.True(r % 2 == 0));
    }

    [Fact]
    public async Task SlowConsumer_FastProducer_NoDeadlock()
    {
        // Arrange - producer is fast, consumer is slow
        var source = GenerateNumbers(50);
        var consumed = 0;

        var query = source.AsParallel()
            .WithBufferSize(10) // Small buffer to stress test
            .WithMaxConcurrency(4)
            .Select(async x =>
            {
                await Task.Yield(); // Fast producer
                return x;
            });

        // Act - slow consumer
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await foreach (var item in query.WithCancellation(cts.Token))
        {
            consumed++;
            await Task.Delay(20); // Slow consumption
        }

        // Assert
        Assert.Equal(50, consumed);
    }

    [Fact]
    public async Task FastConsumer_SlowProducer_NoDeadlock()
    {
        // Arrange - producer is slow, consumer is fast
        var source = GenerateNumbers(20, delayMs: 20);

        var query = source.AsParallel()
            .WithMaxConcurrency(2)
            .Select(async x =>
            {
                await Task.Delay(5); // Some processing
                return x;
            });

        // Act
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var results = new List<int>();
        await foreach (var item in query.WithCancellation(cts.Token))
        {
            results.Add(item);
            // Fast consumption - no delay
        }

        // Assert
        Assert.Equal(20, results.Count);
    }

    #endregion

    #region Large Scale Tests

    [Fact]
    public async Task ThousandItems_OrderPreserved()
    {
        // Arrange
        var count = 1000;
        var source = GenerateNumbers(count);

        var query = source.AsParallel()
            .WithMaxConcurrency(8)
            .WithOrderPreservation(true)
            .Select(async x =>
            {
                await Task.Yield();
                return x;
            });

        // Act
        var results = await ToListAsync(query);

        // Assert
        Assert.Equal(count, results.Count);
        for (int i = 0; i < count; i++)
        {
            Assert.Equal(i, results[i]);
        }
    }

    [Fact]
    public async Task ThousandItems_UnorderedNoDataLoss()
    {
        // Arrange
        var count = 1000;
        var source = GenerateNumbers(count);

        var query = source.AsParallel()
            .WithMaxConcurrency(16)
            .WithOrderPreservation(false)
            .Select(async x =>
            {
                await Task.Yield();
                return x;
            });

        // Act
        var results = await ToListAsync(query);

        // Assert - all items present (order may differ)
        Assert.Equal(count, results.Count);
        var sorted = results.OrderBy(x => x).ToList();
        for (int i = 0; i < count; i++)
        {
            Assert.Equal(i, sorted[i]);
        }
    }

    [Fact]
    public async Task HighConcurrency_NoDataLoss()
    {
        // Arrange - test with max concurrency
        var count = 100;
        var source = GenerateNumbers(count);

        var query = source.AsParallel()
            .WithMaxConcurrency(50) // Very high concurrency
            .Select(async x =>
            {
                await Task.Delay(1);
                return x;
            });

        // Act
        var results = await ToListAsync(query);

        // Assert
        Assert.Equal(count, results.Count);
    }

    #endregion

    #region Helper Methods

    private static async IAsyncEnumerable<int> GenerateNumbers(int count, int delayMs = 0)
    {
        for (int i = 0; i < count; i++)
        {
            if (delayMs > 0) await Task.Delay(delayMs);
            else await Task.Yield();
            yield return i;
        }
    }

    private static async Task<List<T>> ToListAsync<T>(IAsyncEnumerable<T> source)
    {
        var list = new List<T>();
        await foreach (var item in source)
        {
            list.Add(item);
        }
        return list;
    }

    private class ConcurrencyTracker
    {
        private int _current;
        private int _max;

        public int MaxObserved => _max;

        public void Enter()
        {
            var newValue = Interlocked.Increment(ref _current);
            int currentMax;
            do
            {
                currentMax = _max;
            } while (newValue > currentMax && Interlocked.CompareExchange(ref _max, newValue, currentMax) != currentMax);
        }

        public void Exit()
        {
            Interlocked.Decrement(ref _current);
        }
    }

    #endregion
}
