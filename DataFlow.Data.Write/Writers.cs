using System.Text.Json;
using YamlDotNet.Serialization;

namespace DataFlow;

/// <summary>
/// Extension methods for writing IEnumerable / IAsyncEnumerable to text, CSV, JSON, YAML.
/// 
/// Each format follows a uniform 6-overload pattern:
///   1. IEnumerable   + path   + sync  (WriteXxxSync)
///   2. IEnumerable   + path   + async (WriteXxx)
///   3. IAsyncEnumerable + path + async (WriteXxx)
///   4. IEnumerable   + stream + sync  (WriteXxxSync)
///   5. IEnumerable   + stream + async (WriteXxx)
///   6. IAsyncEnumerable + stream + async (WriteXxx)
///
/// All overloads accept an optional Options parameter for full control.
/// Legacy convenience overloads with inline parameters are kept for simplicity.
/// </summary>
public static class Writers
{
    // ══════════════════════════════════════════════════════════════════════
    // TEXT — Unified API
    // ══════════════════════════════════════════════════════════════════════

    #region Text — Unified (6 overloads)

    /// <summary>Writes lines to a file (sync, IEnumerable).</summary>
    public static void WriteTextSync(this IEnumerable<string> lines, string path, WriteOptions? options = null, CancellationToken ct = default)
    {
        if (path is null) throw new ArgumentNullException(nameof(path));
        options ??= new WriteOptions();
        var token = options.CancellationToken != default ? options.CancellationToken : ct;
        options.Metrics.Start();

        using var file = new StreamWriter(path, options.Append, options.Encoding);
        foreach (var line in lines)
        {
            token.ThrowIfCancellationRequested();
            file.WriteLine(line);
            options.Metrics.IncrementRecords();
        }
        options.Metrics.Complete();
    }

    /// <summary>Writes lines to a file (async, IEnumerable).</summary>
    public static async Task WriteText(this IEnumerable<string> lines, string path, WriteOptions? options = null, CancellationToken ct = default)
    {
        if (path is null) throw new ArgumentNullException(nameof(path));
        options ??= new WriteOptions();
        var token = options.CancellationToken != default ? options.CancellationToken : ct;
        options.Metrics.Start();

        await using var file = new StreamWriter(path, options.Append, options.Encoding);
        foreach (var line in lines)
        {
            token.ThrowIfCancellationRequested();
            await file.WriteLineAsync(line);
            options.Metrics.IncrementRecords();
        }
        await file.FlushAsync();
        options.Metrics.Complete();
    }

    /// <summary>Writes lines to a file (async, IAsyncEnumerable).</summary>
    public static async Task WriteText(this IAsyncEnumerable<string> lines, string path, WriteOptions? options = null, CancellationToken ct = default)
    {
        if (path is null) throw new ArgumentNullException(nameof(path));
        options ??= new WriteOptions();
        var token = options.CancellationToken != default ? options.CancellationToken : ct;
        options.Metrics.Start();

        await using var file = new StreamWriter(path, options.Append, options.Encoding);
        await foreach (var line in lines.WithCancellation(token))
        {
            token.ThrowIfCancellationRequested();
            await file.WriteLineAsync(line);
            options.Metrics.IncrementRecords();
        }
        await file.FlushAsync();
        options.Metrics.Complete();
    }

    /// <summary>Writes lines to a stream (sync, IEnumerable).</summary>
    public static void WriteTextSync(this IEnumerable<string> lines, Stream stream, WriteOptions? options = null)
    {
        if (stream is null) throw new ArgumentNullException(nameof(stream));
        options ??= new WriteOptions();
        var ct = options.CancellationToken;
        options.Metrics.Start();

        using var writer = new StreamWriter(stream, options.Encoding, leaveOpen: true);
        foreach (var line in lines)
        {
            ct.ThrowIfCancellationRequested();
            writer.WriteLine(line);
            options.Metrics.IncrementRecords();
        }
        writer.Flush();
        options.Metrics.Complete();
    }

    /// <summary>Writes lines to a stream (async, IEnumerable).</summary>
    public static async Task WriteText(this IEnumerable<string> lines, Stream stream, WriteOptions? options = null)
    {
        if (stream is null) throw new ArgumentNullException(nameof(stream));
        options ??= new WriteOptions();
        var ct = options.CancellationToken;
        options.Metrics.Start();

        await using var writer = new StreamWriter(stream, options.Encoding, leaveOpen: true);
        foreach (var line in lines)
        {
            ct.ThrowIfCancellationRequested();
            await writer.WriteLineAsync(line);
            options.Metrics.IncrementRecords();
        }
        await writer.FlushAsync();
        options.Metrics.Complete();
    }

