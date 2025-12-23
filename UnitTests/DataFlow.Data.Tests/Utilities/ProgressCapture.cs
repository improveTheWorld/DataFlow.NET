using System.Collections.Concurrent;

namespace DataFlow.Data.Tests.Utilities;

// Generic progress capture helper
public sealed class ProgressCapture<T> : IProgress<T>
{
    private readonly ConcurrentQueue<T> _events = new();
    public IReadOnlyCollection<T> Events => _events.ToList().AsReadOnly();
    public void Report(T value) => _events.Enqueue(value);
}
