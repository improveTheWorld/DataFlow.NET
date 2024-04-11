using iCode.Extensions;
using System.Reflection.Metadata.Ecma335;

namespace iCode.Log
{
    public class ListWatcher<T>
    {
        protected List<T>? watchedList = null;

        // Method to watch all objects, set WatchedList to null
        public void WatchAll()
        {
            watchedList = null;
        }

        // Method to add objects to the WatchedList
        public void Watch(params T[] toWatch)
        {
            if (toWatch?.Any() == true)
            {
                (watchedList ??= new List<T>()).AddRange(toWatch);
            }
        }
        public bool isAllWatched()
        { 
            return watchedList == null; 
        }

        // Method to check if an object exists in the WatchedList
        virtual public bool isWatched(T instance)
        {
            return watchedList==null || isInList(instance);
        }

        public bool isInList(T instance)
        {
            return watchedList != null && watchedList.Contains(instance);
        }


    }
}
