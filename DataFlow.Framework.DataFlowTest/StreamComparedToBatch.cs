﻿using DataFlow.Extensions;
using System.Diagnostics;

namespace DataFlow.Framework.DataFlowTest;

public class StreamVSBArchPlaygroundExamples
{
    /// <summary>
    /// 🎯 Comprehensive Batch vs Stream Pipeline Comparison
    /// </summary>
    public static async Task ComprehensivePipelineComparison()
    {
        Console.WriteLine("🔬 Starting Comprehensive Batch vs Stream Pipeline Comparison...\n");

        // ✅ Generate test data once for both batch and stream processing
        var webLogs = TestDataGenerators.GenerateLogEntries(25).ToList();
        var dbLogs = TestDataGenerators.GenerateLogEntries(15).ToList();
        var cacheLogs = TestDataGenerators.GenerateLogEntries(10).ToList();

        var cpuMetrics = TestDataGenerators.GenerateMetrics(20).ToList();
        var memoryMetrics = TestDataGenerators.GenerateMetrics(15).ToList();
        var networkMetrics = TestDataGenerators.GenerateMetrics(12).ToList();

        var orders = TestDataGenerators.GenerateOrderEvents(15).ToList();
        var sensors = TestDataGenerators.GenerateSensorReadings(12).ToList();

        Console.WriteLine("📊 Generated identical test data for comparison:");
        Console.WriteLine($"   • Logs: {webLogs.Count + dbLogs.Count + cacheLogs.Count} entries");
        Console.WriteLine($"   • Metrics: {cpuMetrics.Count + memoryMetrics.Count + networkMetrics.Count} readings");
        Console.WriteLine($"   • Orders: {orders.Count} events");
        Console.WriteLine($"   • Sensors: {sensors.Count} readings\n");

        // 🔄 Run comparisons for each pipeline
        await CompareLogProcessingPipeline(webLogs, dbLogs, cacheLogs);
        await CompareMetricsMonitoringPipeline(cpuMetrics, memoryMetrics, networkMetrics);
        await CompareMixedDataTypesPipeline(orders, sensors);

        Console.WriteLine("\n🎉 All pipeline comparisons completed successfully!");
    }

    /// <summary>
    /// 📋 Compare Log Processing: Batch vs Stream
    /// </summary>
    private static async Task CompareLogProcessingPipeline(
        List<LogEntry> webLogs,
        List<LogEntry> dbLogs,
        List<LogEntry> cacheLogs)
    {
        Console.WriteLine("📋 COMPARING LOG PROCESSING PIPELINE");
        Console.WriteLine(new string('-', 50));

        // ✅ BATCH PROCESSING
        Console.WriteLine("🔄 Processing logs as BATCH...");
        var batchStopwatch = Stopwatch.StartNew();

        var allLogs = webLogs.Concat(dbLogs).Concat(cacheLogs);
        var batchResults = allLogs
            .Cases(
                log => log.Level == "ERROR" || log.Level == "FATAL",  // Critical
                log => log.Level == "WARN",                           // Warning
                log => log.Level == "INFO"                            // Info
            )
            .SelectCase(
                critical => $"🚨 CRITICAL: [{critical.Source}] {critical.Message}",
                warning => $"⚠️ WARNING: [{warning.Source}] {warning.Message}",
                info => $"ℹ️ INFO: [{info.Source}] {info.Message}"
            )
            .AllCases()
            .OrderBy(x => x) // Ensure consistent ordering for comparison
            .ToList();

        batchStopwatch.Stop();
        Console.WriteLine($"✅ Batch processing completed in {batchStopwatch.ElapsedMilliseconds}ms");
        Console.WriteLine($"📊 Batch results: {batchResults.Count} processed items\n");

        // ✅ STREAM PROCESSING
        Console.WriteLine("🌊 Processing logs as STREAM...");
        var streamStopwatch = Stopwatch.StartNew();

        var webServerLogs = new TestDataSource<LogEntry>("WebServer");
        var databaseLogs = new TestDataSource<LogEntry>("Database");
        var cacheLogsSource = new TestDataSource<LogEntry>("Cache");

        var merger = new DataFlow<LogEntry>(null, null,
            webServerLogs, databaseLogs, cacheLogsSource
        );

        // Start streaming
        await webServerLogs.StartStreamingAsync(webLogs, TimeSpan.FromMilliseconds(1));
        await databaseLogs.StartStreamingAsync(dbLogs, TimeSpan.FromMilliseconds(1));
        await cacheLogsSource.StartStreamingAsync(cacheLogs, TimeSpan.FromMilliseconds(1));



        var streamResults =
            await merger
        .Cases(
                log => log.Level == "ERROR" || log.Level == "FATAL",  // Critical
                log => log.Level == "WARN",                           // Warning
                log => log.Level == "INFO"                            // Info
            )
            .SelectCase(
                critical => $"🚨 CRITICAL: [{critical.Source}] {critical.Message}",
                warning => $"⚠️ WARNING: [{warning.Source}] {warning.Message}",
                info => $"ℹ️ INFO: [{info.Source}] {info.Message}"
            )
            .AllCases()
            .ToList();

 

        streamResults = streamResults.OrderBy(x => x).ToList(); // Ensure consistent ordering
        streamStopwatch.Stop();

        Console.WriteLine($"✅ Stream processing completed in {streamStopwatch.ElapsedMilliseconds}ms");
        Console.WriteLine($"📊 Stream results: {streamResults.Count} processed items\n");

        // ✅ COMPARISON
        CompareResults("LOG PROCESSING", batchResults, streamResults, batchStopwatch.ElapsedMilliseconds, streamStopwatch.ElapsedMilliseconds);

        // Cleanup
        webServerLogs.Stop();
        databaseLogs.Stop();
        cacheLogsSource.Stop();
        merger.Dispose();
    }

