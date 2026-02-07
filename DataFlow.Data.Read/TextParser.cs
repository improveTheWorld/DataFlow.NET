using System.Globalization;
using System.Text;
using System.Text.Json;
using static DataFlow.Read;

namespace DataFlow;

public sealed record TextParsingOptions
{
    // General
    public bool TrimWhitespace { get; init; } = true;

    // Preservation rules
    public bool PreserveLeadingZeroNumeric { get; init; } = true;           // "0123" stays string
    public bool PreserveLargeIntegerStrings { get; init; } = true;          // very long digit-only strings stay string
    public int LargeIntegerDigitThreshold { get; init; } = 19;              // > 19 digits (~beyond Int64) preserved

    // Enable/disable primitive parsers
    public bool EnableBoolean { get; init; } = true;
    public bool EnableInt32 { get; init; } = true;
    public bool EnableInt64 { get; init; } = true;
    public bool EnableDecimal { get; init; } = true;                        // preferred over double
    public bool EnableDouble { get; init; } = false;                        // off by default to avoid FP surprises
    public bool EnableDateTime { get; init; } = true;
    public bool EnableGuid { get; init; } = true;

    // Smart decimal detection: auto-detects dot vs comma decimal separators
    // When true, "1234,56" and "1.234,56" are correctly parsed without requiring culture config.
    // Disable to rely solely on FormatProvider for decimal/double parsing.
    public bool SmartDecimalParsing { get; init; } = true;

    // DateTime parsing
    public IFormatProvider FormatProvider { get; init; } = CultureInfo.InvariantCulture;
    public DateTimeStyles DateTimeStyles { get; init; } = DateTimeStyles.AdjustToUniversal | DateTimeStyles.AllowWhiteSpaces;

    // Hooks
    // CustomFirst: invoked BEFORE built-in inference; if returns true, uses result and skips built-ins.
    public Func<string, (bool handled, object? value)>? CustomFirst { get; init; }
    // CustomLast: invoked AFTER built-in inference; can transform the inferred result.
    public Func<string, object?, object?>? CustomLast { get; init; }

    public static readonly TextParsingOptions Default = new();
}

/// <summary>
/// Lightweight scalar inference utilities for general text parsing (non-CSV).
/// Attempts to coerce strings into common .NET primitives with invariant culture semantics.
/// </summary>
internal static class TextParser
{
    // Convenience entry points
    public static object Infer(string? value) => Infer(value, TextParsingOptions.Default);

    public static object Infer(string? value, TextParsingOptions options)
    {
        if (value is null) return string.Empty;
        var s = options.TrimWhitespace ? value.Trim() : value;

        // Custom pre-pass
        if (options.CustomFirst is not null)
        {
            var (handled, v) = options.CustomFirst(s);
            if (handled)
                return Finish(s, v, options);
        }


        var span = s.AsSpan();

        // Preserve rules
        if (options.PreserveLeadingZeroNumeric &&
            span.Length > 1 &&
            span[0] == '0' &&
            IsAllDigits(span))
        {
            return Finish(s, s, options);
        }

        if (options.PreserveLargeIntegerStrings &&
            IsAllDigits(span) &&
            span.Length >= options.LargeIntegerDigitThreshold)
        {
            return Finish(s, s, options);
        }

        // Booleans
        if (options.EnableBoolean && bool.TryParse(s, out var b))
            return Finish(s, b, options);

        // Integers
        if (options.EnableInt32 && int.TryParse(s, NumberStyles.Integer, options.FormatProvider, out var i32))
            return Finish(s, i32, options);

        if (options.EnableInt64 && long.TryParse(s, NumberStyles.Integer, options.FormatProvider, out var i64))
            return Finish(s, i64, options);

        // Decimal first to avoid FP rounding surprises
        if (options.EnableDecimal)
        {
            if (options.SmartDecimalParsing && TrySmartParseDecimal(s, out var smartDec))
                return Finish(s, smartDec, options);
            if (decimal.TryParse(s, NumberStyles.Number, options.FormatProvider, out var dec))
                return Finish(s, dec, options);
        }

        if (options.EnableDouble)
        {
            if (options.SmartDecimalParsing && TrySmartParseDouble(s, out var smartDbl))
                return Finish(s, smartDbl, options);
            if (double.TryParse(s, NumberStyles.Float | NumberStyles.AllowThousands, options.FormatProvider, out var dbl))
                return Finish(s, dbl, options);
        }

