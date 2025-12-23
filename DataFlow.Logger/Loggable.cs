using DataFlow.Framework;

namespace DataFlow.Log
{
    /// <summary>
    /// A wrapper that automatically logs value changes.
    /// </summary>
    /// <typeparam name="T">The type of the value to watch.</typeparam>
    public class Loggable<T> : WatchedValueWrapper<T>
    {
        static public LogLevel level = LogLevel.Info;

        public Loggable(T value, string operation = "")
        {
            this.Actions.Add(() => this.WriteLine($"Value Changes due to {operation}: {Value}", level));
            Value = value;
        }

        public static implicit operator T(Loggable<T> thisOne) => thisOne._Value;
        public static implicit operator Loggable<T>(T value) => new Loggable<T>(value, "assignment/ implicit conversion");
        public static Loggable<T> operator +(Loggable<T> first, T second) => new Loggable<T>((dynamic)first._Value! + (dynamic)second!, "'+' operator");
        public static Loggable<T> operator -(Loggable<T> first, T second) => new Loggable<T>((dynamic)first._Value! - (dynamic)second!, "'-' operator");
    }
}
