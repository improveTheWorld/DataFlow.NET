using DataFlow.Extensions;

namespace DataFlow.Core.Tests.Extensions;

/// <summary>
/// Tests for async multi-type SelectCase/ForEachCase/UnCase overloads on IAsyncEnumerable.
/// </summary>
public class AsyncMultiTypeSelectCaseTests
{
    #region Async Helpers

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

    #region Test Types

    private record ErrorReport(string Message, int Severity);
    private record InfoMetric(string Name, double Value);
    private record WarningLog(string Source);

    #endregion

    #region Async Multi-Type SelectCase (2 Types)

    [Fact]
    public async Task SelectCase_TwoTypes_Async_ReturnsCorrectTypePerCategory()
    {
        // Arrange
        var items = ToAsync(new[] { "error", "info", "error" })
            .Cases(s => s == "error");

        // Act
        var result = await CollectAsync(
            items.SelectCase<string, ErrorReport, InfoMetric>(
                s => new ErrorReport(s, 1),
                s => new InfoMetric(s, 0.5)
            )
        );

        // Assert
        Assert.Equal(3, result.Count);

        // Category 0 (error) - first slot populated
        Assert.NotNull(result[0].result.Item1);
        Assert.Equal("error", result[0].result.Item1!.Message);

        // Category 1 (info) - second slot populated  
        Assert.NotNull(result[1].result.Item2);
        Assert.Equal("info", result[1].result.Item2!.Name);
    }

    [Fact]
    public async Task SelectCase_TwoTypes_Async_PreservesCategoryIndex()
    {
        // Arrange
        var items = ToAsync(new[] { "A", "B", "C" })
            .Cases(s => s == "A", s => s == "B");

        // Act
        var result = await CollectAsync(
            items.SelectCase<string, int, string, double>(
                s => 1,
                s => "two",
                s => 3.0
            )
        );

        // Assert
        Assert.Equal(0, result[0].category); // A
        Assert.Equal(1, result[1].category); // B
        Assert.Equal(2, result[2].category); // C (supra)
    }

    #endregion

    #region Async Multi-Type ForEachCase

    [Fact]
    public async Task ForEachCase_TwoTypes_Async_ExecutesActionsPerCategory()
    {
        // Arrange
        var errorList = new List<ErrorReport>();
        var infoList = new List<InfoMetric>();

        var items = ToAsync(new[] { "error", "info", "error" })
            .Cases(s => s == "error")
            .SelectCase<string, ErrorReport, InfoMetric>(
                s => new ErrorReport(s, 1),
                s => new InfoMetric(s, 0.5)
            );

        // Act
        await CollectAsync(
            items.ForEachCase<string, ErrorReport, InfoMetric>(
                e => errorList.Add(e),
                i => infoList.Add(i)
            )
        );

        // Assert
        Assert.Equal(2, errorList.Count);
        Assert.Single(infoList);
    }

    #endregion

    #region Async Multi-Type UnCase

    [Fact]
    public async Task UnCase_TwoTypes_Async_ReturnsOriginalItems()
    {
        // Arrange
        var originalItems = new[] { "a", "b", "c" };
        var pipeline = ToAsync(originalItems)
            .Cases(s => s == "a")
            .SelectCase<string, int, int>(
                s => 1,
                s => 2
            );

        // Act
        var result = await CollectAsync(pipeline.UnCase<string, int, int>());

        // Assert
        Assert.Equal(originalItems, result);
    }

    #endregion

    #region Async Multi-Type AllCases

    [Fact]
    public async Task AllCases_TwoTypes_Async_ReturnsTupleResults()
    {
        // Arrange
        var items = ToAsync(new[] { "a", "b" })
            .Cases(s => s == "a")
            .SelectCase<string, int, string>(
                s => 100,
                s => "bee"
            );

        // Act
        var result = await CollectAsync(items.AllCases<string, int, string>());

        // Assert
        Assert.Equal(2, result.Count);
    }

    #endregion
}