    /// <summary>Writes lines to a stream (async, IAsyncEnumerable).</summary>
    public static async Task WriteText(this IAsyncEnumerable<string> lines, Stream stream, WriteOptions? options = null)
    {
        if (stream is null) throw new ArgumentNullException(nameof(stream));
        options ??= new WriteOptions();
        var ct = options.CancellationToken;
        options.Metrics.Start();

        await using var writer = new StreamWriter(stream, options.Encoding, leaveOpen: true);
        await foreach (var line in lines.WithCancellation(ct))
        {
            ct.ThrowIfCancellationRequested();
            await writer.WriteLineAsync(line);
            options.Metrics.IncrementRecords();
        }
        await writer.FlushAsync();
        options.Metrics.Complete();
    }

    #endregion

    // ══════════════════════════════════════════════════════════════════════
    // CSV — Unified API
    // ══════════════════════════════════════════════════════════════════════

    #region CSV — Unified (6 overloads)

    /// <summary>Writes records to a CSV file (sync, IEnumerable).</summary>
    public static void WriteCsvSync<T>(this IEnumerable<T> records, string path, CsvWriteOptions? options = null, CancellationToken ct = default)
    {
        if (path is null) throw new ArgumentNullException(nameof(path));
        options ??= new CsvWriteOptions();
        var token = options.CancellationToken != default ? options.CancellationToken : ct;
        options.Metrics.Start();

        using var file = new StreamWriter(path, options.Append, options.Encoding);
        if (options.WriteHeader)
            file.WriteLine(CsvWriter.CsvHeader<T>(options.Separator));

        foreach (var record in records)
        {
            token.ThrowIfCancellationRequested();
            file.WriteLine(record.ToCsvLine(options.Separator));
            options.Metrics.IncrementRecords();
        }
        options.Metrics.Complete();
    }

    /// <summary>Writes records to a CSV file (async, IEnumerable).</summary>
    public static async Task WriteCsv<T>(this IEnumerable<T> records, string path, CsvWriteOptions? options = null, CancellationToken ct = default)
    {
        if (path is null) throw new ArgumentNullException(nameof(path));
        options ??= new CsvWriteOptions();
        var token = options.CancellationToken != default ? options.CancellationToken : ct;
        options.Metrics.Start();

        await using var file = new StreamWriter(path, options.Append, options.Encoding);
        if (options.WriteHeader)
            await file.WriteLineAsync(CsvWriter.CsvHeader<T>(options.Separator));

        foreach (var record in records)
        {
            token.ThrowIfCancellationRequested();
            await file.WriteLineAsync(record.ToCsvLine(options.Separator));
            options.Metrics.IncrementRecords();
        }
        await file.FlushAsync();
        options.Metrics.Complete();
    }

    /// <summary>Writes records to a CSV file (async, IAsyncEnumerable).</summary>
    public static async Task WriteCsv<T>(this IAsyncEnumerable<T> records, string path, CsvWriteOptions? options = null, CancellationToken ct = default)
    {
        if (path is null) throw new ArgumentNullException(nameof(path));
        options ??= new CsvWriteOptions();
        var token = options.CancellationToken != default ? options.CancellationToken : ct;
        options.Metrics.Start();

        await using var file = new StreamWriter(path, options.Append, options.Encoding);
        if (options.WriteHeader)
            await file.WriteLineAsync(CsvWriter.CsvHeader<T>(options.Separator));

        await foreach (var record in records.WithCancellation(token))
        {
            token.ThrowIfCancellationRequested();
            await file.WriteLineAsync(record.ToCsvLine(options.Separator));
            options.Metrics.IncrementRecords();
        }
        await file.FlushAsync();
        options.Metrics.Complete();
    }

