using DataFlow.Data.StringMapper;
using DataFlow.Extensions;
using System.Runtime.CompilerServices;
using System.Text.Json;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace DataFlow.Data;

/// <summary>
/// Provides static methods for lazily reading data from various file formats,
/// with full support for both synchronous (IEnumerable) and asynchronous (IAsyncEnumerable) streaming.
/// The method sync/async suffixes convention is inverted (default is asynchronous) to encourage the asynchronous file reading reflex.
/// </summary>
public static class Read
{
    // --- TEXT ---

    /// <summary>
    /// Lazily reads lines from an existing StreamReader synchronously.
    /// </summary>
    public static IEnumerable<string> textSync(StreamReader file)
    {
        while (!file.EndOfStream)
        {
            yield return file.ReadLine();
        }
    }

    /// <summary>
    /// Lazily reads all lines from a file synchronously.
    /// </summary>
    public static IEnumerable<string> textSync(string path)
    {
        using var file = new StreamReader(path);
        while (!file.EndOfStream)
        {
            yield return file.ReadLine();
        }
    }

    /// <summary>
    /// Lazily reads lines from an existing StreamReader asynchronously.
    /// </summary>
    public static async IAsyncEnumerable<string> text(StreamReader file, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        while (!file.EndOfStream)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return await file.ReadLineAsync();
        }
    }

    /// <summary>
    /// Lazily reads all lines from a file asynchronously. This is the recommended default for text files.
    /// </summary>
    public static async IAsyncEnumerable<string> text(string path, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        using var file = new StreamReader(path);
        while (!file.EndOfStream)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return await file.ReadLineAsync();
        }
    }


    // --- CSV ---
    private static IEnumerable<T> CsvInternalSync<T>(IEnumerable<string> lines, string separator, Action<string, Exception> onError, string[] schema)
    {
        var schemaToUse = schema;
        using var enumerator = lines.GetEnumerator();

        if (schema.IsNullOrEmpty())
        {
            if (enumerator.MoveNext())
            {
                schemaToUse = enumerator.Current.Split(separator, StringSplitOptions.TrimEntries);
            }
            else
            {
                yield break; // Empty file
            }
        }

        while (enumerator.MoveNext())
        {
            var line = enumerator.Current;
            if (line.IsNullOrWhiteSpace()) continue;

            T item = default;
            bool success = false;
            try
            {
                item = line.AsCSV<T>(schemaToUse, separator);
                success = true;
            }
            catch (Exception ex)
            {
                if (onError == null) throw;
                onError(line, new InvalidDataException($"Failed to parse CSV line: \"{line}\". See inner exception for details.", ex));
            }

            if (success)
            {
                yield return item;
            }
        }
    }



    /// <summary>
    /// Lazily reads and deserializes a CSV file into objects of type T synchronously.
    /// </summary>
    /// <param name="path">The path to the CSV file.</param>
    /// <param name="separator">The character used to separate fields.</param>
    /// <param name="onError">An optional action to handle parsing errors for individual lines without stopping the process.</param>
    /// <param name="schema">An optional explicit schema. If not provided, the first line of the file is used as the header.</param>
    public static IEnumerable<T> csvSync<T>(string path, string separator = ";", Action<string, Exception> onError = null, params string[] schema)
    {
        var lines = textSync(path).SkipWhile(line => line.IsNullOrWhiteSpace());
        return CsvInternalSync<T>(lines, separator, onError, schema);
    }

    private static async IAsyncEnumerable<T> CsvInternal<T>(IAsyncEnumerable<string> lines, string separator, Action<string, Exception> onError, string[] schema, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var schemaToUse = schema;
        await using var enumerator = lines.GetAsyncEnumerator(cancellationToken);

        if (schema.IsNullOrEmpty())
        {
            if (await enumerator.MoveNextAsync())
            {
                schemaToUse = enumerator.Current.Split(separator, StringSplitOptions.TrimEntries);
            }
            else
            {
                yield break; // Empty file
            }
        }

        while (await enumerator.MoveNextAsync())
        {
            var line = enumerator.Current;
            if (line.IsNullOrWhiteSpace()) continue;

            T item = default;
            bool success = false;
            try
            {
                item = line.AsCSV<T>(schemaToUse, separator);
                success = true;
            }
            catch (Exception ex)
            {
                if (onError == null) throw;
                onError(line, new InvalidDataException($"Failed to parse CSV line: \"{line}\". See inner exception for details.", ex));
            }

            if (success)
            {
                yield return item;
            }
        }
    }

    /// <summary>
    /// Lazily reads and deserializes a CSV file into objects of type T asynchronously.
    /// </summary>
    /// <param name="path">The path to the CSV file.</param>
    /// <param name="separator">The character used to separate fields.</param>
    /// <param name="onError">An optional action to handle parsing errors for individual lines without stopping the process.</param>
    /// <param name="schema">An optional explicit schema. If not provided, the first line of the file is used as the header.</param>
    public static IAsyncEnumerable<T> csv<T>(string path, string separator = ";", Action<string, Exception> onError = null, params string[] schema)
    {
        var lines = text(path).SkipWhile(line => line.IsNullOrWhiteSpace());
        return CsvInternal<T>(lines, separator, onError, schema);
    }

    // --- JSON ---

    /// <summary>
    /// Asynchronously streams and deserializes a JSON file containing a root-level array into objects of type T.
    /// </summary>
    /// <param name="path">The path to the JSON file.</param>
    /// <param name="options">Optional custom JSON serializer options.</param>
    /// <param name="onError">An optional action to handle deserialization errors for individual objects without stopping the stream.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    public static async IAsyncEnumerable<T> json<T>(string path, JsonSerializerOptions options = null, Action<Exception> onError = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        options ??= new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        await using var stream = File.OpenRead(path);

        await foreach (var element in JsonSerializer.DeserializeAsyncEnumerable<JsonElement>(stream, options, cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            T item = default;
            bool success = false;
            try
            {
                item = element.Deserialize<T>(options);
                if (item != null)
                {
                    success = true;
                }
            }
            catch (Exception ex)
            {
                if (onError == null) throw;
                onError(new JsonException($"Failed to deserialize JSON element: {element}. See inner exception for details.", ex));
            }

            if (success)
            {
                yield return item;
            }
        }
    }

    // --- YAML ---

    /// <summary>
    /// Asynchronously streams and deserializes a YAML file into objects of type T.
    /// Supports both a sequence of documents (`[...]`) and a stream of documents separated by `---`.
    /// </summary>
    /// <param name="path">The path to the YAML file.</param>
    /// <param name="deserializer">Optional custom YAML deserializer. If not provided, a default one is created.</param>
    /// <param name="onError">An optional action to handle deserialization errors for individual documents without stopping the stream.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    public static async IAsyncEnumerable<T> yaml<T>(string path, IDeserializer deserializer = null, Action<Exception> onError = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        deserializer ??= new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();

        using var reader = new StreamReader(path);
        var parser = new Parser(reader);

        parser.Consume<StreamStart>();

        if (parser.Accept<SequenceStart>(out _))
        {
            while (!parser.Accept<SequenceEnd>(out _))
            {
                cancellationToken.ThrowIfCancellationRequested();
                T item = default;
                bool success = false;
                try
                {
                    item = deserializer.Deserialize<T>(parser);
                    success = true;
                }
                catch (Exception ex)
                {
                    if (onError == null) throw;
                    parser.SkipThisAndNestedEvents();
                    onError(new YamlException("Failed to deserialize a YAML document in the sequence. See inner exception for details.", ex));
                }
                if (success)
                {
                    yield return item;
                }
            }
        }
        else
        {
            while (parser.Accept<DocumentStart>(out _))
            {
                cancellationToken.ThrowIfCancellationRequested();
                T item = default;
                bool success = false;
                try
                {
                    item = deserializer.Deserialize<T>(parser);
                    success = true;
                }
                catch (Exception ex)
                {
                    if (onError == null) throw;
                    parser.SkipThisAndNestedEvents();
                    onError(new YamlException("Failed to deserialize a YAML document in the stream. See inner exception for details.", ex));
                }

                if (success)
                {
                    yield return item;
                }
            }
        }
    }
}
