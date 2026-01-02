using DataFlow.Extensions;
using System.Collections.Concurrent;

namespace DataFlow.Core.Tests.Extensions;

/// <summary>
/// Tests for ParallelQueryExtensions - MergeOrdered, Take, ForEach, Do.
/// </summary>
public class ParallelQueryExtensionsTests
{
    #region ForEach

    [Fact]
    public void ForEach_ExecutesForEachElement()
    {
        // Arrange
        var bag = new ConcurrentBag<int>();
        var items = new[] { 1, 2, 3, 4, 5 }.AsParallel();

        // Act
        items.ForEach(n => bag.Add(n)).ToList();

        // Assert
        Assert.Equal(5, bag.Count);
        Assert.Contains(1, bag);
        Assert.Contains(5, bag);
    }

    [Fact]
    public void ForEach_WithIndex_PassesIndex()
    {
        // Arrange
        var logged = new ConcurrentBag<string>();
        var items = new[] { "a", "b", "c" }.AsParallel();

        // Act
        items.ForEach((s, idx) => logged.Add($"{s}:{idx}")).ToList();

        // Assert
        Assert.Equal(3, logged.Count);
    }

    #endregion

    #region Do

    [Fact]
    public void Do_ForcesEnumeration()
    {
        // Arrange
        var count = 0;
        var items = Enumerable.Range(1, 5)
            .AsParallel()
            .ForEach(_ => Interlocked.Increment(ref count));

        // Act
        items.Do();

        // Assert
        Assert.Equal(5, count);
    }

    [Fact]
    public void Do_WithAction_ExecutesPerElement()
    {
        // Arrange
        var count = 0;
        var items = new[] { 1, 2, 3 }.AsParallel();

        // Act
        items.ForEach((_) => Interlocked.Increment(ref count)).Do();

        // Assert
        Assert.Equal(3, count);
    }

    [Fact]
    public void Do_WithAction_DirectCall_ExecutesPerElement()
    {
        // Arrange
        var sum = 0;
        var items = new[] { 1, 2, 3, 4, 5 }.AsParallel();

        // Act - using the new Do(Action<T>) overload
        items.Do(n => Interlocked.Add(ref sum, n));

        // Assert
        Assert.Equal(15, sum);
    }

    [Fact]
    public void Do_WithIndexedAction_ExecutesWithIndices()
    {
        // Arrange
        var logged = new ConcurrentBag<string>();
        var items = new[] { "a", "b", "c" }.AsParallel();

        // Act - using the new Do(Action<T,int>) overload
        items.Do((s, idx) => logged.Add($"{s}:{idx}"));

        // Assert
        Assert.Equal(3, logged.Count);
        // Note: indices may not be in order due to parallel execution
    }

    #endregion


    #region Take with start/count

    [Fact]
    public void Take_WithStartAndCount_ReturnsSlice()
    {
        // Arrange
        var items = Enumerable.Range(0, 10).AsParallel().AsOrdered();

        // Act
        var result = items.Take(2, 3).ToList();

        // Assert
        Assert.Equal(3, result.Count);
        Assert.Contains(2, result);
        Assert.Contains(3, result);
        Assert.Contains(4, result);
    }

    #endregion

    #region MergeOrdered

    [Fact]
    public void MergeOrdered_MergesTwoSortedSequences()
    {
        // Arrange
        var seq1 = new[] { 1, 3, 5 }.AsParallel().AsOrdered();
        var seq2 = new[] { 2, 4, 6 }.AsParallel().AsOrdered();

        // Act
        var result = seq1.MergeOrdered(seq2, (a, b) => a <= b).ToList();

        // Assert
        Assert.Equal(new[] { 1, 2, 3, 4, 5, 6 }, result);
    }

    [Fact]
    public void MergeOrdered_EmptyFirst_ReturnsSecond()
    {
        // Arrange
        var seq1 = Array.Empty<int>().AsParallel();
        var seq2 = new[] { 1, 2, 3 }.AsParallel().AsOrdered();

        // Act
        var result = seq1.MergeOrdered(seq2, (a, b) => a <= b).ToList();

        // Assert
        Assert.Equal(new[] { 1, 2, 3 }, result);
    }

    #endregion
}
