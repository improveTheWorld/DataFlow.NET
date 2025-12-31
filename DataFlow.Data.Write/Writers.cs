using System.Text.Json;
using YamlDotNet.Serialization;

namespace DataFlow.Data
{
    /// <summary>
    /// Extension methods for writing IEnumerable / IAsyncEnumerable to text, CSV, JSON, YAML.
    /// All asynchronous methods return Task (standardized) and accept an optional CancellationToken.
    /// </summary>
    public static class Writers
    {
        // ------------------------------------------------------------------
        // TEXT
        // ------------------------------------------------------------------

        public static void WriteTextSync(this IEnumerable<string> lines, string path, CancellationToken ct = default)
        {
            if (path is null) throw new ArgumentNullException(nameof(path));
            using var file = new StreamWriter(path);
            foreach (var line in lines)
            {
                ct.ThrowIfCancellationRequested();
                file.WriteLine(line);
            }
        }

        public static async Task WriteText(this IEnumerable<string> lines, string path, CancellationToken ct = default)
        {
            if (path is null) throw new ArgumentNullException(nameof(path));
            await using var file = new StreamWriter(path);
            foreach (var line in lines)
            {
                ct.ThrowIfCancellationRequested();
                await file.WriteLineAsync(line);
            }
            await file.FlushAsync();
        }

        public static async Task WriteText(this IAsyncEnumerable<string> lines, string path, CancellationToken ct = default)
        {
            if (path is null) throw new ArgumentNullException(nameof(path));
            await using var file = new StreamWriter(path);
            await foreach (var line in lines.WithCancellation(ct))
            {
                ct.ThrowIfCancellationRequested();
                await file.WriteLineAsync(line);
            }
            await file.FlushAsync();
        }

        // ------------------------------------------------------------------
        // CSV
        // ------------------------------------------------------------------

        public static void WriteCsvSync<T>(this IEnumerable<T> records, string path, bool withHeader = true, string separator = ",", CancellationToken ct = default)
        {
            if (path is null) throw new ArgumentNullException(nameof(path));
            using var file = new StreamWriter(path);
            if (withHeader)
                file.WriteLine(CsvWriter.CsvHeader<T>(separator));

            foreach (var record in records)
            {
                ct.ThrowIfCancellationRequested();
                file.WriteLine(record.ToCsvLine(separator));
            }
        }

        public static async Task WriteCsv<T>(this IEnumerable<T> records, string path, bool withHeader = true, string separator = ",", CancellationToken ct = default)
        {
            if (path is null) throw new ArgumentNullException(nameof(path));
            await using var file = new StreamWriter(path);
            if (withHeader)
                await file.WriteLineAsync(CsvWriter.CsvHeader<T>(separator));

            foreach (var record in records)
            {
                ct.ThrowIfCancellationRequested();
                await file.WriteLineAsync(record.ToCsvLine(separator));
            }
            await file.FlushAsync();
        }

        public static async Task WriteCsv<T>(this IAsyncEnumerable<T> records, string path, bool withHeader = true, string separator = ",", CancellationToken ct = default)
        {
            if (path is null) throw new ArgumentNullException(nameof(path));
            await using var file = new StreamWriter(path);
            if (withHeader)
                await file.WriteLineAsync(CsvWriter.CsvHeader<T>(separator));

            await foreach (var record in records.WithCancellation(ct))
            {
                ct.ThrowIfCancellationRequested();
                await file.WriteLineAsync(record.ToCsvLine(separator));
            }
            await file.FlushAsync();
        }

        // --- CSV with Options ---

        /// <summary>
        /// Writes records to a CSV file using specified options.
        /// </summary>
        public static async Task WriteCsv<T>(this IAsyncEnumerable<T> records, string path, CsvWriteOptions options)
        {
            if (path is null) throw new ArgumentNullException(nameof(path));
            if (options is null) throw new ArgumentNullException(nameof(options));

            var ct = options.CancellationToken;
            options.Metrics.Start();

            await using var file = new StreamWriter(path, options.Append, options.Encoding);
            if (options.WriteHeader)
                await file.WriteLineAsync(CsvWriter.CsvHeader<T>(options.Separator));

            await foreach (var record in records.WithCancellation(ct))
            {
                ct.ThrowIfCancellationRequested();
                await file.WriteLineAsync(record.ToCsvLine(options.Separator));
                options.Metrics.IncrementRecords();
            }
            await file.FlushAsync();
            options.Metrics.Complete();
        }

        /// <summary>
        /// Writes records to a CSV stream using specified options.
        /// </summary>
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

        /// <summary>
        /// Writes records to a CSV stream using specified options.
        /// </summary>
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

        // ------------------------------------------------------------------
        // JSON
        // ------------------------------------------------------------------