        // DateTime
        if (options.EnableDateTime && DateTime.TryParse(s, options.FormatProvider, options.DateTimeStyles, out var dt))
            return Finish(s, dt, options);

        // Guid
        if (options.EnableGuid && Guid.TryParse(s, out var guid))
            return Finish(s, guid, options);

        // Fallback to original string
        return Finish(s, s, options);

        static object Finish(string original, object? inferred, TextParsingOptions opts)
            => opts.CustomLast is not null
                ? (opts.CustomLast(original, inferred) ?? inferred ?? string.Empty)
                : inferred ?? string.Empty;
    }

    // Try-pattern to avoid allocations/boxing in hot paths
    public static bool TryInfer(string? value, out object result) => TryInfer(value, TextParsingOptions.Default, out result);

    public static bool TryInfer(string? value, TextParsingOptions options, out object result)
    {
        result = Infer(value, options);
        return result is not string; // true if we inferred a non-string primitive
    }

    // Convenience generic: attempts to parse directly into T
    public static bool TryInfer<T>(string? value, out T? result)
    {
        var obj = Infer(value, TextParsingOptions.Default);
        if (obj is T t)
        {
            result = t;
            return true;
        }
        result = default;
        return false;
    }

    public static bool TryParse(string? s, Type t, out object? value, TextParsingOptions? options = null)
    {
        var opts = options ?? TextParsingOptions.Default;
        var input = s is null ? string.Empty : (opts.TrimWhitespace ? s.Trim() : s);

        value = null;
        var fp = opts.FormatProvider;

        if (t == typeof(string)) { value = input; return true; }
        if (t == typeof(bool) && bool.TryParse(input, out var b)) { value = b; return true; }
        if (t == typeof(int) && int.TryParse(input, System.Globalization.NumberStyles.Integer, fp, out var i)) { value = i; return true; }
        if (t == typeof(long) && long.TryParse(input, System.Globalization.NumberStyles.Integer, fp, out var l)) { value = l; return true; }
        if (t == typeof(decimal))
        {
            if (opts.SmartDecimalParsing && TrySmartParseDecimal(input, out var smartDec)) { value = smartDec; return true; }
            if (decimal.TryParse(input, System.Globalization.NumberStyles.Number, fp, out var dec)) { value = dec; return true; }
        }
        if (t == typeof(double))
        {
            if (opts.SmartDecimalParsing && TrySmartParseDouble(input, out var smartDbl)) { value = smartDbl; return true; }
            if (double.TryParse(input, System.Globalization.NumberStyles.Float | System.Globalization.NumberStyles.AllowThousands, fp, out var dbl)) { value = dbl; return true; }
        }
        if (t == typeof(DateTime) && DateTime.TryParse(input, fp, opts.DateTimeStyles, out var dt)) { value = dt; return true; }
        if (t == typeof(Guid) && Guid.TryParse(input, out var g)) { value = g; return true; }
        return false;
    }

    public static bool TryParse<T>(string? s, out T? value, TextParsingOptions? options = null)
    {
        if (TryParse(s, typeof(T), out var obj, options) && obj is T t)
        {
            value = t;
            return true;
        }
        value = default;
        return false;
    }
    public static bool IsAllDigits(ReadOnlySpan<char> span)
    {
        for (int i = 0; i < span.Length; i++)
        {
            var c = span[i];
            if ((uint)(c - '0') > 9) return false;
        }
        return span.Length > 0;
    }

    // =========================================================
    // SMART DECIMAL AUTO-DETECTION
    // Handles all common international decimal formats:
    //   "1234.56"     → 1234.56  (US/UK — dot decimal)
    //   "1234,56"     → 1234.56  (FR/DE — comma decimal)
    //   "1.234,56"    → 1234.56  (DE — dot thousands, comma decimal)
    //   "1,234.56"    → 1234.56  (US — comma thousands, dot decimal)
    //   "1 234,56"    → 1234.56  (FR — space thousands, comma decimal)
    //   "1 234.56"    → 1234.56  (UK — space thousands, dot decimal)
    //   "1.234.567,89"→ 1234567.89  (repeating dots = thousands)
    //   "1,234,567.89"→ 1234567.89  (repeating commas = thousands)
    //
    // AMBIGUOUS (returns false — caller falls back to FormatProvider):
    //   "1,234" — could be 1234 (thousands) or 1.234 (decimal)
    //   "1.234" — could be 1234 (thousands) or 1.234 (decimal)
    // =========================================================

