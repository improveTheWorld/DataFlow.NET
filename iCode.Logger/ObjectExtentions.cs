using System.Drawing;

namespace iCode.Log
{
    public static class ObjectExtentions
    {

        public static void WriteLine(this object requester, object toLog, LogLevel level)
        {
            iLogger.Log(toLog, level, requester);
        }

        public static void Info(this object requester, object toLog)
        {
            iLogger.Log(toLog, LogLevel.Info, requester);
        }
        public static void Debug(this object requester, object toLog)
        {
            iLogger.Log(toLog, LogLevel.Debug, requester);
        }
        public static void Trace(this object requester, object toLog)
        {
            iLogger.Log(toLog, LogLevel.Trace, requester);
        }

        public static void Warn(this object requester, object toLog)
        {
            iLogger.Log(toLog, LogLevel.Warn, requester);
        }

        public static void Error(this object requester, object toLog, Exception e)
        {
            iLogger.Log(toLog, LogLevel.Error, requester.ToString() + " Error : " +  e.Message);
        }
        public static void Error(this object requester, object toLog)
        {
            iLogger.Log(toLog, LogLevel.Error, requester);
        }
       
        public static object WatchByLogger(this object requester,string?  InstanceName = null)
        {
            iLogger.Filters.WatchedInstances.Watch(requester);

            nameForLog(requester, ((InstanceName != null) ? InstanceName : nameof(requester)));

            return requester;
        }

        public static bool isWatched(this object requester)
        {
            return iLogger.Filters.WatchedInstances.isWatched(requester);
        }

        public static void nameForLog(this object requester, string name)
        {
            iLogger.GiveName(requester, name);
        }

    }
}
