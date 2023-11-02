namespace iCode.Log
{
    // The LogFilter class extends the Config class and provides additional functionality.
    class LogFilter : Config
    {
        // Predefined static strings for various labels and formats.
        static readonly string nullObjectString = "nullObject";
        static readonly string DateTimeFormat = "yyyy-MM-dd HH:mm:ss.fff";
        static readonly string DateTimeLable = "DateTime: ";
        static readonly string ThreadIdLabel = "ThreadId: ";
        static readonly string ThreadNameLabel = "ThreadName: ";
        static readonly string InstanceNameLabel = "InstanceName: ";
        static readonly string TaskIdLabel = "TaskId: ";

        // Method to create a log header with various details depending on what's enabled in the configuration.
        string GetLogOverHead(object? requester, object objectToLog)
        {
            string logOverHead = "";

            if (IncludeInstanceName && requester != null && InstancesNames.ContainsKey(requester))
            {
                logOverHead += $"[{InstanceNameLabel}{InstancesNames[requester]}], ";
            }

            // Append DateTime if enabled
            if (IncludeTimestamp)
            {
                logOverHead += $"[{DateTimeLable}{DateTime.Now.ToString(DateTimeFormat)}], ";
            }

            // Append ThreadId if enabled
            if (IncludeThreadId)
            {
                logOverHead += $"[{ThreadIdLabel}{Thread.CurrentThread.ManagedThreadId}], ";
            }

            // Append ThreadName if enabled
            if (ThreadNameEnabled)
            {
                logOverHead += $"[{ThreadNameLabel}{Thread.CurrentThread.Name}], ";
            }

            // Append TaskId if enabled
            if (IncludeTaskId)
            {
                logOverHead += $"[{TaskIdLabel}{Task.CurrentId}], ";
            }

            // Append InstanceName if enabled
            if (IncludeInstanceName)
            {
                string? name;
                if (objectToLog != null && InstancesNames.TryGetValue(objectToLog, out name))
                {
                    logOverHead += $"[{IncludeInstanceName}{name}], ";
                }

            }
            return logOverHead;
        }

        // Method to compute the log message.
        public string? ComputeLog(object? requester, object objectToLog)
        {
            // If the requester is null or is being watched and validated (if necessary), proceed.
            if ((requester == null) || ( WatchedInstances.isWatched(requester) && RequesterAcceptanceCriterias.IsCompliant(requester) && WatchedNameSpaces.isWatchedObject(requester) ))
            {
                // Get the string representation of the object to log. If it's null, use the predefined nullObjectString.
                string? message = (objectToLog == null) ? nullObjectString : objectToLog.ToString();

                // If no ValidateLog function is defined or if the message passes the validation, return the complete log string.
                if (MessageAcceptanceCriterias.IsCompliant(message))
                {
                    return GetLogOverHead(requester, objectToLog) + message;
                }
            }

            // If the log message isn't generated, return null.
            return null;
        }
    }
}
