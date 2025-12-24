using DataFlow.Extensions;
using DataFlow.Framework;
using Xunit;

namespace DataFlow.Core.Tests.Cases;

/// <summary>
/// Tests for parallel query execution across different execution models:
/// - IEnumerable (Sequential)
/// - ParallelQuery/PLINQ (IEnumerable.AsParallel)
/// - IAsyncEnumerable (Async Sequential)
/// - ParallelAsyncQuery (IAsyncEnumerable.AsParallel)
/// </summary>
public class ParallelQueriesTests
{
    #region PLINQ Cases/SelectCase Tests

    [Fact]
    public void PLINQ_Cases_TwoCategories_SplitsCorrectly()
    {
        // Arrange
        var logs = TestDataGenerators.GenerateLogEntries(30).ToList();
        var errorCount = logs.Count(l => l.Level == "ERROR" || l.Level == "FATAL");
        var warnCount = logs.Count(l => l.Level == "WARN");

        // Act - PLINQ parallel processing
        var results = logs
            .AsParallel()
            .Cases(
                log => log.Level == "ERROR" || log.Level == "FATAL",
                log => log.Level == "WARN"
            )
            .SelectCase<LogEntry, string>(
                critical => $"CRITICAL: {critical.Message}",
                warning => $"WARNING: {warning.Message}",
                info => $"INFO: {info.Message}"
            )
            .AllCases()
            .ToList();

        // Assert
        Assert.Equal(logs.Count, results.Count);
        Assert.Equal(errorCount, results.Count(r => r.StartsWith("CRITICAL:")));
        Assert.Equal(warnCount, results.Count(r => r.StartsWith("WARNING:")));
    }

    [Fact]
    public void PLINQ_Cases_MetricsMonitoring_FiltersByCriteria()
    {
        // Arrange
        var metrics = TestDataGenerators.GenerateMetrics(50).ToList();
        var cpuAlerts = metrics.Count(m => m.Name == "cpu_usage" && m.Value > 75);
        var memoryAlerts = metrics.Count(m => m.Name == "memory_usage" && m.Value > 85);

        // Act
        var alerts = metrics
            .AsParallel()
            .Cases(
                metric => metric.Name == "cpu_usage" && metric.Value > 75,
                metric => metric.Name == "memory_usage" && metric.Value > 85,
                metric => metric.Name == "network_latency" && metric.Value > 180
            )
            .SelectCase<MetricEntry, string>(
                cpu => $"CPU ALERT: {cpu.Value:F1}%",
                memory => $"MEMORY ALERT: {memory.Value:F1}%",
                latency => $"LATENCY ALERT: {latency.Value:F1}ms"
            )
            .AllCases()
            .ToList();

        // Assert - verify the count matches expected alerts
        Assert.Equal(cpuAlerts, alerts.Count(a => a.StartsWith("CPU ALERT:")));
        Assert.Equal(memoryAlerts, alerts.Count(a => a.StartsWith("MEMORY ALERT:")));
    }

    [Fact]
    public void PLINQ_Cases_OrderEvents_CategorizesByEventType()
    {
        // Arrange
        var orders = TestDataGenerators.GenerateOrderEvents(25).ToList();
        var cancelledCount = orders.Count(o => o.EventType == "cancelled");
        var highValueCount = orders.Count(o => o.EventType != "cancelled" && o.Amount > 500);

        // Act
        var results = orders
            .AsParallel()
            .Cases(
                order => order.EventType == "cancelled",
                order => order.Amount > 500,
                order => order.Status == "failed"
            )
            .SelectCase<OrderEvent, string>(
                cancelled => $"CANCELLED: {cancelled.OrderId}",
                highValue => $"HIGH VALUE: {highValue.OrderId}",
                failed => $"FAILED: {failed.OrderId}"
            )
            .AllCases()
            .ToList();

        // Assert
        Assert.Equal(cancelledCount, results.Count(r => r.StartsWith("CANCELLED:")));
    }

    #endregion

    #region AsyncParallel Tests

    [Fact]
    public async Task AsyncParallel_Where_Select_FiltersAndTransforms()
    {
        // Arrange
        var data = Enumerable.Range(1, 100).Select(i => $"item_{i}").ToList();

        // Act
        var results = await data
            .Async()
            .AsParallel()
            .WithMaxConcurrency(4)
            .Where(item => int.Parse(item.Split('_')[1]) % 2 == 0) // Even numbers
            .Select(item => $"processed_{item}")
            .ToList();

        // Assert
        Assert.Equal(50, results.Count);
        Assert.All(results, r => Assert.StartsWith("processed_item_", r));
    }

    [Fact]
    public async Task AsyncParallel_Take_LimitsResults()
    {
        // Arrange
        var data = Enumerable.Range(1, 100).Select(i => $"value_{i}").ToList();

        // Act
        var results = await data
            .Async()
            .AsParallel()
            .WithMaxConcurrency(Environment.ProcessorCount)
            .Take(10)
            .ToList();

        // Assert
        Assert.Equal(10, results.Count);
    }

