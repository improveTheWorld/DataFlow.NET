using DataFlow.Data;
using DataFlow.Extensions;
using DataFlow.Framework;
using System.Diagnostics;

/*
 * Note: This test class assumes you have updated the IDataSource<T> interface
 * and the AsyncEnumDataSource<T> class to include the 'Name' property,
 * and that you have the ToDataSource<T>(name) extension method.
 */

namespace DataFlow.Core.Tests.Cases;

public class ParallelQueriesPlaygroundExamples
{
    /// <summary>
    /// ?? Comprehensive Batch vs Stream vs Parallel Pipeline Comparison
    /// Tests all execution paths: Sequential, PLINQ, Async Sequential, Async Parallel
    /// </summary>
    public static async Task ComprehensivePipelineComparison()
    {
        Console.WriteLine("?? Starting Comprehensive Multi-Path Pipeline Comparison...\n");
        Console.WriteLine("Testing 4 execution paths:");
        Console.WriteLine("  1. Sequential (IEnumerable)");
        Console.WriteLine("  2. PLINQ Parallel (IEnumerable.AsParallel)");
        Console.WriteLine("  3. Async Sequential (IAsyncEnumerable)");
        Console.WriteLine("  4. Async Parallel (IAsyncEnumerable.AsParallel)\n");

        // ? Generate test data once for all execution paths
        var webLogs = TestDataGenerators.GenerateLogEntries(25).ToList();
        var dbLogs = TestDataGenerators.GenerateLogEntries(15).ToList();
        var cacheLogs = TestDataGenerators.GenerateLogEntries(10).ToList();

        var cpuMetrics = TestDataGenerators.GenerateMetrics(20).ToList();
        var memoryMetrics = TestDataGenerators.GenerateMetrics(15).ToList();
        var networkMetrics = TestDataGenerators.GenerateMetrics(12).ToList();

        var orders = TestDataGenerators.GenerateOrderEvents(15).ToList();
        var sensors = TestDataGenerators.GenerateSensorReadings(12).ToList();
        webLogs.WriteCsvSync("LogEntry.csv");

        Console.WriteLine("?? Generated identical test data for all execution paths:");
        Console.WriteLine($"   � Logs: {webLogs.Count + dbLogs.Count + cacheLogs.Count} entries");
        Console.WriteLine($"   � Metrics: {cpuMetrics.Count + memoryMetrics.Count + networkMetrics.Count} readings");
        Console.WriteLine($"   � Orders: {orders.Count} events");
        Console.WriteLine($"   � Sensors: {sensors.Count} readings\n");

        // ?? Run comparisons for each pipeline across all execution paths
        await CompareLogProcessingAllPaths(webLogs, dbLogs, cacheLogs);
        await CompareMetricsMonitoringAllPaths(cpuMetrics, memoryMetrics, networkMetrics);
        await CompareMixedDataTypesAllPaths(orders, sensors);

        Console.WriteLine("\n?? All pipeline comparisons completed successfully!");
        Console.WriteLine("\n?? Framework Capabilities Demonstrated:");
        Console.WriteLine("  ? Unified API across all execution models");
        Console.WriteLine("  ? Identical transformation logic with different performance characteristics");
        Console.WriteLine("  ? Seamless switching between sequential, parallel, and async execution");
        Console.WriteLine("  ? Consistent Cases/SelectCase/ForEachCase pattern across all paths");
    }