        public static void WriteJsonSync<T>(this IEnumerable<T> items, string path, CancellationToken ct = default)
        {
            if (path is null) throw new ArgumentNullException(nameof(path));
            ct.ThrowIfCancellationRequested();
            using var stream = File.Create(path);
            JsonSerializer.Serialize(stream, items, new JsonSerializerOptions { WriteIndented = true });
        }

        public static async Task WriteJson<T>(this IEnumerable<T> items, string path, CancellationToken ct = default)
        {
            if (path is null) throw new ArgumentNullException(nameof(path));
            ct.ThrowIfCancellationRequested();
            await using var stream = File.Create(path);
            await JsonSerializer.SerializeAsync(stream, items, new JsonSerializerOptions { WriteIndented = true }, ct);
            await stream.FlushAsync(ct);
        }

        public static async Task WriteJson<T>(this IAsyncEnumerable<T> items, string path, CancellationToken ct = default)
        {
            if (path is null) throw new ArgumentNullException(nameof(path));
            await using var stream = File.Create(path);
            await using var jsonWriter = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true });

            jsonWriter.WriteStartArray();

            var serializerOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            await foreach (var item in items.WithCancellation(ct))
            {
                ct.ThrowIfCancellationRequested();
                JsonSerializer.Serialize(jsonWriter, item, serializerOptions);
            }

