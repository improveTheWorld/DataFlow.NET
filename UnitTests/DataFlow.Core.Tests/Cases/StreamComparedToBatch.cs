using DataFlow.Extensions;
using System.Diagnostics;
using DataFlow.Framework;

namespace DataFlow.Core.Tests.Cases;

public class StreamVSBArchPlaygroundExamples
{
    /// <summary>
    /// ?? Comprehensive Batch vs Stream Pipeline Comparison
    /// </summary>
    public static async Task ComprehensivePipelineComparison()
    {
        Console.WriteLine("?? Starting Comprehensive Batch vs Stream Pipeline Comparison...\n");

        // ? Generate test data once for both batch and stream processing
        var webLogs = TestDataGenerators.GenerateLogEntries(25).ToList();
        var dbLogs = TestDataGenerators.GenerateLogEntries(15).ToList();
        var cacheLogs = TestDataGenerators.GenerateLogEntries(10).ToList();

        var cpuMetrics = TestDataGenerators.GenerateMetrics(20).ToList();
        var memoryMetrics = TestDataGenerators.GenerateMetrics(15).ToList();
        var networkMetrics = TestDataGenerators.GenerateMetrics(12).ToList();

        var orders = TestDataGenerators.GenerateOrderEvents(15).ToList();
        var sensors = TestDataGenerators.GenerateSensorReadings(12).ToList();

        Console.WriteLine("?? Generated identical test data for comparison:");
        Console.WriteLine($"   � Logs: {webLogs.Count + dbLogs.Count + cacheLogs.Count} entries");
        Console.WriteLine($"   � Metrics: {cpuMetrics.Count + memoryMetrics.Count + networkMetrics.Count} readings");
        Console.WriteLine($"   � Orders: {orders.Count} events");
        Console.WriteLine($"   � Sensors: {sensors.Count} readings\n");

        // ?? Run comparisons for each pipeline
        await CompareLogProcessingPipeline(webLogs, dbLogs, cacheLogs);
        await CompareMetricsMonitoringPipeline(cpuMetrics, memoryMetrics, networkMetrics);
        await CompareMixedDataTypesPipeline(orders, sensors);

        Console.WriteLine("\n?? All pipeline comparisons completed successfully!");
    }

    /// <summary>
    /// ?? Compare Log Processing: Batch vs Stream
    /// </summary>
    private static async Task CompareLogProcessingPipeline(
        List<LogEntry> webLogs,
        List<LogEntry> dbLogs,
        List<LogEntry> cacheLogs)
    {
        Console.WriteLine("?? COMPARING LOG PROCESSING PIPELINE");
        Console.WriteLine(new string('-', 50));

        // ? BATCH PROCESSING
        Console.WriteLine("?? Processing logs as BATCH...");
        var batchStopwatch = Stopwatch.StartNew();

        var allLogs = webLogs.Concat(dbLogs).Concat(cacheLogs);
        var batchResults = allLogs
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
            .OrderBy(x => x) // Ensure consistent ordering for comparison
            .ToList();

        batchStopwatch.Stop();
        Console.WriteLine($"? Batch processing completed in {batchStopwatch.ElapsedMilliseconds}ms");
        Console.WriteLine($"?? Batch results: {batchResults.Count} processed items\n");

        // ? STREAM PROCESSING
        Console.WriteLine("?? Processing logs as STREAM...");
        var streamStopwatch = Stopwatch.StartNew();



        var merger = new UnifiedStream<LogEntry>().Unify(webLogs.Async(), "WebServerLogs").
            Unify(dbLogs.Async(), "DatabaseLogs").Unify(cacheLogs.Async(), "CacheLogs");


        var streamResults = await merger
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

        streamResults = streamResults.OrderBy(x => x).ToList(); // Ensure consistent ordering
        streamStopwatch.Stop();

        Console.WriteLine($"? Stream processing completed in {streamStopwatch.ElapsedMilliseconds}ms");
        Console.WriteLine($"?? Stream results: {streamResults.Count} processed items\n");

        // ? COMPARISON
        CompareResults("LOG PROCESSING", batchResults, streamResults, batchStopwatch.ElapsedMilliseconds, streamStopwatch.ElapsedMilliseconds);


    }

