namespace iCode.Log
{
    public enum LogLevel
    {
        Trace = 0,
        Debug = 1,
        Info = 2,
        Warn = 3,
        Error = 4,
        Fatal = 5
    }
    public interface ILoggerTarget
    {
        void Log(string message, LogLevel logging);
    }

}

