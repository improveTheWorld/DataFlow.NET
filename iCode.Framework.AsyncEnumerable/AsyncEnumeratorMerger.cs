using System.Threading.Channels;
using iCode.Framework;
using iCode.Log;

namespace iCode.Framework
{
    public class AsyncEnumeratorMerger<T> : IAsyncEnumerator<T>
    {
        readonly List<ChannelReader<T>> Readers;
        readonly List<ChannelReader<T>> ReadersToRemove;
        readonly List<Task> ReadTaskList;
        readonly ControlledTask TokenTask;
        readonly AsyncEnumerableMerger<T> MyEnumerable;

        int ReadersCount = 0;
        public T Current { get; private set; }

        public AsyncEnumeratorMerger(AsyncEnumerableMerger<T> enumerable, IEnumerable<ChannelReader<T>> readers)
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
            ReadersToRemove.Clear();
        }

        void UpdateReadTaskList()
        {
            ReadTaskList.Clear();
            ReadersCount = 0;
            ReadTaskList.AddRange(Readers.Select(reader => { ReadersCount++; return reader.WaitToReadAsync().AsTask(); }));
            ReadTaskList.Add(TokenTask.AwaiterTask);
        }
        public async ValueTask<bool> MoveNextAsync()
        {
            this.Info("MoveNextAsync: Begin");
            ApplyRemoveRequests();
            UpdateReadTaskList();

            if (ReadersCount == 0)
            {
                this.Info("MoveNextAsync: End");
                return false;
            }
            else
            {              
                this.Info($"MoveNextAsync: will wait for {ReadersCount} Task");
                Task completedTask = await Task.WhenAny(ReadTaskList);
                this.Info($"MoveNextAsync:one task completed");
                int channelIndex = ReadTaskList.IndexOf(completedTask);                

                if (channelIndex == ReadersCount)
                {
                    this.Info($"MoveNextAsync: Token activated. Will call MoveNextAsync Recursevely");
                    TokenTask.StartNew();
                    return await MoveNextAsync();
                }
                else
                {
                    try
                    {
                        this.Info($"MoveNextAsync: waill wait for Data available at Channel {channelIndex}");
                        Current = await Readers[channelIndex].ReadAsync();
                        this.Info($"MoveNextAsync: Data received");
                    }
                    catch(ChannelClosedException)
                    {
                        this.Info($"MoveNextAsync: Reader {channelIndex} Closed. Remove it and call MoveNextAsync Recursevely");
                        MyEnumerable.Unlisten(Readers[channelIndex]);
                        return await MoveNextAsync();
                    }       
                }
            }

            this.Info("MoveNextAsync: End");
            return true;
        }

        ValueTask IAsyncDisposable.DisposeAsync()
        {
            TokenTask.Dispose();
            return default;
        }
    }
}


