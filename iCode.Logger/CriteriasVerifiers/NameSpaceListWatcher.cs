namespace iCode.Log
{
    public class NameSpaceListWatcher : ListWatcher<NameSpaceComparer>
    {
        override public bool isWatched(NameSpaceComparer nameSpaceToCheck)
        {
            if (watchedList == null)
            {
                return true;
            } 
            else
            {
                foreach (NameSpaceComparer nameSpace in watchedList)
                {
                    if (nameSpace.isMatching(nameSpaceToCheck))
                        return true;
                }
                return false;
            }
        }

        public bool isWatchedObject(object instance)
        {
            return isWatched(new NameSpaceComparer(instance.GetType().ToString()).ParentNameSpace(1));
        }

    }
}