    /// <summary>
    /// Attempts to parse a numeric string by auto-detecting the decimal separator.
    /// Returns false only for genuinely ambiguous formats (single separator + exactly 3 trailing digits).
    /// </summary>
    internal static bool TrySmartParseDecimal(string s, out decimal result)
    {
        result = 0m;
        var normalized = NormalizeDecimalString(s);
        if (normalized is null) return false;
        return decimal.TryParse(normalized, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
            CultureInfo.InvariantCulture, out result);
    }

    /// <summary>
    /// Attempts to parse a numeric string by auto-detecting the decimal separator (double variant).
    /// Returns false only for genuinely ambiguous formats.
    /// </summary>
    internal static bool TrySmartParseDouble(string s, out double result)
    {
        result = 0d;
        var normalized = NormalizeDecimalString(s);
        if (normalized is null) return false;
        return double.TryParse(normalized, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint | NumberStyles.AllowExponent,
            CultureInfo.InvariantCulture, out result);
    }

    /// <summary>
    /// Analyzes a numeric string's separators to auto-detect the decimal format
    /// and normalizes it to InvariantCulture (dot-decimal, no thousands) for parsing.
    /// Returns null if the format is genuinely ambiguous or not numeric.
    /// </summary>
    internal static string? NormalizeDecimalString(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;

        // Strip leading/trailing whitespace
        s = s.Trim();

        // Handle optional leading sign
        var start = 0;
        if (s.Length > 0 && (s[0] == '-' || s[0] == '+'))
            start = 1;

        // Remove spaces and non-breaking spaces (thousands separator in fr-FR, etc.)
        // We do this AFTER sign detection
        var body = s.Substring(start).Replace(" ", "").Replace("\u00A0", "");
        var sign = start > 0 ? s[0].ToString() : "";

        // Quick check: body must be digits + at most dots and commas
        int dotCount = 0, commaCount = 0;
        int lastDot = -1, lastComma = -1;
        for (int i = 0; i < body.Length; i++)
        {
            var c = body[i];
            if (c == '.') { dotCount++; lastDot = i; }
            else if (c == ',') { commaCount++; lastComma = i; }
            else if ((uint)(c - '0') > 9) return null; // non-numeric character
        }

        // No separators — pure integer with optional sign, let caller handle
        if (dotCount == 0 && commaCount == 0) return null;

        // ─── Case 1: Both dot AND comma present ─────────────────────
        // The LAST separator is the decimal; earlier ones are thousands.
        if (dotCount > 0 && commaCount > 0)
        {
            if (lastComma > lastDot)
            {
                // Comma is decimal: "1.234,56" or "1.234.567,89"
                return sign + body.Replace(".", "").Replace(',', '.');
            }
            else
            {
                // Dot is decimal: "1,234.56" or "1,234,567.89"
                return sign + body.Replace(",", "");
            }
        }

        // ─── Case 2: Only dots ──────────────────────────────────────
        if (dotCount > 0)
        {
            // Multiple dots → they're all thousands separators: "1.234.567"
            if (dotCount > 1)
                return sign + body.Replace(".", "");

            // Single dot: check digits after it
            var afterDot = body.Length - lastDot - 1;

            if (afterDot != 3)
            {
                // Not 3 digits → dot is decimal: "1234.56", "1234.5", "1234.5678"
                return sign + body; // Already in InvariantCulture format
            }

            // Single dot + exactly 3 digits → AMBIGUOUS ("1.234")
            return null;
        }

        // ─── Case 3: Only commas ─────────────────────────────────────
        if (commaCount > 0)
        {
            // Multiple commas → they're all thousands separators: "1,234,567"
            if (commaCount > 1)
                return sign + body.Replace(",", "");

            // Single comma: check digits after it
            var afterComma = body.Length - lastComma - 1;

            if (afterComma != 3)
            {
                // Not 3 digits → comma is decimal: "1234,56", "1234,5", "0,99"
                return sign + body.Replace(',', '.');
            }

            // Single comma + exactly 3 digits → AMBIGUOUS ("1,234")
            return null;
        }

        return null;
    }

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
            Separator = separator ?? ",",
            Schema = schema == null || schema.Length == 0 ? null : schema,
            ErrorAction = onError == null ? ReaderErrorAction.Throw : ReaderErrorAction.Skip,
            ErrorSink = onError == null ? NullErrorSink.Instance : new DelegatingErrorSink(onError, "(string)")
        };

        return AsCsvSync<T>(csvContent, options, cancellationToken);
    }

    // ==========================================
    // YAML from string (SYNC)
    // ==========================================

    public static IEnumerable<T> AsYamlSync<T>(
        string yamlContent,
        YamlReadOptions<T> options,
        CancellationToken cancellationToken = default)
    {
        if (yamlContent == null) throw new ArgumentNullException(nameof(yamlContent));

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(yamlContent));
        foreach (var item in YamlSync<T>(stream, options, filePath: "(string)", cancellationToken))
            yield return item;
    }

    public static IEnumerable<T> AsYamlSync<T>(
        string yamlContent,
        Action<Exception>? onError = null,
        CancellationToken cancellationToken = default)
    {
        if (yamlContent == null) throw new ArgumentNullException(nameof(yamlContent));

        var options = new YamlReadOptions<T>
        {
            ErrorAction = onError == null ? ReaderErrorAction.Throw : ReaderErrorAction.Skip,
            ErrorSink = onError == null ? NullErrorSink.Instance : new DelegatingErrorSink(onError, "(string)")
        };

        return AsYamlSync<T>(yamlContent, options, cancellationToken);
    }

    // ==========================================
    // JSON from string (SYNC)
    // ==========================================

    public static IEnumerable<T> AsJsonSync<T>(
        string jsonContent,
        JsonReadOptions<T> options,
        CancellationToken cancellationToken = default)
    {
        if (jsonContent == null) throw new ArgumentNullException(nameof(jsonContent));

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(jsonContent));
        foreach (var item in JsonSync<T>(stream, options, filePath: "(string)", cancellationToken))
            yield return item;
    }

    public static IEnumerable<T> AsJsonSync<T>(
        string jsonContent,
        JsonSerializerOptions? serializerOptions = null,
        Action<Exception>? onError = null,
        CancellationToken cancellationToken = default)
    {
        if (jsonContent == null) throw new ArgumentNullException(nameof(jsonContent));

        var options = new JsonReadOptions<T>
        {
            SerializerOptions = serializerOptions ?? new JsonSerializerOptions { PropertyNameCaseInsensitive = true },
            ErrorAction = onError == null ? ReaderErrorAction.Throw : ReaderErrorAction.Skip,
            ErrorSink = onError == null ? NullErrorSink.Instance : new DelegatingErrorSink(onError, "(string)")
        };

        return AsJsonSync<T>(jsonContent, options, cancellationToken);
    }

}