    /// <summary>
    /// 📊 Compare Metrics Monitoring: Batch vs Stream
    /// </summary>
    private static async Task CompareMetricsMonitoringPipeline(
        List<MetricEntry> cpuMetrics,
        List<MetricEntry> memoryMetrics,
        List<MetricEntry> networkMetrics)
    {
        Console.WriteLine("📊 COMPARING METRICS MONITORING PIPELINE");
        Console.WriteLine(new string('-', 50));

        // ✅ BATCH PROCESSING
        Console.WriteLine("🔄 Processing metrics as BATCH...");
        var batchStopwatch = Stopwatch.StartNew();

        var allMetrics = cpuMetrics.Concat(memoryMetrics).Concat(networkMetrics);
        var batchResults = allMetrics
            .Cases(
                metric => metric.Name == "cpu_usage" && metric.Value > 75,      // High CPU
                metric => metric.Name == "memory_usage" && metric.Value > 85,   // High Memory
                metric => metric.Name == "network_latency" && metric.Value > 180 // High Latency
            )
            .SelectCase(
                cpu => $"🔥 HIGH CPU ALERT: {cpu.Value:F1}% on {cpu.Tags.GetValueOrDefault("host", "unknown")} - Threshold: 75%",
                memory => $"💾 HIGH MEMORY ALERT: {memory.Value:F1}% on {memory.Tags.GetValueOrDefault("host", "unknown")} - Threshold: 85%",
                latency => $"🌐 HIGH LATENCY ALERT: {latency.Value:F1}ms on {latency.Tags.GetValueOrDefault("host", "unknown")} - Threshold: 180ms"
            )
            .AllCases()
            .OrderBy(x => x)
            .ToList();

        batchStopwatch.Stop();
        Console.WriteLine($"✅ Batch processing completed in {batchStopwatch.ElapsedMilliseconds}ms");
        Console.WriteLine($"📊 Batch results: {batchResults.Count} alerts generated\n");

        // ✅ STREAM PROCESSING
        Console.WriteLine("🌊 Processing metrics as STREAM...");
        var streamStopwatch = Stopwatch.StartNew();

        var cpuSource = new TestDataSource<MetricEntry>("CPU-Monitor");
        var memorySource = new TestDataSource<MetricEntry>("Memory-Monitor");
        var networkSource = new TestDataSource<MetricEntry>("Network-Monitor");

        var merger = new DataFlow<MetricEntry>(null, null,
            cpuSource, memorySource, networkSource
        );

        await cpuSource.StartStreamingAsync(cpuMetrics, TimeSpan.FromMilliseconds(1));
        await memorySource.StartStreamingAsync(memoryMetrics, TimeSpan.FromMilliseconds(1));
        await networkSource.StartStreamingAsync(networkMetrics, TimeSpan.FromMilliseconds(1));

        var streamResults = 
        await merger
            .Cases(
                metric => metric.Name == "cpu_usage" && metric.Value > 75,      // High CPU
                metric => metric.Name == "memory_usage" && metric.Value > 85,   // High Memory
                metric => metric.Name == "network_latency" && metric.Value > 180 // High Latency
            )
            .SelectCase(
                cpu => $"🔥 HIGH CPU ALERT: {cpu.Value:F1}% on {cpu.Tags.GetValueOrDefault("host", "unknown")} - Threshold: 75%",
                memory => $"💾 HIGH MEMORY ALERT: {memory.Value:F1}% on {memory.Tags.GetValueOrDefault("host", "unknown")} - Threshold: 85%",
                latency => $"🌐 HIGH LATENCY ALERT: {latency.Value:F1}ms on {latency.Tags.GetValueOrDefault("host", "unknown")} - Threshold: 180ms"
            )
            .AllCases()
           .ToList();

        streamResults = streamResults.OrderBy(x => x).ToList();
        streamStopwatch.Stop();

        Console.WriteLine($"✅ Stream processing completed in {streamStopwatch.ElapsedMilliseconds}ms");
        Console.WriteLine($"📊 Stream results: {streamResults.Count} alerts generated\n");

        // ✅ COMPARISON
        CompareResults("METRICS MONITORING", batchResults, streamResults, batchStopwatch.ElapsedMilliseconds, streamStopwatch.ElapsedMilliseconds);

        // Cleanup
        cpuSource.Stop();
        memorySource.Stop();
        networkSource.Stop();
        merger.Dispose();
    }