    /// <summary>Writes records to a CSV stream (sync, IEnumerable).</summary>
    public static void WriteCsvSync<T>(this IEnumerable<T> records, Stream stream, CsvWriteOptions? options = null)
    {
        if (stream is null) throw new ArgumentNullException(nameof(stream));
        options ??= new CsvWriteOptions();
        var ct = options.CancellationToken;
        options.Metrics.Start();

        using var writer = new StreamWriter(stream, options.Encoding, leaveOpen: true);
        if (options.WriteHeader)
            writer.WriteLine(CsvWriter.CsvHeader<T>(options.Separator));

        foreach (var record in records)
        {
            ct.ThrowIfCancellationRequested();
            writer.WriteLine(record.ToCsvLine(options.Separator));
            options.Metrics.IncrementRecords();
        }
        writer.Flush();
        options.Metrics.Complete();
    }

    /// <summary>Writes records to a CSV stream (async, IEnumerable).</summary>
    public static async Task WriteCsv<T>(this IEnumerable<T> records, Stream stream, CsvWriteOptions? options = null)
    {
        if (stream is null) throw new ArgumentNullException(nameof(stream));
        options ??= new CsvWriteOptions();
        var ct = options.CancellationToken;
        options.Metrics.Start();

        await using var writer = new StreamWriter(stream, options.Encoding, leaveOpen: true);
        if (options.WriteHeader)
            await writer.WriteLineAsync(CsvWriter.CsvHeader<T>(options.Separator));

        foreach (var record in records)
        {
            ct.ThrowIfCancellationRequested();
            await writer.WriteLineAsync(record.ToCsvLine(options.Separator));
            options.Metrics.IncrementRecords();
        }
        await writer.FlushAsync();
        options.Metrics.Complete();
    }

    /// <summary>Writes records to a CSV stream (async, IAsyncEnumerable).</summary>
    public static async Task WriteCsv<T>(this IAsyncEnumerable<T> records, Stream stream, CsvWriteOptions? options = null)
    {
        if (stream is null) throw new ArgumentNullException(nameof(stream));
        options ??= new CsvWriteOptions();
        var ct = options.CancellationToken;
        options.Metrics.Start();

        await using var writer = new StreamWriter(stream, options.Encoding, leaveOpen: true);
        if (options.WriteHeader)
            await writer.WriteLineAsync(CsvWriter.CsvHeader<T>(options.Separator));

        await foreach (var record in records.WithCancellation(ct))
        {
            ct.ThrowIfCancellationRequested();
            await writer.WriteLineAsync(record.ToCsvLine(options.Separator));
            options.Metrics.IncrementRecords();
        }
        await writer.FlushAsync();
        options.Metrics.Complete();
    }

    #endregion

    #region CSV — Legacy convenience (inline params, kept for beginners)

    public static void WriteCsvSync<T>(this IEnumerable<T> records, string path, bool withHeader, string separator = ",", CancellationToken ct = default)
        => records.WriteCsvSync(path, new CsvWriteOptions { WriteHeader = withHeader, Separator = separator, CancellationToken = ct });

    public static async Task WriteCsv<T>(this IEnumerable<T> records, string path, bool withHeader, string separator = ",", CancellationToken ct = default)
        => await records.WriteCsv(path, new CsvWriteOptions { WriteHeader = withHeader, Separator = separator, CancellationToken = ct });

    public static async Task WriteCsv<T>(this IAsyncEnumerable<T> records, string path, bool withHeader, string separator = ",", CancellationToken ct = default)
        => await records.WriteCsv(path, new CsvWriteOptions { WriteHeader = withHeader, Separator = separator, CancellationToken = ct });

    #endregion

    // ══════════════════════════════════════════════════════════════════════
    // JSON — Unified API
    // ══════════════════════════════════════════════════════════════════════

    #region JSON — Unified (6 overloads)

    /// <summary>Writes items to a JSON file (sync, IEnumerable).</summary>
    public static void WriteJsonSync<T>(this IEnumerable<T> items, string path, JsonWriteOptions? options = null, CancellationToken ct = default)
    {
        if (path is null) throw new ArgumentNullException(nameof(path));
        options ??= new JsonWriteOptions();
        var token = options.CancellationToken != default ? options.CancellationToken : ct;
        token.ThrowIfCancellationRequested();
        options.Metrics.Start();

        using var stream = File.Create(path);
        var serializerOptions = options.SerializerOptions ?? new JsonSerializerOptions { WriteIndented = options.Indented };
        JsonSerializer.Serialize(stream, items, serializerOptions);
        options.Metrics.Complete();
    }

