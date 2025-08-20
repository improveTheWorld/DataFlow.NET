using DataFlow.Framework;

namespace DataFlow.Extensions
{
    /// <summary>
    /// Provides utility extension methods for working with <see cref="string"/> instances.
    /// Includes null/empty helpers, pattern checks, containment utilities, and small
    /// mutation helpers intended for lightweight text manipulation in streaming or
    /// batch processing scenarios.
    /// </summary>
    /// <remarks>
    /// Unless otherwise stated, all methods treat <c>null</c> inputs defensively:
    /// some return boolean defaults (e.g. <see cref="IsNullOrEmpty(string)"/>), while
    /// others expect a non-null source and may throw <see cref="ArgumentNullException"/>.
    /// </remarks>
    public static class StringExtensions
    {
        /// <summary>
        /// Returns the last valid zero-based character index of the specified string.
        /// </summary>
        /// <param name="text">The source string.</param>
        /// <returns>
        /// The index <c>text.Length - 1</c>.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="text"/> is <c>null</c>.</exception>
        /// <exception cref="ArgumentException">Thrown if <paramref name="text"/> is empty (no last index).</exception>
        /// <remarks>
        /// This is a convenience helper frequently used in slicing logic.
        /// </remarks>
        public static int LastIdx(this string text)
        {
            if (text is null) throw new ArgumentNullException(nameof(text));
            if (text.Length == 0) throw new ArgumentException("Cannot get last index of an empty string.", nameof(text));
            return text.Length - 1;
        }

        /// <summary>
        /// Determines whether the string starts with the specified <paramref name="start"/> substring
        /// and ends with the specified <paramref name="end"/> substring.
        /// </summary>
        /// <param name="text">The source string to evaluate.</param>
        /// <param name="start">The required starting substring.</param>
        /// <param name="end">The required ending substring.</param>
        /// <returns>
        /// <c>true</c> if <paramref name="text"/> starts with <paramref name="start"/> and ends with <paramref name="end"/>; otherwise <c>false</c>.
        /// </returns>
        /// <remarks>
        /// Performs two independent substring boundary checks. Returns <c>false</c> if any input is <c>null</c>.
        /// </remarks>
        public static bool IsBetween(this string text, string start, string end)
        {
            if (text == null || start == null || end == null) return false;
            return text.StartsWith(start) && text.EndsWith(end);
        }

        /// <summary>
        /// Determines whether the string begins with any of the provided candidate prefixes.
        /// </summary>
        /// <param name="value">The string to test.</param>
        /// <param name="acceptedStarts">A sequence of potential starting substrings.</param>
        /// <returns>
        /// <c>true</c> if <paramref name="value"/> starts with at least one candidate; otherwise <c>false</c>.
        /// Returns <c>false</c> if <paramref name="acceptedStarts"/> is <c>null</c> or empty.
        /// </returns>
        /// <remarks>
        /// Evaluation stops at the first match (short-circuit).
        /// </remarks>
        public static bool StartsWith(this string value, IEnumerable<string> acceptedStarts)
        {
            if (value == null || acceptedStarts == null) return false;
            return acceptedStarts.Select(candidate => value.StartsWith(candidate))
                                 .FirstOrDefault(match => match);
        }

        /// <summary>
        /// Indicates whether the specified string is <c>null</c> or an empty string (<c>""</c>).
        /// </summary>
        /// <param name="text">The string to test.</param>
        /// <returns>
        /// <c>true</c> if the <paramref name="text"/> parameter is <c>null</c> or an empty string (<c>""</c>); otherwise, <c>false</c>.
        /// </returns>
        public static bool IsNullOrEmpty(this string text) => string.IsNullOrEmpty(text);

        /// <summary>
        /// Indicates whether a specified string is <c>null</c>, empty, or consists only of white-space characters.
        /// </summary>
        /// <param name="text">The string to test.</param>
        /// <returns>
        /// <c>true</c> if the <paramref name="text"/> parameter is <c>null</c> or <see cref="string.Empty"/>, or if <paramref name="text"/> consists exclusively of white-space characters.
        /// </returns>
        public static bool IsNullOrWhiteSpace(this string text) => string.IsNullOrWhiteSpace(text);

        /// <summary>
        /// Determines whether the specified line contains <em>any</em> of the provided token substrings.
        /// </summary>
        /// <param name="line">The line of text to inspect (must not be <c>null</c>).</param>
        /// <param name="tokens">A sequence of substrings to search for. If <c>null</c> or empty, returns <c>false</c>.</param>
        /// <returns>
        /// <c>true</c> if at least one token is found within <paramref name="line"/>; otherwise <c>false</c>.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="line"/> is <c>null</c>.</exception>
        /// <remarks>
        /// This method short-circuits on the first match. Internally uses <see cref="string.Contains(string)"/>.
        /// </remarks>
        public static bool ContainsAny(this string line, IEnumerable<string> tokens)
        {
            Guard.AgainstNullArgument(nameof(line), line);
            if (tokens == null) return false;
            var match = tokens.FirstOrDefault(x => x != null && line.Contains(x));
            return !match.IsNullOrEmpty();
        }

