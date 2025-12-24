
using DataFlow.Extensions;
using DataFlow.Framework;

namespace DataFlow.Core.Tests.Cases;

public class DataFlowPlaygroundExamples
{
    /// <summary>
    /// ?? Interactive Log Processing Pipeline with comprehensive monitoring
    /// </summary>
    public static async Task LogProcessingPlayground()
    {
        Console.WriteLine("?? Starting Log Processing Pipeline Playground...\n");

        // ? Generate realistic test data
        var webLogs = TestDataGenerators.GenerateLogEntries(25).ToList();
        var dbLogs = TestDataGenerators.GenerateLogEntries(15).ToList();
        var cacheLogsList = TestDataGenerators.GenerateLogEntries(10).ToList();

        // ? Create and name test data sources

        Console.WriteLine("?? Generated test data:");
        Console.WriteLine($"   � WebServer: {webLogs.Count} logs");
        Console.WriteLine($"   � Database: {dbLogs.Count} logs");
        Console.WriteLine($"   � Cache: {cacheLogsList.Count} logs\n");

        // ? Create a single DataFlow merger for all log sources
        var logMerger = new UnifiedStream<LogEntry>().
            Unify(webLogs.Async(), "WebServerLogs").
            Unify(dbLogs.Async(), "DatabaseLogs").
            Unify(cacheLogsList.Async(), "CacheLogs");


        Console.WriteLine("?? Streaming started with different intervals...\n");

        // ? Process with comprehensive monitoring using Spy
        await logMerger
            .Cases(
                log => log.Level == "ERROR" || log.Level == "FATAL",  // Critical
                log => log.Level == "WARN",                           // Warning
                log => log.Level == "INFO"                            // Info
            )
            .SelectCase<LogEntry, string>(critical => $"?? CRITICAL: [{critical.Source}] {critical.Message}",
                warning => $"?? WARNING: [{warning.Source}] {warning.Message}",
                info => $"?? INFO: [{info.Source}] {info.Message}"
            )
            .AllCases(false)
            .Display($"?? FINAL RESULTS, {webLogs.Count + dbLogs.Count + cacheLogsList.Count} EXPECTED", separator: "\n?? ");

        Console.WriteLine("\n\n? Log Processing Pipeline completed!\n");


    }

    /// <summary>
    /// ?? Real-time Metrics Monitoring with Alert System
    /// </summary>
    public static async Task MetricsMonitoringPlayground()
    {
        Console.WriteLine("?? Starting Real-time Metrics Monitoring Playground...\n");

        // ? Generate realistic metrics with some high values to trigger alerts
        var cpuData = TestDataGenerators.GenerateMetrics(20).ToList();
        var memoryData = TestDataGenerators.GenerateMetrics(15).ToList();
        var networkData = TestDataGenerators.GenerateMetrics(12).ToList();

        // ? Create and name metrics sources
        // UPDATED: Added names to each data source.


        var merger = new UnifiedStream<MetricEntry>().Unify(cpuData.Async(), "CpuMetrics").
            Unify(memoryData.Async(), "MemoryMetrics").
            Unify(networkData.Async(), "NetworkMetrics");


        Console.WriteLine("?? Generated metrics data:");
        Console.WriteLine($"   � CPU metrics: {cpuData.Count} readings");
        Console.WriteLine($"   � Memory metrics: {memoryData.Count} readings");
        Console.WriteLine($"   � Network metrics: {networkData.Count} readings\n");

        Console.WriteLine("?? Metrics streaming started...\n");

        // ? Monitor with comprehensive alerting
        await merger
            .Cases(
                metric => metric.Name == "cpu_usage" && metric.Value > 75,      // High CPU
                metric => metric.Name == "memory_usage" && metric.Value > 85,   // High Memory
                metric => metric.Name == "network_latency" && metric.Value > 180 // High Latency
            )
            .SelectCase<MetricEntry, string>(
                cpu => $"?? HIGH CPU ALERT: {cpu.Value:F1}% on {cpu.Tags.GetValueOrDefault("host", "unknown")} - Threshold: 75%",
                memory => $"?? HIGH MEMORY ALERT: {memory.Value:F1}% on {memory.Tags.GetValueOrDefault("host", "unknown")} - Threshold: 85%",
                latency => $"?? HIGH LATENCY ALERT: {latency.Value:F1}ms on {latency.Tags.GetValueOrDefault("host", "unknown")} - Threshold: 180ms"
            )
            .AllCases(false)
            .Display($"?? ACTIVE ALERTS, {cpuData.Count + memoryData.Count + networkData.Count} EXPECTED", separator: "\n?? ");

        Console.WriteLine("\n\n? Metrics Monitoring completed!\n");

    }

