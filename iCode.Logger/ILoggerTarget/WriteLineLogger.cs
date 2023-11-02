namespace iCode.Log
{
    public class WriteLineLogger<T> : ILoggerTarget, IDisposable
    {
        readonly protected T Writer;

        public WriteLineLogger(T  writer)
        {
            Writer  = writer;
        }

        void IDisposable.Dispose()
        {
            if(Writer is IDisposable)
            {
               ( (IDisposable)Writer).Dispose();
            }
        }

        public void Log(string objectToLog, LogLevel logLevel)
        {
           ((dynamic)Writer).WriteLine($"[{logLevel.ToString()}] : {objectToLog}");

           
        }
    }
}