    /// <summary>
    /// ?? Compare Metrics Monitoring: Batch vs Stream
    /// </summary>
    private static async Task CompareMetricsMonitoringPipeline(
        List<MetricEntry> cpuMetrics,
        List<MetricEntry> memoryMetrics,
        List<MetricEntry> networkMetrics)
    {
        Console.WriteLine("?? COMPARING METRICS MONITORING PIPELINE");
        Console.WriteLine(new string('-', 50));

        // ? BATCH PROCESSING
        Console.WriteLine("?? Processing metrics as BATCH...");
        var batchStopwatch = Stopwatch.StartNew();

        var allMetrics = cpuMetrics.Concat(memoryMetrics).Concat(networkMetrics);
        var batchResults = allMetrics
            .Cases(
                metric => metric.Name == "cpu_usage" && metric.Value > 75,
                metric => metric.Name == "memory_usage" && metric.Value > 85,
                metric => metric.Name == "network_latency" && metric.Value > 180
            )
            .SelectCase<MetricEntry, string>(
                cpu => $"?? HIGH CPU ALERT: {cpu.Value:F1}% on {cpu.Tags.GetValueOrDefault("host", "unknown")} - Threshold: 75%",
                memory => $"?? HIGH MEMORY ALERT: {memory.Value:F1}% on {memory.Tags.GetValueOrDefault("host", "unknown")} - Threshold: 85%",
                latency => $"?? HIGH LATENCY ALERT: {latency.Value:F1}ms on {latency.Tags.GetValueOrDefault("host", "unknown")} - Threshold: 180ms"
            )
            .AllCases()
            .OrderBy(x => x)
            .ToList();

        batchStopwatch.Stop();
        Console.WriteLine($"? Batch processing completed in {batchStopwatch.ElapsedMilliseconds}ms");
        Console.WriteLine($"?? Batch results: {batchResults.Count} alerts generated\n");

        // ? STREAM PROCESSING
        Console.WriteLine("?? Processing metrics as STREAM...");
        var streamStopwatch = Stopwatch.StartNew();


        var merger = new UnifiedStream<MetricEntry>().Unify(cpuMetrics.Async(), "CpuMetrics").
            Unify(memoryMetrics.Async(), "MemoryMetrics").
            Unify(networkMetrics.Async(), "NetworkMetrics");


        var streamResults = await merger
            .Cases(
                metric => metric.Name == "cpu_usage" && metric.Value > 75,
                metric => metric.Name == "memory_usage" && metric.Value > 85,
                metric => metric.Name == "network_latency" && metric.Value > 180
            )
            .SelectCase<MetricEntry, string>(
                cpu => $"?? HIGH CPU ALERT: {cpu.Value:F1}% on {cpu.Tags.GetValueOrDefault("host", "unknown")} - Threshold: 75%",
                memory => $"?? HIGH MEMORY ALERT: {memory.Value:F1}% on {memory.Tags.GetValueOrDefault("host", "unknown")} - Threshold: 85%",
                latency => $"?? HIGH LATENCY ALERT: {latency.Value:F1}ms on {latency.Tags.GetValueOrDefault("host", "unknown")} - Threshold: 180ms"
            )
            .AllCases()
            .ToList();

        streamResults = streamResults.OrderBy(x => x).ToList();
        streamStopwatch.Stop();

        Console.WriteLine($"? Stream processing completed in {streamStopwatch.ElapsedMilliseconds}ms");
        Console.WriteLine($"?? Stream results: {streamResults.Count} alerts generated\n");

        // ? COMPARISON
        CompareResults("METRICS MONITORING", batchResults, streamResults, batchStopwatch.ElapsedMilliseconds, streamStopwatch.ElapsedMilliseconds);


    }