/// <summary>
/// Extension methods for parsing and inferring values from strings,
/// and for reading CSV/YAML/JSON content directly from string sources.
/// </summary>
public static class StringParsingExtensions
{
    /// <summary>
    /// Infers a value from the specified string using the default <see cref="TextParsingOptions"/>.
    /// This performs heuristic inference (e.g., bool/int/long/decimal/double/DateTime/Guid) and
    /// returns the inferred value, or the original string if no inference applies.
    /// </summary>
    /// <param name="value">The input text to infer from. If null, treated as empty string.</param>
    /// <returns>
    /// The inferred value as an object (which may be a primitive type or the original string).
    /// </returns>
    public static object Parse(string? value)
    => value.Parse(TextParsingOptions.Default);

    /// <summary>
    /// Infers a value from the specified string using the provided <see cref="TextParsingOptions"/>.
    /// This performs heuristic inference (e.g., bool/int/long/decimal/double/DateTime/Guid) and
    /// returns the inferred value, or the original string if no inference applies.
    /// </summary>
    /// <param name="value">The input text to infer from. If null, treated as empty string.</param>
    /// <param name="options">Parsing options controlling trimming, culture, and inference rules.</param>
    /// <returns>
    /// The inferred value as an object (which may be a primitive type or the original string).
    /// </returns>
    public static object Parse(this string? value, TextParsingOptions options)
        => TextParser.Infer(value, options);

