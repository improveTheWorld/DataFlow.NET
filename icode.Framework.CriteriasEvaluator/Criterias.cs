using iCode.Extensions.IEnumerableExtensions;
namespace iCode.Framework
{
    public abstract class Criterias<T>
    {
        protected List<Func<T, bool>> criterias = new();
        public void AcceptAll()
        {
            criterias.Clear();
        }
        public void SetCriteria(Func<T, bool> criteria)
        {
            if (criterias.Count> 0)
            {
                criterias.Clear();
            }
            
            criterias.Add(criteria);

        }
        public void AddCriteria(Func<T, bool> criteria)
        {
            criterias.Add(criteria);
        }

        public abstract bool IsCompliant(T obj);
       
    }
}
