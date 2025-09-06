using DataFlow.Data.StringMapper;
using DataFlow.Extensions;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using YamlDotNet.Core;

namespace DataFlow.Data;

/// <summary>
/// Provides static methods for lazily reading data from various file formats,
/// with full support for both synchronous (IEnumerable) and asynchronous (IAsyncEnumerable) streaming.
/// The method sync/async suffixes convention is inverted (default is asynchronous) to encourage the asynchronous file reading reflex.
/// Simple API for nominal cases + Option-based APIs: Csv / CsvSync, Json, Yaml.
/// </summary>
public static partial class Read
{
  
    // Helper sink wrapping old onError callback
    private sealed class DelegatingErrorSink : IReaderErrorSink
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
            if (_csvAction != null)
            {
                _csvAction(error.RawExcerpt, new InvalidDataException(error.Message));
            }
            else if (_exAction != null)
            {
                _exAction(new InvalidDataException(error.Message));
            }
        }

        public void Dispose() { }
    }
}
