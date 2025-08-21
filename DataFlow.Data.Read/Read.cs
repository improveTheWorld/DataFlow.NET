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
public static class Read
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

    // ---  RFC 4180 CSV (options-based) ---

    public static async IAsyncEnumerable<T> Csv<T>(string path, CsvReadOptions options, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (options == null) throw new ArgumentNullException(nameof(options));
        options.FilePath = path;

        // Local state / helpers
        T? CurrentInstance = default;

        await using var fs = File.OpenRead(path);
        using var reader = new StreamReader(fs, leaveOpen: false);

        string[]? schema = options.Schema;
        bool headerConsumed = false;

        // Buffer for schema/type inference if needed
        var inferenceBuffer = options.InferSchema ? new List<string[]>(options.SchemaInferenceSampleRows) : null;
        bool inferenceCompleted = !options.InferSchema;

        // Enumerate raw fields
        await foreach (var rawFields in CsvRfc4180Parser.ParseAsync(reader, options, cancellationToken))
        {
            if (options.Metrics.TerminatedEarly) yield break;

            // Header handling
            if (schema == null && options.HasHeader && !headerConsumed)
            {
                schema = ProcessHeader(rawFields, options);
                headerConsumed = true;
                continue;
            }

            // Schema inference (no header scenario)
            if (schema == null && options.InferSchema && !headerConsumed && !options.HasHeader)
            {
                // Collect sample to determine width
                inferenceBuffer!.Add(rawFields);
                if (inferenceBuffer.Count >= options.SchemaInferenceSampleRows)
                {
                    schema = GenerateSyntheticSchema(inferenceBuffer, options, path);
                    InferTypesIfRequested(inferenceBuffer, schema, options, path);
                    inferenceCompleted = true;
                    // Replay buffered rows
                    foreach (var buffered in inferenceBuffer)
                    {
                        if (YieldMapped(buffered)) yield return CurrentInstance!;
                    }
                    inferenceBuffer.Clear();
                }
                continue;
            }

            // Header but type inference requested & not completed yet
            if (options.InferSchema && !inferenceCompleted && schema != null)
            {
                inferenceBuffer!.Add(rawFields);
                if (inferenceBuffer.Count >= options.SchemaInferenceSampleRows)
                {
                    InferTypesIfRequested(inferenceBuffer, schema, options, path);
                    inferenceCompleted = true;
                    foreach (var buffered in inferenceBuffer)
                    {
                        if (YieldMapped(buffered)) yield return CurrentInstance!;
                    }
                    inferenceBuffer.Clear();
                }
                continue;
            }

            // If still waiting for inference after EOF later we'll finalize; for now just accumulate
            if (options.InferSchema && !inferenceCompleted)
            {
                inferenceBuffer!.Add(rawFields);
                continue;
            }

            if (schema == null)
            {
                if (!options.HandleError("CSV", options.Metrics.LinesRead, options.Metrics.RecordsRead, path,
                    "SchemaError", "Schema is null (no header and none supplied).", string.Join(",", rawFields)))
                    yield break;
                continue;
            }

            if (YieldMapped(rawFields))
                yield return CurrentInstance!;

            if (options.ShouldEmitProgress()) options.EmitProgress();
        }

        // Finalize inference at EOF if needed
        if (options.InferSchema && !inferenceCompleted)
        {
            if (schema == null)
                schema = GenerateSyntheticSchema(inferenceBuffer!, options, path);

            InferTypesIfRequested(inferenceBuffer!, schema, options, path);

            foreach (var buffered in inferenceBuffer!)
            {
                if (YieldMapped(buffered)) yield return CurrentInstance!;
                if (options.ShouldEmitProgress()) options.EmitProgress();
            }
        }

        options.Complete();

        

        string[] ProcessHeader(string[] headerRow, CsvReadOptions opts)
        {
            var hdr = new string[headerRow.Length];
            for (int i = 0; i < headerRow.Length; i++)
            {
                var raw = headerRow[i];
                var def = $"Column{i + 1}";
                hdr[i] = opts.GenerateColumnName?.Invoke(raw, path, i, def) as string ?? raw ?? def;
            }
            opts.Schema = hdr;
            return hdr;
        }

        string[] GenerateSyntheticSchema(List<string[]> samples, CsvReadOptions opts, string file)
        {
            int maxCols = samples.Count == 0 ? 0 : samples.Max(r => r.Length);
            if (maxCols == 0) return Array.Empty<string>();
            var cols = new string[maxCols];
            for (int i = 0; i < maxCols; i++)
            {
                cols[i] = opts.GenerateColumnName?.Invoke("", file, i, $"Column{i + 1}") as string ?? $"Column{i + 1}";
            }
            opts.Schema = cols;
            return cols;
        }

        void InferTypesIfRequested(List<string[]> samples, string[] sch, CsvReadOptions opts, string file)
        {
            if (opts.SchemaInferenceMode == SchemaInferenceMode.ColumnNamesOnly) return;

            int cols = sch.Length;
            var candidateLists = new List<Type>[cols];
            var failureCounts = new Dictionary<Type, int>[cols];

            Type[] precedence = new[]
            {
            typeof(bool), typeof(int), typeof(long), typeof(decimal),
            typeof(double), typeof(DateTime), typeof(Guid)
        };

            for (int c = 0; c < cols; c++)
            {
                candidateLists[c] = new List<Type>(precedence);
                failureCounts[c] = new Dictionary<Type, int>();
            }

            foreach (var row in samples)
            {
                for (int c = 0; c < cols; c++)
                {
                    string val = c < row.Length ? row[c] : "";
                    if (string.IsNullOrEmpty(val))
                        continue;

                    // Preservation rules eliminate some candidates
                    if (opts.PreserveNumericStringsWithLeadingZeros &&
                        val.Length > 1 && val[0] == '0' && AllDigits(val))
                    {
                        candidateLists[c].RemoveAll(t =>
                            t == typeof(int) || t == typeof(long) || t == typeof(decimal) || t == typeof(double));
                        continue;
                    }
                    if (opts.PreserveLargeIntegerStrings &&
                        val.Length > 18 && AllDigits(val))
                    {
                        candidateLists[c].RemoveAll(t =>
                            t == typeof(int) || t == typeof(long) || t == typeof(decimal) || t == typeof(double));
                        continue;
                    }

                    // Test remaining candidates
                    for (int k = candidateLists[c].Count - 1; k >= 0; k--)
                    {
                        var type = candidateLists[c][k];
                        if (!TryParseAs(val, type))
                        {
                            if (!failureCounts[c].TryGetValue(type, out var f))
                                f = 0;
                            f++;
                            failureCounts[c][type] = f;
                            if (f >= 2) // systematic
                                candidateLists[c].RemoveAt(k);
                        }
                    }
                }
            }

            var inferred = new Type[cols];
            for (int c = 0; c < cols; c++)
            {
                inferred[c] = candidateLists[c].FirstOrDefault() ?? typeof(string);
            }
            opts.InferredTypes = inferred;
        }

        bool YieldMapped(string[] rawFields)
        {
            if (options.Schema == null)
                return false;

            var schemaLocal = options.Schema;
            if (rawFields.Length > schemaLocal.Length && !options.AllowExtraFields)
            {
                if (!options.HandleError("CSV", options.Metrics.LinesRead, options.Metrics.RecordsRead, path,
                    "SchemaError", $"Row has {rawFields.Length} fields but schema has {schemaLocal.Length}.", string.Join(",", rawFields.Take(8))))
                    return false;
            }

            var values = new object?[schemaLocal.Length];
            int upTo = Math.Min(schemaLocal.Length, rawFields.Length);

            for (int i = 0; i < upTo; i++)
            {
                values[i] = options.ConvertFieldValue(rawFields[i], i);
            }
            for (int i = upTo; i < schemaLocal.Length; i++)
            {
                if (!options.AllowMissingTrailingFields)
                {
                    if (!options.HandleError("CSV", options.Metrics.LinesRead, options.Metrics.RecordsRead, path,
                        "SchemaError", $"Missing field '{schemaLocal[i]}'", ""))
                        return false;
                    return false;
                }
                values[i] = default;
            }

            try
            {
                CurrentInstance = DataFlow.Framework.ObjectMaterializer.Create<T>(schemaLocal, values);
                return CurrentInstance != null;
            }
            catch (Exception ex)
            {
                if (!options.HandleError("CSV", options.Metrics.LinesRead, options.Metrics.RecordsRead, path,
                    ex.GetType().Name, ex.Message, string.Join(",", rawFields.Take(8))))
                    return false;
                return false;
            }
        }

        static bool TryParseAs(string val, Type t) =>
            t == typeof(bool) ? bool.TryParse(val, out _) :
            t == typeof(int) ? int.TryParse(val, out _) :
            t == typeof(long) ? long.TryParse(val, out _) :
            t == typeof(decimal) ? decimal.TryParse(val, out _) :
            t == typeof(double) ? double.TryParse(val, out _) :
            t == typeof(DateTime) ? DateTime.TryParse(val, out _) :
            t == typeof(Guid) ? Guid.TryParse(val, out _) :
            false;

        static bool AllDigits(string s)
        {
            for (int i = 0; i < s.Length; i++)
                if (s[i] < '0' || s[i] > '9') return false;
            return true;
        }
    }


    public static IEnumerable<T> CsvSync<T>(string path, CsvReadOptions options)
    {
        if (options == null) throw new ArgumentNullException(nameof(options));
        options.FilePath = path;

        using var reader = new StreamReader(File.OpenRead(path));

        // Local state / helpers
        T? CurrentInstance = default;
        string[]? schema = options.Schema;
        bool headerConsumed = false;
        var inferenceBuffer = options.InferSchema ? new List<string[]>(options.SchemaInferenceSampleRows) : null;
        bool inferenceCompleted = !options.InferSchema;

        foreach (var rawFields in CsvRfc4180Parser.Parse(reader, options))
        {
            if (options.Metrics.TerminatedEarly) yield break;

            if (schema == null && options.HasHeader && !headerConsumed)
            {
                schema = ProcessHeader(rawFields, options);
                headerConsumed = true;
                continue;
            }

            if (schema == null && options.InferSchema && !headerConsumed && !options.HasHeader)
            {
                inferenceBuffer!.Add(rawFields);
                if (inferenceBuffer.Count >= options.SchemaInferenceSampleRows)
                {
                    schema = GenerateSyntheticSchema(inferenceBuffer, options, path);
                    InferTypesIfRequested(inferenceBuffer, schema, options, path);
                    inferenceCompleted = true;
                    foreach (var buffered in inferenceBuffer)
                        if (YieldMapped(buffered)) yield return CurrentInstance!;
                    inferenceBuffer.Clear();
                }
                continue;
            }

            if (options.InferSchema && !inferenceCompleted && schema != null)
            {
                inferenceBuffer!.Add(rawFields);
                if (inferenceBuffer.Count >= options.SchemaInferenceSampleRows)
                {
                    InferTypesIfRequested(inferenceBuffer, schema, options, path);
                    inferenceCompleted = true;
                    foreach (var buffered in inferenceBuffer)
                        if (YieldMapped(buffered)) yield return CurrentInstance!;
                    inferenceBuffer.Clear();
                }
                continue;
            }

            if (options.InferSchema && !inferenceCompleted)
            {
                inferenceBuffer!.Add(rawFields);
                continue;
            }

            if (schema == null)
            {
                if (!options.HandleError("CSV", options.Metrics.LinesRead, options.Metrics.RecordsRead, path,
                    "SchemaError", "Schema is null (no header and none supplied).", string.Join(",", rawFields)))
                    yield break;
                continue;
            }

            if (YieldMapped(rawFields))
                yield return CurrentInstance!;
            if (options.ShouldEmitProgress()) options.EmitProgress();
        }

        if (options.InferSchema && !inferenceCompleted)
        {
            if (schema == null)
                schema = GenerateSyntheticSchema(inferenceBuffer!, options, path);
            InferTypesIfRequested(inferenceBuffer!, schema, options, path);
            foreach (var buffered in inferenceBuffer!)
            {
                if (YieldMapped(buffered)) yield return CurrentInstance!;
                if (options.ShouldEmitProgress()) options.EmitProgress();
            }
        }

        options.Complete();


        string[] ProcessHeader(string[] headerRow, CsvReadOptions opts)
        {
            var hdr = new string[headerRow.Length];
            for (int i = 0; i < headerRow.Length; i++)
            {
                var raw = headerRow[i];
                var def = $"Column{i + 1}";
                hdr[i] = opts.GenerateColumnName?.Invoke(raw, path, i, def) as string ?? raw ?? def;
            }
            opts.Schema = hdr;
            return hdr;
        }

        string[] GenerateSyntheticSchema(List<string[]> samples, CsvReadOptions opts, string file)
        {
            int maxCols = samples.Count == 0 ? 0 : samples.Max(r => r.Length);
            if (maxCols == 0) return Array.Empty<string>();
            var cols = new string[maxCols];
            for (int i = 0; i < maxCols; i++)
                cols[i] = opts.GenerateColumnName?.Invoke("", file, i, $"Column{i + 1}") as string ?? $"Column{i + 1}";
            opts.Schema = cols;
            return cols;
        }

        void InferTypesIfRequested(List<string[]> samples, string[] sch, CsvReadOptions opts, string file)
        {
            if (opts.SchemaInferenceMode == SchemaInferenceMode.ColumnNamesOnly) return;

            int cols = sch.Length;
            var candidateLists = new List<Type>[cols];
            var failureCounts = new Dictionary<Type, int>[cols];
            Type[] precedence = new[]
            {
            typeof(bool), typeof(int), typeof(long), typeof(decimal),
            typeof(double), typeof(DateTime), typeof(Guid)
        };

            for (int c = 0; c < cols; c++)
            {
                candidateLists[c] = new List<Type>(precedence);
                failureCounts[c] = new Dictionary<Type, int>();
            }

            foreach (var row in samples)
            {
                for (int c = 0; c < cols; c++)
                {
                    string val = c < row.Length ? row[c] : "";
                    if (string.IsNullOrEmpty(val))
                        continue;

                    if (opts.PreserveNumericStringsWithLeadingZeros &&
                        val.Length > 1 && val[0] == '0' && AllDigits(val))
                    {
                        candidateLists[c].RemoveAll(t =>
                            t == typeof(int) || t == typeof(long) || t == typeof(decimal) || t == typeof(double));
                        continue;
                    }
                    if (opts.PreserveLargeIntegerStrings &&
                        val.Length > 18 && AllDigits(val))
                    {
                        candidateLists[c].RemoveAll(t =>
                            t == typeof(int) || t == typeof(long) || t == typeof(decimal) || t == typeof(double));
                        continue;
                    }

                    for (int k = candidateLists[c].Count - 1; k >= 0; k--)
                    {
                        var type = candidateLists[c][k];
                        if (!TryParseAs(val, type))
                        {
                            if (!failureCounts[c].TryGetValue(type, out var f))
                                f = 0;
                            f++;
                            failureCounts[c][type] = f;
                            if (f >= 2)
                                candidateLists[c].RemoveAt(k);
                        }
                    }
                }
            }

            var inferred = new Type[cols];
            for (int c = 0; c < cols; c++)
                inferred[c] = candidateLists[c].FirstOrDefault() ?? typeof(string);
            opts.InferredTypes = inferred;
        }

        bool YieldMapped(string[] rawFields)
        {
            if (options.Schema == null)
                return false;

            var schemaLocal = options.Schema;
            if (rawFields.Length > schemaLocal.Length && !options.AllowExtraFields)
            {
                if (!options.HandleError("CSV", options.Metrics.LinesRead, options.Metrics.RecordsRead, path,
                    "SchemaError", $"Row has {rawFields.Length} fields but schema has {schemaLocal.Length}.", string.Join(",", rawFields.Take(8))))
                    return false;
            }

            var values = new object?[schemaLocal.Length];
            int upTo = Math.Min(schemaLocal.Length, rawFields.Length);

            for (int i = 0; i < upTo; i++)
            {
                values[i] = options.ConvertFieldValue(rawFields[i], i);
            }
            for (int i = upTo; i < schemaLocal.Length; i++)
            {
                if (!options.AllowMissingTrailingFields)
                {
                    if (!options.HandleError("CSV", options.Metrics.LinesRead, options.Metrics.RecordsRead, path,
                        "SchemaError", $"Missing field '{schemaLocal[i]}'", ""))
                        return false;
                    return false;
                }
                values[i] = default;
            }

            try
            {
                CurrentInstance = DataFlow.Framework.ObjectMaterializer.Create<T>(schemaLocal, values);
                return CurrentInstance != null;
            }
            catch (Exception ex)
            {
                if (!options.HandleError("CSV", options.Metrics.LinesRead, options.Metrics.RecordsRead, path,
                    ex.GetType().Name, ex.Message, string.Join(",", rawFields.Take(8))))
                    return false;
                return false;
            }
        }

        static bool TryParseAs(string val, Type t) =>
            t == typeof(bool) ? bool.TryParse(val, out _) :
            t == typeof(int) ? int.TryParse(val, out _) :
            t == typeof(long) ? long.TryParse(val, out _) :
            t == typeof(decimal) ? decimal.TryParse(val, out _) :
            t == typeof(double) ? double.TryParse(val, out _) :
            t == typeof(DateTime) ? DateTime.TryParse(val, out _) :
            t == typeof(Guid) ? Guid.TryParse(val, out _) :
            false;

        static bool AllDigits(string s)
        {
            for (int i = 0; i < s.Length; i++)
                if (s[i] < '0' || s[i] > '9') return false;
            return true;
        }
    }
    // Simple CSV APIs
    public static IAsyncEnumerable<T> Csv<T>(string path, string separator = ",", Action<string, Exception>? onError = null, params string[] schema)
    {
        var options = new CsvReadOptions
        {
            Separator = separator.FirstOrDefault(','),
            Schema = schema == null || schema.Length == 0 ? null : schema,
            ErrorAction = onError == null ? ReaderErrorAction.Throw : ReaderErrorAction.Skip,
            ErrorSink = onError == null ? NullErrorSink.Instance : new DelegatingErrorSink(onError, path)
        };
        return Csv<T>(path, options);
    }

    public static IEnumerable<T> CsvSync<T>(string path, string separator = ",", Action<string, Exception>? onError = null, params string[] schema)
    {
        var options = new CsvReadOptions
        {
            Separator = separator.FirstOrDefault(','),
            Schema = schema == null || schema.Length == 0 ? null : schema,
            ErrorAction = onError == null ? ReaderErrorAction.Throw : ReaderErrorAction.Skip,
            ErrorSink = onError == null ? NullErrorSink.Instance : new DelegatingErrorSink(onError, path)
        };
        return CsvSync<T>(path, options);
    }

    // --- JSON (options-based) ---
    // true streaming JSON reader (no full file load)
    public static async IAsyncEnumerable<T> Json<T>(
        string path,
        JsonReadOptions<T> options,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        if (options == null) throw new ArgumentNullException(nameof(options));
        options.FilePath = path;

        if (options.ValidateElements && options.ElementValidator == null)
            throw new ArgumentException("ValidateElements is true but ElementValidator is null. Provide ElementValidator or disable ValidateElements.", nameof(options));

        var fileInfo = new FileInfo(path);
        long totalBytes = fileInfo.Exists ? fileInfo.Length : 0;

        var readerOptions = options.MaxDepth > 0
            ? new JsonReaderOptions { MaxDepth = options.MaxDepth, CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true }
            : new JsonReaderOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };

        const int BufferSize = 64 * 1024;
        byte[] buffer = new byte[BufferSize];
        int bytesRead;
        bool isFinalBlock;
        var state = new JsonReaderState(readerOptions);

        await using var fs = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            BufferSize,
            FileOptions.Asynchronous | FileOptions.SequentialScan);

        bytesRead = await fs.ReadAsync(buffer.AsMemory(0, buffer.Length), ct).ConfigureAwait(false);
        isFinalBlock = bytesRead == 0;
        if (isFinalBlock)
        {
            options.Complete();
            yield break; // empty file
        }

        bool rootDetermined = false;
        bool rootIsArray = false;
        bool rootFinished = false;
        long elementIndex = 0;

        bool fastPath = !(options.ValidateElements && options.ElementValidator != null);

        // Validation slow path tools
        ArrayBufferWriter<byte>? elementBuffer = null;
        Utf8JsonWriter? elementWriter = null;

        // Capture state
        bool capturing = false;
        int openContainers = 0;
        bool elementIsPrimitive = false;

        // Pending items to yield after releasing the reader (avoid ref struct across yield)
        List<T> pending = new List<T>(1);

        while (!rootFinished)
        {
            // Build a reader for current buffer slice
            var span = new ReadOnlySpan<byte>(buffer, 0, bytesRead);
            var reader = new Utf8JsonReader(span, isFinalBlock, state);

            bool producedThisIteration = false;

            while (reader.Read())
            {
                ct.ThrowIfCancellationRequested();
                options.CancellationToken.ThrowIfCancellationRequested();

                if (!rootDetermined)
                {
                    switch (reader.TokenType)
                    {
                        case JsonTokenType.StartArray:
                            rootIsArray = true;
                            rootDetermined = true;
                            continue;

                        case JsonTokenType.StartObject:
                        case JsonTokenType.String:
                        case JsonTokenType.Number:
                        case JsonTokenType.True:
                        case JsonTokenType.False:
                        case JsonTokenType.Null:
                            rootIsArray = false;
                            rootDetermined = true;

                            if (options.RequireArrayRoot && !options.AllowSingleObject)
                            {
                                options.HandleError("JSON", -1, -1, path,
                                    "JsonFormatError", "Root element is not an array.", "");
                                rootFinished = true;
                                break;
                            }

                            elementIndex = 1;

                            if (fastPath)
                            {
                                var valueReader = reader;
                                bool success = false;
                                T item = default;
                                try
                                {
                                    item = JsonSerializer.Deserialize<T>(ref valueReader, options.SerializerOptions)!;
                                    success = true;
                                    reader = valueReader;
                                }
                                catch (Exception ex)
                                {
                                    string excerpt = BuildExcerptAndSkip(ref reader, 128);
                                    if (!options.HandleError("JSON", -1, 1, path,
                                            ex.GetType().Name, ex.Message, excerpt))
                                    {
                                        rootFinished = true;
                                    }
                                }

                                if (success)
                                {
                                    options.Metrics.RecordsRead = 1;
                                    if (options.ShouldEmitProgress())
                                        options.EmitProgress(totalBytes, fs.CanSeek ? fs.Position : null);
                                    pending.Add(item);
                                    producedThisIteration = true;
                                }

                                rootFinished = true;
                            }
                            else
                            {
                                // Capture single non-array root
                                elementBuffer ??= new ArrayBufferWriter<byte>(8 * 1024);
                                elementWriter ??= new Utf8JsonWriter(elementBuffer);
                                elementBuffer.Clear();
                                capturing = true;
                                openContainers = 0;
                                elementIsPrimitive = false;

                                WriteTokenToElement(ref reader, elementWriter, ref openContainers, ref elementIsPrimitive);

                                while (capturing && reader.Read())
                                {
                                    WriteTokenToElement(ref reader, elementWriter, ref openContainers, ref elementIsPrimitive);
                                    if (elementIsPrimitive || openContainers == 0)
                                    {
                                        capturing = false;
                                        elementWriter.Flush();
                                    }
                                }

                                if (!capturing)
                                {
                                    if (TryFinalizeCapturedElement(elementBuffer, options, path, elementIndex, out var item))
                                    {
                                        pending.Add(item);
                                        producedThisIteration = true;
                                    }
                                }

                                rootFinished = true;
                            }
                            break;

                        default:
                            continue;
                    }

                    if (rootFinished || producedThisIteration) break;
                    if (!rootIsArray) break;
                    continue;
                }

                if (!rootIsArray)
                {
                    // Single value already processed
                    break;
                }

                if (reader.TokenType == JsonTokenType.EndArray)
                {
                    rootFinished = true;
                    break;
                }

                // Start of array element
                elementIndex++;

                if (fastPath)
                {
                    var valueReader = reader;
                    bool success = false;
                    T item = default;
                    try
                    {
                        item = JsonSerializer.Deserialize<T>(ref valueReader, options.SerializerOptions)!;
                        success = true;
                        reader = valueReader;
                    }
                    catch (Exception exElem)
                    {
                        string excerpt = BuildExcerptAndSkip(ref reader, 128);
                        if (!options.HandleError("JSON", -1, elementIndex, path,
                                exElem.GetType().Name, exElem.Message, excerpt))
                        {
                            rootFinished = true;
                            break;
                        }
                        continue; // skip malformed element
                    }

                    if (success)
                    {
                        options.Metrics.RecordsRead = elementIndex;
                        if (options.ShouldEmitProgress())
                            options.EmitProgress(totalBytes, fs.CanSeek ? fs.Position : null);
                        pending.Add(item);
                        producedThisIteration = true;
                        if (options.Metrics.TerminatedEarly)
                            rootFinished = true;
                    }
                }
                else
                {
                    // Validation path capture
                    capturing = true;
                    openContainers = 0;
                    elementIsPrimitive = false;

                    elementBuffer ??= new ArrayBufferWriter<byte>(8 * 1024);
                    elementBuffer.Clear();
                    elementWriter ??= new Utf8JsonWriter(elementBuffer);

                    WriteTokenToElement(ref reader, elementWriter, ref openContainers, ref elementIsPrimitive);

                    if (elementIsPrimitive)
                    {
                        capturing = false;
                        elementWriter.Flush();
                    }
                    else
                    {
                        while (capturing && reader.Read())
                        {
                            WriteTokenToElement(ref reader, elementWriter, ref openContainers, ref elementIsPrimitive);
                            if (openContainers == 0)
                            {
                                capturing = false;
                                elementWriter.Flush();
                            }
                        }
                    }

                    if (!capturing)
                    {
                        if (TryFinalizeCapturedElement(elementBuffer, options, path, elementIndex, out var item))
                        {
                            pending.Add(item);
                            producedThisIteration = true;
                            if (options.Metrics.TerminatedEarly)
                                rootFinished = true;
                        }
                    }
                }

                if (producedThisIteration) break; // yield ASAP (after state persistence below)
            }

            // Persist reader state (even if inner loop ended early)
            state = reader.CurrentState;
            int consumedBytes = (int)reader.BytesConsumed;

            // Shift remaining bytes to start of buffer
            int remaining = bytesRead - consumedBytes;
            if (remaining > 0)
            {
                Buffer.BlockCopy(buffer, consumedBytes, buffer, 0, remaining);
            }

            if (!rootFinished)
            {
                int read = await fs.ReadAsync(buffer.AsMemory(remaining, buffer.Length - remaining), ct).ConfigureAwait(false);
                bytesRead = remaining + read;
                isFinalBlock = read == 0;
                if (bytesRead == 0)
                    rootFinished = true;
            }

            // Now it's safe to yield (reader no longer in use)
            if (pending.Count > 0)
            {
                foreach (var itm in pending)
                    yield return itm;
                pending.Clear();
            }
        }

        options.Complete();
    }

    /// <summary>
    /// Finalize a captured element: validate (if required) and deserialize.
    /// Returns true + item when successful; false otherwise.
    /// </summary>
    private static bool TryFinalizeCapturedElement<T>(
        ArrayBufferWriter<byte> elementBuffer,
        JsonReadOptions<T> options,
        string path,
        long elementIndex,
        out T item)
    {
        item = default;
        bool success = false;

        try
        {
            // Existing local variables: elementBuffer (ArrayBufferWriter<byte>)
            ReadOnlyMemory<byte> mem = elementBuffer.WrittenMemory;

            // Build a ReadOnlySequence<byte> that (ideally) wraps the existing backing array without copying.
            ReadOnlySequence<byte> seq;
            if (MemoryMarshal.TryGetArray(mem, out ArraySegment<byte> segment) && segment.Array != null)
            {
                // Zero-copy single segment sequence over the written region.
                seq = new ReadOnlySequence<byte>(segment.Array, segment.Offset, segment.Count);
            }
            else
            {
                // Fallback (should almost never happen for ArrayBufferWriter): make one copy.
                seq = new ReadOnlySequence<byte>(mem.ToArray());
            }

            using var doc = JsonDocument.Parse(seq);  // Uses the sequence overload you actually have.
            var root = doc.RootElement;

            if (options.ValidateElements && options.ElementValidator != null)
            {
                bool valid;
                try
                {
                    valid = options.ElementValidator(root);
                }
                catch (Exception exVal)
                {
                    if (!options.HandleError("JSON", -1, elementIndex, path,
                            exVal.GetType().Name, exVal.Message,
                            Truncate(SafeGetRawText(root), 128)))
                        return false;
                    return false;
                }

                if (!valid)
                {
                    if (!options.HandleError("JSON", -1, elementIndex, path,
                            "ValidationFailed", "Element validator returned false.",
                            Truncate(SafeGetRawText(root), 128)))
                        return false;
                    return false;
                }
            }

            try
            {
                item = root.Deserialize<T>(options.SerializerOptions)!;
                success = true;
            }
            catch (Exception exDeser)
            {
                options.HandleError("JSON", -1, elementIndex, path,
                    exDeser.GetType().Name, exDeser.Message,
                    Truncate(SafeGetRawText(root), 128));
            }
        }
        catch (Exception exOuter)
        {
            options.HandleError("JSON", -1, elementIndex, path,
                exOuter.GetType().Name, exOuter.Message, "");
        }

        elementBuffer.Clear();
        return success;
    }


    /* Helper: build a small excerpt while skipping a value (starting with reader on first token of the value).
     * Returns truncated JSON-like text (best effort). Advances reader past the entire value. */
    private static string BuildExcerptAndSkip(ref Utf8JsonReader reader, int maxChars)
    {
        var sb = new StringBuilder(Math.Min(maxChars, 256));
        int startDepth = 0;
        bool firstToken = true;

        void Append(string s)
        {
            if (sb.Length >= maxChars) return;
            int space = maxChars - sb.Length;
            if (s.Length <= space) sb.Append(s);
            else sb.Append(s.AsSpan(0, space));
        }

        if (reader.TokenType == JsonTokenType.StartObject || reader.TokenType == JsonTokenType.StartArray)
            startDepth = 1;

        AppendToken(ref reader, Append, firstToken);
        firstToken = false;

        if (startDepth == 0)
            return sb.ToString();

        while (startDepth > 0 && reader.Read())
        {
            if (reader.TokenType == JsonTokenType.StartObject || reader.TokenType == JsonTokenType.StartArray)
                startDepth++;
            else if (reader.TokenType == JsonTokenType.EndObject || reader.TokenType == JsonTokenType.EndArray)
                startDepth--;

            AppendToken(ref reader, Append, firstToken);
            firstToken = false;
        }

        return sb.ToString();

        static void AppendToken(ref Utf8JsonReader r, Action<string> add, bool first)
        {
            switch (r.TokenType)
            {
                case JsonTokenType.StartObject: add("{"); break;
                case JsonTokenType.EndObject: add("}"); break;
                case JsonTokenType.StartArray: add("["); break;
                case JsonTokenType.EndArray: add("]"); break;
                case JsonTokenType.PropertyName:
                    if (!first) add(",");
                    add("\"" + JsonEscape(r.GetString()!) + "\":");
                    break;
                case JsonTokenType.String:
                    add("\"" + JsonEscape(r.GetString()!) + "\"");
                    break;
                case JsonTokenType.Number:
                    add(r.GetRawString());
                    break;
                case JsonTokenType.True: add("true"); break;
                case JsonTokenType.False: add("false"); break;
                case JsonTokenType.Null: add("null"); break;
            }
        }
    }

    private static string Truncate(string? s, int max)
    {
        if (string.IsNullOrEmpty(s)) return "";
        return s.Length <= max ? s : s.Substring(0, max);
    }

        private static string SafeGetRawText(JsonElement e)
    {
        try { return e.GetRawText(); }
        catch { return ""; }
    }

    //public static async IAsyncEnumerable<T> Json<T>(string path, JsonReadOptions<T> options, [EnumeratorCancellation] CancellationToken ct = default)
    //{
    //    if (options == null) throw new ArgumentNullException(nameof(options));
    //    options.FilePath = path;

    //    var fileInfo = new FileInfo(path);
    //    long totalBytes = fileInfo.Exists ? fileInfo.Length : 0;

    //    // Prepare serializer options with MaxDepth if requested
    //    JsonSerializerOptions serializerOptions = options.SerializerOptions;
    //    if (options.MaxDepth > 0 && serializerOptions.MaxDepth != options.MaxDepth)
    //    {
    //        // Clone to avoid mutating a shared instance
    //        serializerOptions = new JsonSerializerOptions(serializerOptions)
    //        {
    //            MaxDepth = options.MaxDepth
    //        };
    //    }

    //    // Open stream in async sequential mode
    //    await using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read,
    //                                        bufferSize: 64 * 1024,
    //                                        FileOptions.Asynchronous | FileOptions.SequentialScan);

    //    // Peek first non-whitespace char to decide array vs single object
    //    int firstByte;
    //    do
    //    {
    //        firstByte = fs.ReadByte();
    //    } while (firstByte != -1 && char.IsWhiteSpace((char)firstByte));

    //    if (firstByte == -1)
    //    {
    //        // Empty file: just complete
    //        options.Complete();
    //        yield break;
    //    }

    //    // Rewind one byte (we consumed one lookahead)
    //    fs.Position -= 1;

    //    if ((char)firstByte == '[')
    //    {
    //        // Root is an array => streaming elements
    //        long elementIndex = 0;

    //        // NOTE: DeserializeAsyncEnumerable<T> only works when T is known.
    //        // We use JsonElement first for validation & custom handling, then deserialize to T.
    //        IAsyncEnumerable<JsonElement> elementStream =
    //            JsonSerializer.DeserializeAsyncEnumerable<JsonElement>(fs, serializerOptions, ct);

    //        await foreach (var element in elementStream.WithCancellation(ct))
    //        {
    //            ct.ThrowIfCancellationRequested();
    //            options.CancellationToken.ThrowIfCancellationRequested();

    //            elementIndex++;

    //            bool success = false;
    //            T item = default;

    //            try
    //            {
    //                // Validation (optional)
    //                if (options.ValidateElements && options.ElementValidator != null)
    //                {
    //                    bool valid;
    //                    try
    //                    {
    //                        valid = options.ElementValidator(element);
    //                    }
    //                    catch (Exception exVal)
    //                    {
    //                        if (!options.HandleError("JSON", -1, elementIndex, path,
    //                                exVal.GetType().Name, exVal.Message, Truncate(element.GetRawText(), 128)))
    //                            break;
    //                        continue;
    //                    }

    //                    if (!valid)
    //                    {
    //                        if (!options.HandleError("JSON", -1, elementIndex, path,
    //                                "ValidationFailed", "Element validator returned false.",
    //                                Truncate(element.GetRawText(), 128)))
    //                            break;
    //                        continue;
    //                    }
    //                }

    //                // Deserialize into target type T
    //                try
    //                {
    //                    item = element.Deserialize<T>(serializerOptions)!;
    //                    success = true;
    //                }
    //                catch (Exception exDeser)
    //                {
    //                    if (!options.HandleError("JSON", -1, elementIndex, path,
    //                            exDeser.GetType().Name, exDeser.Message,
    //                            Truncate(element.GetRawText(), 128)))
    //                        break;
    //                }
    //            }
    //            catch (Exception exOuter)
    //            {
    //                if (!options.HandleError("JSON", -1, elementIndex, path,
    //                        exOuter.GetType().Name, exOuter.Message, ""))
    //                    break;
    //            }

    //            if (success)
    //            {
    //                options.Metrics.RecordsRead = elementIndex;
    //                if (options.ShouldEmitProgress())
    //                {
    //                    // fs.Position is approximate progress
    //                    options.EmitProgress(totalBytes, fs.CanSeek ? fs.Position : null);
    //                }
    //                yield return item;
    //                if (options.Metrics.TerminatedEarly) break;
    //            }
    //        }
    //    }
    //    else
    //    {
    //        // Single object scenario
    //        if (options.RequireArrayRoot && !options.AllowSingleObject)
    //        {
    //            options.HandleError("JSON", -1, -1, path,
    //                "JsonFormatError", "Root element is not an array.", "");
    //            options.Complete();
    //            yield break;
    //        }

    //        bool success = false;
    //        T item = default;

    //        try
    //        {
    //            using var doc = await JsonDocument.ParseAsync(fs, new JsonDocumentOptions
    //            {
    //                MaxDepth = options.MaxDepth > 0 ? options.MaxDepth : 64
    //            }, ct);

    //            var root = doc.RootElement;

    //            if (options.ValidateElements && options.ElementValidator != null)
    //            {
    //                bool valid;
    //                try
    //                {
    //                    valid = options.ElementValidator(root);
    //                }
    //                catch (Exception exVal)
    //                {
    //                    options.HandleError("JSON", -1, 1, path,
    //                        exVal.GetType().Name, exVal.Message, "");
    //                    options.Complete();
    //                    yield break;
    //                }

    //                if (!valid)
    //                {
    //                    options.HandleError("JSON", -1, 1, path,
    //                        "ValidationFailed", "Single object failed validation.", "");
    //                    options.Complete();
    //                    yield break;
    //                }
    //            }

    //            try
    //            {
    //                item = root.Deserialize<T>(serializerOptions)!;
    //                success = true;
    //            }
    //            catch (Exception exDeser)
    //            {
    //                options.HandleError("JSON", -1, 1, path,
    //                    exDeser.GetType().Name, exDeser.Message, "");
    //            }
    //        }
    //        catch (Exception exOuter)
    //        {
    //            options.HandleError("JSON", -1, 1, path,
    //                exOuter.GetType().Name, exOuter.Message, "");
    //        }

    //        if (success)
    //        {
    //            options.Metrics.RecordsRead = 1;
    //            if (options.ShouldEmitProgress())
    //                options.EmitProgress(totalBytes, fs.CanSeek ? fs.Position : null);
    //            yield return item;
    //        }
    //    }

    //    options.Complete();
    //}


    // Synchronous streaming JSON (incremental Utf8JsonReader)
    public static IEnumerable<T> JsonSync<T>(
        string path,
        JsonReadOptions<T> options,
        CancellationToken cancellationToken = default)
    {
        if (options == null) throw new ArgumentNullException(nameof(options));
        options.FilePath = path;

        if (options.ValidateElements && options.ElementValidator == null)
            throw new ArgumentException("ValidateElements is true but ElementValidator is null. Provide ElementValidator or disable ValidateElements.", nameof(options));

        var fileInfo = new FileInfo(path);
        long totalBytes = fileInfo.Exists ? fileInfo.Length : 0;

        var readerOptions = options.MaxDepth > 0
            ? new JsonReaderOptions { MaxDepth = options.MaxDepth, CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true }
            : new JsonReaderOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };

        const int BufferSize = 64 * 1024;
        byte[] buffer = new byte[BufferSize];
        int bytesRead;
        bool isFinalBlock;
        var state = new JsonReaderState(readerOptions);

        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read,
                                      BufferSize, FileOptions.SequentialScan);

        bytesRead = fs.Read(buffer, 0, buffer.Length);
        isFinalBlock = bytesRead == 0;
        if (isFinalBlock)
        {
            options.Complete();
            yield break; // empty file
        }

        bool rootDetermined = false;
        bool rootIsArray = false;
        bool rootFinished = false;
        long elementIndex = 0;

        bool fastPath = !(options.ValidateElements && options.ElementValidator != null);

        // Validation (slow) path capture tools
        ArrayBufferWriter<byte>? elementBuffer = null;
        Utf8JsonWriter? elementWriter = null;

        // Capture state
        bool capturing = false;
        int openContainers = 0;
        bool elementIsPrimitive = false;

        // Pending items to yield once reader is out of scope
        List<T> pending = new List<T>(1);

        while (!rootFinished)
        {
            var span = new ReadOnlySpan<byte>(buffer, 0, bytesRead);
            var reader = new Utf8JsonReader(span, isFinalBlock, state);

            bool producedThisIteration = false;

            while (reader.Read())
            {
                cancellationToken.ThrowIfCancellationRequested();
                options.CancellationToken.ThrowIfCancellationRequested();

                if (!rootDetermined)
                {
                    switch (reader.TokenType)
                    {
                        case JsonTokenType.StartArray:
                            rootIsArray = true;
                            rootDetermined = true;
                            continue;

                        case JsonTokenType.StartObject:
                        case JsonTokenType.String:
                        case JsonTokenType.Number:
                        case JsonTokenType.True:
                        case JsonTokenType.False:
                        case JsonTokenType.Null:
                            rootIsArray = false;
                            rootDetermined = true;

                            if (options.RequireArrayRoot && !options.AllowSingleObject)
                            {
                                options.HandleError("JSON", -1, -1, path,
                                    "JsonFormatError", "Root element is not an array.", "");
                                rootFinished = true;
                                break;
                            }

                            elementIndex = 1;

                            if (fastPath)
                            {
                                var valueReader = reader; // copy
                                bool success = false;
                                T item = default;
                                try
                                {
                                    item = JsonSerializer.Deserialize<T>(ref valueReader, options.SerializerOptions)!;
                                    success = true;
                                    reader = valueReader; // advance original
                                }
                                catch (Exception ex)
                                {
                                    string excerpt = BuildExcerptAndSkip(ref reader, 128);
                                    if (!options.HandleError("JSON", -1, 1, path,
                                            ex.GetType().Name, ex.Message, excerpt))
                                    {
                                        rootFinished = true;
                                    }
                                }

                                if (success)
                                {
                                    options.Metrics.RecordsRead = 1;
                                    if (options.ShouldEmitProgress())
                                        options.EmitProgress(totalBytes, fs.CanSeek ? fs.Position : null);
                                    pending.Add(item);
                                    producedThisIteration = true;
                                }

                                rootFinished = true;
                            }
                            else
                            {
                                // Capture entire single root value
                                elementBuffer ??= new ArrayBufferWriter<byte>(8 * 1024);
                                elementWriter ??= new Utf8JsonWriter(elementBuffer);
                                elementBuffer.Clear();
                                capturing = true;
                                openContainers = 0;
                                elementIsPrimitive = false;

                                WriteTokenToElement(ref reader, elementWriter, ref openContainers, ref elementIsPrimitive);

                                while (capturing && reader.Read())
                                {
                                    WriteTokenToElement(ref reader, elementWriter, ref openContainers, ref elementIsPrimitive);
                                    if (elementIsPrimitive || openContainers == 0)
                                    {
                                        capturing = false;
                                        elementWriter.Flush();
                                    }
                                }

                                if (!capturing)
                                {
                                    if (TryFinalizeCapturedElement(elementBuffer, options, path, elementIndex, out var item))
                                    {
                                        options.Metrics.RecordsRead = 1;
                                        if (options.ShouldEmitProgress())
                                            options.EmitProgress(totalBytes, fs.CanSeek ? fs.Position : null);
                                        pending.Add(item);
                                        producedThisIteration = true;
                                    }
                                }

                                rootFinished = true;
                            }
                            break;

                        default:
                            continue;
                    }

                    if (rootFinished || producedThisIteration) break;
                    if (!rootIsArray) break;
                    continue;
                }

                if (!rootIsArray)
                {
                    break; // single value case already handled
                }

                if (reader.TokenType == JsonTokenType.EndArray)
                {
                    rootFinished = true;
                    break;
                }

                // Start of an array element
                elementIndex++;

                if (fastPath)
                {
                    var valueReader = reader; // copy
                    bool success = false;
                    T item = default;
                    try
                    {
                        item = JsonSerializer.Deserialize<T>(ref valueReader, options.SerializerOptions)!;
                        success = true;
                        reader = valueReader;
                    }
                    catch (Exception exElem)
                    {
                        string excerpt = BuildExcerptAndSkip(ref reader, 128);
                        if (!options.HandleError("JSON", -1, elementIndex, path,
                                exElem.GetType().Name, exElem.Message, excerpt))
                        {
                            rootFinished = true;
                            break;
                        }
                        continue; // skip malformed element
                    }

                    if (success)
                    {
                        options.Metrics.RecordsRead = elementIndex;
                        if (options.ShouldEmitProgress())
                            options.EmitProgress(totalBytes, fs.CanSeek ? fs.Position : null);
                        pending.Add(item);
                        producedThisIteration = true;
                        if (options.Metrics.TerminatedEarly)
                            rootFinished = true;
                    }
                }
                else
                {
                    // Validation path: capture the element
                    capturing = true;
                    openContainers = 0;
                    elementIsPrimitive = false;

                    elementBuffer ??= new ArrayBufferWriter<byte>(8 * 1024);
                    elementBuffer.Clear();
                    elementWriter ??= new Utf8JsonWriter(elementBuffer);

                    WriteTokenToElement(ref reader, elementWriter, ref openContainers, ref elementIsPrimitive);

                    if (elementIsPrimitive)
                    {
                        capturing = false;
                        elementWriter.Flush();
                    }
                    else
                    {
                        while (capturing && reader.Read())
                        {
                            WriteTokenToElement(ref reader, elementWriter, ref openContainers, ref elementIsPrimitive);
                            if (openContainers == 0)
                            {
                                capturing = false;
                                elementWriter.Flush();
                            }
                        }
                    }

                    if (!capturing)
                    {
                        if (TryFinalizeCapturedElement(elementBuffer, options, path, elementIndex, out var item))
                        {
                            options.Metrics.RecordsRead = elementIndex;
                            if (options.ShouldEmitProgress())
                                options.EmitProgress(totalBytes, fs.CanSeek ? fs.Position : null);
                            pending.Add(item);
                            producedThisIteration = true;
                            if (options.Metrics.TerminatedEarly)
                                rootFinished = true;
                        }
                    }
                }

                if (producedThisIteration) break; // produce at most one per pass for immediate streaming
            }

            // Persist state & shift buffer before yielding
            state = reader.CurrentState;
            int consumed = (int)reader.BytesConsumed;

            int remaining = bytesRead - consumed;
            if (remaining > 0)
            {
                Buffer.BlockCopy(buffer, consumed, buffer, 0, remaining);
            }

            if (!rootFinished)
            {
                int read = fs.Read(buffer, remaining, buffer.Length - remaining);
                bytesRead = remaining + read;
                isFinalBlock = read == 0;
                if (bytesRead == 0)
                    rootFinished = true;
            }

            // Safe to yield pending items (reader out of scope)
            if (pending.Count > 0)
            {
                foreach (var itm in pending)
                    yield return itm;
                pending.Clear();
            }
        }

        options.Complete();
    }


    //// Writes a single token from reader into element writer (validation path) and updates element state
    private static void WriteTokenToElement(ref Utf8JsonReader reader,
                                            Utf8JsonWriter writer,
                                            ref int openContainers,
                                            ref bool elementIsPrimitive)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.StartObject:
                writer.WriteStartObject();
                openContainers++;
                break;
            case JsonTokenType.EndObject:
                writer.WriteEndObject();
                openContainers--;
                break;
            case JsonTokenType.StartArray:
                writer.WriteStartArray();
                openContainers++;
                break;
            case JsonTokenType.EndArray:
                writer.WriteEndArray();
                openContainers--;
                break;
            case JsonTokenType.PropertyName:
                writer.WritePropertyName(reader.GetString());
                break;
            case JsonTokenType.String:
                writer.WriteStringValue(reader.GetString());
                elementIsPrimitive = openContainers == 0;
                break;
            case JsonTokenType.Number:
#if NET8_0_OR_GREATER
                // Preserve exact numeric text
                writer.WriteRawValue(reader.ValueSpan, skipInputValidation: true);
#else
            if (reader.TryGetInt64(out long l))
                writer.WriteNumberValue(l);
            else if (reader.TryGetDecimal(out decimal dec))
                writer.WriteNumberValue(dec);
            else if (reader.TryGetDouble(out double dbl))
                writer.WriteNumberValue(dbl);
            else
                writer.WriteStringValue(reader.GetString());
#endif
                elementIsPrimitive = openContainers == 0;
                break;
            case JsonTokenType.True:
                writer.WriteBooleanValue(true);
                elementIsPrimitive = openContainers == 0;
                break;
            case JsonTokenType.False:
                writer.WriteBooleanValue(false);
                elementIsPrimitive = openContainers == 0;
                break;
            case JsonTokenType.Null:
                writer.WriteNullValue();
                elementIsPrimitive = openContainers == 0;
                break;
            default:
                break;
        }
    }
  
    private static string JsonEscape(string s)
    {
        // Minimal fast-path escape for excerpt (not full JSON spec)
        if (string.IsNullOrEmpty(s)) return s;
        var sb = new StringBuilder(s.Length + 4);
        foreach (char c in s)
        {
            switch (c)
            {
                case '\\': sb.Append("\\\\"); break;
                case '"': sb.Append("\\\""); break;
                case '\r': sb.Append("\\r"); break;
                case '\n': sb.Append("\\n"); break;
                case '\t': sb.Append("\\t"); break;
                default:
                    if (c < 32)
                        sb.Append("\\u").Append(((int)c).ToString("x4"));
                    else
                        sb.Append(c);
                    break;
            }
        }
        return sb.ToString();
    }

    // Extension: get raw numeric text (Utf8JsonReader does not expose a direct method; ValueSpan safe for numbers)
    private static string GetRawString(this ref Utf8JsonReader reader)
    {
        return reader.HasValueSequence
            ? Encoding.UTF8.GetString(reader.ValueSequence.ToArray())
            : Encoding.UTF8.GetString(reader.ValueSpan);
    }


    public static async IAsyncEnumerable<T> Json<T>(string path, JsonSerializerOptions? options = null, Action<Exception>? onError = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var readOptions = new JsonReadOptions<T>
        {
            SerializerOptions = options ?? new JsonSerializerOptions { PropertyNameCaseInsensitive = true },
            ErrorAction = onError == null ? ReaderErrorAction.Throw : ReaderErrorAction.Skip,
            ErrorSink = onError == null ? NullErrorSink.Instance : new DelegatingErrorSink(e => onError(e), path)
        };
        await foreach (var item in Json<T>(path, readOptions, cancellationToken))
            yield return item;
    }

    public static IEnumerable<T> JsonSync<T>(string path, JsonSerializerOptions? options = null, Action<Exception>? onError = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var readOptions = new JsonReadOptions<T>
        {
            SerializerOptions = options ?? new JsonSerializerOptions { PropertyNameCaseInsensitive = true },
            ErrorAction = onError == null ? ReaderErrorAction.Throw : ReaderErrorAction.Skip,
            ErrorSink = onError == null ? NullErrorSink.Instance : new DelegatingErrorSink(e => onError(e), path)
        };
        foreach (var item in JsonSync<T>(path, readOptions, cancellationToken))
            yield return item;
    }

    // --- YAML (options-based) ---

    public static async IAsyncEnumerable<T> Yaml<T>(string path, YamlReadOptions<T> options, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (options == null) throw new ArgumentNullException(nameof(options));
        options.FilePath = path;

        using var reader = new StreamReader(path);
        var parser = new YamlDotNet.Core.Parser(reader);
        parser.Consume<YamlDotNet.Core.Events.StreamStart>();

        var deserializerBuilder = new YamlDotNet.Serialization.DeserializerBuilder()
            .WithNamingConvention(YamlDotNet.Serialization.NamingConventions.CamelCaseNamingConvention.Instance);

        if (options.DisallowAliases)
        {
            deserializerBuilder = deserializerBuilder.IgnoreUnmatchedProperties();
        }

        var deserializer = deserializerBuilder.Build();
        long record = 0;

        async IAsyncEnumerable<T> SequenceMode()
        {
            if (parser.Accept<YamlDotNet.Core.Events.SequenceStart>(out _))
            {
                parser.Consume<YamlDotNet.Core.Events.SequenceStart>();
                while (!parser.Accept<YamlDotNet.Core.Events.SequenceEnd>(out _))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    options.CancellationToken.ThrowIfCancellationRequested();
                    record++;

                    bool success = false;
                    T item = default;

                    try
                    {
                        item = deserializer.Deserialize<T>(parser);

                        if (options.RestrictTypes && !IsAllowed(options, item))
                        {
                            if (!options.HandleError("YAML", options.Metrics.LinesRead, record, path, "TypeRestriction",
                                    "Deserialized object type not allowed.", item?.GetType().FullName ?? "null"))
                                yield break;
                        }
                        else
                        {
                            success = true;
                        }
                    }
                    catch (Exception ex)
                    {
                        if (!options.HandleError("YAML", options.Metrics.LinesRead, record, path, ex.GetType().Name, ex.Message, ""))
                            yield break;
                    }

                    if (success)
                    {
                        options.Metrics.RecordsRead = record;
                        yield return item;
                        if (options.ShouldEmitProgress()) options.EmitProgress();
                        if (options.Metrics.TerminatedEarly) yield break;
                    }
                }
                parser.Consume<YamlDotNet.Core.Events.SequenceEnd>();
            }
            else
            {
                while (parser.Accept<YamlDotNet.Core.Events.DocumentStart>(out _))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    options.CancellationToken.ThrowIfCancellationRequested();
                    record++;

                    bool success = false;
                    T item = default;

                    try
                    {
                        parser.Consume<YamlDotNet.Core.Events.DocumentStart>();
                        item = deserializer.Deserialize<T>(parser);

                        if (options.RestrictTypes && !IsAllowed(options, item))
                        {
                            if (!options.HandleError("YAML", options.Metrics.LinesRead, record, path, "TypeRestriction",
                                    "Deserialized object type not allowed.", item?.GetType().FullName ?? "null"))
                                yield break;
                        }
                        else
                        {
                            success = true;
                        }

                        if (parser.Accept<YamlDotNet.Core.Events.DocumentEnd>(out _))
                            parser.Consume<YamlDotNet.Core.Events.DocumentEnd>();
                    }
                    catch (Exception ex)
                    {
                        if (!options.HandleError("YAML", options.Metrics.LinesRead, record, path, ex.GetType().Name, ex.Message, ""))
                            yield break;

                        // Skip to next document end
                        while (!parser.Accept<YamlDotNet.Core.Events.DocumentEnd>(out _) &&
                               !parser.Accept<YamlDotNet.Core.Events.StreamEnd>(out _))
                        {
                            if (!parser.MoveNext()) break;
                        }
                        if (parser.Accept<YamlDotNet.Core.Events.DocumentEnd>(out _))
                            parser.Consume<YamlDotNet.Core.Events.DocumentEnd>();
                    }

                    if (success)
                    {
                        options.Metrics.RecordsRead = record;
                        yield return item;
                        if (options.ShouldEmitProgress()) options.EmitProgress();
                        if (options.Metrics.TerminatedEarly) yield break;
                    }
                }
            }

            await Task.CompletedTask;
        }

        await foreach (var item in SequenceMode())
            yield return item;

        options.Complete();
    }

    // Simple YAML API
    public static async IAsyncEnumerable<T> Yaml<T>(string path, YamlDotNet.Serialization.IDeserializer? deserializer = null, Action<Exception>? onError = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var opts = new YamlReadOptions<T>
        {
            ErrorAction = onError == null ? ReaderErrorAction.Throw : ReaderErrorAction.Skip,
            ErrorSink = onError == null ? NullErrorSink.Instance : new DelegatingErrorSink(e => onError(e), path)
        };
        await foreach (var item in Yaml<T>(path, opts, cancellationToken))
            yield return item;
    }

    private static bool IsAllowed<T>(YamlReadOptions<T> options, T item)
    {
        if (!options.RestrictTypes) return true;
        if (item == null) return true;
        var t = item.GetType();
        if (options.AllowedTypes == null)
            return t == typeof(T);
        return options.AllowedTypes.Contains(t);
    }


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