    /// <summary>
    /// ?? Multi-Stream Processing with Different Data Types
    /// </summary>
    public static async Task MixedDataTypesPlayground()
    {
        Console.WriteLine("?? Starting Mixed Data Types Processing Playground...\n");

        // Generate test data
        var orders = TestDataGenerators.GenerateOrderEvents(15).ToList();
        var sensors = TestDataGenerators.GenerateSensorReadings(12).ToList();


        var orderMerger = new UnifiedStream<OrderEvent>().Unify(orders.Async(), "OrderEvents");
        var sensorMerger = new UnifiedStream<SensorReading>().Unify(sensors.Async(), "SensorReadings");

        Console.WriteLine("?? Generated mixed data:");
        Console.WriteLine($"   � Orders: {orders.Count} events");
        Console.WriteLine($"   � Sensor readings: {sensors.Count} readings\n");

        Console.WriteLine("?? Multi-stream processing started...\n");

        // ? Process orders with detailed monitoring
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
            .AllCases(false)
            .Display($"?? ORDER PROCESSING RESULTS, {orders.Count} EXPECTED", separator: "\n?? ");

        // ? Process sensors with environmental monitoring
        var sensorTask = sensorMerger
            .Cases(
                sensor => sensor.Type == "temperature" && sensor.Value > 30,
                sensor => sensor.Type == "humidity" && sensor.Value > 70,
                sensor => sensor.Type == "pressure" && (sensor.Value < 980 || sensor.Value > 1020)
            )
            .SelectCase<SensorReading, string>(
                temp => $"??? HIGH TEMPERATURE: {temp.Value:F1}°C (Sensor: {temp.SensorId})",
                humidity => $"?? HIGH HUMIDITY: {humidity.Value:F1}% (Sensor: {humidity.SensorId})",
                pressure => $"??? ABNORMAL PRESSURE: {pressure.Value:F1}hPa (Sensor: {pressure.SensorId})"
            )
            .AllCases(false)
            .Display($"?? ENVIRONMENTAL MONITORING, {sensors.Count} EXPECTED", separator: "\n?? ");

        // Wait for both pipelines
        await Task.WhenAll(orderTask, sensorTask);

        Console.WriteLine("\n\n? Mixed Data Types Processing completed!\n");


    }

    public static async Task RunAllPlaygrounds()
    {
        Console.WriteLine("?? DataFlow Framework Interactive Playground");
        Console.WriteLine(new string('=', 50));
        Console.WriteLine();

        try
        {
            // Run Log Processing
            await LogProcessingPlayground();

            // ? Add small delay for visual separation
            Console.WriteLine("\n" + new string('=', 50) + "\n");

            // Run Metrics Monitoring
            await MetricsMonitoringPlayground();

            Console.WriteLine("\n" + new string('=', 50) + "\n");

            // Run Mixed Data Types
            await MixedDataTypesPlayground();

            Console.WriteLine("\n?? All playgrounds completed successfully!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"? Error in playground: {ex.Message}");
            throw; // Re-throw to be handled by Main
        }
        finally
        {
            // ? Force garbage collection to clean up resources
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }
    }

}


