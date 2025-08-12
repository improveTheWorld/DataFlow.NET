using DataFlow.Extensions;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using YamlDotNet.Serialization;

namespace DataFlow.Data;

/// <summary>
/// Provides extension methods for writing IEnumerable and IAsyncEnumerable streams
/// to various file formats.
/// </summary>
static public class Writers
{
    // --- TEXT ---
    public static void WriteText(this IEnumerable<string> lines, string path)
    {
        using var file = new StreamWriter(path);
        foreach (var line in lines)
        {
            file.WriteLine(line);
        }
    }

    public static async Task WriteText(this IAsyncEnumerable<string> lines, string path)
    {
        await using var file = new StreamWriter(path);
        await foreach (var line in lines)
        {
            await file.WriteLineAsync(line);
        }
    }

    // --- CSV ---
    public static void WriteCSV<T>(this IEnumerable<T> records, string path, bool withTitle = true, string separator = ",")
    {
        using var file = new StreamWriter(path);
        if (withTitle)
        {
            file.WriteLine(CSV.csv<T>(separator));
        }
        foreach (var record in records)
        {
            file.WriteLine(record.csv(separator));
        }
    }

    public static async ValueTask WriteCSV<T>(this IAsyncEnumerable<T> records, string path, bool withTitle = true, string separator = ",")
    {
        await using var file = new StreamWriter(path);
        if (withTitle)
        {
            await file.WriteLineAsync(CSV.csv<T>(separator));
        }
        await foreach (var record in records)
        {
            await file.WriteLineAsync(record.csv(separator));
        }
    }

    // --- JSON ---
    public static void WriteJSON<T>(this IEnumerable<T> items, string path)
    {
        using var stream = File.Create(path);
        JsonSerializer.Serialize(stream, items, new JsonSerializerOptions { WriteIndented = true });
    }

    public static async Task WriteJSONAsync<T>(this IAsyncEnumerable<T> items, string path)
    {
        await using var stream = File.Create(path);
        // Use Utf8JsonWriter for true async, non-buffering serialization
        await using var jsonWriter = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true });

        jsonWriter.WriteStartArray();
        await foreach (var item in items)
        {
            JsonSerializer.Serialize(jsonWriter, item, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        jsonWriter.WriteEndArray();
        await jsonWriter.FlushAsync();
    }

    // --- YAML ---
    public static void WriteYAML<T>(this IEnumerable<T> items, string path)
    {
        using var writer = new StreamWriter(path);
        var serializer = new SerializerBuilder().Build();
        serializer.Serialize(writer, items);
    }

    public static async Task WriteYAMLAsync<T>(this IAsyncEnumerable<T> items, string path)
    {
        await using var writer = new StreamWriter(path);
        var serializer = new SerializerBuilder().Build();
        // NOTE: YamlDotNet does not support fully async serialization.
        // We must collect the items first. This is a documented trade-off
        serializer.Serialize(writer, items);
    }
}
