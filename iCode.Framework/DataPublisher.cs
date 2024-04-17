using iCode.Extensions;
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
            Writers.AsEnumerable().Where(subscribed => subscribed.Value != null && subscribed.Value(newData)).ForEach(async subscribed =>
            {
                await subscribed.Key.WaitToWriteAsync();
                await subscribed.Key.WriteAsync(newData);
            }).Do();
        }

        public void Dispose()
        {
            Writers.ForEach(x => x.Key.Complete()).Do();
            Writers.Clear();
        }
    }
}


