using System.Threading.Channels;


namespace iCode.Framework
{
    public interface IDataSource<T>
    {
        void AddWriter(ChannelWriter<T> channelWriter, Func<T, bool>? condition);
        void RemoveWriter(ChannelWriter<T> channelWriter);
        Task PublishDataAsync(T newData);

    }
}


