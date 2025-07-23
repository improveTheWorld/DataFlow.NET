using System.Threading.Channels;
using DataFlow.Framework;
using DataFlow.Log;

namespace DataFlow.Framework;

public class DataFlowEnumerator<T> : IAsyncEnumerator<T>
{
    readonly List<ChannelReader<T>> Readers;
    readonly List<ChannelReader<T>> ReadersToRemove;
    readonly List<Task> ReadTaskList;
    readonly ControlledTask TokenTask;
    readonly DataFlow<T> MyEnumerable;

    int ReadersCount = 0;
    public T Current { get; private set; }

    public DataFlowEnumerator(DataFlow<T> enumerable, IEnumerable<ChannelReader<T>> readers)
    {
        MyEnumerable = enumerable;
        TokenTask = new ControlledTask();
        Readers = readers.ToList();
        ReadTaskList = new List<Task>();
        ReadersToRemove = new List<ChannelReader<T>>();
        TokenTask.StartNew();
    }

    public void Unlisten(ChannelReader<T> readers)
    {
        ReadersToRemove.Add(readers);
        TokenTask.CompleteTask();
    }
    void ApplyRemoveRequests()
    {
        Readers.RemoveAll(x => x == null);
        foreach (var reader in ReadersToRemove)
        {
            Readers.Remove(reader); // Actually remove them!
        }
        ReadersToRemove.Clear();
        ReadTaskList.Clear();
        ReadersCount = 0;
        ReadTaskList.AddRange(Readers.Select(reader => { ReadersCount++; return reader.WaitToReadAsync().AsTask(); }));
        ReadTaskList.Add(TokenTask.AwaiterTask);
    }

    public async ValueTask<bool> MoveNextAsync()
    {

        ApplyRemoveRequests();
        

        if (ReadersCount == 0)
        {
            return false;
        }
        else
        {              
            Task completedTask = await Task.WhenAny(ReadTaskList);
            int channelIndex = ReadTaskList.IndexOf(completedTask);                

            if (channelIndex == ReadersCount)
            {
                TokenTask.StartNew();
                return await MoveNextAsync();
            }
            else
            {
                try
                {
                    Current = await Readers[channelIndex].ReadAsync();
                }
                catch(ChannelClosedException)
                {
                   MyEnumerable.Unlisten(Readers[channelIndex]);                        
                    return await MoveNextAsync();
                }
            }
        }


        return true;
    }

    ValueTask IAsyncDisposable.DisposeAsync()
    {
        TokenTask.Dispose();
        return default;
    }
}


