using System.Threading.Channels;
namespace DataFlow.Framework.DataFlowTest;
public record LogEntry(DateTime Timestamp, string Level, string Source, string Message, string? Exception = null);
public record MetricEntry(DateTime Timestamp, string Name, double Value, Dictionary<string, string> Tags);
public record OrderEvent(DateTime Timestamp, string OrderId, string EventType, decimal Amount, string Status);
public record SensorReading(DateTime Timestamp, string SensorId, string Type, double Value, string Unit);

// Test data source implementation
public class TestDataSource<T> : IDataSource<T>
{
    private readonly List<ChannelWriter<T>> _writers = new();
    private readonly CancellationTokenSource _cancellation = new();

    public string Name { get; }

    public TestDataSource(string name)
    {
        Name = name;
    }

    public void AddWriter(ChannelWriter<T> writer, Func<T, bool>? condition = null)
    {
        _writers.Add(writer);
    }

    public void RemoveWriter(ChannelWriter<T> writer)
    {
        _writers.Remove(writer);
    }

    // Method to push data to all subscribers
    public async Task PublishDataAsync(T data)
    {
        foreach (var writer in _writers.ToList())
        {
            try
            {
                await writer.WriteAsync(data, _cancellation.Token);
            }
            catch (InvalidOperationException)
            {
                // Writer was closed, remove it
                _writers.Remove(writer);
            }
        }
    }

    // Method to simulate streaming data
    public async Task StartStreamingAsync(IEnumerable<T> data, TimeSpan interval)
    {
        _ = Task.Run(async () =>
        {
            foreach (var item in data)
            {
                if (_cancellation.Token.IsCancellationRequested) break;

                await PublishDataAsync(item);
                await Task.Delay(interval, _cancellation.Token);
            }

            // Close all writers when done
            foreach (var writer in _writers.ToList())
            {
                writer.Complete();
            }
        });
    }

    public void Stop()
    {
        _cancellation.Cancel();
        foreach (var writer in _writers.ToList())
        {
            writer.Complete();
        }
    }
}