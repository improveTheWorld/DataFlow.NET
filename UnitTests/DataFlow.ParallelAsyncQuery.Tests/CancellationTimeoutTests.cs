using System.Diagnostics;
using System.Runtime.CompilerServices;
using DataFlow;
using DataFlow.Parallel;
using Xunit;

namespace ParallelAsyncQuery.Tests;

/// <summary>
/// Comprehensive tests for cancellation token linking, timeout enforcement, and
/// propagation across all ParallelAsyncQuery operator types (Source, Select, Where, Take, SelectMany).
/// Covers fixes NET-001 (WithTimeout enforcement) and NET-002 (combined token linking).
/// </summary>
public class CancellationTimeoutTests
{
    #region Region 1: WithTimeout Enforcement (NET-001)

    [Fact]
    public async Task WithTimeout_Select_Parallel_CancelsExecution()
    {
        var source = SlowSource(100, delayMs: 50);

        var query = source.AsParallel()
            .WithTimeout(TimeSpan.FromMilliseconds(150))
            .Select(async x => { await Task.Delay(10); return x; });

        await AssertCancelsWithinMs(async () =>
        {
            await foreach (var _ in query) { }
        }, maxMs: 3000);
    }

    [Fact]
    public async Task WithTimeout_Select_Sequential_CancelsExecution()
    {
        var source = SlowSource(100, delayMs: 50);

        var settings = new ParallelExecutionSettings
        {
            ExecutionMode = DataFlow.Parallel.ParallelExecutionMode.Sequential
        };

        var query = source.AsParallel(settings)
            .WithTimeout(TimeSpan.FromMilliseconds(150))
            .Select(async x => { await Task.Delay(10); return x; });

        await AssertCancelsWithinMs(async () =>
        {
            await foreach (var _ in query) { }
        }, maxMs: 3000);
    }

    [Fact]
    public async Task WithTimeout_Where_CancelsExecution()
    {
        var source = SlowSource(100, delayMs: 50);

        var query = source.AsParallel()
            .WithTimeout(TimeSpan.FromMilliseconds(150))
            .Where(async x => { await Task.Delay(10); return x % 2 == 0; });

        await AssertCancelsWithinMs(async () =>
        {
            await foreach (var _ in query) { }
        }, maxMs: 3000);
    }

    [Fact]
    public async Task WithTimeout_Take_CancelsExecution()
    {
        // Take(50) from a slow source, but timeout should fire before 50 items
        var source = SlowSource(100, delayMs: 50);

        var query = source.AsParallel()
            .WithTimeout(TimeSpan.FromMilliseconds(150))
            .Select(async x => { await Task.Delay(10); return x; })
            .Take(50);

        await AssertCancelsWithinMs(async () =>
        {
            await foreach (var _ in query) { }
        }, maxMs: 3000);
    }

    [Fact]
    public async Task WithTimeout_SelectMany_CancelsExecution()
    {
        var source = SlowSource(50, delayMs: 50);

        var query = source.AsParallel()
            .WithTimeout(TimeSpan.FromMilliseconds(150))
            .SelectMany(x => InnerSource(3, delayMs: 10));

        await AssertCancelsWithinMs(async () =>
        {
            await foreach (var _ in query) { }
        }, maxMs: 3000);
    }

    [Fact]
    public async Task WithTimeout_InfiniteTimeout_NoCancellation()
    {
        var source = SlowSource(5, delayMs: 10);

        // Default timeout is InfiniteTimeSpan — should complete normally
        var query = source.AsParallel()
            .Select(async x => { await Task.Delay(5); return x; });

        var results = await query.ToList();

        Assert.Equal(5, results.Count);
    }

    #endregion

    #region Region 2: Combined Token Linking (NET-002)