    /// <summary>Writes items to a JSON file (async, IEnumerable).</summary>
    public static async Task WriteJson<T>(this IEnumerable<T> items, string path, JsonWriteOptions? options = null, CancellationToken ct = default)
    {
        if (path is null) throw new ArgumentNullException(nameof(path));
        options ??= new JsonWriteOptions();
        var token = options.CancellationToken != default ? options.CancellationToken : ct;
        token.ThrowIfCancellationRequested();
        options.Metrics.Start();

        await using var stream = File.Create(path);
        var serializerOptions = options.SerializerOptions ?? new JsonSerializerOptions { WriteIndented = options.Indented };
        await JsonSerializer.SerializeAsync(stream, items, serializerOptions, token);
        await stream.FlushAsync(token);
        options.Metrics.Complete();
    }

    /// <summary>Writes items to a JSON file (async, IAsyncEnumerable).</summary>
    public static async Task WriteJson<T>(this IAsyncEnumerable<T> items, string path, JsonWriteOptions? options = null, CancellationToken ct = default)
    {
        if (path is null) throw new ArgumentNullException(nameof(path));
        options ??= new JsonWriteOptions();
        var token = options.CancellationToken != default ? options.CancellationToken : ct;
        options.Metrics.Start();

        await using var stream = File.Create(path);
        var serializerOptions = options.SerializerOptions ?? new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var writerOptions = new JsonWriterOptions { Indented = options.Indented };

        if (options.JsonLinesFormat)
        {
            await using var writer = new StreamWriter(stream);
            await foreach (var item in items.WithCancellation(token))
            {
                token.ThrowIfCancellationRequested();
                var json = JsonSerializer.Serialize(item, serializerOptions);
                await writer.WriteLineAsync(json);
                options.Metrics.IncrementRecords();
            }
            await writer.FlushAsync();
        }
        else
        {
            await using var jsonWriter = new Utf8JsonWriter(stream, writerOptions);
            jsonWriter.WriteStartArray();

            await foreach (var item in items.WithCancellation(token))
            {
                token.ThrowIfCancellationRequested();
                JsonSerializer.Serialize(jsonWriter, item, serializerOptions);
                options.Metrics.IncrementRecords();
            }

            jsonWriter.WriteEndArray();
            await jsonWriter.FlushAsync(token);
            await stream.FlushAsync(token);
        }
        options.Metrics.Complete();
    }

    /// <summary>Writes items to a JSON stream (sync, IEnumerable).</summary>
    public static void WriteJsonSync<T>(this IEnumerable<T> items, Stream stream, JsonWriteOptions? options = null)
    {
        if (stream is null) throw new ArgumentNullException(nameof(stream));
        options ??= new JsonWriteOptions();
        options.CancellationToken.ThrowIfCancellationRequested();
        options.Metrics.Start();

        var serializerOptions = options.SerializerOptions ?? new JsonSerializerOptions { WriteIndented = options.Indented };
        JsonSerializer.Serialize(stream, items, serializerOptions);
        options.Metrics.Complete();
    }

    /// <summary>Writes items to a JSON stream (async, IEnumerable).</summary>
    public static async Task WriteJson<T>(this IEnumerable<T> items, Stream stream, JsonWriteOptions? options = null)
    {
        if (stream is null) throw new ArgumentNullException(nameof(stream));
        options ??= new JsonWriteOptions();
        var ct = options.CancellationToken;
        options.Metrics.Start();

        var serializerOptions = options.SerializerOptions ?? new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        if (options.JsonLinesFormat)
        {
            await using var writer = new StreamWriter(stream, leaveOpen: true);
            foreach (var item in items)
            {
                ct.ThrowIfCancellationRequested();
                var json = JsonSerializer.Serialize(item, serializerOptions);
                await writer.WriteLineAsync(json);
                options.Metrics.IncrementRecords();
            }
            await writer.FlushAsync();
        }
        else
        {
            var writerOptions = new JsonWriterOptions { Indented = options.Indented };
            await using var jsonWriter = new Utf8JsonWriter(stream, writerOptions);
            jsonWriter.WriteStartArray();

            foreach (var item in items)
            {
                ct.ThrowIfCancellationRequested();
                JsonSerializer.Serialize(jsonWriter, item, serializerOptions);
                options.Metrics.IncrementRecords();
            }

            jsonWriter.WriteEndArray();
            await jsonWriter.FlushAsync(ct);
        }
        options.Metrics.Complete();
    }

