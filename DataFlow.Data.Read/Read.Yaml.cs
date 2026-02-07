using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using DataFlow.Framework;

namespace DataFlow;

/// <summary>
/// Provides static methods for lazily reading data from various file formats,
/// with full support for both synchronous (IEnumerable) and asynchronous (IAsyncEnumerable) streaming.
/// The method sync/async suffixes convention is inverted (default is asynchronous) to encourage the asynchronous file reading reflex.
/// Simple API for nominal cases + Option-based APIs: Csv / CsvSync, Json, Yaml.
/// </summary>
public static partial class Read
{
    internal sealed class CancellableTextReader : TextReader
    {
        private readonly TextReader _inner;
        private readonly CancellationToken _ct;
        private int _counter;
        private readonly int _checkEvery;
        public CancellableTextReader(TextReader inner, CancellationToken ct, int checkEvery = 4096)
        {
            _inner = inner;
            _ct = ct;
            _checkEvery = checkEvery <= 0 ? 4096 : checkEvery;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Probe()
        {
            if (++_counter % _checkEvery == 0)
                _ct.ThrowIfCancellationRequested();
        }
        public override int Read(char[] buffer, int index, int count)
        {
            var read = _inner.Read(buffer, index, count);
            Probe();
            return read;
        }
        public override int Read(Span<char> buffer)
        {
            var read = _inner.Read(buffer);
            Probe();
            return read;
        }
        public override int Peek()
        {
            var val = _inner.Peek();
            Probe();
            return val;
        }
        public override ValueTask<int> ReadAsync(Memory<char> buffer, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _ct.ThrowIfCancellationRequested();
            var read = Read(buffer.Span);
            return new ValueTask<int>(read);
        }
        protected override void Dispose(bool disposing)
        {

            base.Dispose(disposing);
        }
    }



    // --- YAML (options-based) ---

    /// <summary>
    /// core async YAML reader from caller stream (not disposed). File overload delegates here.
    /// </summary>
    public static async IAsyncEnumerable<T> Yaml<T>(
        Stream stream,
        YamlReadOptions<T> options,
        string? filePath = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (options == null) throw new ArgumentNullException(nameof(options));
        options.FilePath = filePath ?? options.FilePath ?? StreamPseudoPath;

        using var reader = new StreamReader(stream, leaveOpen: true);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, options.CancellationToken);
        using var cancellableReader = new CancellableTextReader(reader, linked.Token);
        var baseParser = new YamlDotNet.Core.Parser(cancellableReader);
        if (!baseParser.MoveNext() || baseParser.Current is not YamlDotNet.Core.Events.StreamStart)
            throw new InvalidDataException("YAML: Missing StreamStart event.");

        var secureParser = new SecurityFilteringParser<T>(baseParser, options);

        // BUG-002 FIX: Use case-insensitive property matching to align with JSON behavior
        var deserializerBuilder = new YamlDotNet.Serialization.DeserializerBuilder()
            .WithCaseInsensitivePropertyMatching()
            .IgnoreUnmatchedProperties();
        var deserializer = deserializerBuilder.Build();

        // NET-006 FIX: Detect record types (no parameterless constructor).
        // For these types, deserialize to Dictionary then use ObjectMaterializer.
        bool useRecordPath = !ObjectMaterializer.TryGetParameterlessFactory<T>(out _)
                             && typeof(T) != typeof(string)
                             && !typeof(T).IsValueType;


        long record = 0;
        bool sequenceRootChecked = false;
        bool sequenceRootMode = false;
        bool firstDocumentStartConsumed = false;

