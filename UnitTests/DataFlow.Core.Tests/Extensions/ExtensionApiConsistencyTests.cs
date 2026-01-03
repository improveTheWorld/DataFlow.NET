using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DataFlow;
using DataFlow.Parallel;
using Xunit;

namespace DataFlow.Core.Tests.Extensions;

/// <summary>
/// Tests for the extension method API consistency:
/// - AsyncLinqOperators.Take(start, count) 
/// - AsyncLinqOperators.ToLines(separator)
/// - ParallelQueryExtensions MergeOrdered removal verification
/// </summary>
public class ExtensionApiConsistencyTests
{
    #region AsyncLinqOperators.Take(start, count) Tests

    [Fact]
    public async Task Take_WithStartAndCount_ReturnsCorrectSlice()
    {
        // Arrange
        var source = CreateAsyncEnumerable(0, 1, 2, 3, 4, 5, 6, 7, 8, 9);

        // Act
        var result = await ToListAsync(source.Take(2, 3));

        // Assert - Should skip 2 and take 3: [2, 3, 4]
        Assert.Equal(3, result.Count);
        Assert.Equal(new[] { 2, 3, 4 }, result);
    }

    [Fact]
    public async Task Take_WithStartZero_ReturnsFromBeginning()
    {
        // Arrange
        var source = CreateAsyncEnumerable(10, 20, 30, 40, 50);

        // Act
        var result = await ToListAsync(source.Take( 0, 3));

        // Assert
        Assert.Equal(new[] { 10, 20, 30 }, result);
    }

    [Fact]
    public async Task Take_CountExceedsAvailable_ReturnsWhatIsAvailable()
    {
        // Arrange
        var source = CreateAsyncEnumerable(1, 2, 3);

        // Act
        var result = await ToListAsync(source.Take(1, 100));

        // Assert - Skip 1, take up to 100 but only 2 remain
        Assert.Equal(new[] { 2, 3 }, result);
    }

    [Fact]
    public async Task Take_StartBeyondEnd_ReturnsEmpty()
    {
        // Arrange
        var source = CreateAsyncEnumerable(1, 2, 3);

        // Act
        var result = await ToListAsync(source.Take(10, 5));

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task Take_ZeroCount_ReturnsEmpty()
    {
        // Arrange
        var source = CreateAsyncEnumerable(1, 2, 3);

        // Act
        var result = await ToListAsync(source.Take(0, 0));

        // Assert
        Assert.Empty(result);
    }

    #endregion

    #region AsyncLinqOperators.ToLines Tests

    [Fact]
    public async Task ToLines_WithSeparator_SplitsCorrectly()
    {
        // Arrange
        var slices = CreateAsyncEnumerable("Hello", " ", "World", "|", "Foo", "Bar", "|");

        // Act
        var result = await ToListAsync(slices.ToLines( "|"));

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Equal("Hello World", result[0]);
        Assert.Equal("FooBar", result[1]);
    }

    [Fact]
    public async Task ToLines_NoSeparator_NoLines()
    {
        // Arrange
        var slices = CreateAsyncEnumerable("Hello", " ", "World");

        // Act
        var result = await ToListAsync(slices.ToLines( "|"));

        // Assert - No separator means no complete lines
        Assert.Empty(result);
    }

    [Fact]
    public async Task ToLines_EmptySource_ReturnsEmpty()
    {
        // Arrange
        var slices = CreateAsyncEnumerable<string>();

        // Act
        var result = await ToListAsync(slices.ToLines("|"));    

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task ToLines_ConsecutiveSeparators_CreatesEmptyLines()
    {
        // Arrange
        var slices = CreateAsyncEnumerable("A", "|", "|", "B", "|");

        // Act
        var result = await ToListAsync(slices.ToLines("|"));

        // Assert
        Assert.Equal(3, result.Count);
        Assert.Equal("A", result[0]);
        Assert.Equal("", result[1]);   // Empty line between separators
        Assert.Equal("B", result[2]);
    }

    #endregion

    #region ParallelQuery MergeOrdered Removal Verification

    [Fact]
    public void ParallelQueryExtensions_DoesNotContain_MergeOrdered()
    {
        // This test verifies that MergeOrdered was correctly removed from ParallelQueryExtensions
        // Users should use .AsEnumerable().MergeOrdered() instead

        var type = typeof(ParallelQueryExtensions);
        var mergeMethods = type.GetMethods().Where(m => m.Name == "MergeOrdered");

        Assert.Empty(mergeMethods);
    }

    [Fact]
    public void EnumerableExtensions_Contains_MergeOrdered()
    {
        // Verify MergeOrdered exists on IEnumerable for users to use
        var type = typeof(EnumerableExtensions);
        var mergeMethods = type.GetMethods().Where(m => m.Name == "MergeOrdered");

        Assert.NotEmpty(mergeMethods);
    }

    #endregion

    #region Helper Methods

    private static async IAsyncEnumerable<T> CreateAsyncEnumerable<T>(params T[] items)
    {
        foreach (var item in items)
        {
            await Task.Yield();
            yield return item;
        }
    }

    private static async Task<List<T>> ToListAsync<T>(IAsyncEnumerable<T> source)
    {
        var list = new List<T>();
        await foreach (var item in source)
        {
            list.Add(item);
        }
        return list;
    }

    #endregion
}