        /// <summary>
        /// Replaces a slice of the string at a given index and length with the specified insertion string.
        /// </summary>
        /// <param name="value">The original string.</param>
        /// <param name="index">
        /// The index whose preceding portion (inclusive of <paramref name="index"/>) is kept.
        /// Characters from <c>index + 1</c> forward will be concatenated after the insertion (minus the removed range).
        /// </param>
        /// <param name="length">The length of the section to remove starting at <paramref name="index"/>.</param>
        /// <param name="toInsert">The replacement text to insert after the preserved prefix.</param>
        /// <returns>A new string containing the modified content.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="value"/> is <c>null</c>.</exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown if <paramref name="index"/> or <paramref name="length"/> specify an invalid removal range.
        /// </exception>
        /// <remarks>
        /// This helper is specialized and differs subtly from <see cref="string.Remove(int,int)"/> + insert logic:
        /// It preserves characters up to (and including) <paramref name="index"/>, removes the next <paramref name="length"/> characters
        /// (starting at <c>index + 1</c>), then appends the remainder.
        /// </remarks>
        public static string ReplaceAt(this string value, int index, int length, string toInsert)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));
            if (index < 0 || index >= value.Length) throw new ArgumentOutOfRangeException(nameof(index));
            if (length < 0 || index + length > value.Length) throw new ArgumentOutOfRangeException(nameof(length));

            return value.Substring(0, index + 1) + toInsert + value.Substring(index + length);
        }
    }

    /// <summary>
    /// Represents a mutable window (sub-region) over an original immutable string without copying
    /// the underlying character data until materialization (e.g., via <see cref="ToString"/>).
    /// Provides trimming operations that adjust start and end boundaries safely.
    /// </summary>
    /// <remarks>
    /// Indices are clamped to valid ranges:
    /// <list type="bullet">
    /// <item><description><c>StartIndex</c> is never greater than <c>EndIndex</c>.</description></item>
    /// <item><description><c>EndIndex</c> is never less than <c>StartIndex</c> or beyond the last character.</description></item>
    /// </list>
    /// The effective substring length is <c>EndIndex - StartIndex</c>. An empty region (length 0) is treated as empty.
    /// </remarks>
    public class Subpart
    {
        private readonly string _originalString;
        private int _startIndex;
        private int _endIndex;

        /// <summary>
        /// The clamped starting index (inclusive) of the window.
        /// Internal logic ensures it never exceeds <see cref="_endIndex"/>.
        /// </summary>
        private int StartIndex
        {
            get => _startIndex;
            set
            {
                if (value < 0) value = 0;
                if (value > _endIndex) value = _endIndex;
                _startIndex = value;
            }
        }

        /// <summary>
        /// The clamped ending index (exclusive of length semantics, but used with <c>Substring(start, length)</c>).
        /// Adjusted so it never precedes <see cref="_startIndex"/> and never exceeds the last valid character index.
        /// </summary>
        private int EndIndex
        {
            get => _endIndex;
            set
            {
                if (value < _startIndex) value = _startIndex;
                if (value > _originalString.Length - 1) value = _originalString.Length - 1;
                _endIndex = value;
            }
        }

        /// <summary>
        /// Gets the computed length of the current window (difference between end and start indices).
        /// </summary>
        private int Length => EndIndex - StartIndex;

        /// <summary>
        /// Indicates whether the represented substring is effectively empty
        /// (either because the original string is null/empty or the window length is zero).
        /// </summary>
        /// <returns><c>true</c> if empty; otherwise <c>false</c>.</returns>
        public bool IsNullOrEmpty() =>
            _originalString.IsNullOrEmpty() || EndIndex == StartIndex;

        /// <summary>
        /// Initializes a new <see cref="Subpart"/> with the specified original string and inclusive index bounds.
        /// </summary>
        /// <param name="originalString">The original (non-null) string.</param>
        /// <param name="startIndex">The starting index (will be clamped if out of range).</param>
        /// <param name="endIndex">The ending index (will be clamped; must be &gt;= start).</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="originalString"/> is <c>null</c>.</exception>
        internal Subpart(string originalString, int startIndex, int endIndex)
        {
            _originalString = originalString ?? throw new ArgumentNullException(nameof(originalString));
            _startIndex = startIndex;
            _endIndex = endIndex;
        }

        /// <summary>
        /// Returns the current window content as a new string.
        /// </summary>
        /// <returns>The substring represented by this window; may be empty.</returns>
        public override string ToString()
        {
            return Length <= 0
                ? string.Empty
                : _originalString.Substring(StartIndex, Length);
        }

        /// <summary>
        /// Determines equality with a <see cref="string"/> instance, comparing by content length and characters.
        /// </summary>
        /// <param name="obj">The object to compare (only strings are considered).</param>
        /// <returns><c>true</c> if equal by content; otherwise <c>false</c>.</returns>
        public override bool Equals(object obj)
        {
            if (obj is string str)
            {
                return Equals(str);
            }
            return false;
        }

        /// <summary>
        /// Compares this substring window to a provided <see cref="string"/> for character-by-character equality.
        /// </summary>
        /// <param name="other">The string to compare against.</param>
        /// <returns><c>true</c> if lengths and contents match; otherwise <c>false</c>.</returns>
        public bool Equals(string other)
        {
            if (other == null || other.Length != Length)
            {
                return false;
            }

            for (int i = 0; i < Length; i++)
            {
                if (_originalString[StartIndex + i] != other[i])
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Produces a hash code based on the original string reference and current window indices.
        /// </summary>
        /// <returns>A hash code representing this window.</returns>
        public override int GetHashCode()
        {
            return (_originalString, StartIndex, EndIndex).GetHashCode();
        }

        /// <summary>
        /// Equality operator for comparing a <see cref="Subpart"/> instance to a <see cref="string"/> value.
        /// </summary>
        public static bool operator ==(Subpart subpart, string str) =>
            subpart?.Equals(str) ?? str == null;

        /// <summary>
        /// Inequality operator for comparing a <see cref="Subpart"/> instance to a <see cref="string"/> value.
        /// </summary>
        public static bool operator !=(Subpart subpart, string str) =>
            !(subpart == str);

        /// <summary>
        /// Trims (moves) both the start and end boundaries inward by the specified amounts.
        /// </summary>
        /// <param name="start">Number of characters to trim from the start.</param>
        /// <param name="end">Number of characters to trim from the end.</param>
        /// <returns>The same <see cref="Subpart"/> instance (for fluent chaining).</returns>
        /// <remarks>
        /// Clamping rules prevent start from surpassing end and end from under-running start.
        /// </remarks>
        public Subpart Trim(int start, int end)
        {
            TrimStart(start);
            TrimEnd(end);
            return this;
        }

        /// <summary>
        /// Trims (moves forward) the start boundary by the specified number of characters.
        /// </summary>
        /// <param name="steps">The number of characters to advance the start boundary.</param>
        /// <returns>The same <see cref="Subpart"/> instance.</returns>
        public Subpart TrimStart(int steps)
        {
            StartIndex += steps;
            return this;
        }

        /// <summary>
        /// Trims (moves backward) the end boundary by the specified number of characters.
        /// </summary>
        /// <param name="steps">The number of characters to retract the end boundary.</param>
        /// <returns>The same <see cref="Subpart"/> instance.</returns>
        public Subpart TrimEnd(int steps)
        {
            EndIndex -= steps;
            return this;
        }
    }

    /// <summary>
    /// Provides factory-like extension methods for creating <see cref="Subpart"/> instances over strings.
    /// </summary>
    public static class StringSubPartExtensions
    {
        /// <summary>
        /// Creates a <see cref="Subpart"/> representing an index window over the original string.
        /// </summary>
        /// <param name="originalString">The source string (must not be <c>null</c>).</param>
        /// <param name="startIndex">The initial start index (inclusive).</param>
        /// <param name="endIndex">
        /// The initial end index (inclusive upper boundary reference; internally the usable length is computed as
        /// <c>endIndex - startIndex</c>). Must be within the string bounds.
        /// </param>
        /// <returns>A new <see cref="Subpart"/> instance.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="originalString"/> is <c>null</c>.</exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown if <paramref name="startIndex"/> or <paramref name="endIndex"/> are outside legal bounds
        /// (<c>0 &lt;= startIndex &lt;= endIndex &lt; originalString.Length</c>).
        /// </exception>
        /// <remarks>
        /// Guard checks ensure indices are valid before constructing. The <see cref="Subpart"/> itself will clamp
        /// further adjustments performed via trimming operations.
        /// </remarks>
        public static Subpart SubPart(this string originalString, int startIndex, int endIndex)
        {
            Guard.AgainstNullArgument(nameof(originalString), originalString);
            Guard.AgainstOutOfRange(nameof(startIndex), startIndex, 0, endIndex);
            Guard.AgainstOutOfRange(nameof(endIndex), endIndex, startIndex, originalString.Length - 1);
            return new Subpart(originalString, startIndex, endIndex);
        }
    }
}
