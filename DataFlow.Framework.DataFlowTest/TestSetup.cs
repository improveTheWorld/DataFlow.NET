//using DataFlow.Extensions;

//namespace DataFlow.Framework.DataFlowTest;

//public class AsyncEnumerableMergerTests
//{
//    /// <summary>
//    /// 🎯 Interactive Log Processing Pipeline - CORRECTED
//    /// </summary>
//    //[Test]
//    public async Task TestLogProcessingPipeline()
//    {
//        Console.WriteLine("🎯 Starting Log Processing Pipeline Test...\n");

//        // ✅ Create test data sources
//        var webServerLogs = new TestDataSource<LogEntry>("WebServer");
//        var databaseLogs = new TestDataSource<LogEntry>("Database");
//        var cacheLogs = new TestDataSource<LogEntry>("Cache");

//        // ✅ Create DataFlow (renamed from Merger)
//        var dataFlow = new DataFlow<LogEntry>(null, null,
//            webServerLogs, databaseLogs, cacheLogs
//        );

//        // ✅ Start streaming test data
//        var webLogs = TestDataGenerators.GenerateLogEntries(50);
//        var dbLogs = TestDataGenerators.GenerateLogEntries(30);
//        var cacheLogsList = TestDataGenerators.GenerateLogEntries(20);

//        Console.WriteLine("📦 Generated test data:");
//        Console.WriteLine($"   • Web Server logs: {webLogs.Count()}");
//        Console.WriteLine($"   • Database logs: {dbLogs.Count()}");
//        Console.WriteLine($"   • Cache logs: {cacheLogsList.Count()}\n");

//        // Start streaming with different intervals
//        await webServerLogs.StartStreamingAsync(webLogs, TimeSpan.FromMilliseconds(100));
//        await databaseLogs.StartStreamingAsync(dbLogs, TimeSpan.FromMilliseconds(150));
//        await cacheLogs.StartStreamingAsync(cacheLogsList, TimeSpan.FromMilliseconds(200));

//        Console.WriteLine("🔄 Log processing pipeline started...\n");

//        // ✅ Process with comprehensive monitoring using Spy - CORRECTED
//        await dataFlow
//            .Spy("📥 RAW LOGS",
//                 log => $"{log.Source}|{log.Level}|{log.Message[..Math.Min(30, log.Message.Length)]}",
//                 timeStamp: true, separator: "\n   ")
//            .Cases(
//                log => log.Level == "ERROR" || log.Level == "FATAL",  // Critical
//                log => log.Level == "WARN",                           // Warning
//                log => log.Level == "INFO"                            // Info
//            )
//            .Spy("🎯 AFTER CASES",
//                 result => $"Case[{result.category}]: {result.item?.Source ?? "NULL"}",
//                 timeStamp: true, separator: "\n   ")
//            .SelectCase(
//                critical => $"🚨 CRITICAL: [{critical.Source}] {critical.Message}",
//                warning => $"⚠️ WARNING: [{warning.Source}] {warning.Message}",
//                info => $"ℹ️ INFO: [{info.Source}] {info.Message}"
//            )
//            .Spy("✨ PROCESSED",
//                 result => $"[{result.category}] {result.newItem?[..Math.Min(50, result.newItem?.Length ?? 0)] ?? "IGNORED"}",
//                 timeStamp: true, separator: "\n   ")
//            .Where(x => x.newItem != null)
//            .Select(x => x.newItem!)
//            .Take(30)
//            .Display("🎉 FINAL LOG RESULTS", separator: "\n📋 ");


//        Console.WriteLine("\n✅ Log Processing Pipeline completed!\n");

//        // Clean up
//        webServerLogs.Stop();
//        databaseLogs.Stop();
//        cacheLogs.Stop();
//        dataFlow.Dispose();
//    }

//    /// <summary>
//    /// 📊 Real-time Metrics Monitoring - CORRECTED
//    /// </summary>
//    //[Test]
//    public async Task TestMetricsProcessingPipeline()
//    {
//        Console.WriteLine("📊 Starting Metrics Processing Pipeline Test...\n");

//        // ✅ Create metrics sources
//        var cpuMetrics = new TestDataSource<MetricEntry>("CPU");
//        var memoryMetrics = new TestDataSource<MetricEntry>("Memory");
//        var networkMetrics = new TestDataSource<MetricEntry>("Network");

//        var dataFlow = new DataFlow<MetricEntry>(null, null,
//            cpuMetrics, memoryMetrics, networkMetrics
//        );