    /// <summary>
    /// Parses the string as a specific target type <typeparamref name="T"/> using the provided options.
    /// Throws a <see cref="FormatException"/> if parsing fails.
    /// </summary>
    /// <typeparam name="T">The target type to parse into.</typeparam>
    /// <param name="s">The input string.</param>
    /// <param name="options">Parsing options controlling trimming and culture.</param>
    /// <returns>The parsed value of type <typeparamref name="T"/>.</returns>
    /// <exception cref="FormatException">Thrown when the value cannot be parsed as <typeparamref name="T"/>.</exception>
    public static T ParseAs<T>(this string? s, TextParsingOptions? options = null)
    {
        if (TextParser.TryParse(s, out T? value, options)) return value!;

        var targetType = typeof(T);
        var display = s is null ? "<null>" : s.Length <= 64 ? s : s.Substring(0, 61) + "...";
        throw new FormatException($"Could not parse value '{display}' as {targetType.FullName}.");
    }

    /// <summary>
    /// Attempts to infer a value from the specified string using the provided options.
    /// Returns true if a non-string primitive was inferred; otherwise returns false and outputs the original string.
    /// </summary>
    /// <param name="value">The input string.</param>
    /// <param name="options">Parsing options controlling trimming, culture, and inference rules.</param>
    /// <param name="result">When this method returns, contains the inferred value or the original string.</param>
    /// <returns>True if inference produced a non-string value; otherwise false.</returns>
    public static bool TryParse(this string? value, TextParsingOptions options, out object result)
        => TextParser.TryInfer(value, options, out result);

    /// <summary>
    /// Attempts to infer a value from the specified string using default options.
    /// Returns true if a non-string primitive was inferred; otherwise returns false and outputs the original string.
    /// </summary>
    /// <param name="value">The input string.</param>
    /// <param name="result">When this method returns, contains the inferred value or the original string.</param>
    /// <returns>True if inference produced a non-string value; otherwise false.</returns>
    public static bool TryParse(this string? value, out object result)
        => value.TryParse(TextParsingOptions.Default, out result);

    /// <summary>
    /// Attempts to infer a value of type <typeparamref name="T"/> from the specified string using default options.
    /// </summary>
    /// <typeparam name="T">The target type to infer.</typeparam>
    /// <param name="value">The input string.</param>
    /// <param name="result">When this method returns, contains the inferred value if successful; otherwise default.</param>
    /// <returns>True if inference succeeded and produced a value of type <typeparamref name="T"/>; otherwise false.</returns>
    public static bool TryInfer<T>(this string? value, out T? result)
        => TextParser.TryInfer(value, out result);

    /// <summary>
    /// Attempts to parse the string as the specified <see cref="Type"/>.
    /// </summary>
    /// <param name="s">The input string.</param>
    /// <param name="t">The target type to parse into.</param>
    /// <param name="value">When this method returns, contains the parsed value if successful; otherwise null.</param>
    /// <param name="options">Parsing options controlling trimming and culture.</param>
    /// <returns>True if parsing succeeded; otherwise false.</returns>
    public static bool TryParseAs(this string? s, Type t, out object? value, TextParsingOptions? options = null)
        => TextParser.TryParse(s, t, out value, options);

    /// <summary>
    /// Attempts to parse the string as the specified target type <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The target type to parse into.</typeparam>
    /// <param name="s">The input string.</param>
    /// <param name="value">When this method returns, contains the parsed value if successful; otherwise default.</param>
    /// <param name="options">Parsing options controlling trimming and culture.</param>
    /// <returns>True if parsing succeeded; otherwise false.</returns>
    public static bool TryParseAs<T>(this string? s, out T? value, TextParsingOptions? options = null)
        => TextParser.TryParse(s, out value, options);

    /// <summary>
    /// Returns true if the string consists only of ASCII digits (0-9) and is non-empty.
    /// Null or empty strings return false.
    /// </summary>
    /// <param name="value">The input string.</param>
    /// <returns>True if all characters are digits; otherwise false.</returns>
    public static bool IsAllDigits(this string? value)
        => value is { Length: > 0 } s && TextParser.IsAllDigits(s.AsSpan());

    // ==========================================
    // CSV from string (SYNC)
    // ==========================================

