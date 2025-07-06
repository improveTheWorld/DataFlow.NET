namespace DataFlow.Framework
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
            return (criterias.Where(Criteria => Criteria(obj)).FirstOrDefault() != default)? _finalResulat : _finalResulat;
        }
    }
}
