using DataFlow.Extensions;
using System.Collections.Concurrent;

namespace DataFlow.Core.Tests.Extensions;

/// <summary>
/// Final push tests for ParallelQueryExtensions - additional ForEach, Do, Take scenarios.
/// </summary>
public class ParallelQueryExtensionsFinalTests
{
    #region Additional ForEach

    [Fact]
    public void ForEach_ChainedCalls_ExecutesBoth()
    {
        // Arrange
        var bag1 = new ConcurrentBag<int>();
        var bag2 = new ConcurrentBag<int>();
        var items = new[] { 1, 2, 3 }.AsParallel();

        // Act
        items.ForEach(n => bag1.Add(n))
             .ForEach(n => bag2.Add(n * 10))
             .ToList();

        // Assert
        Assert.Equal(3, bag1.Count);
        Assert.Equal(3, bag2.Count);
    }

    [Fact]
    public void ForEach_WithIndexed_ReceivesIndices()
    {
        // Arrange
        var indexed = new ConcurrentBag<(int val, int idx)>();
        var items = new[] { 10, 20, 30 }.AsParallel().AsOrdered();

        // Act
        items.ForEach((n, i) => indexed.Add((n, i))).ToList();

        // Assert
        Assert.Equal(3, indexed.Count);
    }

    #endregion

    #region Take edge cases

    [Fact]
    public void Take_StartZero_ReturnsSameAsCount()
    {
        // Arrange
        var items = Enumerable.Range(0, 10).AsParallel().AsOrdered();

        // Act
        var result = items.Take(0, 5).ToList();

        // Assert
        Assert.Equal(5, result.Count);
    }

    [Fact]
    public void Take_StartBeyondSequence_ReturnsEmpty()
    {
        // Arrange
        var items = new[] { 1, 2, 3 }.AsParallel();

        // Act
        var result = items.Take(10, 5).ToList();

        // Assert
        Assert.Empty(result);
    }

    #endregion

    #region MergeOrdered edge cases

    [Fact]
    public void MergeOrdered_BothEmpty_ReturnsEmpty()
    {
        // Arrange
        var seq1 = Array.Empty<int>().AsParallel();
        var seq2 = Array.Empty<int>().AsParallel();

        // Act
        var result = seq1.MergeOrdered(seq2, (a, b) => a <= b).ToList();

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void MergeOrdered_EmptySecond_ReturnsFirst()
    {
        // Arrange
        var seq1 = new[] { 1, 2, 3 }.AsParallel().AsOrdered();
        var seq2 = Array.Empty<int>().AsParallel();

        // Act
        var result = seq1.MergeOrdered(seq2, (a, b) => a <= b).ToList();

        // Assert
        Assert.Equal(new[] { 1, 2, 3 }, result);
    }

    [Fact]
    public void MergeOrdered_Interleaved_MergesCorrectly()
    {
        // Arrange
        var seq1 = new[] { 1, 3, 5, 7 }.AsParallel().AsOrdered();
        var seq2 = new[] { 2, 4, 6, 8 }.AsParallel().AsOrdered();

        // Act
        var result = seq1.MergeOrdered(seq2, (a, b) => a <= b).ToList();

        // Assert
        Assert.Equal(new[] { 1, 2, 3, 4, 5, 6, 7, 8 }, result);
    }

    #endregion

    #region Do edge cases

    [Fact]
    public void Do_LargeSequence_ExecutesAll()
    {
        // Arrange
        var count = 0;
        var items = Enumerable.Range(1, 100)
            .AsParallel()
            .ForEach(_ => Interlocked.Increment(ref count));

        // Act
        items.Do();

        // Assert
        Assert.Equal(100, count);
    }

    #endregion
}
