using DataFlow.Framework;
using DataFlow.Extensions;
using System.Text;
using Xunit;

namespace DataFlow.ParallelAsyncQuery.Tests;

/// <summary>
/// Tests for the ParallelAsyncQuery extension methods added for API consistency.
/// </summary>
public class ParallelAsyncQueryExtensionsTests
{
    private static async IAsyncEnumerable<int> GenerateNumbers(int count)
    {
        for (int i = 0; i < count; i++)
        {
            await Task.Yield();
            yield return i;
        }
    }

    private static async IAsyncEnumerable<string> GenerateStrings(int count)
    {
        for (int i = 0; i < count; i++)
        {
            await Task.Yield();
            yield return $"Item{i}";
        }
    }

    #region IsNullOrEmpty Tests

    [Fact]
    public async Task IsNullOrEmpty_NullQuery_ReturnsTrue()
    {
        // Arrange
        ParallelAsyncQuery<int>? query = null;

        // Act
        var result = await query.IsNullOrEmpty();

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task IsNullOrEmpty_EmptyQuery_ReturnsTrue()
    {
        // Arrange
        var source = GenerateNumbers(0);
        var query = source.AsParallel();

        // Act
        var result = await query.IsNullOrEmpty();

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task IsNullOrEmpty_NonEmptyQuery_ReturnsFalse()
    {
        // Arrange
        var source = GenerateNumbers(5);
        var query = source.AsParallel();

        // Act
        var result = await query.IsNullOrEmpty();

        // Assert
        Assert.False(result);
    }

    #endregion

    #region BuildString Tests

    [Fact]
    public async Task BuildString_WithDefaults_FormatsCorrectly()
    {
        // Arrange
        var source = GenerateStrings(3);
        var query = source.AsParallel().WithOrderPreservation(true);

        // Act
        var result = await query.BuildString(null, ", ", "{", "}");

        // Assert
        Assert.NotNull(result);
        Assert.StartsWith("{", result.ToString());
        Assert.EndsWith("}", result.ToString());
        Assert.Contains("Item0", result.ToString());
        Assert.Contains("Item1", result.ToString());
        Assert.Contains("Item2", result.ToString());
    }


    [Fact]
    public async Task BuildString_WithCustomSeparator_UsesSeparator()
    {
        // Arrange
        var source = GenerateStrings(3);
        var query = source.AsParallel().WithOrderPreservation(true);

        // Act
        var result = await query.BuildString(separator: " | ", before: "[", after: "]");

        // Assert
        var str = result.ToString();
        Assert.StartsWith("[", str);
        Assert.EndsWith("]", str);
        Assert.Contains(" | ", str);
    }

    [Fact]
    public async Task BuildString_WithExistingStringBuilder_AppendsToIt()
    {
        // Arrange
        var source = GenerateStrings(2);
        var query = source.AsParallel().WithOrderPreservation(true);
        var sb = new StringBuilder("Prefix: ");

        // Act
        var result = await query.BuildString(sb, separator: ", ", before: "(", after: ")");

        // Assert
        Assert.Same(sb, result);
        Assert.StartsWith("Prefix: ", result.ToString());
        Assert.Contains("(", result.ToString());
    }

    [Fact]
    public async Task BuildString_EmptyQuery_ReturnsDelimitersOnly()
    {
        // Arrange
        var source = GenerateStrings(0);
        var query = source.AsParallel();

        // Act
        var result = await query.BuildString(separator: ",", before: "[", after: "]");

        // Assert
        Assert.Equal("[]", result.ToString());
    }

    #endregion

    #region ToList Tests (verifying existing functionality)

    [Fact]
    public async Task ToList_WithOrderPreservation_MaintainsOrder()
    {
        // Arrange
        var source = GenerateNumbers(10);
        var query = source.AsParallel().WithOrderPreservation(true);

        // Act
        var result = await query.ToList();

        // Assert
        Assert.Equal(10, result.Count);
        for (int i = 0; i < 10; i++)
        {
            Assert.Equal(i, result[i]);
        }
    }

    #endregion
}
