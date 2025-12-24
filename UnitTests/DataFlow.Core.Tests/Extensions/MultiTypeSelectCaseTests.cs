using DataFlow.Extensions;

namespace DataFlow.Core.Tests.Extensions;

/// <summary>
/// Tests for multi-type SelectCase/ForEachCase/UnCase overloads.
/// These overloads allow different return types per branch using tuple-of-types pattern.
/// </summary>
public class MultiTypeSelectCaseTests
{
    #region Test Types

    private record ErrorReport(string Message, int Severity);
    private record WarningLog(string Source, DateTime Timestamp);
    private record InfoMetric(string Name, double Value);
    private record DebugData(string Raw);

    private record TestItem(string Type, string Content);

    #endregion

    #region IEnumerable Multi-Type SelectCase (2 Types)

    [Fact]
    public void SelectCase_TwoTypes_ReturnsCorrectTypePerCategory()
    {
        // Arrange
        var items = new[] { "error", "info", "error" }
            .Cases(s => s == "error");

        // Act
        var result = items.SelectCase<string, ErrorReport, InfoMetric>(
            s => new ErrorReport(s, 1),
            s => new InfoMetric(s, 0.5)
        ).ToList();

        // Assert
        Assert.Equal(3, result.Count);

        // Category 0 (error) - first slot populated
        Assert.NotNull(result[0].result.Item1);
        Assert.Equal("error", result[0].result.Item1!.Message);

        // Category 1 (info) - second slot populated
        Assert.NotNull(result[1].result.Item2);
        Assert.Equal("info", result[1].result.Item2!.Name);

        // Category 0 (error) again
        Assert.NotNull(result[2].result.Item1);
    }

    [Fact]
    public void SelectCase_TwoTypes_CategoryDeterminesActiveSlot()
    {
        // Arrange
        var items = new[] { "A", "B", "A" }
            .Cases(s => s == "A");

        // Act
        var result = items.SelectCase<string, int, string>(
            s => 100,
            s => $"not-A:{s}"
        ).ToList();

        // Assert
        Assert.Equal(0, result[0].category); // A -> category 0
        Assert.Equal(100, result[0].result.Item1);

        Assert.Equal(1, result[1].category); // B -> category 1
        Assert.Equal("not-A:B", result[1].result.Item2);
    }

    #endregion

    #region IEnumerable Multi-Type SelectCase (3 Types)

    [Fact]
    public void SelectCase_ThreeTypes_DistributesCorrectly()
    {
        // Arrange
        var items = new[] {
            new TestItem("error", "err1"),
            new TestItem("warning", "warn1"),
            new TestItem("info", "info1")
        }.Cases(x => x.Type == "error", x => x.Type == "warning");

        // Act
        var result = items.SelectCase<TestItem, ErrorReport, WarningLog, InfoMetric>(
            x => new ErrorReport(x.Content, 1),
            x => new WarningLog(x.Content, DateTime.Now),
            x => new InfoMetric(x.Content, 0.0)
        ).ToList();

        // Assert
        Assert.Equal(3, result.Count);
        Assert.IsType<ErrorReport>(result[0].result.Item1);
        Assert.IsType<WarningLog>(result[1].result.Item2);
        Assert.IsType<InfoMetric>(result[2].result.Item3);
    }

    #endregion

    #region IEnumerable Multi-Type ForEachCase

    [Fact]
    public void ForEachCase_TwoTypes_ExecutesCorrectActionPerCategory()
    {
        // Arrange
        var errorList = new List<ErrorReport>();
        var infoList = new List<InfoMetric>();

        var items = new[] { "error", "info", "error" }
            .Cases(s => s == "error")
            .SelectCase<string, ErrorReport, InfoMetric>(
                s => new ErrorReport(s, 1),
                s => new InfoMetric(s, 0.5)
            );

        // Act
        items.ForEachCase<string, ErrorReport, InfoMetric>(
            e => errorList.Add(e),
            i => infoList.Add(i)
        ).ToList();

        // Assert
        Assert.Equal(2, errorList.Count);
        Assert.Single(infoList);
    }

    [Fact]
    public void ForEachCase_ThreeTypes_ExecutesAllActions()
    {
        // Arrange
        var errorCount = 0;
        var warningCount = 0;
        var otherCount = 0;

        var items = new[] { "error", "warning", "info", "error" }
            .Cases(s => s == "error", s => s == "warning")
            .SelectCase<string, ErrorReport, WarningLog, InfoMetric>(
                s => new ErrorReport(s, 1),
                s => new WarningLog(s, DateTime.Now),
                s => new InfoMetric(s, 0.0)
            );

        // Act
        items.ForEachCase<string, ErrorReport, WarningLog, InfoMetric>(
            _ => errorCount++,
            _ => warningCount++,
            _ => otherCount++
        ).ToList();

        // Assert
        Assert.Equal(2, errorCount);
        Assert.Equal(1, warningCount);
        Assert.Equal(1, otherCount);
    }

    #endregion

    #region IEnumerable Multi-Type UnCase

    [Fact]
    public void UnCase_TwoTypes_ReturnsOriginalItems()
    {
        // Arrange
        var originalItems = new[] { "a", "b", "c" };
        var pipeline = originalItems
            .Cases(s => s == "a")
            .SelectCase<string, int, int>(
                s => 1,
                s => 2
            );

        // Act
        var result = pipeline.UnCase<string, int, int>().ToList();

        // Assert
        Assert.Equal(originalItems, result);
    }

    [Fact]
    public void UnCase_ThreeTypes_PreservesOriginalOrder()
    {
        // Arrange
        var originalItems = new[] { "x", "y", "z", "x" };
        var pipeline = originalItems
            .Cases(s => s == "x", s => s == "y")
            .SelectCase<string, int, string, double>(
                s => 1,
                s => "two",
                s => 3.0
            );

        // Act
        var result = pipeline.UnCase<string, int, string, double>().ToList();

        // Assert
        Assert.Equal(originalItems, result);
    }

    #endregion

    #region IEnumerable Multi-Type AllCases

    [Fact]
    public void AllCases_TwoTypes_ReturnsActiveSlotValues()
    {
        // Arrange
        var items = new[] { "a", "b" }
            .Cases(s => s == "a")
            .SelectCase<string, int, string>(
                s => 100,
                s => "bee"
            );

        // Act - AllCases extracts only the active slot value (not full tuple)
        var result = items.AllCases<string, int, string>().ToList();

        // Assert - returns the active value from each slot
        Assert.Equal(2, result.Count);
        Assert.Equal(100, result[0]);  // category 0 -> int value
        Assert.Equal("bee", result[1]); // category 1 -> string value
    }

    #endregion
}
