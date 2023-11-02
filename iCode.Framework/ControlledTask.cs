using iCode.Log;


namespace iCode.Framework
{
    public class ControlledTask : IDisposable
    {
        public Task? AwaiterTask { get; private set; } = null;
        AutoResetEvent autoResetEvent;
        public ControlledTask()
        {
            autoResetEvent = new AutoResetEvent(false);
        }

        public void StartNew()
        {
            if (AwaiterTask != null)
            {
                if (!AwaiterTask.IsCompleted)
                {
                    CompleteTask();
                }
                else
                {
                    AwaiterTask.Dispose();
                }
            }
            autoResetEvent.Reset();
            AwaiterTask = Task.Run(autoResetEvent.WaitOne);
        }
        public void CompleteTask()
        {
            this.Trace("CompleteTask :Complete Awaiting operation Asked");
            autoResetEvent.Set();
        }

        public void Dispose()
        {
            CompleteTask();
            autoResetEvent.Dispose();
        }
    }
}


