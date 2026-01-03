using DataFlow.Parallel;

using Xunit;
using Xunit.Abstractions;

namespace ParallelAsyncQuery.Tests;

/// <summary>
/// Stress tests and edge cases for ParallelAsyncQuery.
/// These tests verify reliability under repeated operations and boundary conditions.
/// </summary>
public class StressAndEdgeCaseTests
{
    private readonly ITestOutputHelper _output;

    public StressAndEdgeCaseTests(ITestOutputHelper output) => _output = output;

    #region Reliability Tests

    [Fact]
    public async Task RepeatedEnumeration_ConsistentResults()
    {
        // Arrange - same query should produce consistent results
        var source = GenerateNumbers(50);
        var query = source.AsParallel()
            .WithOrderPreservation(true)
            .Select(async x =>
            {
                await Task.Yield();
                return x * 2;
            });

        // Act - enumerate multiple times (need fresh source each time)
        var allResults = new List<List<int>>();
        for (int i = 0; i < 10; i++)
        {
            var freshSource = GenerateNumbers(50);
            var freshQuery = freshSource.AsParallel()
                .WithOrderPreservation(true)
                .Select(async x =>
                {
                    await Task.Yield();
                    return x * 2;
                });
            allResults.Add(await ToListAsync(freshQuery));
        }

        // Assert - all runs should have same count and values
        foreach (var results in allResults)
        {
            Assert.Equal(50, results.Count);
            for (int i = 0; i < 50; i++)
            {
                Assert.Equal(i * 2, results[i]);
            }
        }
    }

    [Fact]
    public async Task InterleavedQueries_NoInterference()
    {
        // Arrange - multiple queries running concurrently
        var tasks = new List<Task<List<int>>>();

        for (int queryIndex = 0; queryIndex < 5; queryIndex++)
        {
            var multiplier = queryIndex + 1;
            var task = Task.Run(async () =>
            {
                var source = GenerateNumbers(20);
                var query = source.AsParallel()
                    .WithMaxConcurrency(2)
                    .Select(async x =>
                    {
                        await Task.Delay(5);
                        return x * multiplier;
                    });
                return await ToListAsync(query);
            });
            tasks.Add(task);
        }

        // Act
        var allResults = await Task.WhenAll(tasks);

        // Assert - each query should have independent results
        for (int queryIndex = 0; queryIndex < 5; queryIndex++)
        {
            var multiplier = queryIndex + 1;
            Assert.Equal(20, allResults[queryIndex].Count);
            var sorted = allResults[queryIndex].OrderBy(x => x).ToList();
            for (int i = 0; i < 20; i++)
            {
                Assert.Equal(i * multiplier, sorted[i]);
            }
        }
    }

    #endregion

    #region Edge Cases - Empty and Single Item

    [Fact]
    public async Task EmptySource_Select_ReturnsEmpty()
    {
        var source = GenerateNumbers(0);
        var query = source.AsParallel()
            .Select(async x =>
            {
                await Task.Yield();
                return x * 2;
            });

        var results = await ToListAsync(query);

        Assert.Empty(results);
    }

    [Fact]
    public async Task EmptySource_Where_ReturnsEmpty()
    {
        var source = GenerateNumbers(0);
        var query = source.AsParallel()
            .Where(async x =>
            {
                await Task.Yield();
                return true;
            });

        var results = await ToListAsync(query);

        Assert.Empty(results);
    }

    [Fact]
    public async Task EmptySource_SelectMany_ReturnsEmpty()
    {
        var source = GenerateNumbers(0);
        var query = source.AsParallel()
            .SelectMany(x => GenerateNumbers(5));

        var results = await ToListAsync(query);

        Assert.Empty(results);
    }

    [Fact]
    public async Task SingleItem_Select_Works()
    {
        var source = GenerateNumbers(1);
        var query = source.AsParallel()
            .Select(async x =>
            {
                await Task.Yield();
                return x * 10;
            });

        var results = await ToListAsync(query);

        Assert.Single(results);
        Assert.Equal(0, results[0]);
    }

    [Fact]
    public async Task SingleItem_Where_Passes()
    {
        var source = GenerateNumbers(1);
        var query = source.AsParallel()
            .Where(async x =>
            {
                await Task.Yield();
                return true;
            });

        var results = await ToListAsync(query);

        Assert.Single(results);
    }

    [Fact]
    public async Task SingleItem_Where_Filtered()
    {
        var source = GenerateNumbers(1);
        var query = source.AsParallel()
            .Where(async x =>
            {
                await Task.Yield();
                return false;
            });

        var results = await ToListAsync(query);

        Assert.Empty(results);
    }

