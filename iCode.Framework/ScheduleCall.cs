using iCode.Log;
using iCode.Extensions;


namespace iCode.Framework
{
        public sealed class ScheduleCall
    {
        static readonly string DateTimeFormat = "yyyy-MM-dd HH:mm:ss.fff";

        static ScheduleCall _Instance;
        public static ScheduleCall Instance
        {
            get
            {
                if (_Instance == null)
                {
                    iLogger.Log("Instantiating ScheduleCall", LogLevel.Debug, _Instance);
                    _Instance = new ScheduleCall();
                }
                return _Instance;
            }
            set { _Instance = value; }
        }


        readonly System.Timers.Timer Alarm;
        Queue<(double, Action)> _ScheduledCalls = new Queue<(double, Action)>();
        Action? CallToDo = null;
        TaskCompletionSource<bool> Completed;
        DateTime StartingTime;

        public Task Task
        {
            get
            {
                return Completed.Task;
            }
        }

   
        void scheduleNextCall()
        {
            DateTime Now;
            double elapsedTime;

            lock (_ScheduledCalls)
            {
                if (_ScheduledCalls.Count > 0)
                {
                    (double nextDueTime, CallToDo) = _ScheduledCalls.Peek();

                    this.Trace($"[{(Now = DateTime.Now).ToString(DateTimeFormat)}] : scheduleNextCall, elapsedTime = { elapsedTime = Now.Subtract(StartingTime).TotalMilliseconds} ms");

                    if ((nextDueTime -= elapsedTime) <= 0)
                    {
                        this.Trace($"[{(Now = DateTime.Now).ToString(DateTimeFormat)}] : scheduleNextCall , Too late for next tic, will schedule next call in 1 ms");
                        Alarm.Interval = 1;
                    }
                    else
                    {
                        Alarm.Interval = nextDueTime;
                        this.Trace($"[{(Now = DateTime.Now).ToString(DateTimeFormat)}] : scheduleNextCall {nextDueTime} ms");
                    }
                }
                else
                {
                    this.Warn($"[{DateTime.Now.ToString(DateTimeFormat)}] : scheduleNextCall called while schedule empty");
                        
                }                    
            }
        }

        void OnPullingTimer(Object source, System.Timers.ElapsedEventArgs e)
        {
            this.Trace($"[{DateTime.Now.ToString(DateTimeFormat)}] : OnPullingTimer Tic");

            DateTime Now;
            double elapsedTime;

            Action currentCall;
            lock (_ScheduledCalls)
            {
                _ScheduledCalls.Dequeue();
                if (CallToDo != null)
                {
                    currentCall = CallToDo;

                    if (_ScheduledCalls.Count > 0)
                    {
                        scheduleNextCall();
                    }
                    else
                    {
                        this.Trace($"[{DateTime.Now.ToString(DateTimeFormat)}] : ScheduleCall Tic, Will execute last Call");
                        CallToDo = null;
                        _stop();
                    }

                    currentCall();
                }
                else
                {
                    this.Warn("OnPullingTimer while CallToDo == null !!!!!");
                }
            }


            this.Trace($"[{DateTime.Now.ToString(DateTimeFormat)}] : Exit OnPullingTimer");
        }

        void _stop(bool ComletetionResult = false)
        {
            this.Trace($"[{DateTime.Now.ToString(DateTimeFormat)}] : Stopping");
            Alarm.Stop();
            Alarm.Close();
            CallToDo = null;
            Completed.SetResult(ComletetionResult);
        }

        ScheduleCall()
        {
            Alarm = new System.Timers.Timer();
            Alarm.AutoReset = false;
            Alarm.Elapsed += OnPullingTimer;
            Alarm.Enabled = false;
            //Completed = new TaskCompletionSource<bool>();
        }

        void _start()
        {
            lock (_ScheduledCalls)
            {
                if (CallToDo != null)
                {
                    this.Trace($"[S{DateTime.Now.ToString(DateTimeFormat)}] : Timer already in progress");
                }
               
                if (_ScheduledCalls.Count > 0)
                {
                    scheduleNextCall();
                    this.Trace($"[{DateTime.Now.ToString(DateTimeFormat)}] : Starting new Schedule, First call scheduled in {Alarm.Interval} ");
                    Completed = new TaskCompletionSource<bool>();
                    Alarm.Start();
                }
                else
                {
                    this.Warn($"[{DateTime.Now.ToString(DateTimeFormat)}] : Start request while no call planned!!!");
                }
            }
        }

        void _addToSchedule(Action callToDo, bool start, params int[] toSchedule)
        {
            (double, Action)[] planning = new (double, Action)[toSchedule.Length];
            double timeToAdd = 0;
            lock (_ScheduledCalls)
            {

                ///////////////////// Compute the new _ScheduledCalls 
                if (CallToDo != null)
                {
                    //Update requests relative to first StartingTime
                    timeToAdd = DateTime.Now.Subtract(StartingTime).TotalMilliseconds;

                }
                else
                {
                    StartingTime = DateTime.Now;
                }

                toSchedule.OrderBy(x => x).ForEach((x,idx)=> planning[idx] = ((x + timeToAdd == 0) ? 1 : x + timeToAdd, callToDo)) ;
                
                _ScheduledCalls = new Queue<(double, Action)>(_ScheduledCalls.AsEnumerable().CombineOrdered(planning, (x, y) => x.Item1 <= y.Item1));
                string serialized = _ScheduledCalls.Select(x => x.Item1.ToString()).Cumul((a, b) => a + ", " + b) ?? string.Empty;
                this.Trace("New Schedule : " + serialized );


                /////////////////////////////////////////////////////// start Alarm
                ///
                if (start)
                {
                    _start();
                }
            }
        }

        public static ScheduleCall Schedule(Action callToDo, params int[] schedule)
        {
            return Schedule(callToDo, true, schedule);
        }

        public static ScheduleCall Schedule(Action callToDo, bool start, params int[] schedule)
        {

            if (schedule == null)
            {
                throw new ArgumentNullException(nameof(schedule));
            }

            Instance._addToSchedule(callToDo, start, schedule);

            return Instance;
        }
        public static void Start()
        {
            Instance._start();
        }
    }
}


