using DataFlow.Data;
using System.Collections.Concurrent;

namespace DataFlow.Data.Tests.Utilities;

public sealed class InMemoryErrorSink : IReaderErrorSink
{
    public ConcurrentBag<ReaderError> Errors { get; } = new();
    public void Report(ReaderError error) => Errors.Add(error);
    public void Dispose() { }
}