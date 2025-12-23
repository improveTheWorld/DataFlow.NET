using DataFlow.Extensions;

namespace DataFlow.Core.Tests.Extensions;

/// <summary>
/// Tests for EnumerableFlatteningExtensions - Flatten methods.
/// </summary>
public class EnumerableFlatteningExtensionsTests
{
    #region Flatten without separator

    [Fact]
    public void Flatten_NestedSequences_ConcatenatesAll()
    {
        // Arrange
        var nested = new[]
        {
            new[] { 1, 2 },
            new[] { 3 },
            new[] { 4, 5 }
        };

        // Act
        var result = nested.Flatten().ToList();

        // Assert
        Assert.Equal(new[] { 1, 2, 3, 4, 5 }, result);
    }

    [Fact]
    public void Flatten_EmptyInnerSequences_AreSkipped()
    {
        // Arrange
        var nested = new[]
        {
            new[] { 1, 2 },
            Array.Empty<int>(),
            new[] { 3 }
        };

        // Act
        var result = nested.Flatten().ToList();

        // Assert
        Assert.Equal(new[] { 1, 2, 3 }, result);
    }

    [Fact]
    public void Flatten_EmptyOuter_ReturnsEmpty()
    {
        // Arrange
        var nested = Array.Empty<IEnumerable<int>>();

        // Act
        var result = nested.Flatten().ToList();

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void Flatten_SingleInnerSequence_ReturnsItsElements()
    {
        // Arrange
        var nested = new[] { new[] { 1, 2, 3 } };

        // Act
        var result = nested.Flatten().ToList();

        // Assert
        Assert.Equal(new[] { 1, 2, 3 }, result);
    }

    [Fact]
    public void Flatten_NullInnerSequence_IsSkipped()
    {
        // Arrange
        IEnumerable<IEnumerable<int>> nested = new List<IEnumerable<int>>
        {
            new[] { 1, 2 },
            null!,
            new[] { 3 }
        };

        // Act
        var result = nested.Flatten().ToList();

        // Assert
        Assert.Equal(new[] { 1, 2, 3 }, result);
    }

    #endregion

    #region Flatten with separator

    [Fact]
    public void Flatten_WithSeparator_InsertsBetweenAndAfter()
    {
        // Arrange
        var nested = new[]
        {
            new[] { "a", "b" },
            new[] { "c" }
        };

        // Act
        var result = nested.Flatten("|").ToList();

        // Assert - separator after each inner sequence
        Assert.Equal(new[] { "a", "b", "|", "c", "|" }, result);
    }

    [Fact]
    public void Flatten_WithSeparator_EmptyInner_StillAddsSeparator()
    {
        // Arrange
        var nested = new[]
        {
            new[] { "a" },
            Array.Empty<string>(),
            new[] { "b" }
        };

        // Act
        var result = nested.Flatten("|").ToList();

        // Assert - empty sequence still gets separator (from outer loop)
        Assert.Equal(new[] { "a", "|", "|", "b", "|" }, result);
    }

    [Fact]
    public void Flatten_WithIntSeparator_Works()
    {
        // Arrange
        var nested = new[]
        {
            new[] { 1, 2 },
            new[] { 3 }
        };

        // Act
        var result = nested.Flatten(0).ToList();

        // Assert
        Assert.Equal(new[] { 1, 2, 0, 3, 0 }, result);
    }

    #endregion
}
