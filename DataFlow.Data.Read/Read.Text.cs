using System.Runtime.CompilerServices;
using System.Text;

namespace DataFlow.Data;

public static partial class Read
{
    private const string StreamPseudoPath = "(stream)";

    // Async from Stream
    public static async IAsyncEnumerable<string> Text(
        Stream stream,
        TextReadOptions options,
        string? filePath = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (options == null) throw new ArgumentNullException(nameof(options));
        options.FilePath = filePath ?? options.FilePath ?? StreamPseudoPath;

        using var reader = CreateStreamReader(stream, options, leaveOpen: true);
        StartIfNeeded(options);

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            options.CancellationToken.ThrowIfCancellationRequested();

            string? line;
            try
            {
#if NETSTANDARD2_0
                line = await reader.ReadLineAsync().ConfigureAwait(false);
#else
                // In .NET modern, ReadLineAsync(CancellationToken) exists for StreamReader? It does not; so keep without ct.
                line = await reader.ReadLineAsync().ConfigureAwait(false);
#endif
            }
            catch (TaskCanceledException) when (cancellationToken.IsCancellationRequested || options.CancellationToken.IsCancellationRequested)
            {
                throw new OperationCanceledException(cancellationToken.IsCancellationRequested ? cancellationToken : options.CancellationToken);
            }

            if (line is null) break;

            options.Metrics.RecordsEmitted++;
            options.Metrics.RawRecordsParsed++;
            options.Metrics.LinesRead++;
            if (options.ShouldEmitProgress()) options.EmitProgress();

            yield return line;
        }

        options.Complete();
    }

    // Async from file path
    public static async IAsyncEnumerable<string> Text(
        string path,
        TextReadOptions options,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (options == null) throw new ArgumentNullException(nameof(options));

        await using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read,
            options.BufferSize > 0 ? options.BufferSize : 64 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan);

        await foreach (var line in Text(fs, options, filePath: path, cancellationToken))
            yield return line;
    }

    // Minimal async overloads
    public static async IAsyncEnumerable<string> Text(
        Stream stream,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var opts = new TextReadOptions();
        await foreach (var line in Text(stream, opts, filePath: StreamPseudoPath, cancellationToken))
            yield return line;
    }

    public static async IAsyncEnumerable<string> Text(
        string path,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var opts = new TextReadOptions();
        await foreach (var line in Text(path, opts, cancellationToken))
            yield return line;
    }

    // Sync from Stream
    public static IEnumerable<string> TextSync(
        Stream stream,
        TextReadOptions options,
        string? filePath = null,
        CancellationToken cancellationToken = default)
    {
        if (options == null) throw new ArgumentNullException(nameof(options));
        options.FilePath = filePath ?? options.FilePath ?? StreamPseudoPath;

        using var reader = CreateStreamReader(stream, options, leaveOpen: true);
        StartIfNeeded(options);

        string? line;
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            options.CancellationToken.ThrowIfCancellationRequested();

            line = reader.ReadLine();
            if (line is null) break;

            options.Metrics.RecordsEmitted++;
            options.Metrics.RawRecordsParsed++;
            options.Metrics.LinesRead++;
            if (options.ShouldEmitProgress()) options.EmitProgress();

            yield return line;
        }

        options.Complete();
    }

    // Sync from file path
    public static IEnumerable<string> TextSync(
        string path,
        TextReadOptions options,
        CancellationToken cancellationToken = default)
    {
        if (options == null) throw new ArgumentNullException(nameof(options));

        using var fs = new FileStream(
        path,
        FileMode.Open,
        FileAccess.Read,
        FileShare.Read,
        options.BufferSize > 0 ? options.BufferSize : 64 * 1024,
        FileOptions.SequentialScan);

        foreach (var line in TextSync(fs, options, filePath: path, cancellationToken))
            yield return line;
    }

    // Minimal sync overloads
    public static IEnumerable<string> TextSync(Stream stream, CancellationToken cancellationToken = default)
        => TextSync(stream, new TextReadOptions(), filePath: StreamPseudoPath, cancellationToken);

    public static IEnumerable<string> TextSync(string path, CancellationToken cancellationToken = default)
        => TextSync(path, new TextReadOptions(), cancellationToken);

    private static void StartIfNeeded(TextReadOptions options)
    {
        if (options.Metrics.StartedUtc == default)
            options.Metrics.StartedUtc = DateTime.UtcNow;
        if (options.ShouldEmitProgress())
            options.EmitProgress();
    }

    private static StreamReader CreateStreamReader(Stream stream, TextReadOptions options, bool leaveOpen)
    {
        var enc = options.Encoding ?? new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
        int buf = options.BufferSize > 0 ? options.BufferSize : 64 * 1024;
        return new StreamReader(stream, enc, options.DetectEncodingFromByteOrderMarks, bufferSize: buf, leaveOpen: leaveOpen);
    }
}