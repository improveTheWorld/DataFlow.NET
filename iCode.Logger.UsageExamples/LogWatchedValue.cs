
using iCode.Framework;
using iCode.Log;

namespace iLoggerUsageExamples
{
    public class Loggable<T> : WatchedValueWrapper<T>
    {
        static public LogLevel level = LogLevel.Info;
        public Loggable(T value, string operation= "") // Constructor that initializes the watched value with a given value
        {

            this.Actions.Add(() => this.WriteLine($"Value Changes du to {operation}: {Value}", level));            
            Value = value;
        }

        public static implicit operator T(Loggable<T> thisOne) => thisOne._Value; // Implicit conversion from LogTrack<T> to T
        public static implicit operator Loggable<T>(T value) => new Loggable<T>(value, "assignment/ implicit convertion");  // Implicit conversion from T to LogTrack<T>
        public static Loggable<T> operator + (Loggable<T> first, T second ) =>  new Loggable<T>((dynamic)first._Value + (dynamic )second,"'+' operator");
        public static Loggable<T> operator -(Loggable<T> first, T second) => new Loggable<T>((dynamic)first._Value - (dynamic)second, "'-' operator");
    }  
}