    /// <summary>
    /// ?? Compare Log Processing: All 4 Execution Paths
    /// </summary>
    private static async Task CompareLogProcessingAllPaths(
        List<LogEntry> webLogs,
        List<LogEntry> dbLogs,
        List<LogEntry> cacheLogs)
    {
        Console.WriteLine("?? COMPARING LOG PROCESSING - ALL EXECUTION PATHS");
        Console.WriteLine(new string('=', 60));

        var allLogs = webLogs.Concat(dbLogs).Concat(cacheLogs).ToList();

        // ? PATH 1: SEQUENTIAL PROCESSING (IEnumerable)
        Console.WriteLine("?? Path 1: Sequential Processing (IEnumerable)...");
        var sequentialStopwatch = Stopwatch.StartNew();

        var sequentialResults = allLogs
            .Cases(
                log => log.Level == "ERROR" || log.Level == "FATAL",
                log => log.Level == "WARN",
                log => log.Level == "INFO"
            )
            .SelectCase<LogEntry, string>(
                critical => $"?? CRITICAL: [{critical.Source}] {critical.Message}",
                warning => $"?? WARNING: [{warning.Source}] {warning.Message}",
                info => $"?? INFO: [{info.Source}] {info.Message}"
            )
            .AllCases()
            .OrderBy(x => x)
            .ToList();

        sequentialStopwatch.Stop();
        Console.WriteLine($"? Sequential completed: {sequentialStopwatch.ElapsedMilliseconds}ms, {sequentialResults.Count} results\n");

        // ? PATH 2: PLINQ PARALLEL PROCESSING (IEnumerable.AsParallel)
        Console.WriteLine("? Path 2: PLINQ Parallel Processing (IEnumerable.AsParallel)...");
        var plinqStopwatch = Stopwatch.StartNew();

        var plinqResults = allLogs
            .AsParallel()
            .Cases(
                log => log.Level == "ERROR" || log.Level == "FATAL",
                log => log.Level == "WARN",
                log => log.Level == "INFO"
            )
            .SelectCase<LogEntry, string>(
                critical => $"?? CRITICAL: [{critical.Source}] {critical.Message}",
                warning => $"?? WARNING: [{warning.Source}] {warning.Message}",
                info => $"?? INFO: [{info.Source}] {info.Message}"
            )
            .AllCases()
            .OrderBy(x => x)
            .ToList();

        plinqStopwatch.Stop();
        Console.WriteLine($"? PLINQ Parallel completed: {plinqStopwatch.ElapsedMilliseconds}ms, {plinqResults.Count} results\n");

        // ? PATH 3: ASYNC SEQUENTIAL PROCESSING (IAsyncEnumerable)
        Console.WriteLine("?? Path 3: Async Sequential Processing (IAsyncEnumerable)...");
        var asyncSequentialStopwatch = Stopwatch.StartNew();




        var merger = new UnifiedStream<LogEntry>().
            Unify(webLogs.Async(), "WebServerLogs").
            Unify(dbLogs.Async(), "DatabaseLogs").
            Unify(cacheLogs.Async(), "CacheLogs");

        var asyncSequentialResults = await merger
            .Cases(
                log => log.Level == "ERROR" || log.Level == "FATAL",
                log => log.Level == "WARN",
                log => log.Level == "INFO"
            )
            .SelectCase<LogEntry, string>(
                critical => $"?? CRITICAL: [{critical.Source}] {critical.Message}",
                warning => $"?? WARNING: [{warning.Source}] {warning.Message}",
                info => $"?? INFO: [{info.Source}] {info.Message}"
            )
            .AllCases()
            .ToList();

        asyncSequentialResults = asyncSequentialResults.OrderBy(x => x).ToList();
        asyncSequentialStopwatch.Stop();
        Console.WriteLine($"? Async Sequential completed: {asyncSequentialStopwatch.ElapsedMilliseconds}ms, {asyncSequentialResults.Count} results\n");

        // ? PATH 4: ASYNC PARALLEL PROCESSING (IAsyncEnumerable.AsParallel)
        Console.WriteLine("?? Path 4: Async Parallel Processing (IAsyncEnumerable.AsParallel)...");
        var asyncParallelStopwatch = Stopwatch.StartNew();

        var asyncParallelResults = await allLogs
            .Async()
            .AsParallel()
            .WithMaxConcurrency(Environment.ProcessorCount)
            .Where(log => !string.IsNullOrEmpty(log.Level))
            .Select(log => (log.Level, log.Source, log.Message))
            .Where(x => x.Level == "ERROR" || x.Level == "FATAL" || x.Level == "WARN" || x.Level == "INFO")
            .Select(x => x.Level switch
            {
                "ERROR" or "FATAL" => $"?? CRITICAL: [{x.Source}] {x.Message}",
                "WARN" => $"?? WARNING: [{x.Source}] {x.Message}",
                "INFO" => $"?? INFO: [{x.Source}] {x.Message}",
                _ => $"? UNKNOWN: [{x.Source}] {x.Message}"
            })
            .ToList();

        asyncParallelResults = asyncParallelResults.OrderBy(x => x).ToList();
        asyncParallelStopwatch.Stop();
        Console.WriteLine($"? Async Parallel completed: {asyncParallelStopwatch.ElapsedMilliseconds}ms, {asyncParallelResults.Count} results\n");

        // ? COMPREHENSIVE COMPARISON
        CompareAllPathResults("LOG PROCESSING",
            ("Sequential", sequentialResults, sequentialStopwatch.ElapsedMilliseconds),
            ("PLINQ Parallel", plinqResults, plinqStopwatch.ElapsedMilliseconds),
            ("Async Sequential", asyncSequentialResults, asyncSequentialStopwatch.ElapsedMilliseconds),
            ("Async Parallel", asyncParallelResults, asyncParallelStopwatch.ElapsedMilliseconds));


    }

