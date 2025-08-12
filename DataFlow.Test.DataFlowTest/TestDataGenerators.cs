namespace DataFlow.Test;
public record LogEntry(DateTime Timestamp, string Level, string Source, string Message, string? Exception = null);
public record MetricEntry(DateTime Timestamp, string Name, double Value, Dictionary<string, string> Tags);
public record OrderEvent(DateTime Timestamp, string OrderId, string EventType, decimal Amount, string Status);
public record SensorReading(DateTime Timestamp, string SensorId, string Type, double Value, string Unit);

public static class TestDataGenerators
{
    private static readonly Random _random = new();
        // Log data generator
    public static IEnumerable<LogEntry> GenerateLogEntries(int count = 100)
    {
        count = count * 10000;
        var levels = new[] { "DEBUG", "INFO", "WARN", "ERROR", "FATAL" };
        var sources = new[] { "WebServer", "Database", "Cache", "Queue", "Auth" };
        var messages = new[]
        {
            "Request processed successfully",
            "Database query executed",
            "Cache miss occurred",
            "Authentication failed",
            "Connection timeout",
            "Memory usage high",
            "Disk space low",
            "Service unavailable",
            "Rate limit exceeded",
            "Invalid request format"
        };

        var exceptions = new[]
        {
            null, null, null, null, null, // Most entries have no exception
            "System.ArgumentNullException: Value cannot be null",
            "System.TimeoutException: Operation timed out",
            "System.UnauthorizedAccessException: Access denied"
        };

        for (int i = 0; i < count; i++)
        {
            var level = levels[_random.Next(levels.Length)];
            var source = sources[_random.Next(sources.Length)];
            var message = messages[_random.Next(messages.Length)];
            var exception = level == "ERROR" || level == "FATAL"
                ? exceptions[_random.Next(5, exceptions.Length)]
                : null;

            yield return new LogEntry(
                DateTime.Now.AddMilliseconds(-_random.Next(0, 60000)),
                level,
                source,
                $"[{source}] {message}",
                exception
            );
        }
    }

    // Metrics data generator
    public static IEnumerable<MetricEntry> GenerateMetrics(int count = 100)
    {
        count = count * 10000;
        var metricNames = new[] { "cpu_usage", "memory_usage", "disk_io", "network_latency", "request_count" };
        var services = new[] { "web-server", "database", "cache", "queue" };
        var environments = new[] { "prod", "staging", "dev" };

        for (int i = 0; i < count; i++)
        {
            var name = metricNames[_random.Next(metricNames.Length)];
            var value = name switch
            {
                "cpu_usage" => _random.NextDouble() * 100,
                "memory_usage" => _random.NextDouble() * 100,
                "disk_io" => _random.NextDouble() * 1000,
                "network_latency" => _random.NextDouble() * 500,
                "request_count" => _random.Next(1, 1000),
                _ => _random.NextDouble() * 100
            };

            var tags = new Dictionary<string, string>
            {
                ["service"] = services[_random.Next(services.Length)],
                ["environment"] = environments[_random.Next(environments.Length)],
                ["host"] = $"host-{_random.Next(1, 10)}"
            };

            yield return new MetricEntry(
                DateTime.Now.AddMilliseconds(-_random.Next(0, 60000)),
                name,
                value,
                tags
            );
        }
    }

    // Order events generator
    public static IEnumerable<OrderEvent> GenerateOrderEvents(int count = 100)
    {
        count = count * 10000;
        var eventTypes = new[] { "created", "paid", "shipped", "delivered", "cancelled", "refunded" };
        var statuses = new[] { "pending", "processing", "completed", "failed", "cancelled" };

        for (int i = 0; i < count; i++)
        {
            var orderId = $"ORD-{_random.Next(10000, 99999)}";
            var eventType = eventTypes[_random.Next(eventTypes.Length)];
            var amount = (decimal)(_random.NextDouble() * 1000);
            var status = statuses[_random.Next(statuses.Length)];

            yield return new OrderEvent(
                DateTime.Now.AddMilliseconds(-_random.Next(0, 60000)),
                orderId,
                eventType,
                amount,
                status
            );
        }
    }

    // Sensor readings generator
    public static IEnumerable<SensorReading> GenerateSensorReadings(int count = 100)
    {
        count = count * 10000;
        var sensorTypes = new[] { "temperature", "humidity", "pressure", "vibration", "light" };
        var units = new Dictionary<string, string>
        {
            ["temperature"] = "°C",
            ["humidity"] = "%",
            ["pressure"] = "hPa",
            ["vibration"] = "Hz",
            ["light"] = "lux"
        };

        for (int i = 0; i < count; i++)
        {
            var type = sensorTypes[_random.Next(sensorTypes.Length)];
            var sensorId = $"{type}-sensor-{_random.Next(1, 20)}";
            var value = type switch
            {
                "temperature" => _random.NextDouble() * 50 - 10, // -10 to 40°C
                "humidity" => _random.NextDouble() * 100,        // 0 to 100%
                "pressure" => _random.NextDouble() * 200 + 900,  // 900 to 1100 hPa
                "vibration" => _random.NextDouble() * 1000,      // 0 to 1000 Hz
                "light" => _random.NextDouble() * 10000,         // 0 to 10000 lux
                _ => _random.NextDouble() * 100
            };

            yield return new SensorReading(
                DateTime.Now.AddMilliseconds(-_random.Next(0, 60000)),
                sensorId,
                type,
                value,
                units[type]
            );
        }
    }
}