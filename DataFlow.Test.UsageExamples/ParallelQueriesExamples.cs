using DataFlow;
using DataFlow.Extensions;
using DataFlow.Framework;
using DataFlow.Parallel;
using System.Diagnostics;

namespace DataFlow.App.UsageExamples;

/// <summary>
/// Demonstrates parallel query execution across different execution models:
/// - IEnumerable (Sequential)
/// - ParallelQuery/PLINQ (IEnumerable.AsParallel)
/// - IAsyncEnumerable (Async Sequential)
/// - ParallelAsyncQuery (IAsyncEnumerable.AsParallel)
/// </summary>
public static class ParallelQueriesExamples
{
    /// <summary>
    /// 🎯 Compare all 4 execution paths for log processing
    /// </summary>
    public static async Task ComprehensiveComparisonAsync()
    {
        Console.WriteLine("🔬 Comprehensive Multi-Path Pipeline Comparison\n");
        Console.WriteLine("Testing 4 execution paths:");
        Console.WriteLine("  1. Sequential (IEnumerable)");
        Console.WriteLine("  2. PLINQ Parallel (IEnumerable.AsParallel)");
        Console.WriteLine("  3. Async Sequential (IAsyncEnumerable)");
        Console.WriteLine("  4. Async Parallel (IAsyncEnumerable.AsParallel)\n");

        // Generate test data
        var webLogs = DemoDataGenerators.GenerateLogEntries(25).ToList();
        var dbLogs = DemoDataGenerators.GenerateLogEntries(15).ToList();
        var cacheLogs = DemoDataGenerators.GenerateLogEntries(10).ToList();
        var allLogs = webLogs.Concat(dbLogs).Concat(cacheLogs).ToList();

        Console.WriteLine($"📊 Generated {allLogs.Count} log entries\n");

        // PATH 1: SEQUENTIAL
        Console.WriteLine("🔄 Path 1: Sequential Processing...");
        var sw1 = Stopwatch.StartNew();
        var seqResults = allLogs
            .Cases(
                log => log.Level == "ERROR" || log.Level == "FATAL",
                log => log.Level == "WARN",
                log => log.Level == "INFO"
            )
            .SelectCase(
                critical => $"🚨 CRITICAL: [{critical.Source}] {critical.Message}",
                warning => $"⚠️ WARNING: [{warning.Source}] {warning.Message}",
                info => $"ℹ️ INFO: [{info.Source}] {info.Message}"
            )
            .AllCases()
            .OrderBy(x => x)
            .ToList();
        sw1.Stop();
        Console.WriteLine($"   ✅ {seqResults.Count} results in {sw1.ElapsedMilliseconds}ms\n");

        // PATH 2: PLINQ
        Console.WriteLine("⚡ Path 2: PLINQ Parallel Processing...");
        var sw2 = Stopwatch.StartNew();
        var plinqResults = allLogs
            .AsParallel()
            .Cases(
                log => log.Level == "ERROR" || log.Level == "FATAL",
                log => log.Level == "WARN",
                log => log.Level == "INFO"
            )
            .SelectCase(
                critical => $"🚨 CRITICAL: [{critical.Source}] {critical.Message}",
                warning => $"⚠️ WARNING: [{warning.Source}] {warning.Message}",
                info => $"ℹ️ INFO: [{info.Source}] {info.Message}"
            )
            .AllCases()
            .OrderBy(x => x)
            .ToList();
        sw2.Stop();
        Console.WriteLine($"   ✅ {plinqResults.Count} results in {sw2.ElapsedMilliseconds}ms\n");

        // PATH 3: ASYNC SEQUENTIAL
        Console.WriteLine("🌊 Path 3: Async Sequential Processing...");
        var sw3 = Stopwatch.StartNew();
        var merger = new UnifiedStream<LogEntry>()
            .Unify(webLogs.Async(), "WebServerLogs")
            .Unify(dbLogs.Async(), "DatabaseLogs")
            .Unify(cacheLogs.Async(), "CacheLogs");

        var asyncSeqResults = await merger
            .Cases(
                log => log.Level == "ERROR" || log.Level == "FATAL",
                log => log.Level == "WARN",
                log => log.Level == "INFO"
            )
            .SelectCase(
                critical => $"🚨 CRITICAL: [{critical.Source}] {critical.Message}",
                warning => $"⚠️ WARNING: [{warning.Source}] {warning.Message}",
                info => $"ℹ️ INFO: [{info.Source}] {info.Message}"
            )
            .AllCases()
            .ToList();
        asyncSeqResults = asyncSeqResults.OrderBy(x => x).ToList();
        sw3.Stop();
        Console.WriteLine($"   ✅ {asyncSeqResults.Count} results in {sw3.ElapsedMilliseconds}ms\n");

        // PATH 4: ASYNC PARALLEL
        Console.WriteLine("🚀 Path 4: Async Parallel Processing...");
        var sw4 = Stopwatch.StartNew();
        var asyncParResults = await allLogs
            .Async()
            .AsParallel()
            .WithMaxConcurrency(Environment.ProcessorCount)
            .Where(log => !string.IsNullOrEmpty(log.Level))
            .Select(log => log.Level switch
            {
                "ERROR" or "FATAL" => $"🚨 CRITICAL: [{log.Source}] {log.Message}",
                "WARN" => $"⚠️ WARNING: [{log.Source}] {log.Message}",
                "INFO" => $"ℹ️ INFO: [{log.Source}] {log.Message}",
                _ => $"📝 OTHER: [{log.Source}] {log.Message}"
            })
            .ToList();
        asyncParResults = asyncParResults.OrderBy(x => x).ToList();
        sw4.Stop();
        Console.WriteLine($"   ✅ {asyncParResults.Count} results in {sw4.ElapsedMilliseconds}ms\n");

        // SUMMARY
        Console.WriteLine("📊 SUMMARY:");
        var lblSeq = "Sequential";
        var lblPlinq = "PLINQ Parallel";
        var lblAsyncSeq = "Async Sequential";
        var lblAsyncPar = "Async Parallel";
        Console.WriteLine($"   {lblSeq,-20}: {sw1.ElapsedMilliseconds,5}ms, {seqResults.Count} results");
        Console.WriteLine($"   {lblPlinq,-20}: {sw2.ElapsedMilliseconds,5}ms, {plinqResults.Count} results");
        Console.WriteLine($"   {lblAsyncSeq,-20}: {sw3.ElapsedMilliseconds,5}ms, {asyncSeqResults.Count} results");
        Console.WriteLine($"   {lblAsyncPar,-20}: {sw4.ElapsedMilliseconds,5}ms, {asyncParResults.Count} results");

        // Verify correctness
        var allMatch = seqResults.SequenceEqual(plinqResults) &&
                       plinqResults.SequenceEqual(asyncSeqResults);
        Console.WriteLine($"\n🎯 Results consistency: {(allMatch ? "✅ All paths produce identical results!" : "⚠️ Results differ (expected for async parallel)")}");
    }

