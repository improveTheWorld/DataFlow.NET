﻿using DataFlow.Extensions;
using System.Diagnostics;

namespace DataFlow.Framework.DataFlowTest;

public class DataFlowPlaygroundExamples
{
    /// <summary>
    /// 🎯 Interactive Log Processing Pipeline with comprehensive monitoring
    /// </summary>
    public static async Task LogProcessingPlayground()
    {
        Console.WriteLine("🚀 Starting Log Processing Pipeline Playground...\n");

        // ✅ Create test data sources
        var webServerLogs = new TestDataSource<LogEntry>("WebServer");
        var databaseLogs = new TestDataSource<LogEntry>("Database");
        var cacheLogs = new TestDataSource<LogEntry>("Cache");

        var merger = new DataFlow<LogEntry>(null, null,
            webServerLogs, databaseLogs, cacheLogs
        );

        // ✅ Generate realistic test data
        var webLogs = TestDataGenerators.GenerateLogEntries(25);
        var dbLogs = TestDataGenerators.GenerateLogEntries(15);
        var cacheLogsList = TestDataGenerators.GenerateLogEntries(10);

        Console.WriteLine("📊 Generated test data:");
        Console.WriteLine($"   • WebServer: {webLogs.Count()} logs");
        Console.WriteLine($"   • Database: {dbLogs.Count()} logs");
        Console.WriteLine($"   • Cache: {cacheLogsList.Count()} logs\n");

        // Start streaming with different intervals to simulate real-world timing
        await webServerLogs.StartStreamingAsync(webLogs, TimeSpan.FromMilliseconds(80));
        await databaseLogs.StartStreamingAsync(dbLogs, TimeSpan.FromMilliseconds(120));
        await cacheLogs.StartStreamingAsync(cacheLogsList, TimeSpan.FromMilliseconds(200));

        Console.WriteLine("🔄 Streaming started with different intervals...\n");

        // ✅ Process with comprehensive monitoring using Spy
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
            .AllCases(false)
            .Display($"🎉 FINAL RESULTS, {webLogs.Count() + dbLogs.Count() + cacheLogsList.Count()}  EXPECTED ", separator: "\n📋 ");
        
        Console.WriteLine("\n\n✅ Log Processing Pipeline completed!\n");

        // Clean up
        webServerLogs.Stop();
        databaseLogs.Stop();
        cacheLogs.Stop();
        merger.Dispose();
    }

    /// <summary>
    /// 📊 Real-time Metrics Monitoring with Alert System
    /// </summary>
    public static async Task MetricsMonitoringPlayground()
    {
        Console.WriteLine("📊 Starting Real-time Metrics Monitoring Playground...\n");

        // ✅ Create metrics sources
        var cpuMetrics = new TestDataSource<MetricEntry>("CPU-Monitor");
        var memoryMetrics = new TestDataSource<MetricEntry>("Memory-Monitor");
        var networkMetrics = new TestDataSource<MetricEntry>("Network-Monitor");

        var merger = new DataFlow<MetricEntry>(null, null,
            cpuMetrics, memoryMetrics, networkMetrics
        );

        // ✅ Generate realistic metrics with some high values to trigger alerts
        var cpuData = TestDataGenerators.GenerateMetrics(20);
        var memoryData = TestDataGenerators.GenerateMetrics(15);
        var networkData = TestDataGenerators.GenerateMetrics(12);

        Console.WriteLine("📈 Generated metrics data:");
        Console.WriteLine($"   • CPU metrics: {cpuData.Count()} readings");
        Console.WriteLine($"   • Memory metrics: {memoryData.Count()} readings");
        Console.WriteLine($"   • Network metrics: {networkData.Count()} readings\n");

        // Start streaming at different rates
        await cpuMetrics.StartStreamingAsync(cpuData, TimeSpan.FromMilliseconds(100));
        await memoryMetrics.StartStreamingAsync(memoryData, TimeSpan.FromMilliseconds(150));
        await networkMetrics.StartStreamingAsync(networkData, TimeSpan.FromMilliseconds(200));

        Console.WriteLine("🔄 Metrics streaming started...\n");

        // ✅ Monitor with comprehensive alerting
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
        .AllCases(false)
        .Display($"🚨 ACTIVE ALERTS,  {cpuData.Count() + memoryData.Count() + networkData.Count()}  EXPECTED", separator: "\n🔔 ");

        Console.WriteLine("\n\n✅ Metrics Monitoring completed!\n");

        // Clean up
        cpuMetrics.Stop();
        memoryMetrics.Stop();
        networkMetrics.Stop();
        merger.Dispose();
    }