    /// <summary>
    /// 🔄 Compare Mixed Data Types Processing: Batch vs Stream
    /// </summary>
    private static async Task CompareMixedDataTypesPipeline(
        List<OrderEvent> orders,
        List<SensorReading> sensors)
    {
        Console.WriteLine("🔄 COMPARING MIXED DATA TYPES PIPELINE");
        Console.WriteLine(new string('-', 50));

        // ✅ BATCH PROCESSING - Orders
        Console.WriteLine("🔄 Processing orders as BATCH...");
        var orderBatchStopwatch = Stopwatch.StartNew();

        var orderBatchResults = orders
            .Cases(
                order => order.EventType == "cancelled",
                order => order.Amount > 500,
                order => order.Status == "failed"
            )
            .SelectCase(
                cancelled => $"❌ CANCELLED ORDER: {cancelled.OrderId} - Amount: ${cancelled.Amount:F2}",
                highValue => $"💎 HIGH VALUE ORDER: {highValue.OrderId} - ${highValue.Amount:F2} - Priority Processing Required",
                failed => $"⚠️ FAILED ORDER: {failed.OrderId} - Needs Investigation"
            )
            .AllCases()
            .OrderBy(x => x)
            .ToList();

        orderBatchStopwatch.Stop();

        // ✅ BATCH PROCESSING - Sensors
        var sensorBatchStopwatch = Stopwatch.StartNew();

        var sensorBatchResults = sensors
            .Cases(
                sensor => sensor.Type == "temperature" && sensor.Value > 30,
                sensor => sensor.Type == "humidity" && sensor.Value > 70,
                sensor => sensor.Type == "pressure" && (sensor.Value < 980 || sensor.Value > 1020)
            )
            .SelectCase(
                temp => $"🌡️ HIGH TEMPERATURE: {temp.Value:F1}°C (Sensor: {temp.SensorId})",
                humidity => $"💧 HIGH HUMIDITY: {humidity.Value:F1}% (Sensor: {humidity.SensorId})",
                pressure => $"🌪️ ABNORMAL PRESSURE: {pressure.Value:F1}hPa (Sensor: {pressure.SensorId})"
            )
            .AllCases()
            .OrderBy(x => x)
            .ToList();

        sensorBatchStopwatch.Stop();

        Console.WriteLine($"✅ Batch processing completed:");
        Console.WriteLine($"   • Orders: {orderBatchResults.Count} results in {orderBatchStopwatch.ElapsedMilliseconds}ms");
        Console.WriteLine($"   • Sensors: {sensorBatchResults.Count} results in {sensorBatchStopwatch.ElapsedMilliseconds}ms\n");

        // ✅ STREAM PROCESSING
        Console.WriteLine("🌊 Processing mixed data as STREAM...");
        var streamStopwatch = Stopwatch.StartNew();

        var orderSource = new TestDataSource<OrderEvent>("Order-System");
        var sensorSource = new TestDataSource<SensorReading>("IoT-Sensors");

        var orderMerger = new DataFlow<OrderEvent>(orderSource);
        var sensorMerger = new DataFlow<SensorReading>(sensorSource);

        await orderSource.StartStreamingAsync(orders, TimeSpan.FromMilliseconds(1));
        await sensorSource.StartStreamingAsync(sensors, TimeSpan.FromMilliseconds(1));

        var orderStreamResults = new List<string>();
        var sensorStreamResults = new List<string>();

        var orderTask = orderMerger
            .Cases(
                order => order.EventType == "cancelled",
                order => order.Amount > 500,
                order => order.Status == "failed"
            )
            .SelectCase(
                cancelled => $"❌ CANCELLED ORDER: {cancelled.OrderId} - Amount: ${cancelled.Amount:F2}",
                highValue => $"💎 HIGH VALUE ORDER: {highValue.OrderId} - ${highValue.Amount:F2} - Priority Processing Required",
                failed => $"⚠️ FAILED ORDER: {failed.OrderId} - Needs Investigation"
            )
            .AllCases()
            .ToList();

        var sensorTask = sensorMerger
            .Cases(
                sensor => sensor.Type == "temperature" && sensor.Value > 30,
                sensor => sensor.Type == "humidity" && sensor.Value > 70,
                sensor => sensor.Type == "pressure" && (sensor.Value < 980 || sensor.Value > 1020)
            )
            .SelectCase(
                temp => $"🌡️ HIGH TEMPERATURE: {temp.Value:F1}°C (Sensor: {temp.SensorId})",
                humidity => $"💧 HIGH HUMIDITY: {humidity.Value:F1}% (Sensor: {humidity.SensorId})",
                pressure => $"🌪️ ABNORMAL PRESSURE: {pressure.Value:F1}hPa (Sensor: {pressure.SensorId})"
            )
            .AllCases()
            .ToList();

        await Task.WhenAll(orderTask, sensorTask);

        orderStreamResults = orderTask.Result.OrderBy(x => x).ToList();
        sensorStreamResults = sensorTask.Result.OrderBy(x => x).ToList();

        streamStopwatch.Stop();

        Console.WriteLine($"✅ Stream processing completed in {streamStopwatch.ElapsedMilliseconds}ms");
        Console.WriteLine($"   • Orders: {orderStreamResults.Count} results");
        Console.WriteLine($"   • Sensors: {sensorStreamResults.Count} results\n");

        // ✅ COMPARISONS
        CompareResults("ORDER PROCESSING", orderBatchResults, orderStreamResults,
            orderBatchStopwatch.ElapsedMilliseconds, streamStopwatch.ElapsedMilliseconds);

        CompareResults("SENSOR MONITORING", sensorBatchResults, sensorStreamResults,
            sensorBatchStopwatch.ElapsedMilliseconds, streamStopwatch.ElapsedMilliseconds);

        // Cleanup
        orderSource.Stop();
        sensorSource.Stop();
        orderMerger.Dispose();
        sensorMerger.Dispose();
    }

