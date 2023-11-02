namespace iCode.Log
{
    public abstract class Criterias<T>
    {
        protected List<Func<T, bool>>? criterias = null;
        public void AcceptAll()
        {
            criterias = null;
        }
        public void SetCriteria(Func<T, bool> criteria)
        {
            if (criterias == null)
            {
                criterias = new();
            }
            else
            {
                criterias.Clear();
            }
            criterias.Add(criteria);

        }
        public void AddCriteria(Func<T, bool> criteria)
        {
            if (criterias == null)
            {
                criterias = new();
            }

            criterias.Add(criteria);
        }

        public abstract bool IsCompliant(T obj);
       
    }

    public class AcceptanceCriterias<T> : Criterias<T>
    {
        override public bool IsCompliant(T obj)
        {
            if (criterias == null)
                return true;
            if (obj == null)
                return false;
            foreach (var Criteria in criterias)
            {
                if (Criteria(obj))
                    return true;
            }
            return false;
        }
    }

    public class RefusalCriterias<T> : Criterias<T>
    {
        override public bool IsCompliant(T obj)
        {
            if (criterias == null)
                return true;
            if (obj == null)
                return false;
            foreach (var Criteria in criterias)
            {
                if (Criteria(obj))
                    return false; // here is the difference between the two  "IsCompliant" implementations
            }
            return true;
        }
    }
}
