namespace DataFlow.Log
{
    public class NameSpaceListWatcher : ListWatcher<NameSpaceComparer>
    {
        override public bool IsWatched(NameSpaceComparer nameSpaceToCheck)
        {
            return (watchedList == null) || (watchedList.FirstOrDefault(x => x.IsMatching(nameSpaceToCheck)) != default);
        }

        public bool IsWatchedObject(object instance)
        {
            return IsWatched(new NameSpaceComparer(instance.GetType().ToString()).ParentNameSpace(1));
        }

    }
}
