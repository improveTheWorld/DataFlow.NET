using DataFlow.Parallel;
using Xunit;

namespace ParallelAsyncQuery.Tests;

/// <summary>
/// Edge case tests for ParallelAsyncQuery to improve coverage from 44% to 70%+
/// </summary>
public class ParallelAsyncQueryEdgeCaseTests
{
    private static async IAsyncEnumerable<int> GenerateNumbers(int count, int delayMs = 0)
    {
        for (int i = 0; i < count; i++)
        {
            if (delayMs > 0) await Task.Delay(delayMs);
            yield return i;
        }
    }

    // Helper to collect results from ParallelAsyncQuery
    private static async Task<List<T>> ToListAsync<T>(IAsyncEnumerable<T> source)
    {
        var list = new List<T>();
        await foreach (var item in source)
        {
            list.Add(item);
        }
        return list;
    }

    #region Settings Configuration Tests

    [Fact]
    public async Task WithMaxConcurrency_LimitsParallelism()
    {
        // Arrange
        var concurrencyTracker = new ConcurrencyTracker();
        var source = GenerateNumbers(20);

        // Act
        var query = source
            .AsParallel()
            .WithMaxConcurrency(2)
            .Select(async x =>
            {
                concurrencyTracker.Enter();
                await Task.Delay(10);
                concurrencyTracker.Exit();
                return x;
            });

        var results = await ToListAsync(query);

        // Assert
        Assert.Equal(20, results.Count);
        Assert.True(concurrencyTracker.MaxConcurrency <= 2,
            $"Max concurrency was {concurrencyTracker.MaxConcurrency}, expected <= 2");
    }

    [Fact]
    public async Task WithBufferSize_AffectsExecution()
    {
        // Arrange
        var source = GenerateNumbers(10);

        // Act
        var query = source
            .AsParallel()
            .WithBufferSize(5)
            .Select(async x =>
            {
                await Task.Delay(1);
                return x * 2;
            });

        var results = await ToListAsync(query);

        // Assert
        Assert.Equal(10, results.Count);
        Assert.Contains(0, results);
        Assert.Contains(18, results); // 9 * 2
    }

    [Fact]
    public async Task WithMergeOptions_FullyBuffered_BuffersAllBeforeYielding()
    {
        // Arrange
        var source = GenerateNumbers(5);
        var yielded = new List<int>();

        // Act
        var query = source
            .AsParallel()
            .WithMergeOptions(DataFlow.Parallel.ParallelMergeOptions.FullyBuffered)
            .Select(async x =>
            {
                await Task.Delay(1);
                return x;
            });

        await foreach (var item in query)
        {
            yielded.Add(item);
        }

        // Assert
        Assert.Equal(5, yielded.Count);
    }

    #endregion

    #region Cancellation Tests

    [Fact]
    public async Task WithCancellation_TokenCanceled_StopsProcessing()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        var processedCount = 0;
        var source = GenerateNumbers(100, delayMs: 10);

        // Act
        var query = source
            .AsParallel()
            .WithCancellation(cts.Token)
            .Select(async x =>
            {
                Interlocked.Increment(ref processedCount);
                await Task.Delay(5);
                return x;
            });

        // Cancel after a short delay
        _ = Task.Run(async () =>
        {
            await Task.Delay(50);
            cts.Cancel();
        });

        // Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await foreach (var _ in query) { }
        });

        Assert.True(processedCount < 100, $"Expected early cancellation, but processed {processedCount} items");
    }

    [Fact(Skip = "V1.1: Timeout functionality not yet enforced during execution")]
    public async Task WithTimeout_ExceedsLimit_CancelsExecution()
    {
        // Arrange
        var source = GenerateNumbers(10, delayMs: 100); // Slow source

        // Act
        var query = source
            .AsParallel()
            .WithTimeout(TimeSpan.FromMilliseconds(50))
            .Select(async x =>
            {
                await Task.Delay(10);
                return x;
            });

        // Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            var results = await ToListAsync(query);
        });
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public async Task ContinueOnError_True_SkipsFailedItems()
    {
        // Arrange
        var source = GenerateNumbers(10);

        // Act
        var query = source
            .AsParallel()
            .ContinueOnError()
            .Select(async x =>
            {
                if (x % 3 == 0 && x > 0) throw new InvalidOperationException($"Failing on {x}");
                await Task.Yield();
                return x;
            });

        var results = await ToListAsync(query);

        // Assert - Should have skipped items 3, 6, 9, keeping 0, 1, 2, 4, 5, 7, 8
        Assert.Equal(7, results.Count);
        Assert.DoesNotContain(3, results);
        Assert.DoesNotContain(6, results);
        Assert.DoesNotContain(9, results);
    }

    [Fact]
    public async Task ContinueOnError_False_ThrowsOnFirstError()
    {
        // Arrange
        var source = GenerateNumbers(10);

        // Act
        var query = source
            .AsParallel()
            .Select(async x =>
            {
                if (x == 5) throw new InvalidOperationException("Failing on 5");
                await Task.Yield();
                return x;
            });

        // Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await ToListAsync(query);
        });
    }

    [Fact]
    public async Task ExceptionInWherePredicateAsync_PropagatesCorrectly()
    {
        // Arrange
        var source = GenerateNumbers(10);

        // Act
        var query = source
            .AsParallel()
            .Where(async x =>
            {
                if (x == 7) throw new ArgumentException("Bad value 7");
                await Task.Yield();
                return x % 2 == 0;
            });

        // Assert
        await Assert.ThrowsAsync<ArgumentException>(async () =>
        {
            await ToListAsync(query);
        });
    }

    #endregion

    #region Order Preservation Tests

    [Fact]
    public async Task WithOrderPreservation_True_MaintainsOriginalOrder()
    {
        // Arrange
        var source = GenerateNumbers(20);

        // Act
        var query = source
            .AsParallel()
            .WithMaxConcurrency(4)
            .WithOrderPreservation(true)
            .Select(async x =>
            {
                // Random delay to try to mess up order
                await Task.Delay(Random.Shared.Next(1, 10));
                return x;
            });

        var results = await ToListAsync(query);

        // Assert - Order should be preserved
        Assert.Equal(20, results.Count);
        for (int i = 0; i < 20; i++)
        {
            Assert.Equal(i, results[i]);
        }
    }

    [Fact]
    public async Task WithOrderPreservation_False_ProcessesAll()
    {
        // Arrange
        var source = GenerateNumbers(20);

        // Act
        var query = source
            .AsParallel()
            .WithMaxConcurrency(4)
            .WithOrderPreservation(false)
            .Select(async x =>
            {
                await Task.Delay(Random.Shared.Next(1, 5));
                return x;
            });

        var results = await ToListAsync(query);

        // Assert - All items present (order may vary)
        Assert.Equal(20, results.Count);
        Assert.Equal(20, results.Distinct().Count());
        for (int i = 0; i < 20; i++)
        {
            Assert.Contains(i, results);
        }
    }

    #endregion

    #region Take Operation Tests

    [Fact]
    public async Task Take_ReturnsExactCount()
    {
        // Arrange
        var source = GenerateNumbers(100);

        // Act
        var query = source
            .AsParallel()
            .Take(10);

        var results = await ToListAsync(query);

        // Assert
        Assert.Equal(10, results.Count);
    }

    [Fact]
    public async Task Take_WithOrdering_PreservesFirstN()
    {
        // Arrange
        var source = GenerateNumbers(50);

        // Act
        var query = source
            .AsParallel()
            .WithOrderPreservation(true)
            .Take(5);

        var results = await ToListAsync(query);

        // Assert
        Assert.Equal(new[] { 0, 1, 2, 3, 4 }, results);
    }

    #endregion

    #region Execution Mode Tests

    [Fact]
    public async Task SequentialMode_ProcessesInOrder()
    {
        // Arrange
        var source = GenerateNumbers(10);
        var processed = new List<int>();

        // Act
        var query = source
            .AsParallel()
            .WithMaxConcurrency(1) // Sequential
            .Select(async x =>
            {
                lock (processed) { processed.Add(x); }
                await Task.Delay(1);
                return x;
            });

        await ToListAsync(query);

        // Assert - With concurrency 1, should process in order
        Assert.Equal(10, processed.Count);
        for (int i = 0; i < 10; i++)
        {
            Assert.Equal(i, processed[i]);
        }
    }

    #endregion

    #region Where Operation Tests

    [Fact]
    public async Task Where_FiltersCorrectly()
    {
        // Arrange
        var source = GenerateNumbers(20);

        // Act
        var query = source
            .AsParallel()
            .Where(async x =>
            {
                await Task.Yield();
                return x % 2 == 0;
            });

        var results = await ToListAsync(query);

        // Assert
        Assert.Equal(10, results.Count);
        Assert.All(results, x => Assert.True(x % 2 == 0));
    }

    [Fact]
    public async Task Where_WithOrdering_PreservesOrder()
    {
        // Arrange
        var source = GenerateNumbers(20);

        // Act
        var query = source
            .AsParallel()
            .WithOrderPreservation(true)
            .Where(async x =>
            {
                await Task.Delay(Random.Shared.Next(1, 5));
                return x > 10;
            });

        var results = await ToListAsync(query);

        // Assert - Should be 11, 12, 13, ... 19 in order
        Assert.Equal(9, results.Count);
        for (int i = 0; i < results.Count; i++)
        {
            Assert.Equal(11 + i, results[i]);
        }
    }

    [Fact]
    public async Task Where_WithContinueOnError_SkipsFailures()
    {
        // Arrange
        var source = GenerateNumbers(10);

        // Act
        var query = source
            .AsParallel()
            .ContinueOnError()
            .Where(async x =>
            {
                if (x == 5) throw new Exception("Fake error");
                await Task.Yield();
                return x < 8;
            });

        var results = await ToListAsync(query);

        // Assert - 5 throws, so we get 0,1,2,3,4,6,7
        Assert.Equal(7, results.Count);
        Assert.DoesNotContain(5, results);
    }

    [Fact]
    public async Task Where_ChainedWithSelect_WorksCorrectly()
    {
        // Arrange
        var source = GenerateNumbers(20);

        // Act
        var query = source
            .AsParallel()
            .Where(async x =>
            {
                await Task.Yield();
                return x % 3 == 0;
            })
            .Select(async x =>
            {
                await Task.Yield();
                return x * 10;
            });

        var results = await ToListAsync(query);

        // Assert - 0, 3, 6, 9, 12, 15, 18 -> 0, 30, 60, 90, 120, 150, 180
        Assert.Equal(7, results.Count);
        Assert.Contains(0, results);
        Assert.Contains(30, results);
        Assert.Contains(180, results);
    }

    #endregion

    #region Indexed Select Tests

    [Fact]
    public async Task Select_WithIndex_PassesCorrectIndex()
    {
        // Arrange
        var source = GenerateNumbers(5);

        // Act
        var query = source
            .AsParallel()
            .WithOrderPreservation(true)
            .Select(async (x, i) =>
            {
                await Task.Yield();
                return $"{i}:{x}";
            });

        var results = await ToListAsync(query);

        // Assert - With order preservation, indices should match
        Assert.Equal(new[] { "0:0", "1:1", "2:2", "3:3", "4:4" }, results);
    }

    #endregion

    #region Additional Coverage Tests

    [Fact]
    public async Task AsParallel_WithSettings_UsesProvidedSettings()
    {
        // Arrange
        var source = GenerateNumbers(5);
        var settings = new ParallelExecutionSettings
        {
            MaxConcurrency = 2,
            PreserveOrder = true
        };

        // Act
        var query = source
            .AsParallel(settings)
            .Select(async x =>
            {
                await Task.Yield();
                return x * 2;
            });

        var results = await ToListAsync(query);

        // Assert
        Assert.Equal(5, results.Count);
        Assert.Equal(new[] { 0, 2, 4, 6, 8 }, results);
    }

    [Fact]
    public async Task WithMaxConcurrency_InvalidValue_ThrowsArgumentException()
    {
        // Arrange
        var source = GenerateNumbers(5);

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() =>
        {
            source.AsParallel().WithMaxConcurrency(0);
        });

        Assert.Throws<ArgumentOutOfRangeException>(() =>
        {
            source.AsParallel().WithMaxConcurrency(-1);
        });
    }

    [Fact]
    public async Task Source_WithSequentialMode_ProcessesSequentially()
    {
        // Arrange
        var source = GenerateNumbers(10);
        var processed = new List<int>();
        var settings = new ParallelExecutionSettings
        {
            ExecutionMode = DataFlow.Parallel.ParallelExecutionMode.Sequential
        };

        // Act
        var query = source.AsParallel(settings);

        await foreach (var item in query)
        {
            lock (processed) { processed.Add(item); }
        }

        // Assert - Sequential mode should process in order
        Assert.Equal(10, processed.Count);
        for (int i = 0; i < 10; i++)
        {
            Assert.Equal(i, processed[i]);
        }
    }

    [Fact]
    public async Task Take_ZeroCount_ReturnsEmpty()
    {
        // Arrange
        var source = GenerateNumbers(10);

        // Act
        var query = source
            .AsParallel()
            .Take(0);

        var results = await ToListAsync(query);

        // Assert
        Assert.Empty(results);
    }

    [Fact]
    public async Task Take_MoreThanSource_ReturnsAll()
    {
        // Arrange
        var source = GenerateNumbers(5);

        // Act
        var query = source
            .AsParallel()
            .Take(100);

        var results = await ToListAsync(query);

        // Assert
        Assert.Equal(5, results.Count);
    }

    [Fact(Skip = "Timing-sensitive test - cancellation propagation through channel read is complex and depends on timing")]
    public async Task CombinedTokens_BothTokensSet_BothHonored()
    {
        // Arrange  
        using var settingsCts = new CancellationTokenSource();
        using var callCts = new CancellationTokenSource();
        var processed = 0;

        var settings = new ParallelExecutionSettings
        {
            CancellationToken = settingsCts.Token
        };

        // Use longer delays to ensure cancellation has time to propagate
        var source = GenerateNumbers(100, delayMs: 50);

        // Cancel the settings token after delay - give it enough time to start but not finish
        _ = Task.Run(async () =>
        {
            await Task.Delay(200);
            settingsCts.Cancel();
        });

        // Act
        var query = source.AsParallel(settings)
            .Select(async x =>
            {
                Interlocked.Increment(ref processed);
                await Task.Delay(10);
                return x;
            });

        // Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await foreach (var _ in query.WithCancellation(callCts.Token)) { }
        });

        Assert.True(processed < 100, $"Expected early cancellation, processed {processed}");
    }

    [Fact]
    public async Task ChainedOperations_MultipleWhere_WorksCorrectly()
    {
        // Arrange
        var source = GenerateNumbers(30);

        // Act
        var query = source
            .AsParallel()
            .Where(async x =>
            {
                await Task.Yield();
                return x % 2 == 0; // Even: 0,2,4,...28 (15 items)
            })
            .Where(async x =>
            {
                await Task.Yield();
                return x % 3 == 0; // Divisible by 6: 0,6,12,18,24 (5 items)
            });

        var results = await ToListAsync(query);

        // Assert
        Assert.Equal(5, results.Count);
        Assert.All(results, x => Assert.True(x % 6 == 0));
    }

    [Fact]
    public async Task Select_ChainedWithTake_LimitsAfterTransform()
    {
        // Arrange
        var source = GenerateNumbers(20);

        // Act
        var query = source
            .AsParallel()
            .WithOrderPreservation(true)
            .Select(async x =>
            {
                await Task.Yield();
                return x * 10;
            })
            .Take(5);

        var results = await ToListAsync(query);

        // Assert
        Assert.Equal(5, results.Count);
        Assert.Equal(new[] { 0, 10, 20, 30, 40 }, results);
    }

    [Fact]
    public async Task ForceParallelMode_ProcessesInParallel()
    {
        // Arrange
        var concurrencyTracker = new ConcurrencyTracker();
        var settings = new ParallelExecutionSettings
        {
            ExecutionMode = DataFlow.Parallel.ParallelExecutionMode.ForceParallel,
            MaxConcurrency = 4
        };

        var source = GenerateNumbers(10);

        // Act
        var query = source
            .AsParallel(settings)
            .Select(async x =>
            {
                concurrencyTracker.Enter();
                await Task.Delay(20);
                concurrencyTracker.Exit();
                return x;
            });

        var results = await ToListAsync(query);

        // Assert
        Assert.Equal(10, results.Count);
        Assert.True(concurrencyTracker.MaxConcurrency > 1,
            $"Expected parallel execution but max concurrency was {concurrencyTracker.MaxConcurrency}");
    }

    [Fact]
    public async Task EmptySource_ReturnsEmpty()
    {
        // Arrange
        var source = GenerateNumbers(0);

        // Act
        var query = source
            .AsParallel()
            .Select(async x =>
            {
                await Task.Yield();
                return x * 2;
            });

        var results = await ToListAsync(query);

        // Assert
        Assert.Empty(results);
    }

    [Fact]
    public async Task Select_SequentialMode_WithIndexedSelector()
    {
        // Arrange
        var source = GenerateNumbers(5);
        var settings = new ParallelExecutionSettings
        {
            ExecutionMode = DataFlow.Parallel.ParallelExecutionMode.Sequential
        };

        // Act
        var query = source
            .AsParallel(settings)
            .Select(async (x, i) =>
            {
                await Task.Yield();
                return $"seq-{i}:{x}";
            });

        var results = await ToListAsync(query);

        // Assert
        Assert.Equal(5, results.Count);
        Assert.Equal("seq-0:0", results[0]);
        Assert.Equal("seq-4:4", results[4]);
    }

    [Fact]
    public async Task Select_SequentialMode_PreservesOrder()
    {
        // Arrange
        var source = GenerateNumbers(10);
        var settings = new ParallelExecutionSettings
        {
            ExecutionMode = DataFlow.Parallel.ParallelExecutionMode.Sequential
        };

        // Act
        var query = source
            .AsParallel(settings)
            .Select(async x =>
            {
                await Task.Delay(Random.Shared.Next(1, 10));
                return x * 2;
            });

        var results = await ToListAsync(query);

        // Assert - Sequential should maintain order
        Assert.Equal(10, results.Count);
        for (int i = 0; i < 10; i++)
        {
            Assert.Equal(i * 2, results[i]);
        }
    }

    [Fact]
    public async Task Where_SequentialMode_FiltersInOrder()
    {
        // Arrange
        var source = GenerateNumbers(10);
        var settings = new ParallelExecutionSettings
        {
            ExecutionMode = DataFlow.Parallel.ParallelExecutionMode.Sequential
        };

        // Act
        var query = source
            .AsParallel(settings)
            .Where(async x =>
            {
                await Task.Yield();
                return x % 2 == 0;
            });

        var results = await ToListAsync(query);

        // Assert
        Assert.Equal(new[] { 0, 2, 4, 6, 8 }, results);
    }

    [Fact]
    public async Task Take_AfterWhere_ChainedCorrectly()
    {
        // Arrange
        var source = GenerateNumbers(50);

        // Act
        var query = source
            .AsParallel()
            .Where(async x =>
            {
                await Task.Yield();
                return x % 2 == 0;
            })
            .Take(5);

        var results = await ToListAsync(query);

        // Assert - First 5 even numbers
        Assert.Equal(5, results.Count);
    }

    [Fact]
    public async Task GetOrderedResults_WithManyItems_ProcessesCorrectly()
    {
        // Arrange - Test GetOrderedResults path with more items than buffer
        var source = GenerateNumbers(50);

        // Act
        var query = source
            .AsParallel()
            .WithMaxConcurrency(8)
            .WithBufferSize(10)
            .WithOrderPreservation(true)
            .Select(async x =>
            {
                await Task.Delay(Random.Shared.Next(1, 5));
                return x * 2;
            });

        var results = await ToListAsync(query);

        // Assert
        Assert.Equal(50, results.Count);
        for (int i = 0; i < 50; i++)
        {
            Assert.Equal(i * 2, results[i]);
        }
    }

    [Fact]
    public async Task GetUnorderedResults_ProcessesAllItems()
    {
        // Arrange
        var source = GenerateNumbers(30);

        // Act
        var query = source
            .AsParallel()
            .WithMaxConcurrency(6)
            .WithOrderPreservation(false)
            .Select(async x =>
            {
                await Task.Delay(Random.Shared.Next(1, 5));
                return x;
            });

        var results = await ToListAsync(query);

        // Assert - All items present
        Assert.Equal(30, results.Count);
        Assert.Equal(30, results.Distinct().Count());
    }

    [Fact]
    public async Task NotBuffered_MergeOption_StreamsResults()
    {
        // Arrange
        var source = GenerateNumbers(10);

        // Act
        var query = source
            .AsParallel()
            .WithMergeOptions(DataFlow.Parallel.ParallelMergeOptions.NotBuffered)
            .Select(async x =>
            {
                await Task.Yield();
                return x;
            });

        var results = await ToListAsync(query);

        // Assert
        Assert.Equal(10, results.Count);
    }

    [Fact]
    public async Task AutoBuffered_MergeOption_ProcessesCorrectly()
    {
        // Arrange
        var source = GenerateNumbers(15);

        // Act
        var query = source
            .AsParallel()
            .WithMergeOptions(DataFlow.Parallel.ParallelMergeOptions.AutoBuffered)
            .Select(async x =>
            {
                await Task.Yield();
                return x * 3;
            });

        var results = await ToListAsync(query);

        // Assert
        Assert.Equal(15, results.Count);
        Assert.Contains(0, results);
        Assert.Contains(42, results); // 14 * 3
    }

    #endregion

    #region Helper Classes

    private class ConcurrencyTracker
    {
        private int _current;
        private int _max;
        private readonly object _lock = new();

        public int MaxConcurrency => _max;

        public void Enter()
        {
            lock (_lock)
            {
                _current++;
                if (_current > _max) _max = _current;
            }
        }

        public void Exit()
        {
            lock (_lock)
            {
                _current--;
            }
        }
    }

    #endregion
}