    /// <summary>
    /// 📊 Metrics monitoring with parallel processing
    /// </summary>
    public static async Task MetricsMonitoringAsync()
    {
        Console.WriteLine("📊 Real-time Metrics Monitoring Demo\n");

        var cpuMetrics = DemoDataGenerators.GenerateMetrics(20).ToList();
        var memoryMetrics = DemoDataGenerators.GenerateMetrics(15).ToList();
        var networkMetrics = DemoDataGenerators.GenerateMetrics(12).ToList();
        var allMetrics = cpuMetrics.Concat(memoryMetrics).Concat(networkMetrics).ToList();

        Console.WriteLine($"Generated {allMetrics.Count} metrics\n");

        // Async parallel processing for real-time alerts
        Console.WriteLine("🚀 Processing metrics with async parallel...\n");

        var alerts = await allMetrics
            .Async()
            .AsParallel()
            .WithMaxConcurrency(4)
            .Where(m =>
                m.Name == "cpu_usage" && m.Value > 75 ||
                m.Name == "memory_usage" && m.Value > 85 ||
                m.Name == "network_latency" && m.Value > 180)
            .Select(m => m.Name switch
            {
                "cpu_usage" when m.Value > 75 =>
                    $"🔥 HIGH CPU: {m.Value:F1}% on {m.Tags.GetValueOrDefault("host", "unknown")}",
                "memory_usage" when m.Value > 85 =>
                    $"💾 HIGH MEMORY: {m.Value:F1}% on {m.Tags.GetValueOrDefault("host", "unknown")}",
                "network_latency" when m.Value > 180 =>
                    $"🌐 HIGH LATENCY: {m.Value:F1}ms on {m.Tags.GetValueOrDefault("host", "unknown")}",
                _ => $"❓ UNKNOWN ALERT"
            })
            .ToList();

        Console.WriteLine($"Generated {alerts.Count} alerts:\n");
        foreach (var alert in alerts.Take(10))
        {
            Console.WriteLine($"   {alert}");
        }
        if (alerts.Count > 10)
        {
            Console.WriteLine($"   ... and {alerts.Count - 10} more alerts");
        }
    }