    [Fact]
    public async Task AsyncParallel_MetricsAlert_GeneratesCorrectAlerts()
    {
        // Arrange
        var metrics = TestDataGenerators.GenerateMetrics(40).ToList();
        var expectedHighValues = metrics.Count(m => m.Value > 50);

        // Act
        var alerts = await metrics
            .Async()
            .AsParallel()
            .WithMaxConcurrency(4)
            .Where(m => m.Value > 50)
            .Select(m => $"ALERT: {m.Name} = {m.Value:F1}")
            .ToList();

        // Assert
        Assert.Equal(expectedHighValues, alerts.Count);
        Assert.All(alerts, a => Assert.StartsWith("ALERT:", a));
    }

    [Fact]
    public async Task AsyncParallel_LogProcessing_TransformsCorrectly()
    {
        // Arrange
        var logs = TestDataGenerators.GenerateLogEntries(30).ToList();

        // Act
        var results = await logs
            .Async()
            .AsParallel()
            .WithMaxConcurrency(Environment.ProcessorCount)
            .Where(log => !string.IsNullOrEmpty(log.Level))
            .Select(log => (log.Level, log.Source, log.Message))
            .Where(x => x.Level == "ERROR" || x.Level == "FATAL" || x.Level == "WARN" || x.Level == "INFO")
            .Select(x => x.Level switch
            {
                "ERROR" or "FATAL" => $"CRITICAL: [{x.Source}] {x.Message}",
                "WARN" => $"WARNING: [{x.Source}] {x.Message}",
                "INFO" => $"INFO: [{x.Source}] {x.Message}",
                _ => $"UNKNOWN: [{x.Source}] {x.Message}"
            })
            .ToList();

        // Assert
        Assert.NotEmpty(results);
        Assert.All(results, r => Assert.True(
            r.StartsWith("CRITICAL:") ||
            r.StartsWith("WARNING:") ||
            r.StartsWith("INFO:") ||
            r.StartsWith("UNKNOWN:")));
    }

    #endregion

    #region Correctness: Sequential vs PLINQ Comparison Tests

    [Fact]
    public void Sequential_And_PLINQ_ProduceSameResults()
    {
        // Arrange
        var logs = TestDataGenerators.GenerateLogEntries(50).ToList();

        // Act - Sequential
        var sequentialResults = logs
            .Cases(
                log => log.Level == "ERROR" || log.Level == "FATAL",
                log => log.Level == "WARN"
            )
            .SelectCase<LogEntry, string>(
                critical => $"CRITICAL: [{critical.Source}] {critical.Message}",
                warning => $"WARNING: [{warning.Source}] {warning.Message}",
                info => $"INFO: [{info.Source}] {info.Message}"
            )
            .AllCases()
            .OrderBy(x => x)
            .ToList();

        // Act - PLINQ
        var plinqResults = logs
            .AsParallel()
            .Cases(
                log => log.Level == "ERROR" || log.Level == "FATAL",
                log => log.Level == "WARN"
            )
            .SelectCase<LogEntry, string>(
                critical => $"CRITICAL: [{critical.Source}] {critical.Message}",
                warning => $"WARNING: [{warning.Source}] {warning.Message}",
                info => $"INFO: [{info.Source}] {info.Message}"
            )
            .AllCases()
            .OrderBy(x => x)
            .ToList();

        // Assert
        Assert.Equal(sequentialResults.Count, plinqResults.Count);
        Assert.True(sequentialResults.SequenceEqual(plinqResults),
            "Sequential and PLINQ should produce identical results (when sorted)");
    }

    [Fact]
    public async Task Async_And_AsyncParallel_ProduceSameResults()
    {
        // Arrange
        var metrics = TestDataGenerators.GenerateMetrics(40).ToList();
        Func<MetricEntry, string> transformer = m => m.Name switch
        {
            "cpu_usage" when m.Value > 75 => $"CPU HIGH: {m.Value:F1}%",
            "memory_usage" when m.Value > 85 => $"MEMORY HIGH: {m.Value:F1}%",
            _ => $"NORMAL: {m.Name}={m.Value:F1}"
        };

        // Act - Async Sequential
        var asyncSequentialResults = await metrics
            .Async()
            .Select(transformer)
            .ToList();
        asyncSequentialResults = asyncSequentialResults.OrderBy(x => x).ToList();

        // Act - Async Parallel
        var asyncParallelResults = await metrics
            .Async()
            .AsParallel()
            .WithMaxConcurrency(Environment.ProcessorCount)
            .Select(transformer)
            .ToList();
        asyncParallelResults = asyncParallelResults.OrderBy(x => x).ToList();

        // Assert
        Assert.Equal(asyncSequentialResults.Count, asyncParallelResults.Count);
        Assert.True(asyncSequentialResults.SequenceEqual(asyncParallelResults),
            "Async sequential and async parallel should produce identical results (when sorted)");
    }