            jsonWriter.WriteEndArray();
            await jsonWriter.FlushAsync(ct);
            await stream.FlushAsync(ct);
        }

        // --- JSON with Options ---

        /// <summary>
        /// Writes items to a JSON stream using specified options.
        /// </summary>
        public static async Task WriteJson<T>(this IAsyncEnumerable<T> items, Stream stream, JsonWriteOptions? options = null)
        {
            if (stream is null) throw new ArgumentNullException(nameof(stream));
            options ??= new JsonWriteOptions();

            var ct = options.CancellationToken;
            options.Metrics.Start();

            var serializerOptions = options.SerializerOptions ?? new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

            if (options.JsonLinesFormat)
            {
                // JSON Lines format: one JSON object per line, no array wrapper
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
                // Standard JSON array format
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

        /// <summary>
        /// Writes items to a JSON stream using specified options (IEnumerable).
        /// </summary>
        public static async Task WriteJson<T>(this IEnumerable<T> items, Stream stream, JsonWriteOptions? options = null)
        {
            if (stream is null) throw new ArgumentNullException(nameof(stream));
            options ??= new JsonWriteOptions();

            var ct = options.CancellationToken;
            options.Metrics.Start();

            var serializerOptions = options.SerializerOptions ?? new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

            if (options.JsonLinesFormat)
            {
                // JSON Lines format: one JSON object per line, no array wrapper
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
                // Standard JSON array format
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

        // ------------------------------------------------------------------
        // YAML
        // ------------------------------------------------------------------

        public static void WriteYamlSync<T>(this IEnumerable<T> items, string path, CancellationToken ct = default)
        {
            if (path is null) throw new ArgumentNullException(nameof(path));
            ct.ThrowIfCancellationRequested();
            using var writer = new StreamWriter(path);
            var serializer = new SerializerBuilder().Build();
            serializer.Serialize(writer, items);
        }

        /// <summary>
        /// Streaming single YAML sequence for IAsyncEnumerable<T>
        /// Produces ONE YAML document representing a list (sequence).
        /// If there are no elements, writes "[]".
        /// </summary>
        public static async Task WriteYaml<T>(this IEnumerable<T> items, string path, bool writeEmptySequenceWhenNoItems = true, CancellationToken ct = default)
        {
            if (path is null) throw new ArgumentNullException(nameof(path));

            await using var writer = new StreamWriter(path);
            var serializer = new SerializerBuilder().Build();

            bool any = false;

            foreach (var item in items)
            {
                ct.ThrowIfCancellationRequested();
                any = true;

                // Serialize the single item to a temp buffer
                using var temp = new StringWriter();
                serializer.Serialize(temp, item);
                var raw = temp.ToString();

                // Normalize lines
                var lines = raw.Replace("\r\n", "\n").TrimEnd('\n').Split('\n');

                // First line after "- "
                await writer.WriteAsync("- ");
                await writer.WriteLineAsync(lines[0]);

                // Subsequent lines (if any) indent with two spaces
                for (int i = 1; i < lines.Length; i++)
                {
                    ct.ThrowIfCancellationRequested();
                    if (lines[i].Length == 0)
                        await writer.WriteLineAsync();
                    else
                    {
                        await writer.WriteAsync("  ");
                        await writer.WriteLineAsync(lines[i]);
                    }
                }
            }

            if (!any && writeEmptySequenceWhenNoItems)
            {
                await writer.WriteLineAsync("[]");
            }

            await writer.FlushAsync();
        }

        /// <summary>
        /// Streaming single YAML sequence for IAsyncEnumerable<T>
        /// Produces ONE YAML document representing a list (sequence).
        /// If there are no elements, writes "[]".
        /// </summary>
        public static async Task WriteYaml<T>(this IAsyncEnumerable<T> items, string path, bool writeEmptySequenceWhenNoItems = true, CancellationToken ct = default)
        {
            if (path is null) throw new ArgumentNullException(nameof(path));

            await using var writer = new StreamWriter(path);
            var serializer = new SerializerBuilder().Build();

            bool any = false;

            await foreach (var item in items.WithCancellation(ct))
            {
                ct.ThrowIfCancellationRequested();
                any = true;

                // Serialize the single item to a temp buffer
                using var temp = new StringWriter();
                serializer.Serialize(temp, item);
                var raw = temp.ToString();

                // Normalize lines
                var lines = raw.Replace("\r\n", "\n").TrimEnd('\n').Split('\n');

                // First line after "- "
                await writer.WriteAsync("- ");
                await writer.WriteLineAsync(lines[0]);

                // Subsequent lines (if any) indent with two spaces
                for (int i = 1; i < lines.Length; i++)
                {
                    ct.ThrowIfCancellationRequested();
                    if (lines[i].Length == 0)
                        await writer.WriteLineAsync();
                    else
                    {
                        await writer.WriteAsync("  ");
                        await writer.WriteLineAsync(lines[i]);
                    }
                }
            }

            if (!any && writeEmptySequenceWhenNoItems)
            {
                await writer.WriteLineAsync("[]");
            }

            await writer.FlushAsync();
        }

        /// <summary>
        /// Multi-document batching: each batch serialized as a YAML sequence document.
        /// Each batch contains up to batchSize items (list form). Documents separated by '---'..
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
                    if (!first)
                        await writer.WriteLineAsync("---");
                    serializer.Serialize(writer, buffer);
                    buffer.Clear();
                    first = false;
                    await writer.FlushAsync();
                }
            }

            if (buffer.Count > 0)
            {
                if (!first)
                    await writer.WriteLineAsync("---");
                serializer.Serialize(writer, buffer);
                await writer.FlushAsync();
            }
        }

        // --- Text with Stream ---

        /// <summary>
        /// Writes lines to a stream using specified options.
        /// </summary>
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

        /// <summary>
        /// Writes lines to a stream using specified options (IEnumerable).
        /// </summary>
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

        // --- YAML with Stream ---

        /// <summary>
        /// Writes items to a YAML stream using specified options.
        /// </summary>
        public static async Task WriteYaml<T>(this IAsyncEnumerable<T> items, Stream stream, YamlWriteOptions? options = null)
        {
            if (stream is null) throw new ArgumentNullException(nameof(stream));
            options ??= new YamlWriteOptions();

            var ct = options.CancellationToken;
            options.Metrics.Start();

            await using var writer = new StreamWriter(stream, options.Encoding, leaveOpen: true);
            var serializer = new SerializerBuilder().Build();

            // If batching is enabled, use batched mode
            if (options.BatchSize.HasValue && options.BatchSize.Value > 0)
            {
                var buffer = new List<T>(options.BatchSize.Value);
                var first = true;

                await foreach (var item in items.WithCancellation(ct))
                {
                    ct.ThrowIfCancellationRequested();
                    buffer.Add(item);
                    options.Metrics.IncrementRecords();

                    if (buffer.Count >= options.BatchSize.Value)
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
            else
            {
                // Single document mode (streaming)
                bool any = false;
                await foreach (var item in items.WithCancellation(ct))
                {
                    ct.ThrowIfCancellationRequested();
                    any = true;
                    options.Metrics.IncrementRecords();

                    using var temp = new StringWriter();
                    serializer.Serialize(temp, item);
                    var raw = temp.ToString();
                    var linesParsed = raw.Replace("\r\n", "\n").TrimEnd('\n').Split('\n');

                    await writer.WriteAsync("- ");
                    await writer.WriteLineAsync(linesParsed[0]);

                    for (int i = 1; i < linesParsed.Length; i++)
                    {
                        if (linesParsed[i].Length == 0)
                            await writer.WriteLineAsync();
                        else
                        {
                            await writer.WriteAsync("  ");
                            await writer.WriteLineAsync(linesParsed[i]);
                        }
                    }
                }

                if (!any && options.WriteEmptySequence)
                    await writer.WriteLineAsync("[]");

                await writer.FlushAsync();
            }

            options.Metrics.Complete();
        }
    }

}