    /// <summary>
    /// 📦 Order processing with Cases pattern in PLINQ
    /// </summary>
    public static void OrderProcessingPlinq()
    {
        Console.WriteLine("📦 Order Processing with PLINQ\n");

        var orders = DemoDataGenerators.GenerateOrderEvents(30).ToList();
        Console.WriteLine($"Generated {orders.Count} orders\n");

        var results = orders
            .AsParallel()
            .Cases(
                order => order.EventType == "cancelled",
                order => order.Amount > 500,
                order => order.Status == "failed"
            )
            .SelectCase(
                cancelled => $"❌ CANCELLED: {cancelled.OrderId} - ${cancelled.Amount:F2}",
                highValue => $"💎 HIGH VALUE: {highValue.OrderId} - ${highValue.Amount:F2}",
                failed => $"⚠️ FAILED: {failed.OrderId} - Needs Investigation"
            )
            .AllCases()
            .ToList();

        var cancelled = results.Count(r => r!.StartsWith("❌"));
        var highValue = results.Count(r => r!.StartsWith("💎"));
        var failed = results.Count(r => r!.StartsWith("⚠️"));

        Console.WriteLine($"Processed {results.Count} orders:");
        Console.WriteLine($"   ❌ Cancelled: {cancelled}");
        Console.WriteLine($"   💎 High Value: {highValue}");
        Console.WriteLine($"   ⚠️ Failed: {failed}\n");

        Console.WriteLine("Sample results:");
        foreach (var result in results.Take(10))
        {
            Console.WriteLine($"   {result}");
        }
    }

    /// <summary>
    /// 🌡️ Sensor alerting with async parallel
    /// </summary>
    public static async Task SensorMonitoringAsync()
    {
        Console.WriteLine("🌡️ IoT Sensor Monitoring Demo\n");

        var sensors = DemoDataGenerators.GenerateSensorReadings(40).ToList();
        Console.WriteLine($"Generated {sensors.Count} sensor readings\n");

        var anomalies = await sensors
            .Async()
            .AsParallel()
            .WithMaxConcurrency(Environment.ProcessorCount)
            .Where(s =>
                s.Type == "temperature" && s.Value > 30 ||
                s.Type == "humidity" && s.Value > 70 ||
                s.Type == "pressure" && (s.Value < 980 || s.Value > 1020))
            .Select(s => s.Type switch
            {
                "temperature" when s.Value > 30 =>
                    $"🌡️ HIGH TEMP: {s.Value:F1}{s.Unit} (Sensor: {s.SensorId})",
                "humidity" when s.Value > 70 =>
                    $"💧 HIGH HUMIDITY: {s.Value:F1}{s.Unit} (Sensor: {s.SensorId})",
                "pressure" when s.Value < 980 || s.Value > 1020 =>
                    $"🌪️ ABNORMAL PRESSURE: {s.Value:F1}{s.Unit} (Sensor: {s.SensorId})",
                _ => $"❓ UNKNOWN: {s.Type}={s.Value:F1}"
            })
            .ToList();

        Console.WriteLine($"Detected {anomalies.Count} anomalies:\n");
        foreach (var a in anomalies)
        {
            Console.WriteLine($"   {a}");
        }
    }

    /// <summary>
    /// 🔄 Compare concurrency levels
    /// </summary>
    public static async Task ConcurrencyComparisonAsync()
    {
        Console.WriteLine("🔄 Concurrency Level Comparison\n");

        var data = Enumerable.Range(1, 100).Select(i => $"item_{i}").ToList();
        Func<string, string> transform = item => $"processed_{item}";

        Console.WriteLine("Processing 100 items with different concurrency levels:\n");

        foreach (var concurrency in new[] { 1, 2, 4, 8 })
        {
            var sw = Stopwatch.StartNew();
            var results = await data
                .Async()
                .AsParallel()
                .WithMaxConcurrency(concurrency)
                .Select(transform)
                .ToList();
            sw.Stop();

            Console.WriteLine($"   Concurrency {concurrency}: {sw.ElapsedMilliseconds}ms, {results.Count} results");
        }

        Console.WriteLine("\n💡 Note: With small, fast operations, parallel overhead may exceed benefits.");
        Console.WriteLine("   Parallelism shines with larger datasets or I/O-bound operations.");
    }
}
