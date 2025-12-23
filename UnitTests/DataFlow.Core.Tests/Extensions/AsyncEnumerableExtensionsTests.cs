using DataFlow.Extensions;

namespace DataFlow.Core.Tests.Extensions;

/// <summary>
/// Tests for AsyncEnumerableExtensions - MergeOrdered, Until, ForEach, Do.
/// </summary>
public class AsyncEnumerableExtensionsTests
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

    private static async Task<List<T>> CollectAsync<T>(IAsyncEnumerable<T> source)
    {
        var result = new List<T>();
        await foreach (var item in source)
            result.Add(item);
        return result;
    }

    #endregion

    #region MergeOrdered

    [Fact]
    public async Task MergeOrdered_TwoSortedSequences_MergesCorrectly()
    {
        // Arrange
        var seq1 = ToAsync(new[] { 1, 3, 5 });
        var seq2 = ToAsync(new[] { 2, 4, 6 });

        // Act
        var result = await CollectAsync(seq1.MergeOrdered(seq2, (a, b) => a <= b));

        // Assert
        Assert.Equal(new[] { 1, 2, 3, 4, 5, 6 }, result);
    }

    [Fact]
    public async Task MergeOrdered_FirstExhaustedFirst_DrainsSecond()
    {
        // Arrange
        var seq1 = ToAsync(new[] { 1, 2 });
        var seq2 = ToAsync(new[] { 3, 4, 5 });

        // Act
        var result = await CollectAsync(seq1.MergeOrdered(seq2, (a, b) => a <= b));

        // Assert
        Assert.Equal(new[] { 1, 2, 3, 4, 5 }, result);
    }

    [Fact]
    public async Task MergeOrdered_EmptyFirst_ReturnsSecond()
    {
        // Arrange
        var seq1 = ToAsync(Array.Empty<int>());
        var seq2 = ToAsync(new[] { 1, 2, 3 });

        // Act
        var result = await CollectAsync(seq1.MergeOrdered(seq2, (a, b) => a <= b));

        // Assert
        Assert.Equal(new[] { 1, 2, 3 }, result);
    }

    #endregion

    #region Until

    [Fact]
    public async Task Until_WithPredicate_StopsAtMatch()
    {
        // Arrange
        var items = ToAsync(new[] { 1, 2, 3, 4, 5 });

        // Act
        var result = await CollectAsync(items.Until(n => n == 3));

        // Assert
        Assert.Equal(new[] { 1, 2, 3 }, result);
    }

    [Fact]
    public async Task Until_WithIndex_StopsAtIndex()
    {
        // Arrange
        var items = ToAsync(new[] { "a", "b", "c", "d", "e" });

        // Act
        var result = await CollectAsync(items.Until(2));

        // Assert
        Assert.Equal(new[] { "a", "b", "c" }, result);
    }

    [Fact]
    public async Task Until_WithIndexedPredicate_StopsCorrectly()
    {
        // Arrange
        var items = ToAsync(new[] { 10, 20, 30, 40, 50 });

        // Act - stop when index > 2
        var result = await CollectAsync(items.Until((n, idx) => idx > 2));

        // Assert
        Assert.Equal(new[] { 10, 20, 30, 40 }, result);
    }

    [Fact]
    public async Task Until_NoMatch_ReturnsAll()
    {
        // Arrange
        var items = ToAsync(new[] { 1, 2, 3 });

        // Act
        var result = await CollectAsync(items.Until(n => n > 100));

        // Assert
        Assert.Equal(new[] { 1, 2, 3 }, result);
    }

    #endregion

    #region ForEach

    [Fact]
    public async Task ForEach_WithAction_ExecutesAndPassesThrough()
    {
        // Arrange
        var processed = new List<int>();
        var items = ToAsync(new[] { 1, 2, 3 });

        // Act
        var result = await CollectAsync(items.ForEach(n => processed.Add(n)));

        // Assert
        Assert.Equal(new[] { 1, 2, 3 }, result);
        Assert.Equal(new[] { 1, 2, 3 }, processed);
    }

    [Fact]
    public async Task ForEach_WithIndexedAction_PassesIndex()
    {
        // Arrange
        var indexed = new List<string>();
        var items = ToAsync(new[] { "a", "b", "c" });

        // Act
        var result = await CollectAsync(items.ForEach((item, idx) => indexed.Add($"{item}:{idx}")));

        // Assert
        Assert.Equal(new[] { "a", "b", "c" }, result);
        Assert.Equal(new[] { "a:0", "b:1", "c:2" }, indexed);
    }

    #endregion

    #region Do

    [Fact]
    public async Task Do_ForcesEnumeration()
    {
        // Arrange
        var count = 0;
        var items = ToAsync(Enumerable.Range(1, 5)).ForEach(_ => count++);

        // Act
        await items.Do();

        // Assert
        Assert.Equal(5, count);
    }

    #endregion
}
