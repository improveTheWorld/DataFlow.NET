using DataFlow.Framework;

using System.Text;
using System.Text.RegularExpressions;


using DataFlow.Extensions;

/// <summary>
/// Provides a set of extension methods for processing sequences of strings (both IEnumerable and IAsyncEnumerable)
/// with advanced, regex-based tokenization and transformation capabilities.
/// </summary>
public static class StringSequenceExtensions
{
    #region IEnumerable Extensions


    /// <summary>
    /// Tokenizes each line using the provided regex patterns and flattens the results into a single, continuous sequence of named tokens.
    /// A special End-Of-File (EOF) token is injected after each line's tokens.
    /// </summary>
    /// <param name="lines">The input sequence of strings to tokenize.</param>
    /// <param name="patterns">An array of regex patterns to use for tokenization.</param>
    /// <returns>A single flattened <see cref="IEnumerable{T}"/> of (string groupName, string subpart) tuples representing all tokens from all lines.</returns>
    /// <remarks>
    /// This method is a lazy transformation. It injects a special '(RegexTokenizer.UNMATCHED.EOF, Environment.NewLine)' token
    /// after processing each line. This token acts as a boundary marker, which is essential for reconstruction methods like 'ToLines'.
    /// </remarks>
    public static IEnumerable<(string groupName, string subpart)> TokenAndFlatten(this IEnumerable<string> lines, params string[] patterns)
        => lines.SelectMany(l => l.Tokenize(new RegexTokenizer(patterns)).Append((RegexTokenizer.UNMATCHED.EOF, Environment.NewLine)));

    /// <summary>
    /// Tokenizes each line into a list of named tokens, preserving the original line-by-line structure of the input sequence.
    /// </summary>
    /// <param name="lines">The input sequence of strings to tokenize.</param>
    /// <param name="patterns">An array of regex patterns to use for tokenization.</param>
    /// <returns>An <see cref="IEnumerable{T}"/> where each item is a <see cref="List{T}"/> of (string groupName, string subpart) tuples, representing the tokens for a single line.</returns>
    /// <remarks>
    /// This is a lazy transformation. Each line is tokenized as it is enumerated from the source sequence.
    /// </remarks>
    public static IEnumerable<List<(string groupName, string subpart)>> Tokenize(this IEnumerable<string> lines, params string[] patterns)
    {
        var regxs = new RegexTokenizer(patterns);
        return lines.Select(line => line.Tokenize(regxs).ToList());
    }



    #endregion

    #region IAsyncEnumerable Extensions


    /// <summary>
    /// Asynchronously tokenizes each line using the provided regex patterns and flattens the results into a single, continuous sequence of named tokens.
    /// A special End-Of-File (EOF) token is injected after each line's tokens.
    /// </summary>
    /// <param name="lines">The input asynchronous sequence of strings to tokenize.</param>
    /// <param name="patterns">An array of regex patterns to use for tokenization.</param>
    /// <returns>A single flattened <see cref="IAsyncEnumerable{T}"/> of (string groupName, string subpart) tuples representing all tokens from all lines.</returns>
    /// <remarks>
    /// This method is a lazy asynchronous transformation. It injects a special '(RegexTokenizer.UNMATCHED.EOF, Environment.NewLine)' token
    /// after processing each line. This token acts as a boundary marker, which is essential for reconstruction methods like 'ToLines'.
    /// </remarks>
    public static IAsyncEnumerable<(string groupName, string subpart)> TokenAndFlatten(this IAsyncEnumerable<string> lines, params string[] patterns)
        => lines.SelectMany(l => l.Tokenize(new RegexTokenizer(patterns)).Append((RegexTokenizer.UNMATCHED.EOF, Environment.NewLine)));

    /// <summary>
    /// Asynchronously tokenizes each line into a list of named tokens, preserving the original line-by-line structure of the input stream.
    /// </summary>
    /// <param name="lines">The input asynchronous sequence of strings to tokenize.</param>
    /// <param name="patterns">An array of regex patterns to use for tokenization.</param>
    /// <returns>An <see cref="IAsyncEnumerable{T}"/> where each item is a <see cref="List{T}"/> of (string groupName, string subpart) tuples, representing the tokens for a single line.</returns>
    /// <remarks>
    /// This is a lazy asynchronous transformation. Each line is tokenized as it is enumerated from the source stream.
    /// </remarks>
    public static IAsyncEnumerable<List<(string groupName, string subpart)>> Tokenize(this IAsyncEnumerable<string> lines, params string[] patterns)
    {
        var regxs = new RegexTokenizer(patterns);
        return lines.Select(line => line.Tokenize(regxs).ToList());
    }

    #endregion
}



//public static class StringEnumerable_Mapper
//{
//    public static IEnumerable<EnumerableWithNote<(string groupName, (int startIndex, int length) slice), string>> Slices(this IEnumerable<string> lines, Regxes regxs)
//   => lines.Select(l => l.Slices(regxs).WithNote(l));

//    public static IEnumerable<(string groupName, string subpart)> Map(this IEnumerable<string> lines, params string[] patterns)
//    => lines.SelectMany(l => l.Map(new Regxes(patterns)).Append((Regxes.UNMATCHED.EOF, Environment.NewLine)));

//    public static IEnumerable<List<(string groupName, string subpart)>> MapLines(this IEnumerable<string> lines, params string[] patterns)
//    {
//        var regxs = new Regxes(patterns);
//        return lines.Select(line => line.Map(regxs).ToList());
//    }


//    public static StringBuilder Append(StringBuilder builder, IEnumerable<string> lines, Regxes regxs, params (string groupName, Func<string, string> transformation)[] transformations)
//    {

//        var rgxRequests = new Dictionary<string /*groupName*/, Func<string, string> /*transformation*/>();
//        transformations.ForEach(_ => rgxRequests[_.groupName] = _.transformation).Do();

//        foreach (var line in lines)
//        {
//            builder.Build(line, line.Slices(regxs), rgxRequests);
//            builder.AppendLine();
//        }
//        return builder;
//    }


//    public static IEnumerable<T?> CSVs<T>(this IEnumerable<string> lines, string[] schema, string separator = ";")
//                                                  => lines.Where(line => !line.IsNullOrWhiteSpace())
//                                                          .Select(line => line.GetCSV<T>(schema, separator))


