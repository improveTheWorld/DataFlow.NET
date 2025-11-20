using DataFlow.Extensions;
using System.Runtime.CompilerServices;
using System.Text;
using DataFlow.Framework;


namespace DataFlow.Data;

/// <summary>
/// Provides static methods for lazily reading data from various file formats,
/// with full support for both synchronous (IEnumerable) and asynchronous (IAsyncEnumerable) streaming.
/// The method sync/async suffixes convention is inverted (default is asynchronous) to encourage the asynchronous file reading reflex.
/// Simple API for nominal cases + Option-based APIs: Csv / CsvSync, Json, Yaml.
/// 
/// CSV Reading Behavior:
/// 
/// LENIENT MODE (default):
/// - Empty strings for numeric fields → default values (0, 0.0, etc.)
/// - Missing trailing fields → default values
/// - Invalid conversions → field kept as string, no error
/// 
/// STRICT MODE:
/// - Set AllowMissingTrailingFields = false
/// - Set ErrorAction = Throw
/// - Invalid conversions will throw InvalidDataException
/// 
/// Example:
/// CSV: "Name,Age\nJohn,\n"
/// 
/// Lenient: { Name = "John", Age = 0 }  ← Empty age becomes 0
/// Strict:  Throws InvalidDataException  ← Empty age causes error
/// </summary>
public static partial class Read
{
   
    // ==========================================
    // CSV from string (SYNC - with Sync suffix)
    // ==========================================

