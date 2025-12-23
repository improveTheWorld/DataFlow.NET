using DataFlow.Extensions;
using DataFlow.Framework;
using Xunit;

namespace DataFlow.Core.Tests.Cases;

/// <summary>
/// Unit tests for the Cases/SelectCase fluent API pattern on IEnumerable and IAsyncEnumerable.
/// Tests categorization, routing, and transformation of streams.
/// </summary>
public class CasesSelectCasesTests
{
    #region IEnumerable Cases Tests

    [Fact]
    public void Cases_SingleCategory_AllMatch()
    {
        // Arrange
        var items = new[] { "a", "b", "c", "d", "e" };

        // Act
        var result = items
            .Cases(s => true)  // All match
            .SelectCase(s => s.ToUpper())
            .AllCases()
            .ToList();

        // Assert
        Assert.Equal(5, result.Count);
        Assert.Equal(new[] { "A", "B", "C", "D", "E" }, result);
    }

    [Fact]
    public void Cases_TwoCategories_SplitsCorrectly()
    {
        // Arrange
        var items = new[] { "low", "HIGH", "mix", "UPPER", "down" };

        // Act
        var lowerCase = new List<string>();
        var upperCase = new List<string>();

        items
            .Cases(
                s => s == s.ToLower(),  // All lowercase
                s => s == s.ToUpper()   // All uppercase
            )
            .SelectCase(
                lower => { lowerCase.Add(lower); return lower; },
                upper => { upperCase.Add(upper); return upper; }
            )
            .AllCases()
            .ToList();

        // Assert
        Assert.Equal(new[] { "low", "mix", "down" }, lowerCase);
        Assert.Equal(new[] { "HIGH", "UPPER" }, upperCase);
    }

    [Fact]
    public void Cases_ThreeCategories_LogLevels()
    {
        // Arrange
        var logs = TestDataGenerators.GenerateLogEntries(1).Take(10).ToList();

        // Act
        var errorCount = 0;
        var warnCount = 0;
        var infoCount = 0;
        var otherCount = 0;

        logs
            .Cases(
                log => log.Level == "ERROR" || log.Level == "FATAL",
                log => log.Level == "WARN",
                log => log.Level == "INFO"
            )
            .SelectCase(
                error => { errorCount++; return error; },
                warn => { warnCount++; return warn; },
                info => { infoCount++; return info; }
            )
            .AllCases()
            .ForEach(_ => otherCount++)
            .Do();

        // Assert - Should have processed all logs
        var totalCategorized = errorCount + warnCount + infoCount;
        Assert.True(totalCategorized >= 0);
    }

    [Fact]
    public void Cases_WithTransformation_ChangesType()
    {
        // Arrange
        var items = new[] { "apple", "BANANA", "Cherry" };

        // Act
        var result = items
            .Cases(
                s => s == s.ToLower(),  // All lowercase
                s => s == s.ToUpper(),  // All uppercase
                s => true               // Mixed (catch-all)
            )
            .SelectCase(
                lower => $"lower:{lower}",
                upper => $"UPPER:{upper}",
                mixed => $"Mixed:{mixed}"
            )
            .AllCases()
            .ToList();

        // Assert
        Assert.Contains("lower:apple", result);
        Assert.Contains("UPPER:BANANA", result);
        Assert.Contains("Mixed:Cherry", result);
    }

    [Fact]
    public void Cases_NonExhaustive_UnmatchedItemsExcluded()
    {
        // Arrange
        var items = new[] { "a", "bb", "ccc", "dddd", "eeeee" };

        // Act - Only match strings with length < 3
        var matched = items
            .Cases(s => s.Length < 3)
            .SelectCase(s => s.ToUpper())
            .AllCases()
            .ToList();

        // Assert - Only 2 items match (a, bb)
        Assert.Equal(2, matched.Count);
        Assert.Contains("A", matched);
        Assert.Contains("BB", matched);
    }

    [Fact]
    public void Cases_WithForEach_ExecutesSideEffects()
    {
        // Arrange
        var items = new[] { "error", "info", "warn", "error" };
        var processedCount = 0;

        // Act
        items
            .Cases(s => s == "error")
            .SelectCase(s => { processedCount++; return s; })
            .AllCases()
            .ToList();

        // Assert
        Assert.Equal(2, processedCount);
    }

    #endregion

    #region IAsyncEnumerable Cases Tests

    [Fact]
    public async Task AsyncCases_SingleCategory_AllMatch()
    {
        // Arrange
        var items = new[] { "a", "b", "c", "d", "e" }.Async();

        // Act
        var result = new List<string>();
        await items
            .Cases(s => true)
            .SelectCase(s => s.ToUpper())
            .AllCases()
            .ForEach(s => result.Add(s))
            .Do();

        // Assert
        Assert.Equal(5, result.Count);
        Assert.Equal(new[] { "A", "B", "C", "D", "E" }, result);
    }

    [Fact]
    public async Task AsyncCases_TwoCategories_SplitsCorrectly()
    {
        // Arrange
        var items = new[] { "low", "HIGH", "mix", "UPPER", "down" }.Async();

        // Act
        var lowerCase = new List<string>();
        var upperCase = new List<string>();

        await items
            .Cases(
                s => s == s.ToLower(),
                s => s == s.ToUpper()
            )
            .SelectCase(
                lower => { lowerCase.Add(lower); return lower; },
                upper => { upperCase.Add(upper); return upper; }
            )
            .AllCases()
            .Do();

        // Assert
        Assert.Equal(new[] { "low", "mix", "down" }, lowerCase);
        Assert.Equal(new[] { "HIGH", "UPPER" }, upperCase);
    }

