using iCode.Framework;

namespace iCode.Log
{
    // Config class definition
    public class Config
    {
        // Dictionary to store the names associated with object instances
        public Dictionary<object, string> InstancesNames = new();

        // Filter function to validate logs
        public AcceptanceCriterias<string?> MessageAcceptanceCriterias = new();

        // Filter function to validate requesters
        public AcceptanceCriterias<object> RequesterAcceptanceCriterias = new();

        public NameSpaceListWatcher WatchedNameSpaces = new();

        List<IWatchedValueWrapper> WatchedValues = new ();

        // List of objects to be watched
        //protected List<object>? WatchedInstances = null;
        public ListWatcher<object> WatchedInstances = new();

        // Flags to enable different properties in the logs
        public bool IncludeTimestamp;
        public bool IncludeThreadId;
        public bool ThreadNameEnabled;
        public bool IncludeTaskId;
        public bool IncludeInstanceName;
             

        // Method to reset all filters and WatchedList
        public void ResetFilters()
        {
            MessageAcceptanceCriterias= new();
            RequesterAcceptanceCriterias = new();
            WatchedInstances = new();
            WatchedNameSpaces = new();
        }
    }
}