    /// <summary>
    /// ?? Compare Mixed Data Types Processing: Batch vs Stream
    /// </summary>
    private static async Task CompareMixedDataTypesPipeline(
        List<OrderEvent> orders,
        List<SensorReading> sensors)
    {
        Console.WriteLine("?? COMPARING MIXED DATA TYPES PIPELINE");
        Console.WriteLine(new string('-', 50));

        // ? BATCH PROCESSING - Orders
        Console.WriteLine("?? Processing orders as BATCH...");
        var orderBatchStopwatch = Stopwatch.StartNew();

        var orderBatchResults = orders
            .Cases(
                order => order.EventType == "cancelled",
                order => order.Amount > 500,
                order => order.Status == "failed"
            )
            .SelectCase<OrderEvent, string>(
                cancelled => $"? CANCELLED ORDER: {cancelled.OrderId} - Amount: ${cancelled.Amount:F2}",
                highValue => $"?? HIGH VALUE ORDER: {highValue.OrderId} - ${highValue.Amount:F2} - Priority Processing Required",
                failed => $"?? FAILED ORDER: {failed.OrderId} - Needs Investigation"
            )
            .AllCases()
            .OrderBy(x => x)
            .ToList();

        orderBatchStopwatch.Stop();

        // ? BATCH PROCESSING - Sensors
        var sensorBatchStopwatch = Stopwatch.StartNew();

        var sensorBatchResults = sensors
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

        sensorBatchStopwatch.Stop();

        Console.WriteLine($"? Batch processing completed:");
        Console.WriteLine($"   � Orders: {orderBatchResults.Count} results in {orderBatchStopwatch.ElapsedMilliseconds}ms");
        Console.WriteLine($"   � Sensors: {sensorBatchResults.Count} results in {sensorBatchStopwatch.ElapsedMilliseconds}ms\n");

        // ? STREAM PROCESSING
        Console.WriteLine("?? Processing mixed data as STREAM...");
        var streamStopwatch = Stopwatch.StartNew();

        // UPDATED: Added names to each data source.



        var orderMerger = new UnifiedStream<OrderEvent>().Unify(orders.Async(), "OrderEvents");
        var sensorMerger = new UnifiedStream<SensorReading>().Unify(sensors.Async(), "SensorReadings");

        var orderTask = orderMerger
            .Cases(
                order => order.EventType == "cancelled",
                order => order.Amount > 500,
                order => order.Status == "failed"
            )
            .SelectCase<OrderEvent, string>(
                cancelled => $"? CANCELLED ORDER: {cancelled.OrderId} - Amount: ${cancelled.Amount:F2}",
                highValue => $"?? HIGH VALUE ORDER: {highValue.OrderId} - ${highValue.Amount:F2} - Priority Processing Required",
                failed => $"?? FAILED ORDER: {failed.OrderId} - Needs Investigation"
            )
            .AllCases()
            .ToList();

        var sensorTask = sensorMerger
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

        await Task.WhenAll(orderTask, sensorTask);

        var orderStreamResults = (await orderTask).OrderBy(x => x).ToList();
        var sensorStreamResults = (await sensorTask).OrderBy(x => x).ToList();

        streamStopwatch.Stop();

        Console.WriteLine($"? Stream processing completed in {streamStopwatch.ElapsedMilliseconds}ms");
        Console.WriteLine($"   � Orders: {orderStreamResults.Count} results");
        Console.WriteLine($"   � Sensors: {sensorStreamResults.Count} results\n");

        // ? COMPARISONS
        CompareResults("ORDER PROCESSING", orderBatchResults, orderStreamResults,
            orderBatchStopwatch.ElapsedMilliseconds, streamStopwatch.ElapsedMilliseconds);

        CompareResults("SENSOR MONITORING", sensorBatchResults, sensorStreamResults,
            sensorBatchStopwatch.ElapsedMilliseconds, streamStopwatch.ElapsedMilliseconds);

    }

