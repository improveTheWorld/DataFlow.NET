using DataFlow.Extensions;
using System.Drawing;
using System.Text;

namespace DataFlow.Log
{
    public static class iLogger_ObjectExtentions
    {

        public static void WriteLine(this object requester, object toLog, LogLevel level = LogLevel.Info)
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

            NameForLog(requester, ((InstanceName != null) ? InstanceName : nameof(requester)));

            return requester;
        }

        public static bool IsWatched(this object requester)
        {
            return iLogger.Filters.WatchedInstances.IsWatched(requester);
        }

        public static void NameForLog(this object requester, string name)
        {
            iLogger.GiveName(requester, name);
        }

        //----------------------------------------------------------------------------

        //public static T LogSpy<T>(this T item, string tag)
        //{
        //    iLogger.Out(tag.IsNullOrEmpty() ? $"{item}" : $"{tag}: {item}");
        //    return item;
        //}

        //public static string LogSpy(this string item, string tag)
        //{
        //    iLogger.Out(tag.IsNullOrEmpty() ? $"'{item}'" : $"{tag}: '{item}'");
        //    return item;
        //}      
    }

    public static class iLogger_IEnumerableExtension
    {
        public static IEnumerable<T> LogSpy<T>(this IEnumerable<T> items, string tag, string separator = ", ", string before = "{", string after = "}")
        {
            return items.LogSpy(tag, x => x is string ? $"'{x}'" : x?.ToString()??"null", separator, before, after);           
        }

        public static IEnumerable<T> LogSpy<T>(this IEnumerable<T> items, string tag, Func<T,string> customDispay, string separator = ", ", string before = "{", string after = "}")
        {
            StringBuilder str = new StringBuilder();

            if (!tag.IsNullOrEmpty())
                str.Append(tag).Append(" :");

            str.Append(before);
            int i = 0;
            foreach (var item in items)
            {
                if (i != 0) str.Append(separator);
                str.Append(customDispay(item));
                yield return item;

                i++;
            }
            str.Append(after);
            iLogger.Out(str.ToString());
        }
    }
}