    /// <summary>Writes items to a JSON stream (async, IAsyncEnumerable).</summary>
    public static async Task WriteJson<T>(this IAsyncEnumerable<T> items, Stream stream, JsonWriteOptions? options = null)
    {
        if (stream is null) throw new ArgumentNullException(nameof(stream));
        options ??= new JsonWriteOptions();
        var ct = options.CancellationToken;
        options.Metrics.Start();

        var serializerOptions = options.SerializerOptions ?? new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        if (options.JsonLinesFormat)
        {
            await using var writer = new StreamWriter(stream, leaveOpen: true);
            await foreach (var item in items.WithCancellation(ct))
            {
                ct.ThrowIfCancellationRequested();
                var json = JsonSerializer.Serialize(item, serializerOptions);
                await writer.WriteLineAsync(json);
                options.Metrics.IncrementRecords();
            }
            await writer.FlushAsync();
        }
        else
        {
            var writerOptions = new JsonWriterOptions { Indented = options.Indented };
            await using var jsonWriter = new Utf8JsonWriter(stream, writerOptions);
            jsonWriter.WriteStartArray();

            await foreach (var item in items.WithCancellation(ct))
            {
                ct.ThrowIfCancellationRequested();
                JsonSerializer.Serialize(jsonWriter, item, serializerOptions);
                options.Metrics.IncrementRecords();
            }

            jsonWriter.WriteEndArray();
            await jsonWriter.FlushAsync(ct);
        }
        options.Metrics.Complete();
    }

    #endregion

    // ══════════════════════════════════════════════════════════════════════
    // YAML — Unified API
    // ══════════════════════════════════════════════════════════════════════

    #region YAML — Unified (6 overloads)

    /// <summary>Writes items to a YAML file (sync, IEnumerable).</summary>
    public static void WriteYamlSync<T>(this IEnumerable<T> items, string path, YamlWriteOptions? options = null, CancellationToken ct = default)
    {
        if (path is null) throw new ArgumentNullException(nameof(path));
        options ??= new YamlWriteOptions();
        var token = options.CancellationToken != default ? options.CancellationToken : ct;
        token.ThrowIfCancellationRequested();
        options.Metrics.Start();

        using var writer = new StreamWriter(path);
        var serializer = new SerializerBuilder().Build();
        WriteYamlSequence(writer, items, serializer, options.WriteEmptySequence, token);
        options.Metrics.Complete();
    }

    /// <summary>Writes items to a YAML file (async, IEnumerable).</summary>
    public static async Task WriteYaml<T>(this IEnumerable<T> items, string path, YamlWriteOptions? options = null, CancellationToken ct = default)
    {
        if (path is null) throw new ArgumentNullException(nameof(path));
        options ??= new YamlWriteOptions();
        var token = options.CancellationToken != default ? options.CancellationToken : ct;
        options.Metrics.Start();

        await using var writer = new StreamWriter(path);
        var serializer = new SerializerBuilder().Build();
        await WriteYamlSequenceAsync(writer, items, serializer, options, token);
        options.Metrics.Complete();
    }

    /// <summary>Writes items to a YAML file (async, IAsyncEnumerable).</summary>
    public static async Task WriteYaml<T>(this IAsyncEnumerable<T> items, string path, YamlWriteOptions? options = null, CancellationToken ct = default)
    {
        if (path is null) throw new ArgumentNullException(nameof(path));
        options ??= new YamlWriteOptions();
        var token = options.CancellationToken != default ? options.CancellationToken : ct;
        options.Metrics.Start();

        await using var writer = new StreamWriter(path);
        var serializer = new SerializerBuilder().Build();
        await WriteYamlSequenceAsync(writer, items, serializer, options, token);
        options.Metrics.Complete();
    }

    /// <summary>Writes items to a YAML stream (sync, IEnumerable).</summary>
    public static void WriteYamlSync<T>(this IEnumerable<T> items, Stream stream, YamlWriteOptions? options = null)
    {
        if (stream is null) throw new ArgumentNullException(nameof(stream));
        options ??= new YamlWriteOptions();
        var ct = options.CancellationToken;
        options.Metrics.Start();

        using var writer = new StreamWriter(stream, options.Encoding, leaveOpen: true);
        var serializer = new SerializerBuilder().Build();
        WriteYamlSequence(writer, items, serializer, options.WriteEmptySequence, ct);
        options.Metrics.Complete();
    }

