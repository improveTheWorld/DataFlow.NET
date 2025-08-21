using DataFlow.Framework;
using System.Text.Json;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace DataFlow.Data.StringMapper
{

    public sealed record StringInferenceOptions
    {
        public bool PreserveLeadingZeroNumeric { get; init; } = true;
        public bool PreserveLargeIntegerStrings { get; init; } = true;

        // Enable/disable individual primitive parsers
        public bool EnableBoolean { get; init; } = true;
        public bool EnableInt32 { get; init; } = true;
        public bool EnableInt64 { get; init; } = true;
        public bool EnableDecimal { get; init; } = true;
        public bool EnableDouble { get; init; } = true;
        public bool EnableDateTime { get; init; } = true;
        public bool EnableGuid { get; init; } = true;

        // Custom hooks:
        // CustomFirst: invoked BEFORE built-in inference; if returns non-null, short-circuits.
        public Func<string, object?>? CustomFirst { get; init; }
        // CustomLast: invoked AFTER built-in inference; receives (original string, inferredValue) and returns final value.
        public Func<string, object?, object?>? CustomLast { get; init; }

        public static readonly StringInferenceOptions Default = new();
    }

    /// <summary>
    /// Provides extension and helper methods for mapping raw string content into strongly
    /// typed objects (CSV-like separated values, JSON, YAML, or custom formats).
    /// </summary>
    /// <remarks>
    /// This static utility focuses on lightweight, allocation‑aware parsing for common
    /// textual representations encountered in data ingestion scenarios.
    /// 
    /// General behaviors / design notes:
    /// - CSV parsing is positional: values are mapped to the provided <paramref name="schema"/> order.
    /// - Primitive inference (<see cref="GetObject"/>) attempts to coerce strings into common scalar
    ///   .NET types (bool, Int32, Int64, Double, DateTime) before falling back to <see cref="string"/>.
    /// - JSON and YAML methods catch format exceptions and return <c>default</c>(T) instead of throwing,
    ///   enabling resilient streaming pipelines. Failures are written to <see cref="Console"/>; callers may
    ///   replace this with structured logging if needed.
    /// - All deserialization methods are culture‑invariant with respect to numeric / boolean parsing
    ///   because they rely on the BCL default invariant conversions for the underlying TryParse operations.
    /// </remarks>
    public static class StringMapper
    {
        #region Static Reusable Infrastructure

        /// <summary>
        /// Shared YAML <see cref="IDeserializer"/> configured for camelCase property name matching.
        /// This instance is thread-safe for concurrent use (YamlDotNet deserializers are stateless after build).
        /// </summary>
        private static readonly IDeserializer YamlDeserializer =
            new DeserializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .Build();

        /// <summary>
        /// Shared System.Text.Json serializer options used for JSON deserialization.
        /// Configured with case-insensitive property name matching to increase robustness
        /// against casing variations in input payloads.
        /// </summary>
        private static readonly JsonSerializerOptions JsonSerializerOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        #endregion

        #region Primitive Inference

        /// <summary>
        /// Attempts to interpret a raw string as one of several primitive .NET types.
        /// </summary>
        /// <param name="objectValue">The raw textual representation.</param>
        /// <returns>
        /// The parsed value as one of:
        /// <list type="bullet">
        ///   <item><description><see cref="bool"/></description></item>
        ///   <item><description><see cref="int"/></description></item>
        ///   <item><description><see cref="long"/></description></item>
        ///   <item><description><see cref="double"/></description></item>
        ///   <item><description><see cref="DateTime"/></description></item>
        ///   <item><description><see cref="string"/> (original input if no other type matched)</description></item>
        /// </list>
        /// </returns>
        /// <remarks>
        /// Parsing order is boolean → Int32 → Int64 → Double → DateTime → string.
        /// This order is chosen to favor the narrowest integral representations before widening.
        /// </remarks>
        // NEW overload with options object (pluggable inference pipeline)
        public static object GetObject(string objectValue) =>
    GetObject(objectValue, preserveLeadingZeroNumeric: true, preserveLargeInteger: true);

        // Existing 2-flag overload now delegates to options-based overload
        public static object GetObject(string objectValue, bool preserveLeadingZeroNumeric, bool preserveLargeInteger)
        {
            var opts = StringInferenceOptions.Default with
            {
                PreserveLeadingZeroNumeric = preserveLeadingZeroNumeric,
                PreserveLargeIntegerStrings = preserveLargeInteger
            };
            return GetObject(objectValue, opts);
        }

        // NEW overload with options object (pluggable inference pipeline)
        public static object GetObject(string objectValue, StringInferenceOptions options)
        {
            if (objectValue == null) return "";

            // Custom pre-pass
            if (options.CustomFirst != null)
            {
                var custom = options.CustomFirst(objectValue);
                if (custom is not null)
                    return options.CustomLast != null
                        ? (options.CustomLast(objectValue, custom) ?? custom)
                        : custom;
            }

            // Preservation decisions
            if (options.PreserveLeadingZeroNumeric && objectValue.Length > 1 && objectValue[0] == '0' && AllDigits(objectValue))
                return Finalize(objectValue, objectValue);

            if (options.PreserveLargeIntegerStrings && objectValue.Length > 18 && AllDigits(objectValue))
                return Finalize(objectValue, objectValue);

            object? candidate = objectValue;

            if (options.EnableBoolean && bool.TryParse(objectValue, out var b))
                return Finalize(objectValue, b);

            if (options.EnableInt32 && int.TryParse(objectValue, out var i))
                return Finalize(objectValue, i);

            if (options.EnableInt64 && long.TryParse(objectValue, out var l))
                return Finalize(objectValue, l);

            if (options.EnableDecimal && decimal.TryParse(objectValue, out var dec))
                return Finalize(objectValue, dec);

            if (options.EnableDouble && double.TryParse(objectValue, out var dbl))
                return Finalize(objectValue, dbl);

            if (options.EnableDateTime && DateTime.TryParse(objectValue, out var dt))
                return Finalize(objectValue, dt);

            if (options.EnableGuid && Guid.TryParse(objectValue, out var g))
                return Finalize(objectValue, g);

            return Finalize(objectValue, objectValue);

            object Finalize(string original, object inferred)
                => options.CustomLast != null
                    ? (options.CustomLast(original, inferred) ?? inferred)
                    : inferred;

            static bool AllDigits(string s)
            {
                for (int idx = 0; idx < s.Length; idx++)
                    if (s[idx] < '0' || s[idx] > '9') return false;
                return true;
            }
        }


        #endregion

        #region CSV / Separated Values

        /// <summary>
        /// Parses a delimited (CSV-like) line into a new instance of <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">The target materialization type.</typeparam>
        /// <param name="line">The raw line containing delimited values.</param>
        /// <param name="schema">
        /// Ordered property / member names that correspond one-to-one with the delimited values.
        /// The length of <paramref name="schema"/> must be equal to or greater than the number
        /// of values obtained after splitting; extra schema entries (if any) will receive default values.
        /// </param>
        /// <param name="separator">The delimiter used to split the line. Default is <c>;</c>.</param>
        /// <returns>
        /// A new <typeparamref name="T"/> instance with members populated by positional mapping.
        /// Returns <c>default</c> if instantiation fails in <c>NEW.GetNew&lt;T&gt;</c>.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="line"/> or <paramref name="schema"/> is null.</exception>
        /// <remarks>
        /// This method:
        /// <list type="bullet">
        ///   <item>Splits using <see cref="string.Split(string?, StringSplitOptions)"/> with <see cref="StringSplitOptions.TrimEntries"/>.</item>
        ///   <item>Per value, applies <see cref="GetObject"/> for primitive inference.</item>
        ///   <item>Relies on an external factory <c>NEW.GetNew&lt;T&gt;</c> from <c>DataFlow.Framework</c> to perform reflection-based construction.</item>
        /// </list>
        /// Performance considerations: for very wide rows or high-frequency parsing, a Span-based splitter
        /// could reduce allocations—future optimization possible.
        /// </remarks>
        public static T? AsCSV<T>(this string line, string[] schema, string separator = ";")
        {
            if (line == null) throw new ArgumentNullException(nameof(line));
            if (schema == null) throw new ArgumentNullException(nameof(schema));

            var values = line
                .Split(separator, StringSplitOptions.TrimEntries)
                .Select(GetObject)
                .ToArray();

            return ObjectMaterializer.Create<T>(schema, values);
        }

        #endregion

        #region JSON

        /// <summary>
        /// Deserializes a JSON string into an instance of <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">Target type for deserialization.</typeparam>
        /// <param name="jsonString">The JSON payload.</param>
        /// <returns>
        /// An instance of <typeparamref name="T"/> if deserialization succeeds; otherwise <c>default</c>.
        /// </returns>
        /// <remarks>
        /// <para>
        /// Uses <see cref="System.Text.Json.JsonSerializer"/> with case-insensitive property name matching.
        /// </para>
        /// <para>
        /// Exceptions:
        /// </para>
        /// <list type="bullet">
        ///   <item>Any <see cref="JsonException"/> is caught; the method returns <c>default</c> and writes a message to <see cref="Console"/>.</item>
        ///   <item>Other exceptions (I/O, memory) are not caught and will bubble to the caller.</item>
        /// </list>
        /// Consider replacing console logging with a structured logging framework in production.
        /// </remarks>
        public static T? AsJson<T>(this string jsonString)
        {
            if (jsonString == null) throw new ArgumentNullException(nameof(jsonString));

            try
            {
                return JsonSerializer.Deserialize<T>(jsonString, JsonSerializerOptions);
            }
            catch (JsonException ex)
            {
                Console.WriteLine($"[DataFlow.Data.StringMapper] JSON Deserialization Error: {ex.Message}");
                return default;
            }
        }

        #endregion

        #region YAML

        /// <summary>
        /// Deserializes a YAML string into an instance of <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">Target type for deserialization.</typeparam>
        /// <param name="ymlString">The YAML payload.</param>
        /// <returns>
        /// An instance of <typeparamref name="T"/> if deserialization succeeds; otherwise <c>default</c>.
        /// </returns>
        /// <remarks>
        /// <para>
        /// Uses a shared <see cref="IDeserializer"/> configured with <see cref="CamelCaseNamingConvention"/>.
        /// </para>
        /// <para>
        /// Catches <see cref="YamlDotNet.Core.YamlException"/> and returns <c>default</c>, logging the error
        /// to <see cref="Console"/>. Other exceptions are not caught.
        /// </para>
        /// </remarks>
        public static T? AsYML<T>(this string ymlString)
        {
            if (ymlString == null) throw new ArgumentNullException(nameof(ymlString));

            try
            {
                return YamlDeserializer.Deserialize<T>(ymlString);
            }
            catch (YamlDotNet.Core.YamlException ex)
            {
                Console.WriteLine($"[DataFlow.Data.StringMapper] YML Deserialization Error: {ex.Message}");
                return default;
            }
        }

        #endregion

        #region Custom Parsing

        /// <summary>
        /// Applies a caller-provided parsing delegate to a raw string.
        /// </summary>
        /// <typeparam name="T">Target type produced by the custom parser.</typeparam>
        /// <param name="line">The input string.</param>
        /// <param name="customParser">A non-null delegate that transforms <paramref name="line"/> into <typeparamref name="T"/>.</param>
        /// <returns>The result of invoking <paramref name="customParser"/>.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="customParser"/> is null.</exception>
        /// <remarks>
        /// Use when the built-in CSV / JSON / YAML helpers are insufficient and you need specialized logic
        /// (e.g., regex extraction, hybrid formats, partial parses).
        /// </remarks>
        public static T AsCustom<T>(this string line, Func<string, T> customParser)
        {
            if (customParser == null) throw new ArgumentNullException(nameof(customParser));
            return customParser(line);
        }

        #endregion
    }
}