    [Fact]
    public async Task Combined_SettingsTokenCancels_BothTokensSet()
    {
        using var settingsCts = new CancellationTokenSource();
        using var callCts = new CancellationTokenSource();
        var processed = 0;

        var settings = new ParallelExecutionSettings
        {
            CancellationToken = settingsCts.Token
        };

        var source = SlowSource(100, delayMs: 50);

        // Cancel settings token after 200ms
        _ = Task.Run(async () => { await Task.Delay(200); settingsCts.Cancel(); });

        var query = source.AsParallel(settings)
            .Select(async x =>
            {
                Interlocked.Increment(ref processed);
                await Task.Delay(10);
                return x;
            });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await foreach (var _ in query.WithCancellation(callCts.Token)) { }
        });

        Assert.True(processed < 100, $"Expected early cancellation, processed {processed}");
    }

    [Fact]
    public async Task Combined_CallTokenCancels_BothTokensSet()
    {
        using var settingsCts = new CancellationTokenSource();
        using var callCts = new CancellationTokenSource();
        var processed = 0;

        var settings = new ParallelExecutionSettings
        {
            CancellationToken = settingsCts.Token
        };

        var source = SlowSource(100, delayMs: 50);

        // Cancel CALL token after 200ms (not settings)
        _ = Task.Run(async () => { await Task.Delay(200); callCts.Cancel(); });

        var query = source.AsParallel(settings)
            .Select(async x =>
            {
                Interlocked.Increment(ref processed);
                await Task.Delay(10);
                return x;
            });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await foreach (var _ in query.WithCancellation(callCts.Token)) { }
        });

        Assert.True(processed < 100, $"Expected early cancellation, processed {processed}");
    }

    [Fact]
    public async Task Combined_SettingsOnly_Cancels()
    {
        using var settingsCts = new CancellationTokenSource();
        var processed = 0;

        var settings = new ParallelExecutionSettings
        {
            CancellationToken = settingsCts.Token
        };

        var source = SlowSource(100, delayMs: 50);
        _ = Task.Run(async () => { await Task.Delay(200); settingsCts.Cancel(); });

        var query = source.AsParallel(settings)
            .Select(async x =>
            {
                Interlocked.Increment(ref processed);
                await Task.Delay(10);
                return x;
            });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await foreach (var _ in query) { }
        });

        Assert.True(processed < 100, $"Expected early cancellation, processed {processed}");
    }

    [Fact]
    public async Task Combined_CallOnly_Cancels()
    {
        using var callCts = new CancellationTokenSource();
        var processed = 0;

        var source = SlowSource(100, delayMs: 50);
        _ = Task.Run(async () => { await Task.Delay(200); callCts.Cancel(); });

        var query = source.AsParallel()
            .Select(async x =>
            {
                Interlocked.Increment(ref processed);
                await Task.Delay(10);
                return x;
            });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await foreach (var _ in query.WithCancellation(callCts.Token)) { }
        });

        Assert.True(processed < 100, $"Expected early cancellation, processed {processed}");
    }

    [Fact]
    public async Task Combined_NeitherCancelled_Completes()
    {
        using var settingsCts = new CancellationTokenSource();
        using var callCts = new CancellationTokenSource();

        var settings = new ParallelExecutionSettings
        {
            CancellationToken = settingsCts.Token
        };

        var source = SlowSource(5, delayMs: 10);
        var query = source.AsParallel(settings)
            .Select(async x => { await Task.Delay(5); return x; });

        var results = new List<int>();
        await foreach (var item in query.WithCancellation(callCts.Token))
        {
            results.Add(item);
        }

        Assert.Equal(5, results.Count);
    }

    [Fact]
    public async Task Combined_Where_SettingsTokenCancels()
    {
        using var settingsCts = new CancellationTokenSource();
        using var callCts = new CancellationTokenSource();

        var settings = new ParallelExecutionSettings
        {
            CancellationToken = settingsCts.Token
        };

        var source = SlowSource(100, delayMs: 50);
        _ = Task.Run(async () => { await Task.Delay(200); settingsCts.Cancel(); });

        var query = source.AsParallel(settings)
            .Where(async x => { await Task.Delay(10); return x % 2 == 0; });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await foreach (var _ in query.WithCancellation(callCts.Token)) { }
        });
    }

    #endregion

    #region Region 3: WithCancellation Token Linking

    [Fact]
    public async Task WithCancellation_PreservesExistingSettingsToken()
    {
        // The critical NET-002 scenario: settings token must NOT be replaced
        using var settingsCts = new CancellationTokenSource();
        using var callCts = new CancellationTokenSource();

        var settings = new ParallelExecutionSettings
        {
            CancellationToken = settingsCts.Token
        };

        var source = SlowSource(100, delayMs: 50);
        _ = Task.Run(async () => { await Task.Delay(200); settingsCts.Cancel(); });

        // WithCancellation should LINK callCts.Token with settingsCts.Token — not replace
        var query = source.AsParallel(settings)
            .WithCancellation(callCts.Token)
            .Select(async x => { await Task.Delay(10); return x; });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await foreach (var _ in query) { }
        });
    }

    [Fact]
    public async Task WithCancellation_DefaultToken_PreservesSettings()
    {
        // Passing default token should preserve the existing settings token
        using var settingsCts = new CancellationTokenSource();

        var settings = new ParallelExecutionSettings
        {
            CancellationToken = settingsCts.Token
        };

        var source = SlowSource(100, delayMs: 50);
        _ = Task.Run(async () => { await Task.Delay(200); settingsCts.Cancel(); });

        // WithCancellation(default) should NOT lose the settings token
        var query = source.AsParallel(settings)
            .WithCancellation(default)
            .Select(async x => { await Task.Delay(10); return x; });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await foreach (var _ in query) { }
        });
    }

    [Fact]
    public async Task WithCancellation_NoSettings_UsesCallToken()
    {
        using var callCts = new CancellationTokenSource();

        var source = SlowSource(100, delayMs: 50);
        _ = Task.Run(async () => { await Task.Delay(200); callCts.Cancel(); });

        var query = source.AsParallel()
            .WithCancellation(callCts.Token)
            .Select(async x => { await Task.Delay(10); return x; });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await foreach (var _ in query) { }
        });
    }

    [Fact]
    public async Task WithCancellation_ChainedCalls_AllTokensHonored()
    {
        // Chain: AsParallel(settingsToken) → WithCancellation(token2) → WithCancellation(token3)
        // Cancel token2 → should still fire
        using var settingsCts = new CancellationTokenSource();
        using var cts2 = new CancellationTokenSource();
        using var cts3 = new CancellationTokenSource();

        var settings = new ParallelExecutionSettings
        {
            CancellationToken = settingsCts.Token
        };

        var source = SlowSource(100, delayMs: 50);
        // Cancel just cts2 (the middle token)
        _ = Task.Run(async () => { await Task.Delay(200); cts2.Cancel(); });

        var query = source.AsParallel(settings)
            .WithCancellation(cts2.Token)
            .WithCancellation(cts3.Token)
            .Select(async x => { await Task.Delay(10); return x; });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await foreach (var _ in query) { }
        });
    }

    #endregion

    #region Region 4: Pre-Cancelled Tokens

    [Fact]
    public async Task PreCancelled_SettingsToken_ThrowsImmediately()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel(); // Pre-cancel

        var settings = new ParallelExecutionSettings
        {
            CancellationToken = cts.Token
        };

        var source = SlowSource(10, delayMs: 10);
        var query = source.AsParallel(settings)
            .Select(async x => { await Task.Delay(5); return x; });

        var sw = Stopwatch.StartNew();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await foreach (var _ in query) { }
        });
        sw.Stop();

        // Should cancel nearly immediately (well under 1 second)
        Assert.True(sw.ElapsedMilliseconds < 1000,
            $"Pre-cancelled token should cancel immediately, took {sw.ElapsedMilliseconds}ms");
    }

    [Fact]
    public async Task PreCancelled_CallToken_ThrowsImmediately()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel(); // Pre-cancel

        var source = SlowSource(10, delayMs: 10);
        var query = source.AsParallel()
            .Select(async x => { await Task.Delay(5); return x; });

        var sw = Stopwatch.StartNew();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await foreach (var _ in query.WithCancellation(cts.Token)) { }
        });
        sw.Stop();

        Assert.True(sw.ElapsedMilliseconds < 1000,
            $"Pre-cancelled token should cancel immediately, took {sw.ElapsedMilliseconds}ms");
    }

    [Fact]
    public async Task PreCancelled_ZeroTimeout_ThrowsQuickly()
    {
        var source = SlowSource(100, delayMs: 50);

        var query = source.AsParallel()
            .WithTimeout(TimeSpan.FromMilliseconds(1))
            .Select(async x => { await Task.Delay(10); return x; });

        var sw = Stopwatch.StartNew();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await foreach (var _ in query) { }
        });
        sw.Stop();

        Assert.True(sw.ElapsedMilliseconds < 2000,
            $"Near-zero timeout should cancel quickly, took {sw.ElapsedMilliseconds}ms");
    }

    #endregion

    #region Region 5: Ordered vs Unordered Cancellation

    [Fact]
    public async Task Ordered_TimeoutCancelsProperly()
    {
        var source = SlowSource(100, delayMs: 50);
        var processed = 0;

        var query = source.AsParallel()
            .WithOrderPreservation(true)
            .WithTimeout(TimeSpan.FromMilliseconds(200))
            .Select(async x =>
            {
                Interlocked.Increment(ref processed);
                await Task.Delay(10);
                return x;
            });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await foreach (var _ in query) { }
        });

        Assert.True(processed < 100, $"Ordered: expected early cancel, processed {processed}");
    }

    [Fact]
    public async Task Unordered_TimeoutCancelsProperly()
    {
        var source = SlowSource(100, delayMs: 50);
        var processed = 0;

        var query = source.AsParallel()
            .WithOrderPreservation(false)
            .WithTimeout(TimeSpan.FromMilliseconds(200))
            .Select(async x =>
            {
                Interlocked.Increment(ref processed);
                await Task.Delay(10);
                return x;
            });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await foreach (var _ in query) { }
        });

        Assert.True(processed < 100, $"Unordered: expected early cancel, processed {processed}");
    }

    [Fact]
    public async Task Ordered_TokenCancelsProperly()
    {
        using var cts = new CancellationTokenSource();
        var processed = 0;

        var source = SlowSource(100, delayMs: 50);
        _ = Task.Run(async () => { await Task.Delay(200); cts.Cancel(); });

        var query = source.AsParallel()
            .WithOrderPreservation(true)
            .WithCancellation(cts.Token)
            .Select(async x =>
            {
                Interlocked.Increment(ref processed);
                await Task.Delay(10);
                return x;
            });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await foreach (var _ in query) { }
        });

        Assert.True(processed < 100, $"Ordered token cancel: processed {processed}");
    }

    [Fact]
    public async Task Unordered_TokenCancelsProperly()
    {
        using var cts = new CancellationTokenSource();
        var processed = 0;

        var source = SlowSource(100, delayMs: 50);
        _ = Task.Run(async () => { await Task.Delay(200); cts.Cancel(); });

        var query = source.AsParallel()
            .WithOrderPreservation(false)
            .WithCancellation(cts.Token)
            .Select(async x =>
            {
                Interlocked.Increment(ref processed);
                await Task.Delay(10);
                return x;
            });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await foreach (var _ in query) { }
        });

        Assert.True(processed < 100, $"Unordered token cancel: processed {processed}");
    }

    #endregion

    #region Region 6: Chained Operator Cancellation

    [Fact]
    public async Task Chain_WhereSelectTake_TimeoutPropagates()
    {
        var source = SlowSource(100, delayMs: 50);

        var query = source.AsParallel()
            .WithTimeout(TimeSpan.FromMilliseconds(200))
            .Where(async x => { await Task.Delay(5); return x % 2 == 0; })
            .Select(async x => { await Task.Delay(5); return x * 10; })
            .Take(30);

        await AssertCancelsWithinMs(async () =>
        {
            await foreach (var _ in query) { }
        }, maxMs: 3000);
    }

    [Fact]
    public async Task Chain_WhereSelectTake_TokenPropagates()
    {
        using var cts = new CancellationTokenSource();
        var source = SlowSource(100, delayMs: 50);

        _ = Task.Run(async () => { await Task.Delay(200); cts.Cancel(); });

        var query = source.AsParallel()
            .WithCancellation(cts.Token)
            .Where(async x => { await Task.Delay(5); return x % 2 == 0; })
            .Select(async x => { await Task.Delay(5); return x * 10; })
            .Take(30);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await foreach (var _ in query) { }
        });
    }

    [Fact]
    public async Task Chain_SelectMany_TimeoutPropagates()
    {
        var source = SlowSource(50, delayMs: 50);

        var query = source.AsParallel()
            .WithTimeout(TimeSpan.FromMilliseconds(200))
            .SelectMany(x => InnerSource(3, delayMs: 10));

        await AssertCancelsWithinMs(async () =>
        {
            await foreach (var _ in query) { }
        }, maxMs: 3000);
    }

    [Fact]
    public async Task Chain_WithIgnorantSource_TokenStillFires()
    {
        // Source does NOT support [EnumeratorCancellation]
        // ThrowIfCancellationRequested in SourceParallelAsyncQuery should catch it
        using var cts = new CancellationTokenSource();

        var source = IgnorantSource(100, delayMs: 50);
        _ = Task.Run(async () => { await Task.Delay(200); cts.Cancel(); });

        var query = source.AsParallel()
            .WithCancellation(cts.Token)
            .Select(async x => { await Task.Delay(10); return x; });

        var processed = new List<int>();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await foreach (var item in query)
            {
                processed.Add(item);
            }
        });

        Assert.True(processed.Count < 100,
            $"Ignorant source: expected early cancel, got {processed.Count} items");
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Async source that supports [EnumeratorCancellation] and introduces delay per item.
    /// </summary>
    private static async IAsyncEnumerable<int> SlowSource(
        int count, int delayMs,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        for (int i = 0; i < count; i++)
        {
            ct.ThrowIfCancellationRequested();
            if (delayMs > 0) await Task.Delay(delayMs, ct);
            yield return i;
        }
    }

    /// <summary>
    /// Async source that deliberately ignores cancellation.
    /// Simulates a real-world source (e.g., database cursor) that doesn't support [EnumeratorCancellation].
    /// The ParallelAsyncQuery's ThrowIfCancellationRequested must catch this.
    /// </summary>
    private static async IAsyncEnumerable<int> IgnorantSource(int count, int delayMs)
    {
        for (int i = 0; i < count; i++)
        {
            if (delayMs > 0) await Task.Delay(delayMs); // No CancellationToken!
            yield return i;
        }
    }

    /// <summary>
    /// Inner source for SelectMany tests.
    /// </summary>
    private static async IAsyncEnumerable<int> InnerSource(
        int count, int delayMs,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        for (int i = 0; i < count; i++)
        {
            if (delayMs > 0) await Task.Delay(delayMs, ct);
            yield return i;
        }
    }

    /// <summary>
    /// Assert that the action throws OperationCanceledException within the given time window.
    /// </summary>
    private static async Task AssertCancelsWithinMs(Func<Task> action, int maxMs)
    {
        var sw = Stopwatch.StartNew();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(action);
        sw.Stop();
        Assert.True(sw.ElapsedMilliseconds < maxMs,
            $"Expected cancellation within {maxMs}ms, took {sw.ElapsedMilliseconds}ms");
    }

    #endregion
}
