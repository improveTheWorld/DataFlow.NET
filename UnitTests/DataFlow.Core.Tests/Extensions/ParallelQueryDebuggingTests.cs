using DataFlow.Extensions;
using System.Collections.Concurrent;

namespace DataFlow.Core.Tests.Extensions;

/// <summary>
/// Additional tests for ParallelQueryExtensions to increase coverage.
/// Tests methods that don't conflict with System.Linq.ParallelEnumerable.
/// </summary>
public class ParallelQueryDebuggingTests
{
    #region IsNullOrEmpty

    [Fact]
    public void IsNullOrEmpty_Null_ReturnsTrue()
    {
        // Arrange
        ParallelQuery<int>? items = null;

        // Act
        var result = items.IsNullOrEmpty();

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsNullOrEmpty_Empty_ReturnsTrue()
    {
        // Arrange
        var items = Array.Empty<int>().AsParallel();

        // Act
        var result = items.IsNullOrEmpty();

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsNullOrEmpty_HasElements_ReturnsFalse()
    {
        // Arrange
        var items = new[] { 1, 2, 3 }.AsParallel();

        // Act
        var result = items.IsNullOrEmpty();

        // Assert
        Assert.False(result);
    }

    #endregion

    #region ForEach additional

    [Fact]
    public void ForEach_Empty_NoExecution()
    {
        // Arrange
        var count = 0;
        var items = Array.Empty<int>().AsParallel();

        // Act
        items.ForEach(n => Interlocked.Increment(ref count)).ToList();

        // Assert
        Assert.Equal(0, count);
    }

    [Fact]
    public void ForEach_LargeSequence_ExecutesAll()
    {
        // Arrange
        var count = 0;
        var items = Enumerable.Range(1, 100).AsParallel();

        // Act
        items.ForEach(_ => Interlocked.Increment(ref count)).ToList();

        // Assert
        Assert.Equal(100, count);
    }

    #endregion
}
