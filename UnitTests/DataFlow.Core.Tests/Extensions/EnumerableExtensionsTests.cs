using DataFlow.Extensions;

namespace DataFlow.Core.Tests.Extensions;

/// <summary>
/// Tests for EnumerableExtensions - ForEach, Do, Until methods.
/// </summary>
public class EnumerableExtensionsTests
{
    #region ForEach

    [Fact]
    public void ForEach_WithAction_ExecutesAndPassesThrough()
    {
        // Arrange
        var processed = new List<int>();
        var items = new[] { 1, 2, 3 };

        // Act - ForEach preserves sequence while executing action
        var result = items.ForEach(n => processed.Add(n)).ToList();

        // Assert
        Assert.Equal(new[] { 1, 2, 3 }, result);
        Assert.Equal(new[] { 1, 2, 3 }, processed);
    }

    [Fact]
    public void ForEach_WithIndexedAction_PassesIndex()
    {
        // Arrange
        var indexed = new List<string>();
        var items = new[] { "a", "b", "c" };

        // Act
        var result = items.ForEach((item, idx) => indexed.Add($"{item}:{idx}")).ToList();

        // Assert
        Assert.Equal(new[] { "a", "b", "c" }, result);
        Assert.Equal(new[] { "a:0", "b:1", "c:2" }, indexed);
    }

    #endregion

    #region Do

    [Fact]
    public void Do_ForcesEnumeration()
    {
        // Arrange
        var count = 0;
        var items = Enumerable.Range(1, 5).Select(n => { count++; return n; });

        // Act - Do forces enumeration
        items.Do();

        // Assert
        Assert.Equal(5, count);
    }

    [Fact]
    public void Do_WithAction_ExecutesPerElement()
    {
        // Arrange
        var count = 0;
        var items = new[] { 1, 2, 3 };

        // Act - Do calls a parameterless action per element
        items.ForEach((_) => count++).Do();

        // Assert
        Assert.Equal(3, count);
    }

    #endregion

    #region Until

    [Fact]
    public void Until_WithPredicate_StopsAtMatch()
    {
        // Arrange
        var items = new[] { 1, 2, 3, 4, 5 };

        // Act - stop when n == 3 (includes the stop element)
        var result = items.Until(n => n == 3).ToList();

        // Assert
        Assert.Equal(new[] { 1, 2, 3 }, result);
    }

    [Fact]
    public void Until_WithIndex_StopsAtIndex()
    {
        // Arrange
        var items = new[] { "a", "b", "c", "d", "e" };

        // Act - stop at index 2 (includes element at index 2)
        var result = items.Until(2).ToList();

        // Assert
        Assert.Equal(new[] { "a", "b", "c" }, result);
    }

    [Fact]
    public void Until_NoMatch_EnumeratesAll()
    {
        // Arrange
        var items = new[] { 1, 2, 3 };

        // Act - condition never true
        var result = items.Until(n => n > 100).ToList();

        // Assert
        Assert.Equal(new[] { 1, 2, 3 }, result);
    }

    #endregion

    #region MergeOrdered

    [Fact]
    public void MergeOrdered_TwoSortedSequences_MergesCorrectly()
    {
        // Arrange
        var seq1 = new[] { 1, 3, 5 };
        var seq2 = new[] { 2, 4, 6 };

        // Act
        var result = seq1.MergeOrdered(seq2, (a, b) => a <= b).ToList();

        // Assert
        Assert.Equal(new[] { 1, 2, 3, 4, 5, 6 }, result);
    }

    [Fact]
    public void MergeOrdered_FirstExhaustedFirst_DrainsSecond()
    {
        // Arrange
        var seq1 = new[] { 1, 2 };
        var seq2 = new[] { 3, 4, 5, 6 };

        // Act
        var result = seq1.MergeOrdered(seq2, (a, b) => a <= b).ToList();

        // Assert
        Assert.Equal(new[] { 1, 2, 3, 4, 5, 6 }, result);
    }

    [Fact]
    public void MergeOrdered_SecondExhaustedFirst_DrainsFirst()
    {
        // Arrange
        var seq1 = new[] { 3, 4, 5, 6 };
        var seq2 = new[] { 1, 2 };

        // Act
        var result = seq1.MergeOrdered(seq2, (a, b) => a <= b).ToList();

        // Assert
        Assert.Equal(new[] { 1, 2, 3, 4, 5, 6 }, result);
    }

    [Fact]
    public void MergeOrdered_EmptyFirst_ReturnsSecond()
    {
        // Arrange
        var seq1 = Array.Empty<int>();
        var seq2 = new[] { 1, 2, 3 };

        // Act
        var result = seq1.MergeOrdered(seq2, (a, b) => a <= b).ToList();

        // Assert
        Assert.Equal(new[] { 1, 2, 3 }, result);
    }

    [Fact]
    public void MergeOrdered_EmptySecond_ReturnsFirst()
    {
        // Arrange
        var seq1 = new[] { 1, 2, 3 };
        var seq2 = Array.Empty<int>();

        // Act
        var result = seq1.MergeOrdered(seq2, (a, b) => a <= b).ToList();

        // Assert
        Assert.Equal(new[] { 1, 2, 3 }, result);
    }

    [Fact]
    public void MergeOrdered_BothEmpty_ReturnsEmpty()
    {
        // Arrange
        var seq1 = Array.Empty<int>();
        var seq2 = Array.Empty<int>();

        // Act
        var result = seq1.MergeOrdered(seq2, (a, b) => a <= b).ToList();

        // Assert
        Assert.Empty(result);
    }

    #endregion

    #region Take with start/count

    [Fact]
    public void Take_WithStartAndCount_ReturnsSlice()
    {
        // Arrange
        var items = new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 };

        // Act - take 3 elements starting at index 2
        var result = items.Take(2, 3).ToList();

        // Assert
        Assert.Equal(new[] { 2, 3, 4 }, result);
    }

    [Fact]
    public void Take_StartBeyondLength_ReturnsEmpty()
    {
        // Arrange
        var items = new[] { 1, 2, 3 };

        // Act
        var result = items.Take(10, 5).ToList();

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void Take_CountExceedsRemaining_ReturnsAvailable()
    {
        // Arrange
        var items = new[] { 1, 2, 3, 4, 5 };

        // Act - start at 3, request 10 but only 2 available
        var result = items.Take(3, 10).ToList();

        // Assert
        Assert.Equal(new[] { 4, 5 }, result);
    }

    #endregion
}
