
namespace DataFlow;

/// <summary>
/// Provides static methods for lazily reading data from various file formats,
/// with full support for both synchronous (IEnumerable) and asynchronous (IAsyncEnumerable) streaming.
/// The method sync/async suffixes convention is inverted (default is asynchronous) to encourage the asynchronous file reading reflex.
/// Simple API for nominal cases + Option-based APIs: Csv / CsvSync, Json, Yaml.
/// </summary>
public static partial class Read
{

    // Helper sink wrapping onError callback
    internal sealed class DelegatingErrorSink : IReaderErrorSink
    {
        private readonly Action<string, Exception>? _csvAction;
        private readonly Action<Exception>? _exAction;
        private readonly string _file;

        public DelegatingErrorSink(Action<string, Exception> csvAction, string file)
        {
            _csvAction = csvAction;
            _file = file;
        }
        public DelegatingErrorSink(Action<Exception> exAction, string file)
        {
            _exAction = exAction;
            _file = file;
        }

        public void Report(ReaderError error)
        {
            // Build message consistent with Throw-mode formatting:
            // "{errorType}: {message}" and append excerpt when available.
            var msg = $"{error.ErrorType}: {error.Message}" +
                      (string.IsNullOrEmpty(error.RawExcerpt) ? "" : " | excerpt: " + error.RawExcerpt);

            if (_csvAction != null)
            {
                // CSV simple overload passes raw excerpt separately as first parameter.
                _csvAction(error.RawExcerpt, new InvalidDataException(msg));
            }
            else if (_exAction != null)
            {
                // JSON/YAML simple overloads receive the enriched message (with excerpt).
                _exAction(new InvalidDataException(msg));
            }
        }

        public void Dispose() { }
    }
}
