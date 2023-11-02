using iCode.Log;
using System.Threading.Channels;


namespace iCode.Framework
{
    public class DataPublisher<T> : IDataSource<T>, IDisposable
    {
        Dictionary<ChannelWriter<T>, Func<T, bool>?> Writers;

        public int Count()
        {
            if (Writers == null)
                return 0;
            else
                return Writers.Count;
        }
        public DataPublisher()
        {
            Writers = new Dictionary<ChannelWriter<T>, Func<T, bool>?>();
        }

        public void AddWriter(ChannelWriter<T> channelWriter, Func<T, bool>? condition = null)
        {
            Writers[channelWriter] = condition;
        }

        public void RemoveWriter(ChannelWriter<T> channelWriter)
        {
            Writers.Remove(channelWriter);
        }

        public async Task PublishDataAsync(T newData)
        {
            this.Trace($"DataPublisher new data : {newData}");
            foreach (var subscribed in Writers)
            {
                if (subscribed.Value == null || subscribed.Value(newData))
                {
                    this.Trace($"DataPublisher start publishing to a new channel");
                    await subscribed.Key.WaitToWriteAsync();
                    await subscribed.Key.WriteAsync(newData);
                    this.Trace($"DataPublisher end publishing for the channel");
                }

            }

        }

        public void Dispose()
        {
            foreach (var subscribed in Writers)
            {
                subscribed.Key.Complete();
            }

            Writers.Clear();

        }
    }
}


