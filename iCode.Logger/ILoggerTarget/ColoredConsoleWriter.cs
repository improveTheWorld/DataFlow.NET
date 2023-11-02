namespace iCode.Log
{
    public class ColoredConsoleWriter : ILoggerTarget, IDisposable
    {
        public struct Colors
        {
            public ConsoleColor TraceColor = ConsoleColor.Gray;
            public ConsoleColor DebugColor = ConsoleColor.Blue;
            public ConsoleColor InfoColor = ConsoleColor.Green;
            public ConsoleColor WarnColor = ConsoleColor.Yellow;
            public ConsoleColor ErrorColor = ConsoleColor.Red;
            public ConsoleColor FatalColor = ConsoleColor.Magenta;

            public Colors() { }

        };


        public Dictionary<LogLevel, ConsoleColor> logLevelColors = initLogLevelgColors();

        static Dictionary<LogLevel, ConsoleColor> initLogLevelgColors()
        {
            Colors colors = new Colors();
            var retValue = new Dictionary<LogLevel, ConsoleColor>();
            retValue[LogLevel.Trace] = colors.TraceColor;
            retValue[LogLevel.Debug] = colors.DebugColor;
            retValue[LogLevel.Info] = colors.InfoColor;
            retValue[LogLevel.Warn] = colors.WarnColor;
            retValue[LogLevel.Error] = colors.ErrorColor;
            retValue[LogLevel.Fatal] = colors.FatalColor;

            return retValue;
        }
        void IDisposable.Dispose()
        {
            ;
        }

        public void Log(string logText, LogLevel logLevel)
        {
            ConsoleColor savedConsoleColo = Console.ForegroundColor;

            Console.ForegroundColor = logLevelColors[logLevel];


            Console.WriteLine(logText);


            Console.ForegroundColor = savedConsoleColo;

        }
    }
}