        while (!options.Metrics.TerminatedEarly && secureParser.Accept<YamlDotNet.Core.Events.StreamEnd>(out _) == false)
        {
            cancellationToken.ThrowIfCancellationRequested();
            options.CancellationToken.ThrowIfCancellationRequested();

            if (!sequenceRootChecked)
            {
                sequenceRootChecked = true;
                if (secureParser.Accept<YamlDotNet.Core.Events.DocumentStart>(out _))
                {
                    secureParser.MoveNext();
                    firstDocumentStartConsumed = true;
                    if (secureParser.Accept<YamlDotNet.Core.Events.SequenceStart>(out _))
                    {
                        secureParser.MoveNext();
                        sequenceRootMode = true;
                    }
                }
                else if (secureParser.Accept<YamlDotNet.Core.Events.SequenceStart>(out _))
                {
                    secureParser.MoveNext();
                    sequenceRootMode = true;
                }
            }

            if (sequenceRootMode)
            {
                if (secureParser.Accept<YamlDotNet.Core.Events.SequenceEnd>(out _))
                {
                    secureParser.MoveNext();
                    if (secureParser.Accept<YamlDotNet.Core.Events.DocumentEnd>(out _))
                        secureParser.MoveNext();
                    break;
                }
            }
            else
            {
                if (!firstDocumentStartConsumed)
                {
                    if (!secureParser.Accept<YamlDotNet.Core.Events.DocumentStart>(out _))
                        break;
                    secureParser.MoveNext();
                }
                firstDocumentStartConsumed = false;
            }

            record++;
            options.Metrics.RawRecordsParsed = record;

            bool success = false;
            T item = default!;
            //bool stopAfterCurrent = false;  
            try
            {
                if (useRecordPath)
                {
                    // NET-006 FIX: Deserialize to Dictionary, then use ObjectMaterializer
                    var dict = deserializer.Deserialize<Dictionary<string, object>>(secureParser);
                    if (dict != null)
                    {
                        var schema = dict.Keys.ToArray();
                        var rawValues = dict.Values.Select(v => v ?? (object)"").ToArray();
                        var values = ConvertYamlValues<T>(schema, rawValues);
                        try
                        {
                            item = ObjectMaterializer.Create<T>(schema, values);
                        }
                        catch (InvalidOperationException)
                        {
                            // Materialization failure from partial data (e.g., after security filter skip).
                            // The root cause was already reported by the parser. Just skip this item.
                            continue;
                        }
                    }
                }
                else
                {
                    item = deserializer.Deserialize<T>(secureParser);
                }

                if (options.RestrictTypes && !IsAllowed(options, item))
                {
                    // HandleError may set TerminatedEarly (Stop mode)
                    if (!options.HandleError("YAML", -1, record, options.FilePath!, "TypeRestriction",
                            "Deserialized object type not allowed.", item?.GetType().FullName ?? "null"))
                        yield break;
                }
                else
                {
                    success = true;
                }
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) when (ex is YamlDotNet.Core.YamlException || ex is InvalidDataException || (useRecordPath && ex is InvalidOperationException))
            {
                // If ErrorAction is Throw, propagate the exception to the caller
                if (options.ErrorAction == ReaderErrorAction.Throw)
                    throw;

                if (options.Metrics.TerminatedEarly)
                    yield break;
                if (!options.HandleError("YAML", -1, record, options.FilePath!,
                        ex.GetType().Name, ex.Message, ""))
                    yield break;

                if (sequenceRootMode)
                    SecurityFilteringParser<T>.ResyncFailedSequenceElement(secureParser);
                else
                    SecurityFilteringParser<T>.SkipDocument(secureParser);
            }

            if (!sequenceRootMode && secureParser.Accept<YamlDotNet.Core.Events.DocumentEnd>(out _))
                secureParser.MoveNext();

            if (success && !options.Metrics.TerminatedEarly)
            {
                options.Metrics.RecordsEmitted++;
                yield return item!;
                if (options.ShouldEmitProgress()) options.EmitProgress();
            }


            if (options.Metrics.TerminatedEarly)
                yield break;
        }

        if (!options.Metrics.TerminatedEarly)
        {
            if (cancellationToken.IsCancellationRequested || options.CancellationToken.IsCancellationRequested)
                throw new OperationCanceledException(cancellationToken.IsCancellationRequested ? cancellationToken : options.CancellationToken);
            options.Complete();
        }
    }