//        // ✅ Generate and stream metrics
//        var cpuData = TestDataGenerators.GenerateMetrics(30);
//        var memoryData = TestDataGenerators.GenerateMetrics(30);
//        var networkData = TestDataGenerators.GenerateMetrics(30);

//        Console.WriteLine("📈 Generated test metrics:");
//        Console.WriteLine($"   • CPU metrics: {cpuData.Count()}");
//        Console.WriteLine($"   • Memory metrics: {memoryData.Count()}");
//        Console.WriteLine($"   • Network metrics: {networkData.Count()}\n");

//        await cpuMetrics.StartStreamingAsync(cpuData, TimeSpan.FromMilliseconds(50));
//        await memoryMetrics.StartStreamingAsync(memoryData, TimeSpan.FromMilliseconds(75));
//        await networkMetrics.StartStreamingAsync(networkData, TimeSpan.FromMilliseconds(100));

//        Console.WriteLine("🔄 Metrics monitoring started...\n");

//        // ✅ Monitor with comprehensive alerting - CORRECTED
//        await dataFlow
//            .Spy("📊 INCOMING METRICS",
//                 metric => $"{metric.Name}={metric.Value:F1} @{metric.Tags.GetValueOrDefault("host", "unknown")}",
//                 timeStamp: true, separator: "\n   📈 ")
//            .Cases(
//                metric => metric.Name == "cpu_usage" && metric.Value > 75,      // High CPU
//                metric => metric.Name == "memory_usage" && metric.Value > 85,   // High Memory
//                metric => metric.Name == "network_latency" && metric.Value > 180 // High Latency
//            )
//            .Spy("🎯 THRESHOLD CHECK",
//                 result => $"Case[{result.category}]: {result.item?.Name}={result.item?.Value:F1} → {(result.category >= 0 ? "ALERT" : "OK")}",
//                 timeStamp: true, separator: "\n   🔍 ")
//            .SelectCase(
//                cpu => $"🔥 HIGH CPU ALERT: {cpu.Value:F1}% on {cpu.Tags.GetValueOrDefault("host", "unknown")} - Threshold: 75%",
//                memory => $"💾 HIGH MEMORY ALERT: {memory.Value:F1}% on {memory.Tags.GetValueOrDefault("host", "unknown")} - Threshold: 85%",
//                latency => $"🌐 HIGH LATENCY ALERT: {latency.Value:F1}ms on {latency.Tags.GetValueOrDefault("host", "unknown")} - Threshold: 180ms"
//            )
//            .Spy("🚨 ALERT GENERATION",
//                 result => result.newItem != null ? $"ALERT: {result.newItem[..Math.Min(60, result.newItem.Length)]}" : "NO ALERT",
//                 timeStamp: true, separator: "\n   ⚠️ ")
//            .Where(x => x.newItem != null)
//            .Select(x => x.newItem!)
//            .Take(15)
//            .Display("🚨 ACTIVE ALERTS", separator: "\n🔔 ");

//        Console.WriteLine("\n✅ Metrics Processing Pipeline completed!\n");

//        // Clean up
//        cpuMetrics.Stop();
//        memoryMetrics.Stop();
//        networkMetrics.Stop();
//        dataFlow.Dispose();
//    }

//    /// <summary>
//    /// 🔄 Multi-Stream Processing with Different Data Types - CORRECTED
//    /// </summary>
//    //[Test]
//    public async Task TestMixedDataTypesPipeline()
//    {
//        Console.WriteLine("🔄 Starting Mixed Data Types Processing Test...\n");

//        // ✅ Test with different data types
//        var orderSource = new TestDataSource<OrderEvent>("Orders");
//        var sensorSource = new TestDataSource<SensorReading>("Sensors");

//        var orderDataFlow = new DataFlow<OrderEvent>(orderSource);
//        var sensorDataFlow = new DataFlow<SensorReading>(sensorSource);

//        // Generate test data
//        var orders = TestDataGenerators.GenerateOrderEvents(20);
//        var sensors = TestDataGenerators.GenerateSensorReadings(20);

//        Console.WriteLine("📦 Generated mixed data:");
//        Console.WriteLine($"   • Orders: {orders.Count()} events");
//        Console.WriteLine($"   • Sensor readings: {sensors.Count()} readings\n");

//        await orderSource.StartStreamingAsync(orders, TimeSpan.FromMilliseconds(100));
//        await sensorSource.StartStreamingAsync(sensors, TimeSpan.FromMilliseconds(150));

//        Console.WriteLine("🔄 Multi-stream processing started...\n");

