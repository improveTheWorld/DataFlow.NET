
/* This code defines a generic class WatchedValueWrapper<T> that provides a mechanism for watching changes to a value of type T. The class allows registering actions to be executed whenever the value changes. The class supports implicit conversions between WatchedValueWrapper<T> and T.

The main parts of the code are as follows:

1. protected T _Value;: This field stores the actual value being watched.

2. public T Value: This property provides access to the watched value. Whenever the value is set, it triggers the execution of all registered actions.

3. List<Action> Actions = new List<Action>();: This list stores the actions to be executed when the value changes.

4. public WatchedValueWrapper(T value): This constructor initializes the watched value with a given value.

5. public static WatchedValueWrapper<T> operator +(WatchedValueWrapper<T> thisOne, Action action): This operator overload allows adding an action to the list of actions to be executed when the value changes.

6. public static WatchedValueWrapper<T> operator -(WatchedValueWrapper<T> thisOne, Action action): This operator overload allows removing an action from the list of actions.

7. public static implicit operator T(WatchedValueWrapper<T> thisOne): This implicit conversion operator allows converting an instance of WatchedValueWrapper<T> to type T by accessing the underlying value.

8. public static implicit operator WatchedValueWrapper<T>(T value): This implicit conversion operator allows converting a value of type T to an instance of WatchedValueWrapper<T>. */


namespace iCode.Framework
{

    public interface IWatchedValueWrapper
    {
        // Common interface elements if any
    }


    public class WatchedValueWrapper<T>
    {
        protected T _Value; // The value being watched

        public T Value // Property to access and set the watched value
        {
            get { return _Value; }
            set
            {
                _Value = value; // Set the new value
                Invoke();
            }
        }

       

        protected List<Action> Actions = new List<Action>(); // List to store the actions to be executed when the value changes

        protected void Invoke()
        {
            foreach (var action in Actions) // Invoke all the registered actions
            {
                action();
            }
        }

        public WatchedValueWrapper(T value) // Constructor that initializes the watched value with a given value
        {
            Value = value;
        }

        public WatchedValueWrapper() // Default constructor
        {
        }

        public static WatchedValueWrapper<T> operator +(WatchedValueWrapper<T> thisOne, Action action)
        {
            thisOne.Actions.Add(action); // Add an action to be executed when the value changes
            return thisOne;
        }

        public static WatchedValueWrapper<T> operator -(WatchedValueWrapper<T> thisOne, Action action)
        {
            thisOne.Actions.Remove(action); // Remove an action from the list of actions to be executed when the value changes
            return thisOne;
        }

        public static implicit operator T(WatchedValueWrapper<T> thisOne) => thisOne._Value;
        public static implicit operator WatchedValueWrapper<T>(T value) =>  new WatchedValueWrapper<T>(value); // Implicit conversion from T to WatchedValueWrapper<T>
    }
}