    /// <summary>Writes items to a YAML stream (async, IEnumerable).</summary>
    public static async Task WriteYaml<T>(this IEnumerable<T> items, Stream stream, YamlWriteOptions? options = null)
    {
        if (stream is null) throw new ArgumentNullException(nameof(stream));
        options ??= new YamlWriteOptions();
        var token = options.CancellationToken;
        options.Metrics.Start();

        await using var writer = new StreamWriter(stream, options.Encoding, leaveOpen: true);
        var serializer = new SerializerBuilder().Build();
        await WriteYamlSequenceAsync(writer, items, serializer, options, token);
        options.Metrics.Complete();
    }

    /// <summary>Writes items to a YAML stream (async, IAsyncEnumerable).</summary>
    public static async Task WriteYaml<T>(this IAsyncEnumerable<T> items, Stream stream, YamlWriteOptions? options = null)
    {
        if (stream is null) throw new ArgumentNullException(nameof(stream));
        options ??= new YamlWriteOptions();
        var ct = options.CancellationToken;
        options.Metrics.Start();

        await using var writer = new StreamWriter(stream, options.Encoding, leaveOpen: true);
        var serializer = new SerializerBuilder().Build();
        await WriteYamlSequenceAsync(writer, items, serializer, options, ct);
        options.Metrics.Complete();
    }

    #endregion

    #region YAML — Legacy convenience (inline params, kept for beginners)

    public static async Task WriteYaml<T>(this IEnumerable<T> items, string path, bool writeEmptySequenceWhenNoItems, CancellationToken ct = default)
        => await items.WriteYaml(path, new YamlWriteOptions { WriteEmptySequence = writeEmptySequenceWhenNoItems, CancellationToken = ct });

    public static async Task WriteYaml<T>(this IAsyncEnumerable<T> items, string path, bool writeEmptySequenceWhenNoItems, CancellationToken ct = default)
        => await items.WriteYaml(path, new YamlWriteOptions { WriteEmptySequence = writeEmptySequenceWhenNoItems, CancellationToken = ct });

    #endregion

    #region YAML — WriteYamlBatched (specialized)

    /// <summary>
    /// Multi-document batching: each batch serialized as a YAML sequence document.
    /// Each batch contains up to batchSize items (list form). Documents separated by '---'.
    /// </summary>
    public static async Task WriteYamlBatched<T>(
        this IAsyncEnumerable<T> items,
        string path,
        int batchSize = 1000,
        CancellationToken ct = default)
    {
        if (path is null) throw new ArgumentNullException(nameof(path));
        if (batchSize <= 0) throw new ArgumentOutOfRangeException(nameof(batchSize));

        await using var writer = new StreamWriter(path);
        var serializer = new SerializerBuilder().Build();
        var buffer = new List<T>(batchSize);
        var first = true;

        await foreach (var item in items.WithCancellation(ct))
        {
            ct.ThrowIfCancellationRequested();
            buffer.Add(item);

            if (buffer.Count >= batchSize)
            {
                if (!first) await writer.WriteLineAsync("---");
                serializer.Serialize(writer, buffer);
                buffer.Clear();
                first = false;
                await writer.FlushAsync();
            }
        }

        if (buffer.Count > 0)
        {
            if (!first) await writer.WriteLineAsync("---");
            serializer.Serialize(writer, buffer);
            await writer.FlushAsync();
        }
    }

    #endregion

    // ══════════════════════════════════════════════════════════════════════
    // YAML — Internal helpers
    // ══════════════════════════════════════════════════════════════════════

    #region YAML Internal Helpers

    /// <summary>Sync helper: writes a YAML sequence to a StreamWriter.</summary>
    private static void WriteYamlSequence<T>(StreamWriter writer, IEnumerable<T> items, ISerializer serializer, bool writeEmptySequence, CancellationToken ct)
    {
        bool any = false;
        foreach (var item in items)
        {
            ct.ThrowIfCancellationRequested();
            any = true;
            WriteYamlItem(writer, item, serializer);
        }
        if (!any && writeEmptySequence)
            writer.WriteLine("[]");
        writer.Flush();
    }

