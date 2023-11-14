namespace iCode.Framework
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
}
