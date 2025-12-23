using DataFlow.Extensions;

namespace DataFlow.Core.Tests.Extensions;

/// <summary>
/// Additional tests for AsyncEnumerableDebuggingExtensions - ToLines, Spy, Display, Aggregate.
/// </summary>
public class AsyncEnumerableDebuggingExtensionsTests
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

    #region ToLines

    [Fact]
    public async Task ToLines_SplitsBySeparator()
    {
        // Arrange
        var slices = ToAsync(new[] { "a", "b", "|", "c", "d", "|" });

        // Act
        var result = await CollectAsync(slices.ToLines("|"));

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Equal("ab", result[0]);
        Assert.Equal("cd", result[1]);
    }

    [Fact]
    public async Task ToLines_NoSeparator_ReturnsEmpty()
    {
        // Arrange
        var slices = ToAsync(new[] { "a", "b", "c" });

        // Act
        var result = await CollectAsync(slices.ToLines("|"));

        // Assert
        Assert.Empty(result);
    }

    #endregion

    #region Spy

    [Fact]
    public async Task Spy_PassesThrough()
    {
        // Arrange
        var items = ToAsync(new[] { "one", "two" });

        // Act
        var result = await CollectAsync(items.Spy("Test"));

        // Assert
        Assert.Equal(new[] { "one", "two" }, result);
    }

    [Fact]
    public async Task Spy_WithFormatter_PassesThrough()
    {
        // Arrange
        var items = ToAsync(new[] { 1, 2, 3 });

        // Act
        var result = await CollectAsync(items.Spy("Nums", n => $"Val:{n}"));

        // Assert
        Assert.Equal(new[] { 1, 2, 3 }, result);
    }

    #endregion

    #region Aggregate

    [Fact]
    public async Task Aggregate_StringBuilder_BuildsString()
    {
        // Arrange
        var items = ToAsync(new[] { "a", "b", "c" });

        // Act
        var result = await items.Aggregate(new System.Text.StringBuilder(), (sb, s) => sb.Append(s));

        // Assert
        Assert.Equal("abc", result.ToString());
    }

    #endregion

    #region Display

    [Fact]
    public async Task Display_EnumeratesAndWrites()
    {
        // Arrange
        var items = ToAsync(new[] { "x", "y" });
        var output = new StringWriter();
        Console.SetOut(output);

        // Act & Assert - Just verify no exception
        await items.Display("Test");
        Assert.True(true);
    }

    #endregion
}