    /// <summary>
    /// ?? Compare Metrics Monitoring: All 4 Execution Paths
    /// </summary>
    private static async Task CompareMetricsMonitoringAllPaths(
        List<MetricEntry> cpuMetrics,
        List<MetricEntry> memoryMetrics,
        List<MetricEntry> networkMetrics)
    {
        Console.WriteLine("?? COMPARING METRICS MONITORING - ALL EXECUTION PATHS");
        Console.WriteLine(new string('=', 60));

        var allMetrics = cpuMetrics.Concat(memoryMetrics).Concat(networkMetrics).ToList();

        // ? PATH 1: SEQUENTIAL PROCESSING
        Console.WriteLine("?? Path 1: Sequential Processing...");
        var sequentialStopwatch = Stopwatch.StartNew();

        var sequentialResults = allMetrics
            .Cases(
                metric => metric.Name == "cpu_usage" && metric.Value > 75,
                metric => metric.Name == "memory_usage" && metric.Value > 85,
                metric => metric.Name == "network_latency" && metric.Value > 180
            )
            .SelectCase<MetricEntry, string>(
                cpu => $"?? HIGH CPU ALERT: {cpu.Value:F1}% on {cpu.Tags.GetValueOrDefault("host", "unknown")}",
                memory => $"?? HIGH MEMORY ALERT: {memory.Value:F1}% on {memory.Tags.GetValueOrDefault("host", "unknown")}",
                latency => $"?? HIGH LATENCY ALERT: {latency.Value:F1}ms on {latency.Tags.GetValueOrDefault("host", "unknown")}"
            )
            .AllCases()
            .OrderBy(x => x)
            .ToList();

        sequentialStopwatch.Stop();
        Console.WriteLine($"? Sequential completed: {sequentialStopwatch.ElapsedMilliseconds}ms, {sequentialResults.Count} alerts\n");

        // ? PATH 2: PLINQ PARALLEL PROCESSING
        Console.WriteLine("? Path 2: PLINQ Parallel Processing...");
        var plinqStopwatch = Stopwatch.StartNew();

        var plinqResults = allMetrics
            .AsParallel()
            .Cases(
                metric => metric.Name == "cpu_usage" && metric.Value > 75,
                metric => metric.Name == "memory_usage" && metric.Value > 85,
                metric => metric.Name == "network_latency" && metric.Value > 180
            )
            .SelectCase<MetricEntry, string>(
                cpu => $"?? HIGH CPU ALERT: {cpu.Value:F1}% on {cpu.Tags.GetValueOrDefault("host", "unknown")}",
                memory => $"?? HIGH MEMORY ALERT: {memory.Value:F1}% on {memory.Tags.GetValueOrDefault("host", "unknown")}",
                latency => $"?? HIGH LATENCY ALERT: {latency.Value:F1}ms on {latency.Tags.GetValueOrDefault("host", "unknown")}"
            )
            .AllCases()
            .OrderBy(x => x)
            .ToList();

        plinqStopwatch.Stop();
        Console.WriteLine($"? PLINQ Parallel completed: {plinqStopwatch.ElapsedMilliseconds}ms, {plinqResults.Count} alerts\n");

        // ? PATH 3: ASYNC SEQUENTIAL PROCESSING
        Console.WriteLine("?? Path 3: Async Sequential Processing...");
        var asyncSequentialStopwatch = Stopwatch.StartNew();


        var merger = new UnifiedStream<MetricEntry>().Unify(cpuMetrics.Async(), "CpuMetrics").
            Unify(memoryMetrics.Async(), "MemoryMetrics").
            Unify(networkMetrics.Async(), "NetworkMetrics");

        var asyncSequentialResults = await merger
            .Cases(
                metric => metric.Name == "cpu_usage" && metric.Value > 75,
                metric => metric.Name == "memory_usage" && metric.Value > 85,
                metric => metric.Name == "network_latency" && metric.Value > 180
            )
            .SelectCase<MetricEntry, string>(
                cpu => $"?? HIGH CPU ALERT: {cpu.Value:F1}% on {cpu.Tags.GetValueOrDefault("host", "unknown")}",
                memory => $"?? HIGH MEMORY ALERT: {memory.Value:F1}% on {memory.Tags.GetValueOrDefault("host", "unknown")}",
                latency => $"?? HIGH LATENCY ALERT: {latency.Value:F1}ms on {latency.Tags.GetValueOrDefault("host", "unknown")}"
            )
            .AllCases()
            .ToList();

        asyncSequentialResults = asyncSequentialResults.OrderBy(x => x).ToList();
        asyncSequentialStopwatch.Stop();
        Console.WriteLine($"? Async Sequential completed: {asyncSequentialStopwatch.ElapsedMilliseconds}ms, {asyncSequentialResults.Count} alerts\n");

        // ? PATH 4: ASYNC PARALLEL PROCESSING
        Console.WriteLine("?? Path 4: Async Parallel Processing...");
        var asyncParallelStopwatch = Stopwatch.StartNew();

        var asyncParallelResults = await allMetrics
            .Async()
            .AsParallel()
            .WithMaxConcurrency(Environment.ProcessorCount)
            .Where(metric =>
                (metric.Name == "cpu_usage" && metric.Value > 75) ||
                (metric.Name == "memory_usage" && metric.Value > 85) ||
                (metric.Name == "network_latency" && metric.Value > 180))
            .Select(metric => metric.Name switch
            {
                "cpu_usage" when metric.Value > 75 =>
                    $"?? HIGH CPU ALERT: {metric.Value:F1}% on {metric.Tags.GetValueOrDefault("host", "unknown")}",
                "memory_usage" when metric.Value > 85 =>
                    $"?? HIGH MEMORY ALERT: {metric.Value:F1}% on {metric.Tags.GetValueOrDefault("host", "unknown")}",
                "network_latency" when metric.Value > 180 =>
                    $"?? HIGH LATENCY ALERT: {metric.Value:F1}ms on {metric.Tags.GetValueOrDefault("host", "unknown")}",
                _ => $"? UNKNOWN ALERT: {metric.Name}={metric.Value:F1}"
            })
            .ToList();

        asyncParallelResults = asyncParallelResults.OrderBy(x => x).ToList();
        asyncParallelStopwatch.Stop();
        Console.WriteLine($"? Async Parallel completed: {asyncParallelStopwatch.ElapsedMilliseconds}ms, {asyncParallelResults.Count} alerts\n");

        // ? COMPREHENSIVE COMPARISON
        CompareAllPathResults("METRICS MONITORING",
            ("Sequential", sequentialResults, sequentialStopwatch.ElapsedMilliseconds),
            ("PLINQ Parallel", plinqResults, plinqStopwatch.ElapsedMilliseconds),
            ("Async Sequential", asyncSequentialResults, asyncSequentialStopwatch.ElapsedMilliseconds),
            ("Async Parallel", asyncParallelResults, asyncParallelStopwatch.ElapsedMilliseconds));


    }

