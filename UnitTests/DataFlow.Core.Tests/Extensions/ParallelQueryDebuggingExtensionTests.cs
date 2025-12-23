using DataFlow.Extensions;
using System.Collections.Concurrent;

namespace DataFlow.Core.Tests.Extensions;

/// <summary>
/// Tests for ParallelQueryDebuggingExtension - Spy and Display methods.
/// </summary>
public class ParallelQueryDebuggingExtensionTests
{
    #region Spy

    [Fact]
    public void Spy_Strings_PassesThrough()
    {
        // Arrange
        var items = new[] { "a", "b", "c" }.AsParallel();

        // Act
        var result = items.Spy("Test").ToList();

        // Assert
        Assert.Equal(3, result.Count);
    }

    [Fact]
    public void Spy_WithFormatter_PassesThrough()
    {
        // Arrange
        var items = new[] { 1, 2, 3 }.AsParallel();

        // Act
        var result = items.Spy("Numbers", n => $"Value: {n}").ToList();

        // Assert
        Assert.Equal(3, result.Count);
        Assert.Contains(1, result);
        Assert.Contains(2, result);
        Assert.Contains(3, result);
    }

    [Fact]
    public void Spy_Empty_ReturnsEmpty()
    {
        // Arrange
        var items = Array.Empty<string>().AsParallel();

        // Act
        var result = items.Spy("Empty").ToList();

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void Spy_EmptyWithFormatter_ReturnsEmpty()
    {
        // Arrange
        var items = Array.Empty<int>().AsParallel();

        // Act
        var result = items.Spy("Empty", n => n.ToString()).ToList();

        // Assert
        Assert.Empty(result);
    }

    #endregion

    #region Display

    [Fact]
    public void Display_Strings_Materializes()
    {
        // Arrange
        var items = new[] { "x", "y", "z" }.AsParallel();
        var output = new StringWriter();
        Console.SetOut(output);

        // Act & Assert - just verify no exception
        items.Display("Test");
        Assert.True(true);
    }

    [Fact]
    public void Display_Empty_NoException()
    {
        // Arrange
        var items = Array.Empty<string>().AsParallel();
        var output = new StringWriter();
        Console.SetOut(output);

        // Act & Assert
        items.Display("Empty");
        Assert.True(true);
    }

    #endregion
}