    /// <summary>
    /// ?? Compare results element by element
    /// </summary>
    private static void CompareResults(string pipelineName, List<string> batchResults, List<string> streamResults, long batchTime, long streamTime)
    {
        Console.WriteLine($"?? COMPARISON RESULTS FOR {pipelineName}:");
        Console.WriteLine(new string('=', 60));

        bool countMatch = batchResults.Count == streamResults.Count;
        Console.WriteLine($"?? Result Count: Batch={batchResults.Count}, Stream={streamResults.Count} {(countMatch ? "? MATCH" : "? MISMATCH")}");

        var speedup = batchTime == 0 ? "N/A" : $"{(double)streamTime / batchTime:F2}x";
        Console.WriteLine($"??  Performance: Batch={batchTime}ms, Stream={streamTime}ms (Stream is {speedup} of batch time)");

        int mismatches = 0;
        for (int i = 0; i < Math.Max(batchResults.Count, streamResults.Count); i++)
        {
            string batchItem = i < batchResults.Count ? batchResults[i] : "<MISSING>";
            string streamItem = i < streamResults.Count ? streamResults[i] : "<MISSING>";
            if (batchItem != streamItem) mismatches++;
        }

        Console.WriteLine($"\n?? COMPARISON SUMMARY:");
        Console.WriteLine($"   � Total items compared: {Math.Max(batchResults.Count, streamResults.Count)}");
        Console.WriteLine($"   � Mismatches: {mismatches} {(mismatches > 0 ? "?" : "?")}");
        string verdict = mismatches == 0 && countMatch ? "?? PERFECT MATCH" : "?? DIFFERENCES DETECTED";
        Console.WriteLine($"   � Verdict: {verdict}");

        Console.WriteLine(new string('-', 60) + "\n");
    }

    /// <summary>
    /// ?? Original playground methods (kept for backward compatibility and demonstration)
    /// </summary>
    public static async Task LogProcessingPlayground()
    {
        Console.WriteLine("?? Starting Log Processing Pipeline Playground...");
        Console.WriteLine("Simulating real-time logs from multiple sources (WebServer, Database, Cache)...");
        Console.WriteLine(new string('-', 70));



        // Create a DataFlow merger to process all log entries
        var merger = new UnifiedStream<LogEntry>().Unify(TestDataGenerators.GenerateLogEntries(15).Async(), "WebServerLogs").
            Unify(TestDataGenerators.GenerateLogEntries(10).Async(), "DatabaseLogs").
            Unify(TestDataGenerators.GenerateLogEntries(8).Async(), "CacheLogs");

        try
        {
            // Define the processing pipeline
            var processingTask = merger
                .Cases(
                    log => log.Level == "ERROR" || log.Level == "FATAL", // Case 1: Critical logs
                    log => log.Level == "WARN",                          // Case 2: Warning logs
                    log => log.Level == "INFO"                           // Case 3: Info logs
                )
                .SelectCase<LogEntry, string>(
                    critical => $"?? CRITICAL: [{critical.Source}] {critical.Message}",
                    warning => $"?? WARNING: [{warning.Source}] {warning.Message}",
                    info => $"?? INFO: [{info.Source}] {info.Message}"
                )
                .AllCases()
                .Do();

            await processingTask; // Wait for the pipeline to complete
        }
        finally
        {
            ; // Clean up resources
        }

        Console.WriteLine(new string('-', 70));
        Console.WriteLine("? Log Processing Playground finished.\n");
    }