    /// <summary>Async helper: writes a YAML sequence from IEnumerable to a StreamWriter.</summary>
    private static async Task WriteYamlSequenceAsync<T>(StreamWriter writer, IEnumerable<T> items, ISerializer serializer, YamlWriteOptions options, CancellationToken ct)
    {
        if (options.BatchSize.HasValue && options.BatchSize.Value > 0)
        {
            await WriteYamlBatchedAsync(writer, ToAsyncEnumerable(items, ct), serializer, options.BatchSize.Value, ct);
        }
        else
        {
            bool any = false;
            foreach (var item in items)
            {
                ct.ThrowIfCancellationRequested();
                any = true;
                options.Metrics.IncrementRecords();
                await WriteYamlItemAsync(writer, item, serializer, ct);
            }
            if (!any && options.WriteEmptySequence)
                await writer.WriteLineAsync("[]");
            await writer.FlushAsync();
        }
    }

    /// <summary>Async helper: writes a YAML sequence from IAsyncEnumerable to a StreamWriter.</summary>
    private static async Task WriteYamlSequenceAsync<T>(StreamWriter writer, IAsyncEnumerable<T> items, ISerializer serializer, YamlWriteOptions options, CancellationToken ct)
    {
        if (options.BatchSize.HasValue && options.BatchSize.Value > 0)
        {
            await WriteYamlBatchedAsync(writer, items, serializer, options.BatchSize.Value, ct);
        }
        else
        {
            bool any = false;
            await foreach (var item in items.WithCancellation(ct))
            {
                ct.ThrowIfCancellationRequested();
                any = true;
                options.Metrics.IncrementRecords();
                await WriteYamlItemAsync(writer, item, serializer, ct);
            }
            if (!any && options.WriteEmptySequence)
                await writer.WriteLineAsync("[]");
            await writer.FlushAsync();
        }
    }

    /// <summary>Writes a single YAML item as a "- " prefixed sequence entry (sync).</summary>
    private static void WriteYamlItem<T>(StreamWriter writer, T item, ISerializer serializer)
    {
        using var temp = new StringWriter();
        serializer.Serialize(temp, item);
        var raw = temp.ToString();
        var lines = raw.Replace("\r\n", "\n").TrimEnd('\n').Split('\n');

        writer.Write("- ");
        writer.WriteLine(lines[0]);
        for (int i = 1; i < lines.Length; i++)
        {
            if (lines[i].Length == 0) writer.WriteLine();
            else { writer.Write("  "); writer.WriteLine(lines[i]); }
        }
    }

    /// <summary>Writes a single YAML item as a "- " prefixed sequence entry (async).</summary>
    private static async Task WriteYamlItemAsync<T>(StreamWriter writer, T item, ISerializer serializer, CancellationToken ct)
    {
        using var temp = new StringWriter();
        serializer.Serialize(temp, item);
        var raw = temp.ToString();
        var lines = raw.Replace("\r\n", "\n").TrimEnd('\n').Split('\n');

        await writer.WriteAsync("- ");
        await writer.WriteLineAsync(lines[0]);
        for (int i = 1; i < lines.Length; i++)
        {
            ct.ThrowIfCancellationRequested();
            if (lines[i].Length == 0) await writer.WriteLineAsync();
            else { await writer.WriteAsync("  "); await writer.WriteLineAsync(lines[i]); }
        }
    }

    /// <summary>Batched YAML writing helper for stream-based overloads.</summary>
    private static async Task WriteYamlBatchedAsync<T>(StreamWriter writer, IAsyncEnumerable<T> items, ISerializer serializer, int batchSize, CancellationToken ct)
    {
        var buffer = new List<T>(batchSize);
        var first = true;

        await foreach (var item in items.WithCancellation(ct))
        {
            ct.ThrowIfCancellationRequested();
            buffer.Add(item);

            if (buffer.Count >= batchSize)
            {
                if (!first) await writer.WriteLineAsync("---");
                serializer.Serialize(writer, buffer);
                buffer.Clear();
                first = false;
                await writer.FlushAsync();
            }
        }

        if (buffer.Count > 0)
        {
            if (!first) await writer.WriteLineAsync("---");
            serializer.Serialize(writer, buffer);
            await writer.FlushAsync();
        }
    }

    /// <summary>Adapter: wraps IEnumerable as IAsyncEnumerable for internal use.</summary>
    private static async IAsyncEnumerable<T> ToAsyncEnumerable<T>(IEnumerable<T> source, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        foreach (var item in source)
        {
            ct.ThrowIfCancellationRequested();
            yield return item;
        }
        await Task.CompletedTask; // Suppress CS1998
    }

    #endregion
}