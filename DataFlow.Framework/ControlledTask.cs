
namespace DataFlow.Framework;

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
                AwaiterTask.Dispose(); //DON'T dispose running tasks - let them complete naturally
            }
        }
        autoResetEvent.Reset();
        AwaiterTask = Task.Run(() => autoResetEvent?.WaitOne() ?? false);
    }
    public void CompleteTask()
    {
        autoResetEvent?.Set();
    }

    public void Dispose()
    {
        CompleteTask();
        if (AwaiterTask != null && !AwaiterTask.IsCompleted)
        {
            AwaiterTask.Wait(TimeSpan.FromMilliseconds(200));
        }
        autoResetEvent?.Dispose();
        autoResetEvent = null;
    }
}