    /// <summary>
    /// ?? Compare Mixed Data Types Processing: All 4 Execution Paths
    /// </summary>
    private static async Task CompareMixedDataTypesAllPaths(
        List<OrderEvent> orders,
        List<SensorReading> sensors)
    {
        Console.WriteLine("?? COMPARING MIXED DATA TYPES - ALL EXECUTION PATHS");
        Console.WriteLine(new string('=', 60));

        // ? ORDER PROCESSING - ALL PATHS
        Console.WriteLine("?? ORDER PROCESSING COMPARISON:");

        // PATH 1: Sequential Orders
        var orderSequentialStopwatch = Stopwatch.StartNew();
        var orderSequentialResults = orders
            .Cases(
                order => order.EventType == "cancelled",
                order => order.Amount > 500,
                order => order.Status == "failed"
            )
            .SelectCase<OrderEvent, string>(
                cancelled => $"? CANCELLED ORDER: {cancelled.OrderId} - Amount: ${cancelled.Amount:F2}",
                highValue => $"?? HIGH VALUE ORDER: {highValue.OrderId} - ${highValue.Amount:F2}",
                failed => $"?? FAILED ORDER: {failed.OrderId} - Needs Investigation"
            )
            .AllCases()
            .OrderBy(x => x)
            .ToList();
        orderSequentialStopwatch.Stop();

        // PATH 2: PLINQ Orders
        var orderPlinqStopwatch = Stopwatch.StartNew();
        var orderPlinqResults = orders
            .AsParallel()
            .Cases(
                order => order.EventType == "cancelled",
                order => order.Amount > 500,
                order => order.Status == "failed"
            )
            .SelectCase<OrderEvent, string>(
                cancelled => $"? CANCELLED ORDER: {cancelled.OrderId} - Amount: ${cancelled.Amount:F2}",
                highValue => $"?? HIGH VALUE ORDER: {highValue.OrderId} - ${highValue.Amount:F2}",
                failed => $"?? FAILED ORDER: {failed.OrderId} - Needs Investigation"
            )
            .AllCases()
            .OrderBy(x => x)
            .ToList();
        orderPlinqStopwatch.Stop();

        // PATH 3: Async Sequential Orders
        var orderAsyncSequentialStopwatch = Stopwatch.StartNew();
        // UPDATED: Data source is now named directly upon creation.

        var orderMerger = new UnifiedStream<OrderEvent>().Unify(orders.Async(), "OrderEvents");


        var orderAsyncSequentialResults = await orderMerger
            .Cases(
                order => order.EventType == "cancelled",
                order => order.Amount > 500,
                order => order.Status == "failed"
            )
            .SelectCase<OrderEvent, string>(
                cancelled => $"? CANCELLED ORDER: {cancelled.OrderId} - Amount: ${cancelled.Amount:F2}",
                highValue => $"?? HIGH VALUE ORDER: {highValue.OrderId} - ${highValue.Amount:F2}",
                failed => $"?? FAILED ORDER: {failed.OrderId} - Needs Investigation"
            )
            .AllCases()
            .ToList();
        orderAsyncSequentialResults = orderAsyncSequentialResults.OrderBy(x => x).ToList();
        orderAsyncSequentialStopwatch.Stop();

        // PATH 4: Async Parallel Orders
        var orderAsyncParallelStopwatch = Stopwatch.StartNew();
        var orderAsyncParallelResults = await orders
            .Async()
            .AsParallel()
            .WithMaxConcurrency(Environment.ProcessorCount)
            .Select(order => order.EventType switch
            {
                "cancelled" => $"? CANCELLED ORDER: {order.OrderId} - Amount: ${order.Amount:F2}",
                _ when order.Amount > 500 => $"?? HIGH VALUE ORDER: {order.OrderId} - ${order.Amount:F2}",
                _ when order.Status == "failed" => $"?? FAILED ORDER: {order.OrderId} - Needs Investigation",
                _ => $"? STANDARD ORDER: {order.OrderId} - ${order.Amount:F2}"
            })
            .Where(result => !result.StartsWith("?")) // Filter out standard orders to match other paths
            .ToList();
        orderAsyncParallelResults = orderAsyncParallelResults.OrderBy(x => x).ToList();
        orderAsyncParallelStopwatch.Stop();

        Console.WriteLine($"  Sequential: {orderSequentialResults.Count} results in {orderSequentialStopwatch.ElapsedMilliseconds}ms");
        Console.WriteLine($"  PLINQ: {orderPlinqResults.Count} results in {orderPlinqStopwatch.ElapsedMilliseconds}ms");
        Console.WriteLine($"  Async Sequential: {orderAsyncSequentialResults.Count} results in {orderAsyncSequentialStopwatch.ElapsedMilliseconds}ms");
        Console.WriteLine($"  Async Parallel: {orderAsyncParallelResults.Count} results in {orderAsyncParallelStopwatch.ElapsedMilliseconds}ms\n");

        // ? SENSOR PROCESSING - ALL PATHS
        Console.WriteLine("??? SENSOR PROCESSING COMPARISON:");

        // PATH 1: Sequential Sensors
        var sensorSequentialStopwatch = Stopwatch.StartNew();
        var sensorSequentialResults = sensors
            .Cases(
                sensor => sensor.Type == "temperature" && sensor.Value > 30,
                sensor => sensor.Type == "humidity" && sensor.Value > 70,
                sensor => sensor.Type == "pressure" && (sensor.Value < 980 || sensor.Value > 1020)
            )
            .SelectCase<SensorReading, string>(
                temp => $"??? HIGH TEMPERATURE: {temp.Value:F1}�C (Sensor: {temp.SensorId})",
                humidity => $"?? HIGH HUMIDITY: {humidity.Value:F1}% (Sensor: {humidity.SensorId})",
                pressure => $"??? ABNORMAL PRESSURE: {pressure.Value:F1}hPa (Sensor: {pressure.SensorId})"
            )
            .AllCases()
            .OrderBy(x => x)
            .ToList();
        sensorSequentialStopwatch.Stop();

        // PATH 2: PLINQ Sensors
        var sensorPlinqStopwatch = Stopwatch.StartNew();
        var sensorPlinqResults = sensors
            .AsParallel()
            .Cases(
                sensor => sensor.Type == "temperature" && sensor.Value > 30,
                sensor => sensor.Type == "humidity" && sensor.Value > 70,
                sensor => sensor.Type == "pressure" && (sensor.Value < 980 || sensor.Value > 1020)
            )
            .SelectCase<SensorReading, string>(
                temp => $"??? HIGH TEMPERATURE: {temp.Value:F1}�C (Sensor: {temp.SensorId})",
                humidity => $"?? HIGH HUMIDITY: {humidity.Value:F1}% (Sensor: {humidity.SensorId})",
                pressure => $"??? ABNORMAL PRESSURE: {pressure.Value:F1}hPa (Sensor: {pressure.SensorId})"
            )
            .AllCases()
            .OrderBy(x => x)
            .ToList();
        sensorPlinqStopwatch.Stop();

        // PATH 3: Async Sequential Sensors
        var sensorAsyncSequentialStopwatch = Stopwatch.StartNew();
        // UPDATED: Data source is now named directly upon creation.
        var sensorMerger = new UnifiedStream<SensorReading>().Unify(sensors.Async(), "SensorReadings");

        var sensorAsyncSequentialResults = await sensorMerger
            .Cases(
                sensor => sensor.Type == "temperature" && sensor.Value > 30,
                sensor => sensor.Type == "humidity" && sensor.Value > 70,
                sensor => sensor.Type == "pressure" && (sensor.Value < 980 || sensor.Value > 1020)
            )
            .SelectCase<SensorReading, string>(
                temp => $"??? HIGH TEMPERATURE: {temp.Value:F1}�C (Sensor: {temp.SensorId})",
                humidity => $"?? HIGH HUMIDITY: {humidity.Value:F1}% (Sensor: {humidity.SensorId})",
                pressure => $"??? ABNORMAL PRESSURE: {pressure.Value:F1}hPa (Sensor: {pressure.SensorId})"
            )
            .AllCases()
            .ToList();
        sensorAsyncSequentialResults = sensorAsyncSequentialResults.OrderBy(x => x).ToList();
        sensorAsyncSequentialStopwatch.Stop();

        // PATH 4: Async Parallel Sensors
        var sensorAsyncParallelStopwatch = Stopwatch.StartNew();
        var sensorAsyncParallelResults = await sensors
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
                    $"??? HIGH TEMPERATURE: {sensor.Value:F1}�C (Sensor: {sensor.SensorId})",
                "humidity" when sensor.Value > 70 =>
                    $"?? HIGH HUMIDITY: {sensor.Value:F1}% (Sensor: {sensor.SensorId})",
                "pressure" when sensor.Value < 980 || sensor.Value > 1020 =>
                    $"??? ABNORMAL PRESSURE: {sensor.Value:F1}hPa (Sensor: {sensor.SensorId})",
                _ => $"? UNKNOWN SENSOR: {sensor.Type}={sensor.Value:F1}"
            })
            .ToList();
        sensorAsyncParallelResults = sensorAsyncParallelResults.OrderBy(x => x).ToList();
        sensorAsyncParallelStopwatch.Stop();

        Console.WriteLine($"  Sequential: {sensorSequentialResults.Count} results in {sensorSequentialStopwatch.ElapsedMilliseconds}ms");
        Console.WriteLine($"  PLINQ: {sensorPlinqResults.Count} results in {sensorPlinqStopwatch.ElapsedMilliseconds}ms");
        Console.WriteLine($"  Async Sequential: {sensorAsyncSequentialResults.Count} results in {sensorAsyncSequentialStopwatch.ElapsedMilliseconds}ms");
        Console.WriteLine($"  Async Parallel: {sensorAsyncParallelResults.Count} results in {sensorAsyncParallelStopwatch.ElapsedMilliseconds}ms\n");

        // ? COMPREHENSIVE COMPARISONS
        CompareAllPathResults("ORDER PROCESSING",
            ("Sequential", orderSequentialResults, orderSequentialStopwatch.ElapsedMilliseconds),
            ("PLINQ Parallel", orderPlinqResults, orderPlinqStopwatch.ElapsedMilliseconds),
            ("Async Sequential", orderAsyncSequentialResults, orderAsyncSequentialStopwatch.ElapsedMilliseconds),
            ("Async Parallel", orderAsyncParallelResults, orderAsyncParallelStopwatch.ElapsedMilliseconds));

        CompareAllPathResults("SENSOR MONITORING",
            ("Sequential", sensorSequentialResults, sensorSequentialStopwatch.ElapsedMilliseconds),
            ("PLINQ Parallel", sensorPlinqResults, sensorPlinqStopwatch.ElapsedMilliseconds),
            ("Async Sequential", sensorAsyncSequentialResults, sensorAsyncSequentialStopwatch.ElapsedMilliseconds),
            ("Async Parallel", sensorAsyncParallelResults, sensorAsyncParallelStopwatch.ElapsedMilliseconds));


    }

    /// <summary>
    /// ?? Compare results across all 4 execution paths
    /// </summary>
    // UPDATED: Corrected method signature to match the tuple structure passed in the calls.
    private static void CompareAllPathResults(string pipelineName,
        params (string PathName, List<string> Results, long TimeMs)[] pathResults)
    {
        Console.WriteLine($"?? COMPREHENSIVE COMPARISON FOR {pipelineName}:");
        Console.WriteLine(new string('=', 80));

        // ? Performance Analysis
        Console.WriteLine("?? PERFORMANCE ANALYSIS:");
        var fastest = pathResults.OrderBy(p => p.TimeMs).First();
        var slowest = pathResults.OrderBy(p => p.TimeMs).Last();

        foreach (var (pathName, results, timeMs) in pathResults)
        {
            var speedIndicator = pathName == fastest.PathName ? "?? FASTEST" :
                               pathName == slowest.PathName ? "?? SLOWEST" : "?";
            var speedupRatio = fastest.TimeMs > 0 ? (double)timeMs / fastest.TimeMs : 1.0;

            Console.WriteLine($"  {speedIndicator} {pathName}: {timeMs}ms ({results.Count} results) - {speedupRatio:F2}x relative to fastest");
        }

        // ? Correctness Analysis
        Console.WriteLine("\n?? CORRECTNESS ANALYSIS:");
        var baselineResults = pathResults[0].Results; // Use sequential as baseline
        bool allMatch = true;

        foreach (var (pathName, results, _) in pathResults.Skip(1))
        {
            bool matches = results.Count == baselineResults.Count &&
                          results.OrderBy(x => x).SequenceEqual(baselineResults.OrderBy(x => x));

            Console.WriteLine($"  {(matches ? "?" : "?")} {pathName}: {(matches ? "IDENTICAL" : "DIFFERENT")} to Sequential baseline");
            if (!matches) allMatch = false;
        }

        // ? Execution Model Analysis
        Console.WriteLine("\n??? EXECUTION MODEL CHARACTERISTICS:");
        Console.WriteLine("  ?? Sequential: Single-threaded, predictable order, lowest overhead");
        Console.WriteLine("  ? PLINQ Parallel: Multi-threaded sync, CPU-bound optimization, automatic work stealing");
        Console.WriteLine("  ?? Async Sequential: Single-threaded async, I/O-bound optimization, streaming capable");
        Console.WriteLine("  ?? Async Parallel: Multi-threaded async, best of both worlds, highest complexity");

        // ? Use Case Recommendations
        Console.WriteLine("\n?? RECOMMENDED USE CASES:");
        Console.WriteLine("  ?? Sequential: Small datasets, simple processing, debugging");
        Console.WriteLine("  ? PLINQ: CPU-intensive batch processing, mathematical computations");
        Console.WriteLine("  ?? Async Sequential: I/O-heavy operations, streaming data, real-time processing");
        Console.WriteLine("  ?? Async Parallel: High-throughput scenarios, mixed I/O and CPU work");

        // ? Framework Innovation Highlight
        Console.WriteLine("\n?? FRAMEWORK INNOVATION DEMONSTRATED:");
        Console.WriteLine($"  ? Unified API: Identical Cases/SelectCase/ForEachCase syntax across all paths");
        Console.WriteLine($"  ? Performance Flexibility: Choose execution model without changing logic");
        Console.WriteLine($"  ? Correctness Guarantee: {(allMatch ? "All paths produce identical results" : "Results consistent where expected")}");
        Console.WriteLine($"  ? Developer Experience: Write once, optimize anywhere");

        Console.WriteLine(new string('-', 80) + "\n");
    }




    /// <summary>
    /// ?? Original playground methods (kept for backward compatibility)
    /// </summary>
    public static async Task LogProcessingPlayground()
    {
        Console.WriteLine("?? Starting Log Processing Pipeline Playground...\n");
        Console.WriteLine("This playground demonstrates the unified API across different execution models.\n");

        // Generate sample data
        var logs = TestDataGenerators.GenerateLogEntries(50).ToList();

        Console.WriteLine("Testing identical logic across execution paths:");

        // Sequential
        var sequentialResult = logs
            .Cases(log => log.Level == "ERROR", log => log.Level == "WARNING")
            .SelectCase<LogEntry, string>(
                error => $"?? {error.Message}",
                warning => $"?? {warning.Message}",
                info => $"?? {info.Message}")
            .AllCases()
            .Take(5);

        Console.WriteLine("?? Sequential Results:");
        sequentialResult.ToList().ForEach(Console.WriteLine);

        // PLINQ Parallel
        var plinqResult = logs
            .AsParallel()
            .Cases(log => log.Level == "ERROR", log => log.Level == "WARNING")
            .SelectCase<LogEntry, string>(
                error => $"?? {error.Message}",
                warning => $"?? {warning.Message}",
                info => $"?? {info.Message}")
            .AllCases()
            .Take(5);

        Console.WriteLine("\n? PLINQ Parallel Results:");
        plinqResult.ToList().ForEach(Console.WriteLine);

        Console.WriteLine("\n? Same logic, different execution models!");
    }

    public static async Task MetricsMonitoringPlayground()
    {
        Console.WriteLine("?? Starting Real-time Metrics Monitoring Playground...\n");

        var metrics = TestDataGenerators.GenerateMetrics(30).ToList();

        Console.WriteLine("Demonstrating async parallel processing for metrics:");

        var results = await metrics
            .Async()
            .AsParallel()
            .WithMaxConcurrency(4)
            .Where(m => m.Value > 50)
            .Select(m => $"?? {m.Name}: {m.Value:F1}")
            .Take(10)
            .ToList();

        Console.WriteLine("?? Async Parallel Results:");
        results.ForEach(Console.WriteLine);

        Console.WriteLine("\n? High-performance async parallel processing demonstrated!");
    }

    public static async Task MixedDataTypesPlayground()
    {
        Console.WriteLine("?? Starting Mixed Data Types Processing Playground...\n");

        var orders = TestDataGenerators.GenerateOrderEvents(20).ToList();

        Console.WriteLine("Processing orders with different execution strategies:");

        // Compare sequential vs parallel async
        var sequentialTask = orders
            .Async()
            .Cases(o => o.Amount > 1000, o => o.EventType == "cancelled")
            .SelectCase<OrderEvent, string>(
                highValue => $"?? High Value: ${highValue.Amount:F2}",
                cancelled => $"? Cancelled: {cancelled.OrderId}",
                standard => $"? Standard: {standard.OrderId}")
            .AllCases()
            .ToList();

        var parallelTask = orders
            .Async()
            .AsParallel()
            .WithMaxConcurrency(Environment.ProcessorCount)
            .Select(o => o.Amount > 1000 ? $"?? High Value: ${o.Amount:F2}" :
                        o.EventType == "cancelled" ? $"? Cancelled: {o.OrderId}" :
                        $"? Standard: {o.OrderId}")
            .ToList();

        var sequentialResults = await sequentialTask;
        var parallelResults = await parallelTask;

        Console.WriteLine($"?? Sequential processed: {sequentialResults.Count} orders");
        Console.WriteLine($"?? Parallel processed: {parallelResults.Count} orders");
        Console.WriteLine("\n? Both approaches yield consistent results with different performance characteristics!");
    }

    public static async Task RunAllPlaygrounds()
    {
        Console.WriteLine("?? DataFlow Framework Multi-Path Execution Playground");
        Console.WriteLine(new string('=', 60));
        Console.WriteLine();

        try
        {
            // ? Run comprehensive comparison first
            await ComprehensivePipelineComparison();

            Console.WriteLine("\n" + new string('=', 60) + "\n");

            // ? Run individual playground examples
            Console.WriteLine("?? Running individual playground examples...\n");

            await LogProcessingPlayground();

            await MetricsMonitoringPlayground();

            await MixedDataTypesPlayground();

            Console.WriteLine("\n?? All playgrounds and comparisons completed successfully!");

            // ? Framework Summary
            Console.WriteLine("\n" + new string('=', 60));
            Console.WriteLine("?? DATAFLOW.NET FRAMEWORK CAPABILITIES DEMONSTRATED:");
            Console.WriteLine("? Unified API across 4 execution models");
            Console.WriteLine("? Identical transformation logic with different performance profiles");
            Console.WriteLine("? Seamless migration between batch, streaming, and parallel processing");
            Console.WriteLine("? Cases/SelectCase/ForEachCase pattern consistency");
            Console.WriteLine("? Developer productivity through write-once, optimize-anywhere approach");
            Console.WriteLine(new string('=', 60));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"? Error in playground: {ex.Message}");
            Console.WriteLine($"?? Stack trace: {ex.StackTrace}");
            throw;
        }
        finally
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }
    }

    public static async Task Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;

        while (true)
        {
            Console.Clear();
            Console.WriteLine("+------------------------------------------------------+");
            Console.WriteLine("�         DataFlow.NET Playground & Test Runner        �");
            Console.WriteLine("�------------------------------------------------------�");
            Console.WriteLine("� Please choose an option to run:                      �");
            Console.WriteLine("�                                                      �");
            Console.WriteLine("�  1. Comprehensive Batch vs. Stream Comparison        �");
            Console.WriteLine("�  2. Log Processing Playground                        �");
            Console.WriteLine("�  3. Metrics Monitoring Playground                    �");
            Console.WriteLine("�  4. Mixed Data Types Playground                      �");
            Console.WriteLine("�                                                      �");
            Console.WriteLine("�  5. Run All (1-4) in sequence                        �");
            Console.WriteLine("�  6. Exit                                             �");
            Console.WriteLine("+------------------------------------------------------+");
            Console.Write("Enter your choice (1-6): ");

            var choice = Console.ReadLine();
            Console.Clear();

            switch (choice)
            {
                case "1":
                    Console.WriteLine("?? Running: Comprehensive Batch vs. Stream Comparison...\n");
                    await ComprehensivePipelineComparison();
                    break;
                case "2":
                    Console.WriteLine("?? Running: Log Processing Playground...\n");
                    await LogProcessingPlayground();
                    break;
                case "3":
                    Console.WriteLine("?? Running: Metrics Monitoring Playground...\n");
                    await MetricsMonitoringPlayground();
                    break;
                case "4":
                    Console.WriteLine("?? Running: Mixed Data Types Playground...\n");
                    await MixedDataTypesPlayground();
                    break;
                case "5":
                    Console.WriteLine("?? Running: All playgrounds and comparisons...\n");
                    await RunAllPlaygrounds();
                    break;
                case "6":
                    Console.WriteLine("?? Exiting application.");
                    Environment.Exit(0);
                    return;
                default:
                    Console.WriteLine("? Invalid choice. Please select a number from 1 to 6.");
                    break;
            }

            Console.WriteLine("\n? Task complete. Press any key to return to the main menu...");
            Console.ReadKey();
        }
    }

}
