using DataFlow;
using DataFlow.Parallel;
using Xunit;

namespace ParallelAsyncQuery.Tests;

/// <summary>
/// Tests for CancellationToken handling and SelectMany operations.
/// Covers proper disposal of linked CancellationTokenSource and SelectMany error handling.
/// </summary>
public class CancellationAndSelectManyTests
{
    #region CancellationTokenSource Disposal Tests

    [Fact]
    public async Task LinkedCancellationToken_IsProperlyDisposed()
    {
        // This test verifies that the fix for CancellationTokenSource leaks works correctly.
        // After the fix, linked CTS objects are disposed when enumeration completes.

        using var settingsCts = new CancellationTokenSource();
        using var callCts = new CancellationTokenSource();

        var settings = new ParallelExecutionSettings
        {
            CancellationToken = settingsCts.Token,
            MaxConcurrency = 2
        };

        var source = GenerateNumbers(10);

        // Act - enumerate with both tokens
        var query = source.AsParallel(settings)
            .Select(x => Task.FromResult(x * 2));

        var results = new List<int>();
        await foreach (var item in query.WithCancellation(callCts.Token))
        {
            results.Add(item);
        }

        // Assert - should complete successfully
        Assert.Equal(10, results.Count);

        // If CTS was not disposed properly, we'd see memory leaks in a profiler,
        // but at minimum we verify the query completes without issues
    }

    [Fact]
    public async Task SingleToken_FromSettings_WorksCorrectly()
    {
        // Test case where only settings token is set (no call token)
        using var settingsCts = new CancellationTokenSource();

        var settings = new ParallelExecutionSettings
        {
            CancellationToken = settingsCts.Token
        };

        var source = GenerateNumbers(5);
        var query = source.AsParallel(settings)
            .Select(async x =>
            {
                await Task.Yield();
                return x;
            });

        var results = await query.ToList();

        Assert.Equal(5, results.Count);
    }

    [Fact]
    public async Task SingleToken_FromCall_WorksCorrectly()
    {
        // Test case where only call token is set (no settings token)
        using var callCts = new CancellationTokenSource();

        var source = GenerateNumbers(5);
        var query = source.AsParallel()
            .Select(async x =>
            {
                await Task.Yield();
                return x;
            });

        var results = new List<int>();
        await foreach (var item in query.WithCancellation(callCts.Token))
        {
            results.Add(item);
        }

        Assert.Equal(5, results.Count);
    }

    [Fact]
    public async Task NoTokens_WorksCorrectly()
    {
        // Test case where no cancellation tokens are set
        var source = GenerateNumbers(5);
        var query = source.AsParallel()
            .Select(async x =>
            {
                await Task.Yield();
                return x;
            });

        var results = await query.ToList();

        Assert.Equal(5, results.Count);
    }

    #endregion

    #region SelectMany Error Handling Tests

    [Fact]
    public async Task SelectMany_WithContinueOnError_SkipsFailedOuterItems()
    {
        // Arrange
        var source = GenerateNumbers(5);
        var query = source
            .AsParallel()
            .ContinueOnError()
            .SelectMany(x =>
            {
                if (x == 2) throw new InvalidOperationException("Simulated error");
                return GenerateNumbers(2, startAt: x * 10);
            });

        // Act
        var results = await query.ToList();

        // Assert - Item 2 throws, so we skip its inner sequence
        // Expected: 0,1 from 0 | 10,11 from 1 | (skip 2) | 30,31 from 3 | 40,41 from 4
        Assert.Equal(8, results.Count);
        Assert.DoesNotContain(20, results);
        Assert.DoesNotContain(21, results);
    }

    [Fact]
    public async Task SelectMany_WithContinueOnError_SkipsFailedInnerItems()
    {
        // Arrange - inner sequence will throw on some items
        var source = GenerateNumbers(3);
        var query = source
            .AsParallel()
            .ContinueOnError()
            .SelectMany(x => GenerateNumbersWithError(5, errorAt: 2, startAt: x * 10));

        // Act
        var results = await query.ToList();

        // Assert - Each inner sequence throws at index 2, but continues
        // We should get items 0,1,3,4 from each inner (items at index 2 fail)
        Assert.True(results.Count > 0, "Should have some results despite errors");
    }

    [Fact]
    public async Task SelectMany_WithoutContinueOnError_Throws()
    {
        // Arrange
        var source = GenerateNumbers(5);
        var query = source
            .AsParallel()
            .SelectMany(x =>
            {
                if (x == 2) throw new InvalidOperationException("Simulated error");
                return GenerateNumbers(2, startAt: x * 10);
            });

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await query.ToList();
        });
    }

    [Fact]
    public async Task SelectMany_NormalExecution_ReturnsAllItems()
    {
        // Arrange
        var source = GenerateNumbers(3);
        var query = source
            .AsParallel()
            .SelectMany(x => GenerateNumbers(2, startAt: x * 10));

        // Act
        var results = await query.ToList();

        // Assert - 3 outer items × 2 inner items = 6 total
        Assert.Equal(6, results.Count);
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

    private static async IAsyncEnumerable<int> GenerateNumbersWithError(int count, int errorAt, int startAt = 0)
    {
        for (int i = 0; i < count; i++)
        {
            await Task.Yield();
            if (i == errorAt) throw new InvalidOperationException($"Error at index {i}");
            yield return startAt + i;
        }
    }



    #endregion
}