    /// <summary>
    /// 🔄 Multi-Stream Processing with Different Data Types
    /// </summary>
    public static async Task MixedDataTypesPlayground()
    {
        Console.WriteLine("🔄 Starting Mixed Data Types Processing Playground...\n");

        // ✅ Different data sources
        var orderSource = new TestDataSource<OrderEvent>("Order-System");
        var sensorSource = new TestDataSource<SensorReading>("IoT-Sensors");

        var orderMerger = new DataFlow<OrderEvent>(orderSource);
        var sensorMerger = new DataFlow<SensorReading>(sensorSource);

        // Generate test data
        var orders = TestDataGenerators.GenerateOrderEvents(15);
        var sensors = TestDataGenerators.GenerateSensorReadings(12);

        Console.WriteLine("📦 Generated mixed data:");
        Console.WriteLine($"   • Orders: {orders.Count()} events");
        Console.WriteLine($"   • Sensor readings: {sensors.Count()} readings\n");

        await orderSource.StartStreamingAsync(orders, TimeSpan.FromMilliseconds(120));
        await sensorSource.StartStreamingAsync(sensors, TimeSpan.FromMilliseconds(180));

        Console.WriteLine("🔄 Multi-stream processing started...\n");


        // ✅ Process orders with detailed monitoring
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
            .AllCases(false)
            .Display($"📋 ORDER PROCESSING RESULTS, {orders.Count()}  EXPECTED", separator: "\n🔸 ");

        // ✅ Process sensors with environmental monitoring
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
            .AllCases(false)
            .Display($"🌍 ENVIRONMENTAL MONITORING, {sensors.Count()}", separator: "\n🔹 ");

        // Wait for both pipelines
        await Task.WhenAll(orderTask, sensorTask);

        Console.WriteLine("\n\n✅ Mixed Data Types Processing completed!\n");

        // Clean up
        orderSource.Stop();
        sensorSource.Stop();
        orderMerger.Dispose();
        sensorMerger.Dispose();
    }

    public static async Task RunAllPlaygrounds()
    {
        Console.WriteLine("🎮 DataFlow Framework Interactive Playground");
        Console.WriteLine(new string('=', 50));
        Console.WriteLine();

        try
        {
            // Run Log Processing
            await LogProcessingPlayground();

            // ✅ Add small delay for visual separation
            await Task.Delay(500);
            Console.WriteLine("\n" + new string('=', 50) + "\n");

            // Run Metrics Monitoring
            await MetricsMonitoringPlayground();

            await Task.Delay(500);
            Console.WriteLine("\n" + new string('=', 50) + "\n");

            // Run Mixed Data Types
            await MixedDataTypesPlayground();

            Console.WriteLine("\n🎉 All playgrounds completed successfully!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error in playground: {ex.Message}");
            throw; // Re-throw to be handled by Main
        }
        finally
        {
            // ✅ Force garbage collection to clean up resources
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }
    }

    //public static async Task Main(string[] args)
    //{
    //    Console.OutputEncoding = System.Text.Encoding.UTF8;
    //    await DataFlowPlaygroundExamples.RunAllPlaygrounds();

    //    Console.WriteLine("\nPress any key to exit...");
    //    Console.ReadKey();
    //    Environment.Exit(0);
    //}

}

