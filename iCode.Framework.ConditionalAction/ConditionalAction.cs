
namespace iCode.Framework.ConditionalAction
{
    public class ConditionalAction
    {
        Action _Action;
        Func<bool> Condition;

        public ConditionalAction(Action action, Func<bool> condition)
        {
            _Action = action;
            Condition = condition;
        }

        public ConditionalAction(Action action)
        {
            _Action = action;
            Condition = ()=>true;
        }
        
        Action FinalAction => () =>
        {
            if (Condition() )
            {
                _Action();
            }
        };

        public static implicit operator Action(ConditionalAction thisOne) => thisOne.FinalAction;

        public static implicit operator ConditionalAction(Action action) => new ConditionalAction(action);

    }
}