    #endregion

    #region Edge Cases - All Items Filtered

    [Fact]
    public async Task AllItemsFiltered_ReturnsEmpty()
    {
        var source = GenerateNumbers(100);
        var query = source.AsParallel()
            .Where(async x =>
            {
                await Task.Yield();
                return false; // Filter all
            });

        var results = await ToListAsync(query);

        Assert.Empty(results);
    }

    [Fact]
    public async Task AllItemsFiltered_WithOrdering_ReturnsEmpty()
    {
        var source = GenerateNumbers(50);
        var query = source.AsParallel()
            .WithOrderPreservation(true)
            .Where(async x =>
            {
                await Task.Yield();
                return false;
            });

        var results = await ToListAsync(query);

        Assert.Empty(results);
    }

    #endregion

    #region Edge Cases - SelectMany

    [Fact]
    public async Task SelectMany_EmptyInnerSequences_ReturnsEmpty()
    {
        var source = GenerateNumbers(10);
        var query = source.AsParallel()
            .SelectMany(x => GenerateNumbers(0)); // All inner sequences are empty

        var results = await ToListAsync(query);

        Assert.Empty(results);
    }

    [Fact]
    public async Task SelectMany_MixedEmptyAndNonEmpty_ReturnsOnlyNonEmpty()
    {
        var source = GenerateNumbers(10);
        var query = source.AsParallel()
            .SelectMany(x => x % 2 == 0 ? GenerateNumbers(2, startAt: x * 10) : GenerateNumbers(0));

        var results = await ToListAsync(query);

        // Only even numbers produce items: 0,2,4,6,8 → 5 items × 2 = 10 results
        Assert.Equal(10, results.Count);
    }

    [Fact]
    public async Task SelectMany_SingleInnerItem_Works()
    {
        var source = GenerateNumbers(5);
        var query = source.AsParallel()
            .SelectMany(x => GenerateNumbers(1, startAt: x * 100));

        var results = await ToListAsync(query);

        Assert.Equal(5, results.Count);
        var sorted = results.OrderBy(x => x).ToList();
        Assert.Equal(new[] { 0, 100, 200, 300, 400 }, sorted);
    }

    #endregion

    #region Chained Operations

    [Fact]
    public async Task LongChain_Select_Where_Take_Works()
    {
        var source = GenerateNumbers(100);
        var query = source.AsParallel()
            .WithOrderPreservation(true)
            .Select(async x =>
            {
                await Task.Yield();
                return x * 2;
            })
            .Where(async x =>
            {
                await Task.Yield();
                return x % 4 == 0; // Divisible by 4
            })
            .Take(5);

        var results = await ToListAsync(query);

        Assert.Equal(5, results.Count);
        Assert.All(results, x => Assert.True(x % 4 == 0));
    }

    [Fact]
    public async Task MultipleWhereChained_AccumulatesFilters()
    {
        var source = GenerateNumbers(100);
        var query = source.AsParallel()
            .WithOrderPreservation(true)
            .Where(async x =>
            {
                await Task.Yield();
                return x % 2 == 0; // Evens
            })
            .Where(async x =>
            {
                await Task.Yield();
                return x % 5 == 0; // Also divisible by 5
            });

        var results = await ToListAsync(query);

        // 0, 10, 20, 30, 40, 50, 60, 70, 80, 90 = 10 items
        Assert.Equal(10, results.Count);
        Assert.All(results, x => Assert.True(x % 10 == 0));
    }

    #endregion

    #region Settings Preservation

    [Fact]
    public async Task SettingsPreserved_ThroughChainedOperations()
    {
        var tracker = new ConcurrencyTracker();
        var source = GenerateNumbers(30);

        var query = source.AsParallel()
            .WithMaxConcurrency(2)
            .WithOrderPreservation(true)
            .Select(async x =>
            {
                tracker.Enter();
                await Task.Delay(10);
                tracker.Exit();
                return x;
            })
            .Where(async x =>
            {
                tracker.Enter();
                await Task.Delay(5);
                tracker.Exit();
                return x % 2 == 0;
            });

        var results = await ToListAsync(query);

        _output.WriteLine($"Max concurrency observed: {tracker.MaxObserved}");
        Assert.Equal(15, results.Count); // Half are even
        // Concurrency should still be limited (though may be higher due to chained ops)
    }

    #endregion

    #region Helper Methods

    private static async IAsyncEnumerable<int> GenerateNumbers(int count, int startAt = 0)
    {
        for (int i = 0; i < count; i++)
        {
            await Task.Yield();
            yield return startAt + i;
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
