using iCode.Log;

namespace iCode.Framework
{
    public class PeriodicCall
    {
        readonly System.Timers.Timer Alarm;
        readonly Action CallToDo;
        readonly TaskCompletionSource<bool> Completed;
        int Counter;
        public Task Task
        {
            get
            {
                return Completed.Task;
            }
        }


        void OnPullingTimer(Object source, System.Timers.ElapsedEventArgs e)
        {
            this.Trace($"PeriodicCall Tic, Remaining {Counter} call.s");
            CallToDo();

            if (Counter > 0)
            {
                Counter--;
            }

            if (Counter == 0)
            {
                Stop(true);
            }
        }

        public void Stop(bool ComletetionResult = false)
        {
            if(!Completed.Task.IsCompleted)
            {
                Completed.SetResult(ComletetionResult);
                Alarm.Dispose();
                Alarm.Close();
            }           
        }
        
        

        public PeriodicCall(Action callToDo, int pollingPeriod, int dueTime , int nbrOfCall )
        {
            if (nbrOfCall == 0)
            {
                Counter = -1;
            }
            else
            {
                Counter = nbrOfCall;
            }

            CallToDo = callToDo;
            Alarm = new System.Timers.Timer( pollingPeriod);
            Alarm.AutoReset = true;
            Alarm.Elapsed += OnPullingTimer;
            Alarm.Enabled = true;   
            Completed = new TaskCompletionSource<bool>();
        }
        public PeriodicCall(Action callToDo, int pollingPeriod) : this(callToDo, pollingPeriod, pollingPeriod, 0) {}
        public PeriodicCall(Action callToDo, int pollingPeriod, int nbrOfCall) : this(callToDo, pollingPeriod, pollingPeriod, nbrOfCall) {}
    }
}


