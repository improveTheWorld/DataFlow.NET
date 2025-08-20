using DataFlow.Log;
using System.Threading.Channels;
using DataFlow.Extensions;

namespace DataFlow.Framework;

public class DataFlow<T> : IAsyncEnumerable<T>, IDisposable
{
    private bool _disposed = false;
    readonly public Dictionary<IDataSource<T>, Channel<T>> Subscriptions = new Dictionary<IDataSource<T>, Channel<T>>();

    public DataFlowEnumerator<T> Enumerator;

    public DataFlow(IDataSource<T> dataSource, Func<T, bool>? condition = null, ChannelOptions? options = null)
    {
        ListenTo(dataSource, condition, options);
    }

    public DataFlow(Func<T, bool>? condition = null, ChannelOptions? options = null, params  IDataSource<T>[] dataSource)
    {
        dataSource.ForEach(source => ListenTo(source, condition, options)).Do();
    }

    public DataFlow(DataFlow<T> Source, Func<T, bool>? condition = null, ChannelOptions? options = null)
    {
        Source.Subscriptions.ForEach(subscription => ListenTo(subscription.Key, condition, options)).Do(); 
    }

    Channel<T> CreateChannel(ChannelOptions? options = null)
    {
        Channel<T> dataChannel;
        if (options == null)
        {
            dataChannel = Channel.CreateUnbounded<T>();
        }
        else
        {
            if (options is UnboundedChannelOptions)
            {
                dataChannel = Channel.CreateUnbounded<T>((UnboundedChannelOptions)options);
            }
            else
            {
                dataChannel = Channel.CreateBounded<T>((BoundedChannelOptions)options);
            }
        }
        return dataChannel;
    }

    public DataFlow<T> ListenTo(IDataSource<T> dataSource, Func<T, bool>? condition = null, ChannelOptions? options = null)
    {
        if (dataSource == null)
        {
            throw new ArgumentNullException("dataPublisher");
        }

        var dataChannel = CreateChannel(options);

        dataSource.AddWriter(dataChannel.Writer, condition);
        Subscriptions[dataSource] = dataChannel;

        return this;
    }

    void Unlisten(IDataSource<T> dataSource, Channel<T> dataChannel)
    {
        if (Enumerator != null && dataChannel?.Reader !=null)
        {
            Enumerator.Unlisten(dataChannel.Reader);
        }

        if(dataChannel?.Writer != null)
        {
            dataSource.RemoveWriter(dataChannel.Writer);
        }
        if (dataSource != null)
        {
            Subscriptions.Remove(dataSource);
        }
        
            
    }

    public DataFlow<T> Unlisten(IDataSource<T> dataSource)
    {
        Channel<T> dataChannel;

        if (Subscriptions.TryGetValue(dataSource, out dataChannel))
        {
            Unlisten(dataSource, dataChannel); 
        }
        
        return this;
    }

    public DataFlow<T> Unlisten(ChannelReader<T> reader)
    {
        KeyValuePair<IDataSource<T>,Channel<T>>? subscription = Subscriptions.FirstOrDefault((x)=>x.Value.Reader == reader);
        
        if (subscription != null)
        {
            Unlisten(subscription.Value.Key, subscription.Value.Value);
        }

        return this;
    }

    //Implementation for the GetEnumerator method.
    IAsyncEnumerator<T> IAsyncEnumerable<T>.GetAsyncEnumerator(CancellationToken token)
    {
        token.Register(Dispose);
        return GetAsyncEnumerator();
    }

    public IAsyncEnumerator<T> GetAsyncEnumerator()
    {
        Enumerator = new DataFlowEnumerator<T>(this,Subscriptions.Values.Select(x => x.Reader));

        if(this.IsWatched())
        {
            Enumerator.WatchByLogger();
        }
        return Enumerator;
    }

    public void Dispose()
    {
        DisposeAsync().AsTask().Wait(); // For synchronous dispose, but prefer async
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        // Clean up all subscriptions unconditionally
        foreach (var (dataSource, dataChannel) in Subscriptions.ToList()) // ToList to avoid modification during enumeration
        {
            try
            {
                if (Enumerator != null)
                {
                    Enumerator.Unlisten(dataChannel.Reader);
                }
                dataSource.RemoveWriter(dataChannel.Writer);
                // Complete the channel to release resources
                dataChannel.Writer.TryComplete();
            }
            catch (Exception ex)
            {
                // Log or handle cleanup errors to prevent silent failures
                Console.WriteLine($"Warning: Error cleaning up subscription for {dataSource.Name}: {ex.Message}");
            }
            finally
            {
                Subscriptions.Remove(dataSource);
            }
        }

        // Dispose enumerator if it exists
        if (Enumerator is IAsyncDisposable disposable)
        {
            await disposable.DisposeAsync().ConfigureAwait(false);
        }
        else if (Enumerator is IDisposable syncDisposable)
        {
            syncDisposable.Dispose();
        }

        // Clear references
        Enumerator = null;
        Subscriptions.Clear();

        GC.SuppressFinalize(this);
    }

}


