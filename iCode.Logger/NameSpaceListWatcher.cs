namespace iCode.Log
{
    public class NameSpaceListWatcher : ListWatcher<NameSpaceComparer>
    {
        override public bool isWatched(NameSpaceComparer nameSpaceToCheck)
        {
            return (watchedList == null) || (watchedList.FirstOrDefault(x => x.isMatching(nameSpaceToCheck)) != default);
        }

        public bool isWatchedObject(object instance)
        {
            return isWatched(new NameSpaceComparer(instance.GetType().ToString()).ParentNameSpace(1));
        }

    }
}
