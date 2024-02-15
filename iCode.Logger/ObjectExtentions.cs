using iCode.Extensions;
using System.Drawing;

namespace iCode.Log
{
    public static class ObjectExtentions
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

        //----------------------------------------------------------------------------

        public static T Out<T>(this T item, string title = "")
        {
            iLogger.Out(title.IsNullOrEmpty() ? $"{item}" : $"{title}: {item}");
            return item;
        }

        public static string Out(this string item, string title = "")
        {
            iLogger.Out(title.IsNullOrEmpty() ? $"'{item}'" : $"{title}: '{item}'");
            return item;
        }

        public static IEnumerable<T> Out<T>(this IEnumerable<T> items, string title = "")
        {
            string display = title.IsNullOrEmpty()? "{" :  $"{title}: {{";
            int i=0;
            foreach (var item in items)
            {
                if( i!=0 )
                {
                    display += ", ";
                }
                if(item is string)
                {
                    display += "'" + item + "'";
                }
                else
                {
                    display += item;
                }

                yield return item;
                i++;
            }
            display += "}";
            iLogger.Out(display);
        }

        public static IEnumerable<T> Out<T,Y>(this IEnumerable<T> items, Func<T,Y> derivateOut, string title = "")
        {
            foreach (var item in items)
            {
                derivateOut(item).Out(title);
                yield return item;
            }
        }

        public static IEnumerable<T> WriteLines<T>(this IEnumerable<T> items)
        {
            foreach (var item in items)
            {
                iLogger.Out(item.ToString());
                yield return item;
            }
        }
    }

    public static class IEnumerableExtension
    {

       

        

        //public static void Info(this object requester, object toLog)
        //{
        //    iLogger.Log(toLog, LogLevel.Info, requester);
        //}
        //public static void Debug(this object requester, object toLog)
        //{
        //    iLogger.Log(toLog, LogLevel.Debug, requester);
        //}
        //public static void Trace(this object requester, object toLog)
        //{
        //    iLogger.Log(toLog, LogLevel.Trace, requester);
        //}

        //public static void Warn(this object requester, object toLog)
        //{
        //    iLogger.Log(toLog, LogLevel.Warn, requester);
        //}

        //public static void Error(this object requester, object toLog, Exception e)
        //{
        //    iLogger.Log(toLog, LogLevel.Error, requester.ToString() + " Error : " + e.Message);
        //}
        //public static void Error(this object requester, object toLog)
        //{
        //    iLogger.Log(toLog, LogLevel.Error, requester);
        //}

        //public static object WatchByLogger(this object requester, string? InstanceName = null)
        //{
        //    iLogger.Filters.WatchedInstances.Watch(requester);

        //    nameForLog(requester, ((InstanceName != null) ? InstanceName : nameof(requester)));

        //    return requester;
        //}

        //public static bool isWatched(this object requester)
        //{
        //    return iLogger.Filters.WatchedInstances.isWatched(requester);
        //}

        //public static void nameForLog(this object requester, string name)
        //{
        //    iLogger.GiveName(requester, name);
        //}

    }
}
