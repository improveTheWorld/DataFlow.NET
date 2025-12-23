using DataFlow.Extensions;

namespace DataFlow.Core.Tests.Extensions;

/// <summary>
/// Tests for EnumerableDebuggingExtension - ToLines, Spy, Display.
/// </summary>
public class EnumerableDebuggingExtensionTests
{
    #region ToLines

    [Fact]
    public void ToLines_SplitsBySeparator()
    {
        // Arrange
        var slices = new[] { "a", "b", "|", "c", "d", "|", "e" };

        // Act
        var result = slices.ToLines("|").ToList();

        // Assert
        Assert.Equal(2, result.Count);  // Two complete lines (before last separator)
        Assert.Equal("ab", result[0]);
        Assert.Equal("cd", result[1]);
        // Note: "e" is incomplete (no trailing separator) so not captured
    }

    [Fact]
    public void ToLines_NoSeparator_ReturnsEmpty()
    {
        // Arrange
        var slices = new[] { "a", "b", "c" };

        // Act
        var result = slices.ToLines("|").ToList();

        // Assert - no separators means no complete lines
        Assert.Empty(result);
    }

    [Fact]
    public void ToLines_ConsecutiveSeparators_CreateEmptyLines()
    {
        // Arrange
        var slices = new[] { "a", "|", "|", "b", "|" };

        // Act
        var result = slices.ToLines("|").ToList();

        // Assert
        Assert.Equal(3, result.Count);
        Assert.Equal("a", result[0]);
        Assert.Equal("", result[1]);   // empty between |  |
        Assert.Equal("b", result[2]);
    }

    #endregion

    #region Spy

    [Fact]
    public void Spy_PassesElementsThrough()
    {
        // Arrange
        var items = new[] { "one", "two", "three" };
        var output = new StringWriter();
        Console.SetOut(output);

        // Act - Spy should pass elements through unchanged
        var result = items.Spy("Test").ToList();

        // Assert - elements are passed through
        Assert.Equal(new[] { "one", "two", "three" }, result);
    }

    [Fact]
    public void Spy_WithFormatter_PassesElementsThrough()
    {
        // Arrange
        var items = new[] { 1, 2, 3 };
        var output = new StringWriter();
        Console.SetOut(output);

        // Act
        var result = items.Spy("Numbers", n => $"Value: {n}").ToList();

        // Assert
        Assert.Equal(new[] { 1, 2, 3 }, result);
    }

    #endregion

    #region Display Integration (smoke test)

    [Fact]
    public void Display_EnumeratesAndWrites()
    {
        // Arrange
        var items = new[] { "a", "b", "c" };
        var output = new StringWriter();
        Console.SetOut(output);

        // Act
        items.Display("Test");

        // Assert - Just verify it ran without exception
        // Display is a debugging method so we just ensure it doesn't crash
        Assert.True(true);
    }

    #endregion
}
