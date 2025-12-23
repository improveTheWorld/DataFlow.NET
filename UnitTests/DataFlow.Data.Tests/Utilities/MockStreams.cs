using System.Text;

namespace DataFlow.Data.Tests.Utilities;

/// <summary>
/// Mock stream implementations for testing buffer boundaries and error conditions.
/// </summary>
public static class MockStreams
{
    /// <summary>
    /// Creates a stream that returns data in fixed-size chunks.
    /// Useful for testing buffer boundary handling in parsers.
    /// </summary>
    /// <param name="data">The data to return.</param>
    /// <param name="chunkSize">Maximum bytes returned per Read() call.</param>
    /// <returns>A stream that returns data in chunks.</returns>
    public static Stream Chunked(string data, int chunkSize)
    {
        return new ChunkedStream(Encoding.UTF8.GetBytes(data), chunkSize);
    }

    /// <summary>
    /// Creates a stream that throws an exception at a specific byte position.
    /// Useful for testing error recovery in streaming parsers.
    /// </summary>
    /// <param name="data">The data to return before the error.</param>
    /// <param name="failAtByte">The byte position at which to throw.</param>
    /// <param name="exception">The exception to throw.</param>
    /// <returns>A stream that fails at the specified position.</returns>
    public static Stream FailsAt(string data, int failAtByte, Exception exception)
    {
        return new FailingStream(Encoding.UTF8.GetBytes(data), failAtByte, exception);
    }

    /// <summary>
    /// Creates a stream that respects cancellation tokens during Read operations.
    /// </summary>
    /// <param name="data">The data to return.</param>
    /// <param name="delayPerChunkMs">Optional delay per read operation (for testing timeouts).</param>
    /// <returns>A cancellation-aware stream.</returns>
    public static Stream Cancellable(string data, int delayPerChunkMs = 0)
    {
        return new CancellableStream(Encoding.UTF8.GetBytes(data), delayPerChunkMs);
    }

    #region Stream Implementations

    private sealed class ChunkedStream : MemoryStream
    {
        private readonly int _maxChunkSize;

        public ChunkedStream(byte[] data, int maxChunkSize) : base(data)
        {
            _maxChunkSize = maxChunkSize > 0 ? maxChunkSize : 1;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            // Limit the read to the chunk size
            var toRead = Math.Min(count, _maxChunkSize);
            return base.Read(buffer, offset, toRead);
        }

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            var toRead = Math.Min(count, _maxChunkSize);
            return await base.ReadAsync(buffer, offset, toRead, cancellationToken);
        }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            var toRead = Math.Min(buffer.Length, _maxChunkSize);
            return await base.ReadAsync(buffer.Slice(0, toRead), cancellationToken);
        }
    }

    private sealed class FailingStream : MemoryStream
    {
        private readonly int _failAtByte;
        private readonly Exception _exception;
        private long _bytesRead;

        public FailingStream(byte[] data, int failAtByte, Exception exception) : base(data)
        {
            _failAtByte = failAtByte;
            _exception = exception;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (_bytesRead >= _failAtByte)
                throw _exception;

            var remaining = (int)Math.Min(count, _failAtByte - _bytesRead);
            if (remaining <= 0)
                throw _exception;

            var read = base.Read(buffer, offset, remaining);
            _bytesRead += read;
            return read;
        }

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            if (_bytesRead >= _failAtByte)
                throw _exception;

            var remaining = (int)Math.Min(count, _failAtByte - _bytesRead);
            if (remaining <= 0)
                throw _exception;

            var read = await base.ReadAsync(buffer, offset, remaining, cancellationToken);
            _bytesRead += read;
            return read;
        }
    }

    private sealed class CancellableStream : MemoryStream
    {
        private readonly int _delayMs;

        public CancellableStream(byte[] data, int delayMs) : base(data)
        {
            _delayMs = delayMs;
        }

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            if (_delayMs > 0)
                await Task.Delay(_delayMs, cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();
            return await base.ReadAsync(buffer, offset, count, cancellationToken);
        }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (_delayMs > 0)
                await Task.Delay(_delayMs, cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();
            return await base.ReadAsync(buffer, cancellationToken);
        }
    }

    #endregion
}
