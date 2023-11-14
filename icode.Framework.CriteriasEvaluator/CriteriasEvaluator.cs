namespace iCode.Framework
{
    public class CriteriasEvaluator<T> : Criterias<T>
    {
        readonly bool _finalResulat;

        /// <param name="finalResult">
        /// True for Acceptance Criterias.
        /// False for  Refusal Criterias.
        /// </param>
        public CriteriasEvaluator(bool finalResult) //
        {
            _finalResulat = finalResult;
        }
        override public bool IsCompliant(T obj)
        {
            if (criterias == null)
                return true;
            if (obj == null)
                return false;
            foreach (var Criteria in criterias)
            {
                if (Criteria(obj))
                    return _finalResulat; // here is the difference between the two  "IsCompliant" implementations
            }
            return true;
        }
    }
}