    public static async IAsyncEnumerable<T> Yaml<T>(string path, YamlReadOptions<T> options, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // Ensure the FileStream is disposed after enumeration (fix for test file lock failures)
        await using var fs = File.OpenRead(path);
        await foreach (var item in Yaml<T>(fs, options, filePath: path, cancellationToken))
            yield return item;
    }


    // Simple YAML API
    public static async IAsyncEnumerable<T> Yaml<T>(string path, Action<Exception>? onError = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var opts = new YamlReadOptions<T>
        {
            ErrorAction = onError == null ? ReaderErrorAction.Throw : ReaderErrorAction.Skip,
            ErrorSink = onError == null ? NullErrorSink.Instance : new DelegatingErrorSink(e => onError(e), path)
        };
        await foreach (var item in Yaml<T>(path, opts, cancellationToken))
            yield return item;
    }

    /// <summary>
    /// Reads YAML content from a stream using default options (async).
    /// </summary>
    /// <typeparam name="T">The type to deserialize each YAML document/element into.</typeparam>
    /// <param name="stream">The input stream to read from (not disposed by this method).</param>
    /// <param name="onError">Optional error handler. If provided, errors are skipped; otherwise thrown.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An async enumerable of deserialized items.</returns>
    public static async IAsyncEnumerable<T> Yaml<T>(
        Stream stream,
        Action<Exception>? onError = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var opts = new YamlReadOptions<T>
        {
            ErrorAction = onError == null ? ReaderErrorAction.Throw : ReaderErrorAction.Skip,
            ErrorSink = onError == null ? NullErrorSink.Instance : new DelegatingErrorSink(e => onError(e), StreamPseudoPath)
        };
        await foreach (var item in Yaml<T>(stream, opts, filePath: null, cancellationToken))
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

    // --- YAML (synchronous option-based) ---
    public static IEnumerable<T> YamlSync<T>(
        Stream stream,
        YamlReadOptions<T> options,
        string? filePath = null,
        CancellationToken cancellationToken = default)
    {
        if (options == null) throw new ArgumentNullException(nameof(options));
        options.FilePath = filePath ?? options.FilePath ?? StreamPseudoPath;

        using var reader = new StreamReader(stream, leaveOpen: true);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, options.CancellationToken);
        using var cancellableReader = new CancellableTextReader(reader, linked.Token);
        var baseParser = new YamlDotNet.Core.Parser(cancellableReader);
        if (!baseParser.MoveNext() || baseParser.Current is not YamlDotNet.Core.Events.StreamStart)
            throw new InvalidDataException("YAML: Missing StreamStart event.");

        var secureParser = new SecurityFilteringParser<T>(baseParser, options);

        // BUG-002 FIX: Use case-insensitive property matching to align with JSON behavior
        var deserializerBuilder = new YamlDotNet.Serialization.DeserializerBuilder()
            .WithCaseInsensitivePropertyMatching()
            .IgnoreUnmatchedProperties();
        var deserializer = deserializerBuilder.Build();

        // NET-006 FIX: Detect record types (no parameterless constructor).
        bool useRecordPath = !ObjectMaterializer.TryGetParameterlessFactory<T>(out _)
                             && typeof(T) != typeof(string)
                             && !typeof(T).IsValueType;


        long record = 0;
        bool sequenceRootChecked = false;
        bool sequenceRootMode = false;
        bool firstDocumentStartConsumed = false;

        while (!options.Metrics.TerminatedEarly && secureParser.Accept<YamlDotNet.Core.Events.StreamEnd>(out _) == false)
        {
            cancellationToken.ThrowIfCancellationRequested();
            options.CancellationToken.ThrowIfCancellationRequested();

            if (!sequenceRootChecked)
            {
                sequenceRootChecked = true;
                if (secureParser.Accept<YamlDotNet.Core.Events.DocumentStart>(out _))
                {
                    secureParser.MoveNext();
                    firstDocumentStartConsumed = true;
                    if (secureParser.Accept<YamlDotNet.Core.Events.SequenceStart>(out _))
                    {
                        secureParser.MoveNext();
                        sequenceRootMode = true;
                    }
                }
                else if (secureParser.Accept<YamlDotNet.Core.Events.SequenceStart>(out _))
                {
                    secureParser.MoveNext();
                    sequenceRootMode = true;
                }
            }

            if (sequenceRootMode)
            {
                if (secureParser.Accept<YamlDotNet.Core.Events.SequenceEnd>(out _))
                {
                    secureParser.MoveNext();
                    if (secureParser.Accept<YamlDotNet.Core.Events.DocumentEnd>(out _))
                        secureParser.MoveNext();
                    break;
                }
            }
            else
            {
                if (!firstDocumentStartConsumed)
                {
                    if (!secureParser.Accept<YamlDotNet.Core.Events.DocumentStart>(out _))
                        break;
                    secureParser.MoveNext();
                }
                firstDocumentStartConsumed = false;
            }

            record++;
            options.Metrics.RawRecordsParsed = record;

            bool success = false;
            T item = default!;

            try
            {
                if (useRecordPath)
                {
                    // NET-006 FIX: Deserialize to Dictionary, then use ObjectMaterializer
                    var dict = deserializer.Deserialize<Dictionary<string, object>>(secureParser);
                    if (dict != null)
                    {
                        var schema = dict.Keys.ToArray();
                        var rawValues = dict.Values.Select(v => v ?? (object)"").ToArray();
                        var values = ConvertYamlValues<T>(schema, rawValues);
                        try
                        {
                            item = ObjectMaterializer.Create<T>(schema, values);
                        }
                        catch (InvalidOperationException)
                        {
                            // Materialization failure from partial data — root cause already reported.
                            continue;
                        }
                    }
                }
                else
                {
                    item = deserializer.Deserialize<T>(secureParser);
                }

                if (options.RestrictTypes && !IsAllowed(options, item))
                {
                    if (!options.HandleError("YAML", -1, record, options.FilePath!,
                            "TypeRestriction",
                            "Deserialized object type not allowed.",
                            item?.GetType().FullName ?? "null"))
                        yield break;
                }
                else
                {
                    success = true;
                }
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) when (ex is YamlDotNet.Core.YamlException || (useRecordPath && ex is InvalidOperationException))
            {
                if (options.Metrics.TerminatedEarly)
                    yield break;
                if (!options.HandleError("YAML", -1, record, options.FilePath!,
                        ex.GetType().Name, ex.Message, ""))
                    yield break;

                if (sequenceRootMode)
                    SecurityFilteringParser<T>.ResyncFailedSequenceElement(secureParser);
                else
                    SecurityFilteringParser<T>.SkipDocument(secureParser);
            }

            if (!sequenceRootMode && secureParser.Accept<YamlDotNet.Core.Events.DocumentEnd>(out _))
                secureParser.MoveNext();

            if (success && !options.Metrics.TerminatedEarly)
            {
                options.Metrics.RecordsEmitted++;
                yield return item;
                if (options.ShouldEmitProgress()) options.EmitProgress();
            }

            if (options.Metrics.TerminatedEarly)
                yield break;
        }

        if (!options.Metrics.TerminatedEarly)
        {
            if (cancellationToken.IsCancellationRequested || options.CancellationToken.IsCancellationRequested)
                throw new OperationCanceledException(cancellationToken.IsCancellationRequested ? cancellationToken : options.CancellationToken);
            options.Complete();
        }
    }

