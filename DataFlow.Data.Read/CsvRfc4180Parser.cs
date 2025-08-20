// RFC 4180 CSV parser (synchronous & asynchronous helpers).
using System.Runtime.CompilerServices;
using System.Text;

namespace DataFlow.Data;

internal static class CsvRfc4180Parser
{
    internal static IEnumerable<string[]> Parse(TextReader reader, CsvReadOptions options)
    {
        var sb = new StringBuilder();
        var fields = new List<string>(32);
        string? line;
        bool inQuotes = false;
        long physicalLine = 0;
        long recordNumber = 0;

        void CommitField()
        {
            fields.Add(options.TrimWhitespace ? sb.ToString().Trim() : sb.ToString());
            sb.Clear();
        }

        while ((line = reader.ReadLine()) != null)
        {
            options.CancellationToken.ThrowIfCancellationRequested();
            physicalLine++;
            options.Metrics.LinesRead = physicalLine;
            var span = line.AsSpan();
            int i = 0;
            while (i < span.Length)
            {
                char c = span[i];

                if (inQuotes)
                {
                    if (c == '"')
                    {
                        if (i + 1 < span.Length && span[i + 1] == '"')
                        {
                            sb.Append('"');
                            i += 2;
                            continue;
                        }
                        inQuotes = false;
                        i++;
                        continue;
                    }
                    else
                    {
                        sb.Append(c);
                        i++;
                        continue;
                    }
                }
                else
                {
                    if (c == '"')
                    {
                        inQuotes = true;
                        i++;
                        continue;
                    }
                    else if (c == options.Separator)
                    {
                        CommitField();
                        i++;
                        continue;
                    }
                    else
                    {
                        sb.Append(c);
                        i++;
                        continue;
                    }
                }
            }

            // End of physical line
            if (inQuotes)
            {
                // Append newline and continue reading next physical line
                sb.Append('\n');
                continue;
            }

            // Complete record
            CommitField();
            recordNumber++;
            options.Metrics.RecordsRead = recordNumber;

            if (options.ShouldEmitProgress()) options.EmitProgress();

            yield return fields.ToArray();
            fields.Clear();
            sb.Clear();

            if (options.Metrics.TerminatedEarly)
                yield break;
        }

        if (inQuotes)
        {
            // Unterminated quoted field
            if (!options.HandleError("CSV", options.Metrics.LinesRead, options.Metrics.RecordsRead + 1, options.FilePath ?? "",
                "CsvFormatError", "Unterminated quoted field at EOF", sb.ToString().Length > 128 ? sb.ToString()[..128] : sb.ToString()))
            {
                yield break;
            }
        }
    }

    internal static async IAsyncEnumerable<string[]> ParseAsync(StreamReader reader, CsvReadOptions options, [EnumeratorCancellation] CancellationToken ct = default)
    {
        var sb = new StringBuilder();
        var fields = new List<string>(32);
        string? line;
        bool inQuotes = false;
        long physicalLine = 0;
        long recordNumber = 0;

        void CommitField()
        {
            fields.Add(options.TrimWhitespace ? sb.ToString().Trim() : sb.ToString());
            sb.Clear();
        }

        while ((line = await reader.ReadLineAsync()) != null)
        {
            ct.ThrowIfCancellationRequested();
            options.CancellationToken.ThrowIfCancellationRequested();
            physicalLine++;
            options.Metrics.LinesRead = physicalLine;

            var span = line.AsSpan();
            int i = 0;
            while (i < span.Length)
            {
                char c = span[i];

                if (inQuotes)
                {
                    if (c == '"')
                    {
                        if (i + 1 < span.Length && span[i + 1] == '"')
                        {
                            sb.Append('"');
                            i += 2;
                            continue;
                        }
                        inQuotes = false;
                        i++;
                        continue;
                    }
                    else
                    {
                        sb.Append(c);
                        i++;
                        continue;
                    }
                }
                else
                {
                    if (c == '"')
                    {
                        inQuotes = true;
                        i++;
                        continue;
                    }
                    else if (c == options.Separator)
                    {
                        CommitField();
                        i++;
                        continue;
                    }
                    else
                    {
                        sb.Append(c);
                        i++;
                        continue;
                    }
                }
            }

            if (inQuotes)
            {
                sb.Append('\n');
                continue;
            }

            CommitField();
            recordNumber++;
            options.Metrics.RecordsRead = recordNumber;

            if (options.ShouldEmitProgress()) options.EmitProgress();

            yield return fields.ToArray();
            fields.Clear();
            sb.Clear();

            if (options.Metrics.TerminatedEarly)
                yield break;
        }

        if (inQuotes)
        {
            if (!options.HandleError("CSV", options.Metrics.LinesRead, options.Metrics.RecordsRead + 1, options.FilePath ?? "",
                "CsvFormatError", "Unterminated quoted field at EOF", sb.ToString().Length > 128 ? sb.ToString()[..128] : sb.ToString()))
            {
                yield break;
            }
        }
    }
}