    /// <summary>
    /// Parses CSV content provided as a string using the specified <see cref="CsvReadOptions"/> (synchronous).
    /// </summary>
    /// <typeparam name="T">The record type to materialize.</typeparam>
    /// <param name="csvContent">The CSV content as UTF-16 string.</param>
    /// <param name="options">CSV reader options (separator, schema, error handling, etc.).</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>An enumerable of deserialized records.</returns>
    public static IEnumerable<T> AsCsv<T>(
        this string csvContent,
        CsvReadOptions options,
        CancellationToken cancellationToken = default)
        => TextParser.AsCsvSync<T>(csvContent, options, cancellationToken);

    /// <summary>
    /// Parses CSV content provided as a string using a simplified configuration (synchronous).
    /// </summary>
    /// <typeparam name="T">The record type to materialize.</typeparam>
    /// <param name="csvContent">The CSV content as UTF-16 string.</param>
    /// <param name="separator">The field separator (default ","). Only the first char is used.</param>
    /// <param name="onError">Optional error callback. If provided, rows with errors are skipped.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <param name="schema">Optional schema (column names) to apply.</param>
    /// <returns>An enumerable of deserialized records.</returns>
    public static IEnumerable<T> AsCsv<T>(
        this string csvContent,
        string separator = ",",
        Action<string, Exception>? onError = null,
        CancellationToken cancellationToken = default,
        params string[] schema)
        => TextParser.AsCsvSync<T>(csvContent, separator, onError, cancellationToken, schema);

    // ==========================================
    // YAML from string (SYNC)
    // ==========================================

    /// <summary>
    /// Parses YAML content provided as a string using the specified <see cref="YamlReadOptions{T}"/> (synchronous).
    /// </summary>
    /// <typeparam name="T">The record type to materialize.</typeparam>
    /// <param name="yamlContent">The YAML content as UTF-16 string.</param>
    /// <param name="options">YAML reader options (error handling, etc.).</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>An enumerable of deserialized records.</returns    >
    public static IEnumerable<T> AsYaml<T>(
        this string yamlContent,
        YamlReadOptions<T> options,
        CancellationToken cancellationToken = default)
        => TextParser.AsYamlSync(yamlContent, options, cancellationToken);

    /// <summary>
    /// Parses YAML content provided as a string using a simplified configuration (synchronous).
    /// </summary>
    /// <typeparam name="T">The record type to materialize.</typeparam>
    /// <param name="yamlContent">The YAML content as UTF-16 string.</param>
    /// <param name="onError">Optional error callback. If provided, items with errors are skipped.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>An enumerable of deserialized records.</returns>
    public static IEnumerable<T> AsYaml<T>(
        this string yamlContent,
        Action<Exception>? onError = null,
        CancellationToken cancellationToken = default)
        => TextParser.AsYamlSync<T>(yamlContent, onError, cancellationToken);

    // ==========================================
    // JSON from string (SYNC)
    // ==========================================

    /// <summary>
    /// Parses JSON content provided as a string using the specified <see cref="JsonReadOptions{T}"/> (synchronous).
    /// </summary>
    /// <typeparam name="T">The record type to materialize.</typeparam>
    /// <param name="jsonContent">The JSON content as UTF-16 string.</param>
    /// <param name="options">JSON reader options and serializer settings.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>An enumerable of deserialized records.</returns>
    public static IEnumerable<T> AsJson<T>(
        this string jsonContent,
        JsonReadOptions<T> options,
        CancellationToken cancellationToken = default)
        => TextParser.AsJsonSync(jsonContent, options, cancellationToken);

    /// <summary>
    /// Parses JSON content provided as a string using a simplified configuration (synchronous).
    /// </summary>
    /// <typeparam name="T">The record type to materialize.</typeparam>
    /// <param name="jsonContent">The JSON content as UTF-16 string.</param>
    /// <param name="serializerOptions">Optional <see cref="System.Text.Json.JsonSerializerOptions"/>.</param>
    /// <param name="onError">Optional error callback. If provided, items with errors are skipped.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>An enumerable of deserialized records.</returns>
    public static IEnumerable<T> AsJson<T>(
        this string jsonContent,
        JsonSerializerOptions? serializerOptions = null,
        Action<Exception>? onError = null,
        CancellationToken cancellationToken = default)
        => TextParser.AsJsonSync<T>(jsonContent, serializerOptions, onError, cancellationToken);
}
