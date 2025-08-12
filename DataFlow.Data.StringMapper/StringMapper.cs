using DataFlow.Framework;
using System.Text.Json; // Required for JSON deserialization
using YamlDotNet.Serialization; // Required for YAML deserialization
using YamlDotNet.Serialization.NamingConventions;

namespace DataFlow.Data.StringMapper;

/// <summary>
/// Provides extension methods for parsing strings into various structured data formats.
/// </summary>
public static class StringMapper
{
    /// <summary>
    /// A reusable deserializer for YAML, configured for common use cases.
    /// </summary>
    private static readonly IDeserializer YamlDeserializer = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance) // Handles typical camelCase YAML properties
        .Build();

    /// <summary>
    /// A reusable serializer for JSON, with standard web options.
    /// </summary>
    private static readonly JsonSerializerOptions JsonSerializerOptions = new JsonSerializerOptions
    {
        PropertyNameCaseInsensitive = true // Makes JSON deserialization more robust
    };

    public static object GetObject(string objectValue)
    {
        bool boolValue;
        Int32 intValue;
        Int64 bigintValue;
        double doubleValue;
        DateTime dateValue;

        if (bool.TryParse(objectValue, out boolValue))
            return boolValue;
        else if (Int32.TryParse(objectValue, out intValue))
            return intValue;
        else if (Int64.TryParse(objectValue, out bigintValue))
            return bigintValue;
        else if (double.TryParse(objectValue, out doubleValue))
            return doubleValue;
        else if (DateTime.TryParse(objectValue, out dateValue))
            return dateValue;
        else return objectValue;
    }

    /// <summary>
    /// Parses a single line of separated values into an object of type T.
    /// </summary>
    /// <typeparam name="T">The target type.</typeparam>
    /// <param name="line">The string line to parse.</param>
    /// <param name="schema">The ordered list of property names corresponding to the values in the line.</param>
    /// <param name="separator">The character separating the values.</param>
    /// <returns>A new instance of T populated with the parsed values.</returns>
    public static T? AsCSV<T>(this string line, string[] schema, string separator = ";")
            => NEW.GetNew<T>(schema,
                             line.Split(separator, StringSplitOptions.TrimEntries)
                            .Select(x => GetObject(x))
                            .ToArray());

    /// <summary>
    /// Parses a JSON string into an object of type T.
    /// </summary>
    /// <typeparam name="T">The target type.</typeparam>
    /// <param name="jsonString">The JSON string to parse.</param>
    /// <returns>A new instance of T populated from the JSON string.</returns>
    public static T? AsJson<T>(this string jsonString)
    {
        try
        {
            return JsonSerializer.Deserialize<T>(jsonString, JsonSerializerOptions);
        }
        catch (JsonException ex)
        {
            // In a real application, consider logging this error.
            Console.WriteLine($"[DataFlow.Data.StringMapper] JSON Deserialization Error: {ex.Message}");
            return default;
        }
    }

    /// <summary>
    /// Parses a YAML string into an object of type T.
    /// </summary>
    /// <typeparam name="T">The target type.</typeparam>
    /// <param name="ymlString">The YAML string to parse.</param>
    /// <returns>A new instance of T populated from the YAML string.</returns>
    public static T? AsYML<T>(this string ymlString)
    {
        try
        {
            return YamlDeserializer.Deserialize<T>(ymlString);
        }
        catch (YamlDotNet.Core.YamlException ex)
        {
            // In a real application, consider logging this error.
            Console.WriteLine($"[DataFlow.Data.StringMapper] YML Deserialization Error: {ex.Message}");
            return default;
        }
    }

    /// <summary>
    /// Parses a string into an object of type T using a custom parsing function.
    /// </summary>
    /// <typeparam name="T">The target type.</typeparam>
    /// <param name="line">The raw string line to parse.</param>
    /// <param name="customParser">The function that defines the custom parsing logic.</param>
    /// <returns>A new instance of T as returned by the custom parser.</returns>
    public static T AsCustom<T>(this string line, Func<string, T> customParser)
    {
        if (customParser == null)
        {
            throw new ArgumentNullException(nameof(customParser));
        }
        return customParser(line);
    }
}
