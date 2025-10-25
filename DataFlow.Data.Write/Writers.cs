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
    }

}