    public static async Task MetricsMonitoringPlayground()
    {
        Console.WriteLine("?? Starting Real-time Metrics Monitoring Playground...");
        Console.WriteLine("Simulating a stream of metrics to generate real-time alerts...");
        Console.WriteLine(new string('-', 70));


        var merger = new UnifiedStream<MetricEntry>().Unify(TestDataGenerators.GenerateMetrics().Async(), "CpuMetrics").
            Unify(TestDataGenerators.GenerateMetrics().Async(), "MemoryMetrics").
            Unify(TestDataGenerators.GenerateMetrics().Async(), "NetworkMetrics");

        try
        {
            // Define the alerting pipeline
            var alertingTask = merger
                .Cases(
                    metric => metric.Name == "cpu_usage" && metric.Value > 75,
                    metric => metric.Name == "memory_usage" && metric.Value > 85,
                    metric => metric.Name == "network_latency" && metric.Value > 180
                )
                .SelectCase<MetricEntry, string>(
                    cpu => $"?? HIGH CPU ALERT: {cpu.Value:F1}% on {cpu.Tags.GetValueOrDefault("host", "unknown")} - Threshold: 75%",
                    memory => $"?? HIGH MEMORY ALERT: {memory.Value:F1}% on {memory.Tags.GetValueOrDefault("host", "unknown")} - Threshold: 85%",
                    latency => $"?? HIGH LATENCY ALERT: {latency.Value:F1}ms on {latency.Tags.GetValueOrDefault("host", "unknown")} - Threshold: 180ms"
                )
                .AllCases()
                .Do();

            await alertingTask;
        }
        finally
        {
            ;
        }

        Console.WriteLine(new string('-', 70));
        Console.WriteLine("? Metrics Monitoring Playground finished.\n");
    }

    public static async Task MixedDataTypesPlayground()
    {
        Console.WriteLine("?? Starting Mixed Data Types Processing Playground...");
        Console.WriteLine("Running two independent pipelines (Orders & Sensors) concurrently...");
        Console.WriteLine(new string('-', 70));

        // Setup sources for two different data types

        // Create a separate DataFlow instance for each data type
        var orderMerger = new UnifiedStream<OrderEvent>().Unify(TestDataGenerators.GenerateOrderEvents(15).Async(), "OrderEvents");
        var sensorMerger = new UnifiedStream<SensorReading>().Unify(TestDataGenerators.GenerateSensorReadings(20).Async(), "SensorReadings");

        try
        {
            // Define the Order processing pipeline
            var orderTask = orderMerger
                .Cases(
                    order => order.EventType == "cancelled",
                    order => order.Amount > 500
                )
                .SelectCase<OrderEvent, string>(
                    cancelled => $"[Order] ? CANCELLED: {cancelled.OrderId}, Amount: ${cancelled.Amount:F2}",
                    highValue => $"[Order] ?? HIGH VALUE: {highValue.OrderId}, Amount: ${highValue.Amount:F2}"
                )
                .AllCases()
                .Do();

            // Define the Sensor processing pipeline
            var sensorTask = sensorMerger
                .Cases(
                    sensor => sensor.Type == "temperature" && sensor.Value > 30,
                    sensor => sensor.Type == "pressure" && (sensor.Value < 980 || sensor.Value > 1020)
                )
                .SelectCase<SensorReading, string>(
                    temp => $"[Sensor] ??? HIGH TEMP: {temp.Value:F1}�C (ID: {temp.SensorId})",
                    pressure => $"[Sensor] ??? ABNORMAL PRESSURE: {pressure.Value:F1}hPa (ID: {pressure.SensorId})"
                )
                .AllCases()
                .Do();
            // Run both pipelines concurrently
            await Task.WhenAll(orderTask, sensorTask);
        }
        finally
        {
            ;
        }

        Console.WriteLine(new string('-', 70));
        Console.WriteLine("? Mixed Data Types Playground finished.\n");
    }



    /// <summary>
    /// ?? Original playground methods (kept for backward compatibility)
    /// </summary>
    public static async Task RunAllPlaygrounds()
    {
        Console.WriteLine("?? DataFlow Framework Interactive Playground");
        Console.WriteLine(new string('=', 50));
        Console.WriteLine();

        try
        {
            // ? Run comprehensive comparison first
            await ComprehensivePipelineComparison();

            Console.WriteLine("\n" + new string('=', 50) + "\n");

            // ? Optionally run individual playgrounds
            Console.WriteLine("?? Running individual playground examples...\n");

            await LogProcessingPlayground(); ;

            await MetricsMonitoringPlayground();

            await MixedDataTypesPlayground();

            Console.WriteLine("\n?? All playgrounds and comparisons completed successfully!");
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


}