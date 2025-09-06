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
    // --- TEXT ---

    public static IEnumerable<string> TextSync(StreamReader file)
    {
        while (!file.EndOfStream)
        {
            yield return file.ReadLine();
        }
    }

    public static IEnumerable<string> TextSync(string path)
    {
        using var file = new StreamReader(path);
        while (!file.EndOfStream)
        {
            yield return file.ReadLine();
        }
    }

    public static async IAsyncEnumerable<string> Text(StreamReader file, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        while (!file.EndOfStream)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return await file.ReadLineAsync();
        }
    }

    public static async IAsyncEnumerable<string> Text(string path, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        using var file = new StreamReader(path);
        while (!file.EndOfStream)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return await file.ReadLineAsync();
        }
    }
}