    /// <summary>
    /// 🔍 Compare results element by element
    /// </summary>
    private static void CompareResults(string pipelineName, List<string> batchResults, List<string> streamResults, long batchTime, long streamTime)
    {
        Console.WriteLine($"🔍 COMPARISON RESULTS FOR {pipelineName}:");
        Console.WriteLine(new string('=', 60));

        // ✅ Count comparison
        bool countMatch = batchResults.Count == streamResults.Count;
        Console.WriteLine($"📊 Result Count: Batch={batchResults.Count}, Stream={streamResults.Count} {(countMatch ? "✅ MATCH" : "❌ MISMATCH")}");

        // ✅ Performance comparison
        var speedup = batchTime == 0 ? "N/A" : $"{(double)streamTime / batchTime:F2}x";
        Console.WriteLine($"⏱️  Performance: Batch={batchTime}ms, Stream={streamTime}ms (Stream is {speedup} of batch time)");

        // ✅ Element-by-element comparison
        int matches = 0;
        int mismatches = 0;
        int maxComparisons = Math.Max(batchResults.Count, streamResults.Count);

        Console.WriteLine($"\n🔍 Element-by-element comparison (showing first 10 items):");

        for (int i = 0; i < Math.Min(10, maxComparisons); i++)
        {
            string batchItem = i < batchResults.Count ? batchResults[i] : "<MISSING>";
            string streamItem = i < streamResults.Count ? streamResults[i] : "<MISSING>";

            bool match = batchItem == streamItem;
            if (match) matches++; else mismatches++;

            string status = match ? "✅" : "❌";
            Console.WriteLine($"   [{i:D2}] {status} Batch: {batchItem}");
            if (!match)
            {
                Console.WriteLine($"        Stream: {streamItem}");
            }
        }

        if (maxComparisons > 10)
        {
            // Check remaining items without displaying them
            for (int i = 10; i < maxComparisons; i++)
            {
                string batchItem = i < batchResults.Count ? batchResults[i] : "<MISSING>";
                string streamItem = i < streamResults.Count ? streamResults[i] : "<MISSING>";

                if (batchItem == streamItem) matches++; else mismatches++;
            }
            Console.WriteLine($"   ... and {maxComparisons - 10} more items compared");
        }

        // ✅ Final verdict
        Console.WriteLine($"\n📈 COMPARISON SUMMARY:");
        Console.WriteLine($"   • Total items compared: {maxComparisons}");
        Console.WriteLine($"   • Matches: {matches} ✅");
        Console.WriteLine($"   • Mismatches: {mismatches} {(mismatches > 0 ? "❌" : "✅")}");
        Console.WriteLine($"   • Accuracy: {(maxComparisons == 0 ? 100 : (matches * 100.0 / maxComparisons)):F1}%");

        string verdict = mismatches == 0 && countMatch ? "🎉 PERFECT MATCH" : "⚠️ DIFFERENCES DETECTED";
        Console.WriteLine($"   • Verdict: {verdict}");

        Console.WriteLine(new string('-', 60) + "\n");
    }

