using System.Threading.Channels;
using iCode.Framework;

namespace iCode.Log
{
    // Logger class definition
    public class iLogger : IDisposable
    {
        // Log processing variable using LogFilter class
        static readonly LogFilter logProc = new ();

        // Property to get Filters from logProc object
        public static Config Filters => logProc;

        // DebugLogger flag to control debug logging, default is false
        public static bool DebugLogger { get; set; } = false;

        public static LogLevel MaxAuthorizedLogLevel= LogLevel.Fatal;

        // Flag to indicate whether buffering is enabled
        static bool bufferEnabled;

        // List of ILoggerTarget objects that logs messages
        //static readonly List<ILoggerTarget> loggerTargets = new() { new WriteLineLogger<TextWriter>(Console.Out) };
        static readonly List<ILoggerTarget> loggerTargets = new() { new ColoredConsoleWriter() };

        // A Channel that buffers log entries
        static Channel<(string message, LogLevel logLevel)>? Buffer = null;

        // Property to get or set the BufferEnabled flag
        public static bool BufferEnabled
        {
            get
            {
                // return current buffer enabled status
                return bufferEnabled;
            }

            set
            {
                // lock logger list for thread safety
                lock (loggerTargets)
                {
                    // trace execution for debug purposes
                    TraceExecution($"Setting BufferEnable to {value}");
                    bufferEnabled = value;

                    // if buffer is enabled and Buffer is not initialized, initialize it
                    if (bufferEnabled && Buffer == null)
                    {
                        Buffer = Channel.CreateUnbounded<(string message, LogLevel logLevel)>();
                    }
                }

                // if value is true, start Loop
                if (value)
                {
                    Loop();
                }
            }
        }

        // Method to associate a name with an object instance
        static public void GiveName(object instanceToName, string name) => Filters.InstancesNames[instanceToName] = name;

        // Method to trace the execution path for debugging
        static void TraceExecution(string message)
        {
            if (DebugLogger)
            {
                Console.WriteLine($"__Logger trace {DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")} : " + message);
            }
        }

        // Method to keep reading and processing messages from the Buffer
        async static void Loop()
        {
            TraceExecution($"Starting Buffer loop in thread {Thread.CurrentThread.ManagedThreadId}");

            while (true)
            {
                await Buffer.Reader.WaitToReadAsync();
                (string message, LogLevel loglevel) = await Buffer.Reader.ReadAsync();

                TraceExecution($"New message in buffer will be treated in thread {Thread.CurrentThread.ManagedThreadId}");

                foreach (ILoggerTarget log in loggerTargets)
                {
                    log.Log(message, loglevel);
                }
            }
        }

        // Log method to log the object with a specific LogLevel
        public static void Log(object objectToLog, LogLevel logLevel = LogLevel.Trace, object? requester = null)
        {
            if (logLevel > MaxAuthorizedLogLevel)
                return;

            // ensure thread safety
            lock (loggerTargets)
            {
                string? message;
                message = logProc.ComputeLog(requester, objectToLog);

                if (message != null)
                {
                    PutInQueue(message, logLevel);
                }
            }
        }

       public static void  WriteLine(string message, LogLevel logLevel = LogLevel.Trace)
        {
            // ensure thread safety
            lock (loggerTargets)
            {
                if (message != null)
                {
                    PutInQueue(message, logLevel);
                }
            }
        }


        // Specific logging methods for different log levels
        public static void Info(object toLog)
        {
            Log(toLog, LogLevel.Info);
        }
        public static void Debug(object toLog)
        {
            Log(toLog, LogLevel.Debug);
        }
        public static void Trace(object toLog)
        {
            Log(toLog, LogLevel.Trace);
        }
        public static void Warn(object toLog)
        {
            Log(toLog, LogLevel.Warn);
        }
        public static void Error(object toLog, Exception e)
        {
            Log(toLog.ToString() + " Error : " + e.Message, LogLevel.Error);
        }
        public static void Error(object toLog)
        {
            Log(toLog, LogLevel.Error);
        }

        // Method to add message to the Buffer queue or directly log it if buffer is not enabled
        async static void PutInQueue(string message, LogLevel logLevel)
        {
            if (bufferEnabled)
            {
                Buffer.Writer.WriteAsync((message, logLevel));
            }
            else
            {
                TraceExecution($"Direct logging in thread {Thread.CurrentThread.ManagedThreadId}");
                foreach (ILoggerTarget log in loggerTargets)
                {
                    log.Log(message, logLevel);
                }
            }
        }



        // Method to clear loggerTarget list after disposing the old ones
        public static void ResetLoggers(ILoggerTarget? logger = null)
        {
        
            foreach (ILoggerTarget log in loggerTargets)
            {
                if (log is IDisposable)
                {
                    ((IDisposable)log).Dispose();
                }
            }

            loggerTargets.Clear();
            if(logger != null)
            {
                loggerTargets.Add(logger);
            }
        }

        // Method to add a logger to the existing list
        public static void AddLogger(ILoggerTarget logger)
        {
            loggerTargets.Add(logger);
        }

        // Dispose method to cleanup
        void IDisposable.Dispose()
        {
            foreach (ILoggerTarget log in loggerTargets)
            {
                if (log is IDisposable)
                {
                    ((IDisposable)log).Dispose();
                }
            }
        }

        // Method to remove a logger
        public static void RemoveLogger(ILoggerTarget logger)
        {
            if (logger is IDisposable)
            {
                ((IDisposable)logger).Dispose();
            }
            loggerTargets.Remove(logger);
        }

        public static ILoggerTarget? CreateKafkaEventHubLogger(string eventHubNamespace, string connectionString, string topic = "iLogger")
        {
            return  new EventHubKafkaLogger(eventHubNamespace, connectionString, topic);
        }
        public static ILoggerTarget? AddKafkaEventHubLogger(string eventHubNamespace, string connectionString, string topic = "iLogger")
        {
            var  newLogger = CreateKafkaEventHubLogger(eventHubNamespace, connectionString, topic);

            if(newLogger != null)
                AddLogger(newLogger);

            return newLogger;
        }

        // Method to get a file logger
        public static ILoggerTarget? CreateFileLogger(String fullPath)
        {
            StreamWriter? writer = FilePath.CreatePathAndFile(fullPath);

            if (writer != null)
            {
                writer.AutoFlush = true;
                var logger = new WriteLineLogger<StreamWriter>(writer);
                return logger;
            }
            else
            {
                return null;
            }
        }

        // Method to add a file logger
        public static ILoggerTarget? AddFileLogger(String fullPath)
        {
            var newLogger = CreateFileLogger(fullPath);

            if (newLogger != null)
                AddLogger(newLogger);

            return newLogger;
        }

        public static ILoggerTarget? AddColoredConsoleWriter()
        {
            foreach(var logger in loggerTargets)
            {
                if (logger is ColoredConsoleWriter)
                    return logger;
            }
            var newlogger = new ColoredConsoleWriter();
            AddLogger(newlogger);
            return newlogger;

        }
    }
}
