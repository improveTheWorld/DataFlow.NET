using DataFlow.Extensions;

namespace DataFlow.Core.Tests.Extensions;

/// <summary>
/// Tests for AsyncEnumerableFlatteningExtensions - Flatten overloads.
/// </summary>
public class AsyncEnumerableFlatteningExtensionsTests
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

    #region Flatten - Async of Async

    [Fact]
    public async Task Flatten_AsyncOfAsync_FlattensNested()
    {
        // Arrange
        var nested = ToAsync(new[]
        {
            ToAsync(new[] { 1, 2 }),
            ToAsync(new[] { 3, 4, 5 })
        });

        // Act
        var result = await CollectAsync(nested.Flatten());

        // Assert
        Assert.Equal(new[] { 1, 2, 3, 4, 5 }, result);
    }

    #endregion

    #region Flatten - Async of Sync

    [Fact]
    public async Task Flatten_AsyncOfSync_FlattensNested()
    {
        // Arrange
        var nested = ToAsync(new IEnumerable<int>[]
        {
            new[] { 1, 2 },
            new[] { 3, 4 }
        });

        // Act
        var result = await CollectAsync(nested.Flatten());

        // Assert
        Assert.Equal(new[] { 1, 2, 3, 4 }, result);
    }

    [Fact]
    public async Task Flatten_AsyncOfSync_EmptyInner_IsSkipped()
    {
        // Arrange
        var nested = ToAsync(new IEnumerable<int>[]
        {
            new[] { 1 },
            Array.Empty<int>(),
            new[] { 2 }
        });

        // Act
        var result = await CollectAsync(nested.Flatten());

        // Assert
        Assert.Equal(new[] { 1, 2 }, result);
    }

    #endregion

    #region Flatten - Sync of Async

    [Fact]
    public async Task Flatten_SyncOfAsync_FlattensNested()
    {
        // Arrange
        var nested = new[]
        {
            ToAsync(new[] { "a", "b" }),
            ToAsync(new[] { "c" })
        };

        // Act
        var result = await CollectAsync(nested.Flatten());

        // Assert
        Assert.Equal(new[] { "a", "b", "c" }, result);
    }

    #endregion

    #region Flatten with separator

    [Fact]
    public async Task Flatten_WithSeparator_InsertsBetweenAndAfter()
    {
        // Arrange
        var nested = ToAsync(new IEnumerable<string>[]
        {
            new[] { "a", "b" },
            new[] { "c" }
        });

        // Act
        var result = await CollectAsync(nested.Flatten("|"));

        // Assert
        Assert.Equal(new[] { "a", "b", "|", "c", "|" }, result);
    }

    [Fact]
    public async Task Flatten_SyncOfAsync_WithSeparator()
    {
        // Arrange
        var nested = new[]
        {
            ToAsync(new[] { 1, 2 }),
            ToAsync(new[] { 3 })
        };

        // Act
        var result = await CollectAsync(nested.Flatten(0));

        // Assert
        Assert.Equal(new[] { 1, 2, 0, 3, 0 }, result);
    }

    #endregion
}