    #endregion

    #region Sensor Processing Tests

    [Fact]
    public void PLINQ_SensorReadings_FiltersAnomalies()
    {
        // Arrange
        var sensors = TestDataGenerators.GenerateSensorReadings(30).ToList();
        var tempAnomalies = sensors.Count(s => s.Type == "temperature" && s.Value > 30);
        var humidityAnomalies = sensors.Count(s => s.Type == "humidity" && s.Value > 70);

        // Act
        var anomalies = sensors
            .AsParallel()
            .Cases(
                sensor => sensor.Type == "temperature" && sensor.Value > 30,
                sensor => sensor.Type == "humidity" && sensor.Value > 70,
                sensor => sensor.Type == "pressure" && (sensor.Value < 980 || sensor.Value > 1020)
            )
            .SelectCase<SensorReading, string>(
                temp => $"HIGH TEMP: {temp.Value:F1}°C (Sensor: {temp.SensorId})",
                humidity => $"HIGH HUMIDITY: {humidity.Value:F1}% (Sensor: {humidity.SensorId})",
                pressure => $"ABNORMAL PRESSURE: {pressure.Value:F1}hPa (Sensor: {pressure.SensorId})"
            )
            .AllCases()
            .ToList();

        // Assert
        Assert.Equal(tempAnomalies, anomalies.Count(a => a.StartsWith("HIGH TEMP:")));
        Assert.Equal(humidityAnomalies, anomalies.Count(a => a.StartsWith("HIGH HUMIDITY:")));
    }

    [Fact]
    public async Task AsyncParallel_SensorProcessing_TransformsWithPatternMatch()
    {
        // Arrange
        var sensors = TestDataGenerators.GenerateSensorReadings(25).ToList();

        // Act
        var results = await sensors
            .Async()
            .AsParallel()
            .WithMaxConcurrency(Environment.ProcessorCount)
            .Where(sensor =>
                (sensor.Type == "temperature" && sensor.Value > 30) ||
                (sensor.Type == "humidity" && sensor.Value > 70) ||
                (sensor.Type == "pressure" && (sensor.Value < 980 || sensor.Value > 1020)))
            .Select(sensor => sensor.Type switch
            {
                "temperature" when sensor.Value > 30 =>
                    $"HIGH TEMP: {sensor.Value:F1}°C (Sensor: {sensor.SensorId})",
                "humidity" when sensor.Value > 70 =>
                    $"HIGH HUMIDITY: {sensor.Value:F1}% (Sensor: {sensor.SensorId})",
                "pressure" when sensor.Value < 980 || sensor.Value > 1020 =>
                    $"ABNORMAL PRESSURE: {sensor.Value:F1}hPa (Sensor: {sensor.SensorId})",
                _ => $"UNKNOWN SENSOR: {sensor.Type}={sensor.Value:F1}"
            })
            .ToList();

        // Assert - verify structure of results
        Assert.All(results, r => Assert.True(
            r.StartsWith("HIGH TEMP:") ||
            r.StartsWith("HIGH HUMIDITY:") ||
            r.StartsWith("ABNORMAL PRESSURE:") ||
            r.StartsWith("UNKNOWN SENSOR:")));
    }

    #endregion

    #region Performance Characteristics Tests

    [Fact]
    public async Task AsyncParallel_WithDifferentConcurrency_AllProduceSameResults()
    {
        // Arrange
        var data = Enumerable.Range(1, 50).Select(i => $"item_{i}").ToList();
        Func<string, string> transformer = item => $"processed_{item}";

        // Act - Concurrency 1
        var results1 = await data
            .Async()
            .AsParallel()
            .WithMaxConcurrency(1)
            .Select(transformer)
            .ToList();
        results1 = results1.OrderBy(x => x).ToList();

        // Act - Concurrency 4
        var results4 = await data
            .Async()
            .AsParallel()
            .WithMaxConcurrency(4)
            .Select(transformer)
            .ToList();
        results4 = results4.OrderBy(x => x).ToList();

        // Act - Concurrency = ProcessorCount
        var resultsMax = await data
            .Async()
            .AsParallel()
            .WithMaxConcurrency(Environment.ProcessorCount)
            .Select(transformer)
            .ToList();
        resultsMax = resultsMax.OrderBy(x => x).ToList();

        // Assert - All produce identical results
        Assert.Equal(results1.Count, results4.Count);
        Assert.Equal(results4.Count, resultsMax.Count);
        Assert.True(results1.SequenceEqual(results4), "Concurrency 1 and 4 should match");
        Assert.True(results4.SequenceEqual(resultsMax), "Concurrency 4 and Max should match");
    }

    #endregion
}
