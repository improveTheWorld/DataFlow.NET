using DataFlow.Extensions;
using DataFlow.Framework;
using System.Collections.Concurrent;

namespace DataFlow.Core.Tests.Extensions;

/// <summary>
/// Tests for ParallelAsyncQueryExtensions - ForEach, Do, Sum.
/// </summary>
public class ParallelAsyncQueryExtensionsTests
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

    #region ForEach (synchronous action)

    [Fact]
    public async Task ForEach_SyncAction_ExecutesForEachElement()
    {
        // Arrange
        var bag = new ConcurrentBag<int>();
        var source = ToAsync(new[] { 1, 2, 3, 4, 5 });
        var query = source.AsParallel().ForEach(n => bag.Add(n));

        // Act
        await query.Do();

        // Assert
        Assert.Equal(5, bag.Count);
        Assert.Contains(1, bag);
        Assert.Contains(5, bag);
    }

    [Fact]
    public async Task ForEach_WithIndex_PassesIndex()
    {
        // Arrange
        var logged = new ConcurrentBag<string>();
        var source = ToAsync(new[] { "a", "b", "c" });
        var query = source.AsParallel().ForEach((s, idx) => logged.Add($"{s}:{idx}"));

        // Act
        await query.Do();

        // Assert
        Assert.Equal(3, logged.Count);
    }

    #endregion

    #region ForEach (async action)

    [Fact]
    public async Task ForEach_AsyncAction_ExecutesForEachElement()
    {
        // Arrange
        var bag = new ConcurrentBag<int>();
        var source = ToAsync(new[] { 1, 2, 3 });
        var query = source.AsParallel().ForEach(async n =>
        {
            await Task.Delay(1);
            bag.Add(n);
        });

        // Act
        await query.Do();

        // Assert
        Assert.Equal(3, bag.Count);
    }

    [Fact]
    public async Task ForEach_AsyncWithIndex_ExecutesForEachElement()
    {
        // Arrange
        var logged = new ConcurrentBag<string>();
        var source = ToAsync(new[] { "x", "y" });
        var query = source.AsParallel().ForEach(async (s, idx) =>
        {
            await Task.Delay(1);
            logged.Add($"{s}:{idx}");
        });

        // Act
        await query.Do();

        // Assert
        Assert.Equal(2, logged.Count);
    }

    #endregion

    #region Do

    [Fact]
    public async Task Do_ForcesEnumeration()
    {
        // Arrange
        var count = 0;
        var source = ToAsync(Enumerable.Range(1, 5));
        var query = source.AsParallel().ForEach(_ => Interlocked.Increment(ref count));

        // Act
        await query.Do();

        // Assert
        Assert.Equal(5, count);
    }

    [Fact]
    public async Task Do_EmptySource_NoExecution()
    {
        // Arrange
        var count = 0;
        var source = ToAsync(Array.Empty<int>());
        var query = source.AsParallel().ForEach(_ => Interlocked.Increment(ref count));

        // Act
        await query.Do();

        // Assert
        Assert.Equal(0, count);
    }

    #endregion

    #region Sum

    [Fact]
    public async Task Sum_Int_CalculatesTotal()
    {
        // Arrange
        var source = ToAsync(new[] { 1, 2, 3, 4, 5 });
        var query = source.AsParallel();

        // Act
        var result = await query.Sum();

        // Assert
        Assert.Equal(15, result);
    }

    [Fact]
    public async Task Sum_Empty_ReturnsZero()
    {
        // Arrange
        var source = ToAsync(Array.Empty<int>());
        var query = source.AsParallel();

        // Act
        var result = await query.Sum();

        // Assert
        Assert.Equal(0, result);
    }

    [Fact]
    public async Task Sum_Long_CalculatesTotal()
    {
        // Arrange
        var source = ToAsync(new long[] { 1L, 2L, 3L });
        var query = source.AsParallel();

        // Act
        var result = await query.Sum();

        // Assert
        Assert.Equal(6L, result);
    }

    #endregion
}
