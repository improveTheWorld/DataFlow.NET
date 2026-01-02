using DataFlow.Extensions;
using DataFlow.Framework;
using System.Collections.Concurrent;

namespace DataFlow.Core.Tests.Extensions;

/// <summary>
/// Additional tests for ParallelAsyncQueryExtensions to increase coverage.
/// </summary>
public class ParallelAsyncQueryExtensionsAdditionalTests
{
    #region Helpers

    private static async IAsyncEnumerable<T> ToAsync<T>(IEnumerable<T> items)
    {
        foreach (var item in items)
        {
            await Task.Yield();
            yield return item;
        }
    }

    #endregion

    #region Sum - Decimal

    [Fact]
    public async Task Sum_Decimal_CalculatesTotal()
    {
        // Arrange
        var source = ToAsync(new decimal[] { 1.5m, 2.5m, 3.0m });
        var query = source.AsParallel();

        // Act
        var result = await query.Sum();

        // Assert
        Assert.Equal(7.0m, result);
    }

    #endregion

    #region BuildString

    [Fact]
    public async Task BuildString_ConcatenatesStrings()
    {
        // Arrange
        var source = ToAsync(new[] { "a", "b", "c" });
        var query = source.AsParallel();

        // Act
        var result = await query.BuildString(null, ", ", "{", "}");

        // Assert
        // May not be ordered, but should contain all chars
        Assert.Contains("a", result.ToString());
        Assert.Contains("b", result.ToString());
        Assert.Contains("c", result.ToString());
    }

    #endregion

    #region Large sequences

    [Fact]
    public async Task ForEach_LargeSequence_ExecutesAll()
    {
        // Arrange
        var count = 0;
        var source = ToAsync(Enumerable.Range(1, 100));
        var query = source.AsParallel().ForEach(_ => Interlocked.Increment(ref count));

        // Act
        await query.Do();

        // Assert
        Assert.Equal(100, count);
    }

    [Fact]
    public async Task Sum_Large_CalculatesCorrectly()
    {
        // Arrange
        var source = ToAsync(Enumerable.Range(1, 100));
        var query = source.AsParallel();

        // Act
        var result = await query.Sum();

        // Assert
        Assert.Equal(5050, result);  // Sum of 1 to 100
    }

    #endregion

    #region Settings variations

    [Fact]
    public async Task WithMaxConcurrency_ExecutesWithLimit()
    {
        // Arrange
        var count = 0;
        var source = ToAsync(Enumerable.Range(1, 10));
        var query = source.AsParallel()
            .WithMaxConcurrency(2)
            .ForEach(_ => Interlocked.Increment(ref count));

        // Act
        await query.Do();

        // Assert
        Assert.Equal(10, count);
    }

    [Fact]
    public async Task WithSequential_ExecutesCorrectly()
    {
        // Arrange
        var count = 0;
        var source = ToAsync(Enumerable.Range(1, 10));
        var query = source.AsParallel()
            .WithMaxConcurrency(1)  // Sequential
            .ForEach(_ => Interlocked.Increment(ref count));

        // Act
        await query.Do();

        // Assert
        Assert.Equal(10, count);
    }

    #endregion
}