    /// <summary>
    /// Parse CSV content from a string (synchronous, options-based).
    /// </summary>
    public static IEnumerable<T> AsCsvSync<T>(
        string csvContent,
        CsvReadOptions options,
        CancellationToken cancellationToken = default)
    {
        if (csvContent == null) throw new ArgumentNullException(nameof(csvContent));

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csvContent));
        foreach (var item in CsvSync<T>(stream, options, filePath: "(string)", cancellationToken))
            yield return item;
    }

    /// <summary>
    /// Parse CSV content from a string (synchronous, simple overload).
    /// </summary>
    public static IEnumerable<T> AsCsvSync<T>(
        string csvContent,
        string separator = ",",
        Action<string, Exception>? onError = null,
        CancellationToken cancellationToken = default,
        params string[] schema)
    {
        if (csvContent == null) throw new ArgumentNullException(nameof(csvContent));

        var options = new CsvReadOptions
        {
            Separator = separator.FirstOrDefault(','),
            Schema = schema == null || schema.Length == 0 ? null : schema,
            ErrorAction = onError == null ? ReaderErrorAction.Throw : ReaderErrorAction.Skip,
            ErrorSink = onError == null ? NullErrorSink.Instance : new DelegatingErrorSink(onError, "(string)")
        };

        return AsCsvSync<T>(csvContent, options, cancellationToken);
    }

    // ---------------------------------------------------------
    // PUBLIC OPTION-BASED ASYNC (FILE)  -> delegates to stream
    // ---------------------------------------------------------
    public static async IAsyncEnumerable<T> Csv<T>(string path, CsvReadOptions options, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Path cannot be null or empty.", nameof(path));

        if (!File.Exists(path))
            throw new FileNotFoundException($"CSV file not found: {path}", path);

        await using var fs = File.OpenRead(path);
        await foreach (var rec in Csv<T>(fs, options, filePath: path, cancellationToken))
            yield return rec;
    }

    // ---------------------------------------------------------
    // PUBLIC OPTION-BASED ASYNC (STREAM)
    // ---------------------------------------------------------
    public static IAsyncEnumerable<T> Csv<T>(Stream stream, CsvReadOptions options, string? filePath = null, CancellationToken cancellationToken = default)
    {
        if (options == null) throw new ArgumentNullException(nameof(options));
        options.FilePath = filePath ?? options.FilePath ?? StreamPseudoPath;
        var reader = new StreamReader(stream, leaveOpen: true);
        return CsvCoreAsync<T>(reader, options, options.FilePath!, cancellationToken);
    }

    // ---------------------------------------------------------
    // PUBLIC OPTION-BASED SYNC (FILE) -> delegates
    // ---------------------------------------------------------
    public static IEnumerable<T> CsvSync<T>(string path, CsvReadOptions options, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Path cannot be null or empty.", nameof(path));

        if (!File.Exists(path))
            throw new FileNotFoundException($"CSV file not found: {path}", path);

        using var fs = File.OpenRead(path);
        foreach (var row in CsvSync<T>(fs, options, filePath: path, cancellationToken))
            yield return row;
    }

    // ---------------------------------------------------------
    // PUBLIC OPTION-BASED SYNC (STREAM)
    // ---------------------------------------------------------
    public static IEnumerable<T> CsvSync<T>(Stream stream, CsvReadOptions options, string? filePath = null, CancellationToken cancellationToken = default)
    {
        if (options == null) throw new ArgumentNullException(nameof(options));
        options.FilePath = filePath ?? options.FilePath ?? StreamPseudoPath;
        using var reader = new StreamReader(stream, leaveOpen: true);
        foreach (var r in CsvCoreSync<T>(reader, options, options.FilePath!, cancellationToken))
            yield return r;
    }

    // ---------------------------------------------------------
    // SIMPLE CSV (async) existing
    // ---------------------------------------------------------
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

    // ---------------------------------------------------------
    // SIMPLE CSV with CancellationToken (NEW)
    // ---------------------------------------------------------
    public static IAsyncEnumerable<T> Csv<T>(string path, string separator, Action<string, Exception>? onError, CancellationToken cancellationToken, params string[] schema)
        => Csv<T>(path,
            new CsvReadOptions
            {
                Separator = separator.FirstOrDefault(','),
                Schema = schema == null || schema.Length == 0 ? null : schema,
                ErrorAction = onError == null ? ReaderErrorAction.Throw : ReaderErrorAction.Skip,
                ErrorSink = onError == null ? NullErrorSink.Instance : new DelegatingErrorSink(onError, path)
            },
            cancellationToken);

    public static IEnumerable<T> CsvSync<T>(string path, string separator, Action<string, Exception>? onError, CancellationToken cancellationToken, params string[] schema)
        => CsvSync<T>(path,
            new CsvReadOptions
            {
                Separator = separator.FirstOrDefault(','),
                Schema = schema == null || schema.Length == 0 ? null : schema,
                ErrorAction = onError == null ? ReaderErrorAction.Throw : ReaderErrorAction.Skip,
                ErrorSink = onError == null ? NullErrorSink.Instance : new DelegatingErrorSink(onError, path)
            },
            cancellationToken);

    // ---------------------------------------------------------
    // SIMPLE CSV (STREAM) async/sync
    // ---------------------------------------------------------
    public static IAsyncEnumerable<T> Csv<T>(Stream stream, string separator = ",", Action<string, Exception>? onError = null, string? filePath = null, CancellationToken cancellationToken = default, params string[] schema)
        => Csv<T>(
            stream,
            new CsvReadOptions
            {
                Separator = separator.FirstOrDefault(','),
                Schema = schema == null || schema.Length == 0 ? null : schema,
                ErrorAction = onError == null ? ReaderErrorAction.Throw : ReaderErrorAction.Skip,
                ErrorSink = onError == null ? NullErrorSink.Instance : new DelegatingErrorSink(onError, filePath ?? StreamPseudoPath)
            },
            filePath,
            cancellationToken);

    public static IEnumerable<T> CsvSync<T>(Stream stream, string separator = ",", Action<string, Exception>? onError = null, string? filePath = null, CancellationToken cancellationToken = default, params string[] schema)
        => CsvSync<T>(
            stream,
            new CsvReadOptions
            {
                Separator = separator.FirstOrDefault(','),
                Schema = schema == null || schema.Length == 0 ? null : schema,
                ErrorAction = onError == null ? ReaderErrorAction.Throw : ReaderErrorAction.Skip,
                ErrorSink = onError == null ? NullErrorSink.Instance : new DelegatingErrorSink(onError, filePath ?? StreamPseudoPath)
            },
            filePath,
            cancellationToken);

    // =========================================================
    // INTERNAL CORE (Async)
    // =========================================================
    private static async IAsyncEnumerable<T> CsvCoreAsync<T>(
        TextReader reader,
        CsvReadOptions options,
        string filePath,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        GeneralMaterializationSession<T>? session = null;
        T? current = default;
        string[]? schema = options.Schema;
        bool headerConsumed = false;
        var inferenceBuffer = options.InferSchema ? new List<string[]>(options.SchemaInferenceSampleRows) : null;
        bool inferenceCompleted = !options.InferSchema;

        await foreach (var rawFields in CsvRfc4180Parser.ParseAsync(reader, options, ct: ct))
        {
            ct.ThrowIfCancellationRequested();
            options.CancellationToken.ThrowIfCancellationRequested();

            if (schema == null && options.HasHeader && !headerConsumed)
            {
                schema = ProcessHeader(rawFields);
                headerConsumed = true;
                continue;
            }

            if (schema == null && options.InferSchema && !headerConsumed && !options.HasHeader)
            {
                inferenceBuffer!.Add(rawFields);
                if (inferenceBuffer.Count >= options.SchemaInferenceSampleRows)
                {
                    schema = GenerateSyntheticSchema(inferenceBuffer);
                    InferTypesIfRequested(inferenceBuffer, schema);
                    inferenceCompleted = true;
                    foreach (var buffered in inferenceBuffer)
                        if (YieldMapped(buffered)) { options.Metrics.RecordsEmitted++; yield return current!; }
                    inferenceBuffer.Clear();
                }
                continue;
            }

            if (options.InferSchema && !inferenceCompleted && schema != null)
            {
                inferenceBuffer!.Add(rawFields);
                if (inferenceBuffer.Count >= options.SchemaInferenceSampleRows)
                {
                    InferTypesIfRequested(inferenceBuffer, schema);
                    inferenceCompleted = true;
                    foreach (var buffered in inferenceBuffer)
                        if (YieldMapped(buffered)) { options.Metrics.RecordsEmitted++; yield return current!; }
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
                if (!options.HandleError("CSV", options.Metrics.LinesRead, options.Metrics.RawRecordsParsed, filePath,
                        "SchemaError", "Schema is null (no header and none supplied).", string.Join(",", rawFields)))
                    yield break;
                continue;
            }

            if (YieldMapped(rawFields))
            {
                options.Metrics.RecordsEmitted++;
                yield return current!;
            }

            if (options.Metrics.TerminatedEarly && options.ErrorAction == ReaderErrorAction.Stop)
                yield break;
        }

        if (options.InferSchema && !inferenceCompleted)
        {
            if (schema == null)
                schema = GenerateSyntheticSchema(inferenceBuffer!);
            InferTypesIfRequested(inferenceBuffer!, schema);
            foreach (var buffered in inferenceBuffer!)
            {
                ct.ThrowIfCancellationRequested();
                options.CancellationToken.ThrowIfCancellationRequested();
                if (YieldMapped(buffered))
                {
                    options.Metrics.RecordsEmitted++;
                    yield return current!;
                }
            }
        }

        options.Complete();

        // ---- Local helpers (async core) ----
        string[] ProcessHeader(string[] headerRow)
        {
            var hdr = new string[headerRow.Length];
            for (int i = 0; i < headerRow.Length; i++)
            {
                var raw = headerRow[i];
                var def = $"Column{i + 1}";
                hdr[i] = options.GenerateColumnName?.Invoke(raw, filePath, i) ?? raw ?? def;
            }
            options.Schema = hdr;
            return hdr;
        }

        string[] GenerateSyntheticSchema(List<string[]> samples)
        {
            int maxCols = samples.Count == 0 ? 0 : samples.Max(r => r.Length);
            if (maxCols == 0) return Array.Empty<string>();
            var cols = new string[maxCols];
            for (int i = 0; i < maxCols; i++)
                cols[i] = options.GenerateColumnName?.Invoke("", filePath, i) ?? $"Column{i + 1}";
            options.Schema = cols;
            return cols;
        }

        void InferTypesIfRequested(List<string[]> samples, string[] sch)
        {
            if (options.SchemaInferenceMode == SchemaInferenceMode.ColumnNamesOnly) return;
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
                ct.ThrowIfCancellationRequested();
                options.CancellationToken.ThrowIfCancellationRequested();
                for (int c = 0; c < cols; c++)
                {
                    string val = c < row.Length ? row[c] : "";
                    if (string.IsNullOrEmpty(val)) continue;
                    if (options.PreserveNumericStringsWithLeadingZeros &&
                        val.Length > 1 && val[0] == '0' && val.IsAllDigits())
                    {
                        candidateLists[c].RemoveAll(t =>
                            t == typeof(int) || t == typeof(long) || t == typeof(decimal) || t == typeof(double));
                        continue;
                    }
                    if (options.PreserveLargeIntegerStrings && val.Length > 18 && val.IsAllDigits())
                    {
                        candidateLists[c].RemoveAll(t =>
                            t == typeof(int) || t == typeof(long) || t == typeof(decimal) || t == typeof(double));
                        continue;
                    }
                    for (int k = candidateLists[c].Count - 1; k >= 0; k--)
                    {
                        var type = candidateLists[c][k];
                        if (!val.TryParseAs( type, out _))
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
            options.InferredTypes = inferred;
            options.InferredTypeFinalized = new bool[inferred.Length];
        }

        bool YieldMapped(string[] rawFields)
        {
            if (options.Schema == null)
                return false;
            var schemaLocal = options.Schema;
            if (rawFields.Length > schemaLocal.Length && !options.AllowExtraFields)
            {
                if (!options.HandleError("CSV", options.Metrics.LinesRead, options.Metrics.RawRecordsParsed, filePath,
                        "SchemaError", $"Row has {rawFields.Length} fields but schema has {schemaLocal.Length}.",
                        string.Join(",", rawFields.Take(8))))
                    return false;
            }
            var values = new object?[schemaLocal.Length];
            int upTo = Math.Min(schemaLocal.Length, rawFields.Length);
            for (int i = 0; i < upTo; i++)
            {
                ct.ThrowIfCancellationRequested();
                options.CancellationToken.ThrowIfCancellationRequested();
                values[i] = options.ConvertFieldValue(rawFields[i], i);
            }
            for (int i = upTo; i < schemaLocal.Length; i++)
            {
                if (!options.AllowMissingTrailingFields)
                {
                    if (!options.HandleError("CSV", options.Metrics.LinesRead, options.Metrics.RawRecordsParsed, filePath,
                            "SchemaError", $"Missing field '{schemaLocal[i]}'", ""))
                        return false;
                    return false;
                }
                values[i] = default;
            }
            try
            {
                if (session == null)
                {
                    session = ObjectMaterializer.CreateGeneralSession<T>(schemaLocal);
                }
                current = session.Create(values);
                return current != null;
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                if (!options.HandleError("CSV", options.Metrics.LinesRead, options.Metrics.RawRecordsParsed, filePath,
                        ex.GetType().Name, ex.Message, string.Join(",", rawFields.Take(8))))
                    return false;
                return false;
            }
        }
    }

    // =========================================================
    // INTERNAL CORE (Sync)
    // =========================================================
    private static IEnumerable<T> CsvCoreSync<T>(
        TextReader reader,
        CsvReadOptions options,
        string filePath,
        CancellationToken ct)
    {
        GeneralMaterializationSession<T>? session = null;
        T? current = default;
        string[]? schema = options.Schema;
        bool headerConsumed = false;
        var inferenceBuffer = options.InferSchema ? new List<string[]>(options.SchemaInferenceSampleRows) : null;
        bool inferenceCompleted = !options.InferSchema;

        foreach (var rawFields in CsvRfc4180Parser.Parse(reader, options, ct: ct))
        {
            ct.ThrowIfCancellationRequested();
            options.CancellationToken.ThrowIfCancellationRequested();

            if (schema == null && options.HasHeader && !headerConsumed)
            {
                schema = ProcessHeader(rawFields);
                headerConsumed = true;
                continue;
            }

            if (schema == null && options.InferSchema && !headerConsumed && !options.HasHeader)
            {
                inferenceBuffer!.Add(rawFields);
                if (inferenceBuffer.Count >= options.SchemaInferenceSampleRows)
                {
                    schema = GenerateSyntheticSchema(inferenceBuffer);
                    InferTypesIfRequested(inferenceBuffer, schema);
                    inferenceCompleted = true;
                    foreach (var buffered in inferenceBuffer)
                        if (YieldMapped(buffered)) { options.Metrics.RecordsEmitted++; yield return current!; }
                    inferenceBuffer.Clear();
                }
                continue;
            }

            if (options.InferSchema && !inferenceCompleted && schema != null)
            {
                inferenceBuffer!.Add(rawFields);
                if (inferenceBuffer.Count >= options.SchemaInferenceSampleRows)
                {
                    InferTypesIfRequested(inferenceBuffer, schema);
                    inferenceCompleted = true;
                    foreach (var buffered in inferenceBuffer)
                        if (YieldMapped(buffered)) { options.Metrics.RecordsEmitted++; yield return current!; }
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
                if (!options.HandleError("CSV", options.Metrics.LinesRead, options.Metrics.RawRecordsParsed, filePath,
                        "SchemaError", "Schema is null (no header and none supplied).", string.Join(",", rawFields)))
                    yield break;
                continue;
            }

            if (YieldMapped(rawFields))
            {
                options.Metrics.RecordsEmitted++;
                yield return current!;
            }

            if (options.Metrics.TerminatedEarly) yield break;
        }

        if (options.InferSchema && !inferenceCompleted)
        {
            if (schema == null)
                schema = GenerateSyntheticSchema(inferenceBuffer!);
            InferTypesIfRequested(inferenceBuffer!, schema);
            foreach (var buffered in inferenceBuffer!)
            {
                ct.ThrowIfCancellationRequested();
                options.CancellationToken.ThrowIfCancellationRequested();
                if (YieldMapped(buffered))
                {
                    options.Metrics.RecordsEmitted++;
                    yield return current!;
                }
            }
        }

        options.Complete();

        // Local helpers identical to async core (duplicated minimally)
        string[] ProcessHeader(string[] headerRow)
        {
            var hdr = new string[headerRow.Length];
            for (int i = 0; i < headerRow.Length; i++)
            {
                var raw = headerRow[i];
                var def = $"Column{i + 1}";
                hdr[i] = options.GenerateColumnName?.Invoke(raw, filePath, i) ?? raw ?? def;
            }
            options.Schema = hdr;
            return hdr;
        }

        string[] GenerateSyntheticSchema(List<string[]> samples)
        {
            int maxCols = samples.Count == 0 ? 0 : samples.Max(r => r.Length);
            if (maxCols == 0) return Array.Empty<string>();
            var cols = new string[maxCols];
            for (int i = 0; i < maxCols; i++)
                cols[i] = options.GenerateColumnName?.Invoke("", filePath, i) ?? $"Column{i + 1}";
            options.Schema = cols;
            return cols;
        }

        void InferTypesIfRequested(List<string[]> samples, string[] sch)
        {
            if (options.SchemaInferenceMode == SchemaInferenceMode.ColumnNamesOnly) return;
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
                    if (string.IsNullOrEmpty(val)) continue;
                    if (options.PreserveNumericStringsWithLeadingZeros &&
                        val.Length > 1 && val[0] == '0' && val.IsAllDigits())
                    {
                        candidateLists[c].RemoveAll(t =>
                            t == typeof(int) || t == typeof(long) || t == typeof(decimal) || t == typeof(double));
                        continue;
                    }
                    if (options.PreserveLargeIntegerStrings && val.Length > 18 && val.IsAllDigits())
                    {
                        candidateLists[c].RemoveAll(t =>
                            t == typeof(int) || t == typeof(long) || t == typeof(decimal) || t == typeof(double));
                        continue;
                    }
                    for (int k = candidateLists[c].Count - 1; k >= 0; k--)
                    {
                        var type = candidateLists[c][k];
                        if (!val.TryParseAs(type, out _))
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
            options.InferredTypes = inferred;
            options.InferredTypeFinalized = new bool[inferred.Length];
        }

        bool YieldMapped(string[] rawFields)
        {
            if (options.Schema == null) return false;
            var schemaLocal = options.Schema;
            if (rawFields.Length > schemaLocal.Length && !options.AllowExtraFields)
            {
                if (!options.HandleError("CSV", options.Metrics.LinesRead, options.Metrics.RawRecordsParsed, filePath,
                        "SchemaError", $"Row has {rawFields.Length} fields but schema has {schemaLocal.Length}.",
                        string.Join(",", rawFields.Take(8))))
                    return false;
            }
            var values = new object?[schemaLocal.Length];
            int upTo = Math.Min(schemaLocal.Length, rawFields.Length);
            for (int i = 0; i < upTo; i++)
            {
                ct.ThrowIfCancellationRequested();
                options.CancellationToken.ThrowIfCancellationRequested();
                values[i] = options.ConvertFieldValue(rawFields[i], i);
            }
            for (int i = upTo; i < schemaLocal.Length; i++)
            {
                if (!options.AllowMissingTrailingFields)
                {
                    if (!options.HandleError("CSV", options.Metrics.LinesRead, options.Metrics.RawRecordsParsed, filePath,
                            "SchemaError", $"Missing field '{schemaLocal[i]}'", ""))
                        return false;
                    return false;
                }
                values[i] = default;
            }
            try
            {
                if (session == null)
                {
                    session = ObjectMaterializer.CreateGeneralSession<T>(schemaLocal);
                }
                current = session.Create(values);
                return current != null;
            }
            catch (OperationCanceledException) { throw; }   
            catch (Exception ex)
            {
                if (!options.HandleError("CSV", options.Metrics.LinesRead, options.Metrics.RawRecordsParsed, filePath,
                        ex.GetType().Name, ex.Message, string.Join(",", rawFields.Take(8))))
                    return false;
                return false;
            }
        }
    } 
  
}

