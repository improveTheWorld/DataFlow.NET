using DataFlow.Framework;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading.Channels;


namespace DataFlow.Framework
{
    public interface IDataSource<T>
    {
        void AddWriter(ChannelWriter<T> channelWriter, Func<T, bool>? condition);
        void RemoveWriter(ChannelWriter<T> channelWriter);
        Task PublishDataAsync(T newData);

    }
}