//        // ✅ Process orders with detailed monitoring - FIXED
//        var orderTask = orderDataFlow
//            .Spy("📦 ORDER EVENTS",
//                 order => $"Order[{order.OrderId}]: {order.EventType} - ${order.Amount:F2} ({order.Status})",
//                 timeStamp: true, separator: "\n   💰 ")
//            .Cases(
//                order => order.EventType == "cancelled",
//                order => order.Amount > 500,
//                order => order.Status == "failed"
//            )
//            .Spy("🎯 ORDER ANALYSIS",
//                 result => $"Case[{result.category}]: {result.item?.OrderId} → {(result.category >= 0 ? "ACTION NEEDED" : "NORMAL")}",
//                 timeStamp: true, separator: "\n   📊 ")
//            .SelectCase(
//                cancelled => $"❌ CANCELLED ORDER: {cancelled.OrderId} - Amount: ${cancelled.Amount:F2}",
//                highValue => $"💎 HIGH VALUE ORDER: {highValue.OrderId} - ${highValue.Amount:F2} - Priority Processing Required",
//                failed => $"⚠️ FAILED ORDER: {failed.OrderId} - Needs Investigation"
//            )
//            .AllCases()
//            .Take(10)
//            .Display("📋 ORDER PROCESSING RESULTS", separator: "\n🔸 ");

//        // ✅ Process sensors with environmental monitoring - FIXED
//        var sensorTask = sensorDataFlow
//            .Spy("🌡️ SENSOR READINGS",
//                 sensor => $"Sensor[{sensor.SensorId}]: {sensor.Type}={sensor.Value:F1} ",
//                 timeStamp: true, separator: "\n   📡 ")
//            .Cases(
//                sensor => sensor.Type == "temperature" && sensor.Value > 30,
//                sensor => sensor.Type == "humidity" && sensor.Value > 70,
//                sensor => sensor.Type == "pressure" && (sensor.Value < 980 || sensor.Value > 1020)
//            )
//            .Spy("🎯 ENVIRONMENTAL CHECK",
//                 result => $"Case[{result.category}]: {result.item?.Type}={result.item?.Value:F1} → {(result.category >= 0 ? "THRESHOLD EXCEEDED" : "NORMAL")}",
//                 timeStamp: true, separator: "\n   🔍 ")
//            .SelectCase(
//                temp => $"🌡️ HIGH TEMPERATURE: {temp.Value:F1}°C  (Sensor: {temp.SensorId})",
//                humidity => $"💧 HIGH HUMIDITY: {humidity.Value:F1}% (Sensor: {humidity.SensorId})",
//                pressure => $"🌪️ ABNORMAL PRESSURE: {pressure.Value:F1}hPa (Sensor: {pressure.SensorId})"
//            )
//            .AllCases()
//            .Take(10)
//            .Display("🌍 ENVIRONMENTAL MONITORING", separator: "\n🔹 ");

//        // ✅ Wait for both pipelines to complete in parallel
//        await Task.WhenAll(orderTask, sensorTask);

//        Console.WriteLine("\n✅ Mixed Data Types Processing completed!\n");

//        // Clean up
//        orderSource.Stop();
//        sensorSource.Stop();
//        orderDataFlow.Dispose();
//        sensorDataFlow.Dispose();
//    }

//    /// <summary>
//    /// 📺 Simple Display Pipeline Test - CORRECTED
//    /// </summary>
//    //[Test]
//    public async Task TestSimpleDisplayPipeline()
//    {
//        Console.WriteLine("📺 Starting Simple Display Pipeline Test...\n");

//        // ✅ Simple test using Display() extension
//        var logSource = new TestDataSource<LogEntry>("TestLogs");
//        var dataFlow = new DataFlow<LogEntry>(logSource);

//        var testLogs = TestDataGenerators.GenerateLogEntries(10);

//        Console.WriteLine($"📝 Generated {testLogs.Count()} test logs\n");

//        await logSource.StartStreamingAsync(testLogs, TimeSpan.FromMilliseconds(50));

//        Console.WriteLine("🔄 Simple display pipeline started...\n");

//        // ✅ Use Display() for simple output - CORRECTED
//        await dataFlow
//            .Spy("📥 INCOMING LOGS",
//                 log => $"{log.Level}|{log.Source}|{log.Message[..Math.Min(25, log.Message.Length)]}",
//                 timeStamp: true, separator: "\n   ")
//            .Cases(
//                log => log.Level == "ERROR",
//                log => log.Level == "WARN",
//                log => log.Level == "INFO"
//            )
//            .SelectCase(
//                error => $"🚨 {error.Message}",
//                warn => $"⚠️ {warn.Message}",
//                info => $"ℹ️ {info.Message}"
//            )
//            .Take(10)
//            .AllCases()
//            .Display("📋 PROCESSED LOGS", separator: "\n🔸 ");

