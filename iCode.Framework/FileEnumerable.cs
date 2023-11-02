using System.Collections;


namespace iCode.Framework
{
    public class FileEnumerable : IEnumerable<string?>
    {
        private StreamReader File;
        public readonly int Count = 0;
        public FileEnumerable(StreamReader file)
        {
            if (file == null)
            {
                throw new ArgumentNullException("file");
            }

            File = file;

        }       

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public IEnumerator<string?> GetEnumerator()
        {
            return new LinesEnumerator(File);
        }
    }

    public class LinesEnumerator : IEnumerator<string?>
    {
        StreamReader _File;
        
        string? CurrentLine = null;

        public LinesEnumerator(StreamReader file)
        {
            _File = file;
        }

        public bool MoveNext()
        {
            bool somethingToRead = !_File.EndOfStream;

            if (somethingToRead)
            {
                CurrentLine = _File.ReadLine();
            }
            return somethingToRead;
        }

        public void Reset()
        {
            _File.BaseStream.Seek(0, System.IO.SeekOrigin.Begin);
            _File = new StreamReader(_File.BaseStream);
            
        }
        void IDisposable.Dispose()
        {
            Reset();
            CurrentLine = null;
        }

        object IEnumerator.Current
        {
            get
            {
                return Current;
            }
        }

        public string? Current
        {
            get
            {
                return CurrentLine;
            }
        }
    }
}
