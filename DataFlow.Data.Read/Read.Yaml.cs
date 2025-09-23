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
   
    // --- YAML (options-based) ---

    public static async IAsyncEnumerable<T> Yaml<T>(string path, YamlReadOptions<T> options, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (options == null) throw new ArgumentNullException(nameof(options));
        options.FilePath = path;

        using var reader = new StreamReader(path);
        var baseParser = new YamlDotNet.Core.Parser(reader);
        // Correct priming: advance once and verify we are at StreamStart.
        if (!baseParser.MoveNext() || baseParser.Current is not YamlDotNet.Core.Events.StreamStart)
            throw new InvalidDataException("YAML: Missing StreamStart event.");
        // IMPORTANT: Do NOT advance again here. The security parser's first advance
        // will move from StreamStart to the first real document event (DocumentStart or SequenceStart).

        // Use generic security parser
        var secureParser = new SecurityFilteringParser<T>(baseParser, options);

        var deserializerBuilder = new YamlDotNet.Serialization.DeserializerBuilder()
            .WithNamingConvention(YamlDotNet.Serialization.NamingConventions.CamelCaseNamingConvention.Instance);
        var deserializer = deserializerBuilder.Build();

        long record = 0;
        bool sequenceRootChecked = false;
        bool sequenceRootMode = false;
        bool firstDocumentStartConsumed = false;

        while (!options.Metrics.TerminatedEarly && secureParser.Accept<YamlDotNet.Core.Events.StreamEnd>(out _) == false)
        {
            cancellationToken.ThrowIfCancellationRequested();
            options.CancellationToken.ThrowIfCancellationRequested();

            // Root mode detection (run once)
            if (!sequenceRootChecked)
            {
                sequenceRootChecked = true;
                // Expect (and consume) first DocumentStart
                if (secureParser.Accept<YamlDotNet.Core.Events.DocumentStart>(out _))
                {
                    secureParser.MoveNext(); // consume DocumentStart
                    firstDocumentStartConsumed = true;
                    // After DocumentStart, check if root is a sequence
                    if (secureParser.Accept<YamlDotNet.Core.Events.SequenceStart>(out _))
                    {
                        secureParser.MoveNext(); // consume SequenceStart
                        sequenceRootMode = true;
                    }
                }
                else if (secureParser.Accept<YamlDotNet.Core.Events.SequenceStart>(out _))
                {
                    // Extremely rare case: implicit doc start not surfaced; handle anyway
                    secureParser.MoveNext();
                    sequenceRootMode = true;
                }
            }

            if (sequenceRootMode)
            {
                // End of the sequence (then expect DocumentEnd)
                if (secureParser.Accept<YamlDotNet.Core.Events.SequenceEnd>(out _))
                {
                    secureParser.MoveNext(); // consume SequenceEnd
                    if (secureParser.Accept<YamlDotNet.Core.Events.DocumentEnd>(out _))
                        secureParser.MoveNext();
                    break;
                }
            }
            else
            {
                // Multi-document mode: for subsequent documents consume DocumentStart each loop
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
            T item = default;
            bool terminateAfterYield = false;
            try
            {
                item = deserializer.Deserialize<T>(secureParser);
                
                if (options.RestrictTypes && !IsAllowed(options, item))
                {
                    if (!options.HandleError("YAML", -1, record, path, "TypeRestriction",
                            "Deserialized object type not allowed.", item?.GetType().FullName ?? "null"))
                        yield break;
                }
                else
                {
                    success = true;
                }
            }
            catch (YamlDotNet.Core.YamlException ex)
            {
                // If a prior security violation (or any Stop/Throw) already set TerminatedEarly,
                // suppress a secondary parse error to avoid double counting.
                if (options.Metrics.TerminatedEarly)
                    yield break;

                if (!options.HandleError("YAML", -1, record, path,
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
                options.Metrics.RecordsEmitted++; // ensure emitted count
                yield return item;
                if (options.ShouldEmitProgress()) options.EmitProgress();
            }
            else if (success && terminateAfterYield)
            {
                // Termination was triggered while reading ahead; still emit this record.
                options.Metrics.RecordsEmitted++;
                yield return item;
            }

            if (terminateAfterYield || options.Metrics.TerminatedEarly)
                yield break;
        }

        // Only mark completion if we did not terminate early.
        if (!options.Metrics.TerminatedEarly)
            options.Complete();
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

    private static bool IsAllowed<T>(YamlReadOptions<T> options, T item)
    {
        if (!options.RestrictTypes) return true;
        if (item == null) return true;
        var t = item.GetType();
        if (options.AllowedTypes == null)
            return t == typeof(T);
        return options.AllowedTypes.Contains(t);
    }


}