//        Console.WriteLine("\n✅ Simple Display Pipeline completed!\n");

//        // Clean up
//        logSource.Stop();
//        dataFlow.Dispose();
//    }

//    /// <summary>
//    /// 🧪 Advanced Multi-Pipeline Test - NEW
//    /// </summary>
//    //[Test]
//    public async Task TestAdvancedMultiPipeline()
//    {
//        Console.WriteLine("🧪 Starting Advanced Multi-Pipeline Test...\n");

//        // ✅ Create multiple data sources
//        var errorSource = new TestDataSource<LogEntry>("ErrorLogs");
//        var metricSource = new TestDataSource<MetricEntry>("SystemMetrics");
//        var eventSource = new TestDataSource<OrderEvent>("BusinessEvents");

//        var errorFlow = new DataFlow<LogEntry>(errorSource);
//        var metricFlow = new DataFlow<MetricEntry>(metricSource);
//        var eventFlow = new DataFlow<OrderEvent>(eventSource);

//        // Generate diverse test data
//        var errors = TestDataGenerators.GenerateLogEntries(15);
//        var metrics = TestDataGenerators.GenerateMetrics(20);
//        var events = TestDataGenerators.GenerateOrderEvents(25);

//        Console.WriteLine("🎯 Generated diverse test data:");
//        Console.WriteLine($"   • Error logs: {errors.Count()}");
//        Console.WriteLine($"   • System metrics: {metrics.Count()}");
//        Console.WriteLine($"   • Business events: {events.Count()}\n");

//        // Start all streams
//        await errorSource.StartStreamingAsync(errors, TimeSpan.FromMilliseconds(80));
//        await metricSource.StartStreamingAsync(metrics, TimeSpan.FromMilliseconds(120));
//        await eventSource.StartStreamingAsync(events, TimeSpan.FromMilliseconds(90));

//        Console.WriteLine("🚀 All pipelines started in parallel...\n");

//        // ✅ Process all three streams in parallel - CORRECTED
//        var errorTask = errorFlow
//            .Spy("🚨 ERROR STREAM", log => $"ERROR: {log.Message[..Math.Min(40, log.Message.Length)]}")
//            .Cases(log => log.Level == "FATAL", log => log.Level == "ERROR")
//            .SelectCase(
//                fatal => $"💀 FATAL: {fatal.Message}",
//                error => $"🔥 ERROR: {error.Message}"
//            )
//            .AllCases()
//            .Take(8)
//            .Display("🚨 CRITICAL ISSUES", separator: "\n❗ ");

//        var metricTask = metricFlow
//            .Spy("📊 METRIC STREAM", metric => $"METRIC: {metric.Name}={metric.Value:F1}")
//            .Cases(
//                metric => metric.Value > 90,
//                metric => metric.Value > 75,
//                metric => metric.Value < 10
//            )
//            .SelectCase(
//                critical => $"🔴 CRITICAL: {critical.Name}={critical.Value:F1}%",
//                warning => $"🟡 WARNING: {warning.Name}={warning.Value:F1}%",
//                low => $"🟢 LOW: {low.Name}={low.Value:F1}%"
//            )
//            .AllCases()
//            .Take(12)
//            .Display("📊 SYSTEM STATUS", separator: "\n📈 ");

//        var eventTask = eventFlow
//            .Spy("💼 EVENT STREAM", evt => $"EVENT: {evt.EventType} - ${evt.Amount:F0}")
//            .Cases(
//                evt => evt.Amount > 1000,
//                evt => evt.EventType == "cancelled",
//                evt => evt.Status == "failed"
//            )
//            .SelectCase(
//                highValue => $"💎 BIG ORDER: {highValue.OrderId} - ${highValue.Amount:F2}",
//                cancelled => $"❌ CANCELLED: {cancelled.OrderId}",
//                failed => $"⚠️ FAILED: {failed.OrderId}"
//            )
//            .AllCases()
//            .Take(15)
//            .Display("💼 BUSINESS ALERTS", separator: "\n💰 ");

//        // ✅ Wait for all three pipelines to complete
//        await Task.WhenAll(errorTask, metricTask, eventTask);

//        Console.WriteLine("\n🎉 All Advanced Multi-Pipeline Processing completed!\n");

//        // Clean up all resources
//        errorSource.Stop();
//        metricSource.Stop();
//        eventSource.Stop();
//        errorFlow.Dispose();
//        metricFlow.Dispose();
//        eventFlow.Dispose();
//    }
//}