    /// <summary>
    /// 🎯 Original playground methods (kept for backward compatibility)
    /// </summary>
    public static async Task LogProcessingPlayground()
    {
        // Keep original implementation for individual testing
        Console.WriteLine("🚀 Starting Log Processing Pipeline Playground...\n");
        // ... (original implementation)
    }

    public static async Task MetricsMonitoringPlayground()
    {
        // Keep original implementation for individual testing
        Console.WriteLine("📊 Starting Real-time Metrics Monitoring Playground...\n");
        // ... (original implementation)
    }

    public static async Task MixedDataTypesPlayground()
    {
        // Keep original implementation for individual testing
        Console.WriteLine("🔄 Starting Mixed Data Types Processing Playground...\n");
        // ... (original implementation)
    }

    public static async Task RunAllPlaygrounds()
    {
        Console.WriteLine("🎮 DataFlow Framework Interactive Playground");
        Console.WriteLine(new string('=', 50));
        Console.WriteLine();

        try
        {
            // ✅ Run comprehensive comparison first
            await ComprehensivePipelineComparison();

            Console.WriteLine("\n" + new string('=', 50) + "\n");

            // ✅ Optionally run individual playgrounds
            Console.WriteLine("🎯 Running individual playground examples...\n");

            await LogProcessingPlayground();
            await Task.Delay(500);

            await MetricsMonitoringPlayground();
            await Task.Delay(500);

            await MixedDataTypesPlayground();

            Console.WriteLine("\n🎉 All playgrounds and comparisons completed successfully!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error in playground: {ex.Message}");
            Console.WriteLine($"📍 Stack trace: {ex.StackTrace}");
            throw;
        }
        finally
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }
    }
    
    //public static async Task Main(string[] args)
    //{
    //    Console.OutputEncoding = System.Text.Encoding.UTF8;

    //    // ✅ Add option to run only comparison
    //    if (args.Length > 0 && args[0] == "--compare-only")
    //    {
    //        await ComprehensivePipelineComparison();
    //    }
    //    else
    //    {
    //        await RunAllPlaygrounds();
    //    }

    //    Console.WriteLine("\nPress any key to exit...");
    //    Console.ReadKey();
    //    Environment.Exit(0);
    //}
}