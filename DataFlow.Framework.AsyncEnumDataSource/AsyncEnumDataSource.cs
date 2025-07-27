using DataFlow.Framework;
using System.Collections.Concurrent;
using System.Threading.Channels;


namespace DataFlow.Framework
{
    /// <summary>
    /// An IDataSource<T> implementation that wraps an IAsyncEnumerable<T>.
    /// It reads from the enumerable in the background and publishes items to all subscribed writers.
    /// </summary>
    public class AsyncEnumDataSource<T> : IDataSource<T>
    {
        public string Name { get; }

        private readonly IAsyncEnumerable<T> _sourceEnumerable;
        private readonly ConcurrentDictionary<ChannelWriter<T>, Func<T, bool>?> _writers = new();
        private Task? _processingTask;
        private readonly object _startLock = new();


        /// <summary>
        /// Initializes a new instance of the AsyncEnumerableDataSource class.
        /// </summary>
        /// <param name="sourceEnumerable">The asynchronous enumerable to use as the data source.</param>
        /// <param name="name">The unique name for this data source, used for logging and identification.</param>
        public AsyncEnumDataSource(IAsyncEnumerable<T> sourceEnumerable, string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Data source name cannot be null or whitespace.", nameof(name));
            }

            Name = name;
            _sourceEnumerable = sourceEnumerable ?? throw new ArgumentNullException(nameof(sourceEnumerable));
        }

        /// <summary>
        /// The background task that reads from the source enumerable and pushes data to writers.
        /// </summary>
        private async Task ProcessEnumerableAsync()
        {
            try
            {
                // Asynchronously iterate over the source data
                await foreach (var item in _sourceEnumerable)
                {
                    // Publish the data to all subscribed writers in parallel
                    await PublishToWritersAsync(item);
                }
            }
            catch (Exception ex)
            {
                // If the source enumerable throws an error, propagate it to the writers.
                // Include the source name in the exception for better diagnostics.
                var newEx = new Exception($"An error occurred in data source '{Name}'. See inner exception for details.", ex);
                CompleteAllWriters(newEx);
            }
            finally
            {
                // When the enumerable is exhausted, signal completion to all writers.
                CompleteAllWriters();
            }
        }

        /// <summary>
        /// Adds a channel writer to the list of subscribers.
        /// </summary>
        public void AddWriter(ChannelWriter<T> channelWriter, Func<T, bool>? condition)
        {
            _writers.TryAdd(channelWriter, condition);
            lock (_startLock)
            {
                // Start the processing task only when the first writer is added.
                if (_processingTask == null)
                {
                    _processingTask = ProcessEnumerableAsync();
                }
            }
        }

        /// <summary>
        /// Removes a channel writer from the list of subscribers.
        /// </summary>
        public void RemoveWriter(ChannelWriter<T> channelWriter)
        {
            _writers.TryRemove(channelWriter, out _);
        }

        /// <summary>
        /// Allows for manually publishing data to the stream, in addition to the data from the enumerable.
        /// </summary>
        public Task PublishDataAsync(T newData)
        {
            return PublishToWritersAsync(newData);
        }

        private Task PublishToWritersAsync(T data)
        {
            var writeTasks = _writers.Select(async writerEntry =>
            {
                // Apply the condition if one exists
                if (writerEntry.Value == null || writerEntry.Value(data))
                {
                    await writerEntry.Key.WriteAsync(data);
                }
            });

            return Task.WhenAll(writeTasks);
        }

        private void CompleteAllWriters(Exception? ex = null)
        {
            foreach (var writer in _writers.Keys)
            {
                writer.TryComplete(ex);
            }
        }

        public void Dispose()
        {
            // Cleanly complete all writers on disposal.
            CompleteAllWriters();
            // We can also consider waiting for the processing task to finish,
            // but that might block the disposing thread.
            // _processingTask?.Wait();
        }
    }
}