    public static IEnumerable<T> YamlSync<T>(
        string path,
        YamlReadOptions<T> options,
        CancellationToken cancellationToken = default)
    {
        using var fs = File.OpenRead(path);
        foreach (var item in YamlSync<T>(fs, options, filePath: path, cancellationToken))
            yield return item;
    }

    // --- YAML (synchronous simple overload) ---
    public static IEnumerable<T> YamlSync<T>(
        string path,
        Action<Exception>? onError = null,
        CancellationToken cancellationToken = default)
    {
        var opts = new YamlReadOptions<T>
        {
            ErrorAction = onError == null ? ReaderErrorAction.Throw : ReaderErrorAction.Skip,
            ErrorSink = onError == null
                ? NullErrorSink.Instance
                : new DelegatingErrorSink(e => onError(e), path)
        };
        foreach (var item in YamlSync<T>(path, opts, cancellationToken))
            yield return item;
    }

    // Stream-based simple sync overload:
    public static IEnumerable<T> YamlSync<T>(
        Stream stream,
        Action<Exception>? onError = null,
        string? filePath = null,
        CancellationToken cancellationToken = default)
    {
        var opts = new YamlReadOptions<T>
        {
            ErrorAction = onError == null ? ReaderErrorAction.Throw : ReaderErrorAction.Skip,
            ErrorSink = onError == null
                ? NullErrorSink.Instance
                : new DelegatingErrorSink(e => onError(e), filePath ?? StreamPseudoPath)
        };
        foreach (var item in YamlSync<T>(stream, opts, filePath, cancellationToken))
            yield return item;
    }