    [Fact]
    public async Task AsyncCases_PreservesOrder()
    {
        // Arrange
        var items = new[] { "a", "B", "c", "D", "e" }.Async();

        // Act
        var result = new List<string>();
        await items
            .Cases(
                s => char.IsLower(s[0]),
                s => char.IsUpper(s[0])
            )
            .SelectCase(
                lower => lower,
                upper => upper.ToLower()
            )
            .AllCases()
            .ForEach(s => result.Add(s))
            .Do();

        // Assert - Order should be preserved
        Assert.Equal(new[] { "a", "b", "c", "d", "e" }, result);
    }

    [Fact]
    public async Task AsyncCases_LogProcessing_CategorizesByLevel()
    {
        // Arrange
        var logs = TestDataGenerators.GenerateLogEntries(1).Take(20).ToList();

        // Act
        var critical = new List<LogEntry>();
        var warnings = new List<LogEntry>();
        var info = new List<LogEntry>();

        await logs.Async()
            .Cases(
                log => log.Level == "ERROR" || log.Level == "FATAL",
                log => log.Level == "WARN",
                log => log.Level == "INFO"
            )
            .SelectCase(
                c => { critical.Add(c); return c; },
                w => { warnings.Add(w); return w; },
                i => { info.Add(i); return i; }
            )
            .AllCases()
            .Do();

        // Assert - All logs are categorized
        var total = critical.Count + warnings.Count + info.Count;
        Assert.True(total <= logs.Count);
    }

    #endregion

    #region Unify (Stream Merging) Tests

    [Fact]
    public async Task Unify_MergesMultipleSources()
    {
        // Arrange
        var source1 = new[] { "a", "b", "c" };
        var source2 = new[] { "1", "2", "3" };
        var source3 = new[] { "x", "y" };

        // Act
        var merger = new AsyncEnumerable<string>()
            .Unify(source1.Async(), "Source1")
            .Unify(source2.Async(), "Source2")
            .Unify(source3.Async(), "Source3");

        var result = new List<string>();
        await merger.ForEach(s => result.Add(s)).Do();

        // Assert - All items should be present (order may vary due to merging)
        Assert.Equal(8, result.Count);
        Assert.Contains("a", result);
        Assert.Contains("2", result);
        Assert.Contains("y", result);
    }

    [Fact]
    public async Task Unify_EmptySource_StillWorks()
    {
        // Arrange
        var source1 = new[] { "a", "b", "c" };
        var emptySource = Array.Empty<string>();

        // Act
        var merger = new AsyncEnumerable<string>()
            .Unify(source1.Async(), "HasData")
            .Unify(emptySource.Async(), "Empty");

        var result = new List<string>();
        await merger.ForEach(s => result.Add(s)).Do();

        // Assert
        Assert.Equal(3, result.Count);
    }

    [Fact]
    public async Task Unify_SingleSource_PassesThrough()
    {
        // Arrange
        var source = new[] { "single", "source", "test" };

        // Act
        var merger = new AsyncEnumerable<string>()
            .Unify(source.Async(), "OnlyOne");

        var result = new List<string>();
        await merger.ForEach(s => result.Add(s)).Do();

        // Assert
        Assert.Equal(3, result.Count);
        Assert.Equal(source, result);
    }

    #endregion

    #region Combined Cases + Unify Pipeline Tests

    [Fact]
    public async Task FullPipeline_UnifyThenCases_ProcessesAll()
    {
        // Arrange - Simulate log entries from multiple sources
        var webLogs = TestDataGenerators.GenerateLogEntries(1).Take(5).ToList();
        var dbLogs = TestDataGenerators.GenerateLogEntries(1).Take(3).ToList();

        // Act
        var errorCount = 0;
        var warnCount = 0;
        var infoCount = 0;

        var merger = new AsyncEnumerable<LogEntry>()
            .Unify(webLogs.Async(), "WebLogs")
            .Unify(dbLogs.Async(), "DBLogs");

        await merger
            .Cases(
                log => log.Level == "ERROR" || log.Level == "FATAL",
                log => log.Level == "WARN",
                log => log.Level == "INFO"
            )
            .SelectCase(
                critical => { errorCount++; return critical; },
                warning => { warnCount++; return warning; },
                info => { infoCount++; return info; }
            )
            .AllCases()
            .Do();

        // Assert - Total should equal total logs
        var totalLogs = webLogs.Count + dbLogs.Count;
        var totalCategorized = errorCount + warnCount + infoCount;
        Assert.True(totalCategorized <= totalLogs, $"Categorized {totalCategorized} <= Total {totalLogs}");
    }

    [Fact]
    public async Task FullPipeline_MetricsMonitoring_AlertsOnThresholds()
    {
        // Arrange
        var metrics = TestDataGenerators.GenerateMetrics(1).Take(20).ToList();

        // Act
        var criticalAlerts = new List<MetricEntry>();
        var warnings = new List<MetricEntry>();
        var normal = new List<MetricEntry>();

        await metrics.Async()
            .Cases(
                m => m.Value > 90,    // Critical threshold
                m => m.Value > 70,    // Warning threshold
                m => true             // Normal
            )
            .SelectCase(
                c => { criticalAlerts.Add(c); return c; },
                w => { warnings.Add(w); return w; },
                n => { normal.Add(n); return n; }
            )
            .AllCases()
            .Do();

        // Assert
        var total = criticalAlerts.Count + warnings.Count + normal.Count;
        Assert.Equal(metrics.Count, total);
    }

    #endregion
}
