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
        baseParser.Consume<YamlDotNet.Core.Events.StreamStart>();

        // UPDATED: use generic security parser
        var secureParser = new SecurityFilteringParser<T>(baseParser, options);

        var deserializerBuilder = new YamlDotNet.Serialization.DeserializerBuilder()
            .WithNamingConvention(YamlDotNet.Serialization.NamingConventions.CamelCaseNamingConvention.Instance);
        var deserializer = deserializerBuilder.Build();

        long record = 0;
        bool sequenceRootChecked = false;
        bool sequenceRootMode = false;

        while (!options.Metrics.TerminatedEarly && secureParser.Accept<YamlDotNet.Core.Events.StreamEnd>(out _) == false)
        {
            cancellationToken.ThrowIfCancellationRequested();
            options.CancellationToken.ThrowIfCancellationRequested();

            if (!sequenceRootChecked)
            {
                sequenceRootChecked = true;
                if (secureParser.Accept<YamlDotNet.Core.Events.SequenceStart>(out _))
                {
                    secureParser.MoveNext(); // consume sequence start
                    sequenceRootMode = true;
                }
            }

            if (sequenceRootMode)
            {
                if (secureParser.Accept<YamlDotNet.Core.Events.SequenceEnd>(out _))
                {
                    secureParser.MoveNext(); // consume end
                    break;
                }
            }
            else
            {
                if (!secureParser.Accept<YamlDotNet.Core.Events.DocumentStart>(out _))
                    break;
                secureParser.MoveNext(); // consume DocumentStart
            }

            record++;
            options.Metrics.RawRecordsParsed = record;

            bool success = false;
            T item = default;
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
                if (!options.HandleError("YAML", -1, record, path,
                        ex.GetType().Name, ex.Message, ""))
                    yield break;

                if (sequenceRootMode)
                    SecurityFilteringParser<T>.SkipSequenceElement(secureParser);
                else
                    SecurityFilteringParser<T>.SkipDocument(secureParser);
            }

            if (!sequenceRootMode && secureParser.Accept<YamlDotNet.Core.Events.DocumentEnd>(out _))
                secureParser.MoveNext();

            if (success)
            {
                options.Metrics.RecordsEmitted++; // ensure emitted count
                yield return item;
                if (options.ShouldEmitProgress()) options.EmitProgress();
            }

            if (options.Metrics.TerminatedEarly) yield break;
        }

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


}