    /// <summary>
    /// NET-006 FIX: Converts YAML dictionary values (strings) to the types expected
    /// by the record's primary constructor parameters.
    /// </summary>
    private static object?[] ConvertYamlValues<T>(string[] schema, object?[] values)
    {
        var type = typeof(T);
        // Find the primary constructor (most parameters)
        var ctor = type.GetConstructors(BindingFlags.Public | BindingFlags.Instance)
            .OrderByDescending(c => c.GetParameters().Length)
            .FirstOrDefault();

        if (ctor == null) return values;

        var ctorParams = ctor.GetParameters();
        // Build a case-insensitive lookup: paramName → paramType
        var paramTypes = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in ctorParams)
            if (p.Name != null)
                paramTypes[p.Name] = p.ParameterType;

        var converted = new object?[values.Length];
        for (int i = 0; i < values.Length; i++)
        {
            var val = values[i];
            if (i < schema.Length && paramTypes.TryGetValue(schema[i], out var targetType))
            {
                converted[i] = ConvertSingleValue(val, targetType);
            }
            else
            {
                converted[i] = val;
            }
        }
        return converted;
    }

    private static object? ConvertSingleValue(object? value, Type targetType)
    {
        if (value == null) return null;
        var actualType = Nullable.GetUnderlyingType(targetType) ?? targetType;
        if (actualType.IsInstanceOfType(value)) return value;

        try
        {
            // String → target type conversion
            if (value is string s)
            {
                if (string.IsNullOrWhiteSpace(s))
                    return targetType.IsValueType ? Activator.CreateInstance(targetType) : null;

                if (actualType == typeof(int))      return int.Parse(s, CultureInfo.InvariantCulture);
                if (actualType == typeof(long))     return long.Parse(s, CultureInfo.InvariantCulture);
                if (actualType == typeof(double))   return double.Parse(s, CultureInfo.InvariantCulture);
                if (actualType == typeof(decimal))  return decimal.Parse(s, CultureInfo.InvariantCulture);
                if (actualType == typeof(bool))     return bool.Parse(s);
                if (actualType == typeof(DateTime)) return DateTime.Parse(s, CultureInfo.InvariantCulture);
                if (actualType == typeof(Guid))     return Guid.Parse(s);
                if (actualType.IsEnum)              return Enum.Parse(actualType, s, ignoreCase: true);
            }

            return Convert.ChangeType(value, actualType, CultureInfo.InvariantCulture);
        }
        catch
        {
            // Lenient: return default for value types, null for reference types
            return targetType.IsValueType ? Activator.CreateInstance(targetType) : null;
        }
    }
}

