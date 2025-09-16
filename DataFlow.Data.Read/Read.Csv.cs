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

        var inferenceBuffer = options.InferSchema ? new List<string[]>(options.SchemaInferenceSampleRows) : null;
        bool inferenceCompleted = !options.InferSchema;

        await foreach (var rawFields in CsvRfc4180Parser.ParseAsync(reader, options, cancellationToken))
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
                        if (YieldMapped(buffered)) { options.Metrics.RecordsEmitted++; yield return CurrentInstance!; }
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
                        if (YieldMapped(buffered)) { options.Metrics.RecordsEmitted++; yield return CurrentInstance!; }
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
                if (!options.HandleError("CSV", options.Metrics.LinesRead, options.Metrics.RawRecordsParsed, path,
                    "SchemaError", "Schema is null (no header and none supplied).", string.Join(",", rawFields)))
                    yield break;
                continue;
            }

            if (YieldMapped(rawFields))
            {
                options.Metrics.RecordsEmitted++;
                yield return CurrentInstance!;
            }
        }

        if (options.InferSchema && !inferenceCompleted)
        {
            if (schema == null)
                schema = GenerateSyntheticSchema(inferenceBuffer!, options, path);

            InferTypesIfRequested(inferenceBuffer!, schema, options, path);

            foreach (var buffered in inferenceBuffer!)
            {
                if (YieldMapped(buffered))
                {
                    options.Metrics.RecordsEmitted++;
                    yield return CurrentInstance!;
                }
                // Removed per-record progress emission here as well.
            }
        }

        options.Complete();

        // ---- Local helper functions restored ----

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
            // Initialize finalization tracking array for demotion bookkeeping.
            opts.InferredTypeFinalized = new bool[inferred.Length];
        }

        bool YieldMapped(string[] rawFields)
        {
            if (options.Schema == null)
                return false;

            var schemaLocal = options.Schema;
            if (rawFields.Length > schemaLocal.Length && !options.AllowExtraFields)
            {
                if (!options.HandleError("CSV", options.Metrics.LinesRead, options.Metrics.RawRecordsParsed, path,
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
                    if (!options.HandleError("CSV", options.Metrics.LinesRead, options.Metrics.RawRecordsParsed, path,
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
                if (!options.HandleError("CSV", options.Metrics.LinesRead, options.Metrics.RawRecordsParsed, path,
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
                        if (YieldMapped(buffered))
                        {
                            options.Metrics.RecordsEmitted++;
                            yield return CurrentInstance!;
                        }
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
                        if (YieldMapped(buffered))
                        {
                            options.Metrics.RecordsEmitted++;
                            yield return CurrentInstance!;
                        }
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
                if (!options.HandleError("CSV", options.Metrics.LinesRead, options.Metrics.RawRecordsParsed, path,
                    "SchemaError", "Schema is null (no header and none supplied).", string.Join(",", rawFields)))
                    yield break;
                continue;
            }

            if (YieldMapped(rawFields))
            {
                options.Metrics.RecordsEmitted++;
                yield return CurrentInstance!;
            }
        }

        if (options.InferSchema && !inferenceCompleted)
        {
            if (schema == null)
                schema = GenerateSyntheticSchema(inferenceBuffer!, options, path);
            InferTypesIfRequested(inferenceBuffer!, schema, options, path);
            foreach (var buffered in inferenceBuffer!)
            {
                if (YieldMapped(buffered))
                {
                    options.Metrics.RecordsEmitted++;
                    yield return CurrentInstance!;
                }
                // Progress emission removed.
            }
        }

        options.Complete();

        // ---- Local helper functions restored ----

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
            opts.InferredTypeFinalized = new bool[inferred.Length];
        }

        bool YieldMapped(string[] rawFields)
        {
            if (options.Schema == null)
                return false;

            var schemaLocal = options.Schema;
            if (rawFields.Length > schemaLocal.Length && !options.AllowExtraFields)
            {
                if (!options.HandleError("CSV", options.Metrics.LinesRead, options.Metrics.RawRecordsParsed, path,
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
                    if (!options.HandleError("CSV", options.Metrics.LinesRead, options.Metrics.RawRecordsParsed, path,
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
                if (!options.HandleError("CSV", options.Metrics.LinesRead, options.Metrics.RawRecordsParsed, path,
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

 

}
