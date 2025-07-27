using System.Threading.Channels;


namespace DataFlow.Framework;

public interface IDataSource<T>
{
    string Name { get; }
    void AddWriter(ChannelWriter<T> channelWriter, Func<T, bool>? condition);
    void RemoveWriter(ChannelWriter<T> channelWriter);
    Task PublishDataAsync(T newData);

}
