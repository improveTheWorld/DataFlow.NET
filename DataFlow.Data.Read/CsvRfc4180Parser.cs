// Entire file replaced: new high-fidelity RFC 4180 parser with quote/line ending controls & raw record capture.
using System.Text;

namespace DataFlow.Data;

internal static class CsvRfc4180Parser
{
    private const int BufferSize = 8192;

    internal static IEnumerable<string[]> Parse(TextReader reader, CsvReadOptions options)
    {
        var fields = new List<string>(32);
        var fieldSb = new StringBuilder(256);
        var rawRecordSb = options.CaptureRawRecord || options.RawRecordObserver != null
            ? new StringBuilder(512)
            : null;

        char[] buffer = new char[BufferSize];
        int read;
        bool inQuotes = false;
        bool afterClosingQuote = false;
        bool atStartOfField = true;

        long recordNumber = 0;
        long physicalLine = 0;
        bool lastCharWasCR = false;

        // This variable holds a completed record until we yield it.
        // (We defer yield so that EmitRecord can stay a void local function.)
        string[]? pendingRecord = null;

        void CommitField()
        {
            string val = options.TrimWhitespace ? fieldSb.ToString().Trim() : fieldSb.ToString();
            fields.Add(val);
            fieldSb.Clear();
            atStartOfField = true;
            afterClosingQuote = false;
        }

        void EmitRecord()
        {
            recordNumber++;
            physicalLine++;
            options.Metrics.RawRecordsParsed = recordNumber;
            options.Metrics.LinesRead = physicalLine;

            // Guard rail: MaxColumnsPerRow
            if (options.MaxColumnsPerRow > 0 && fields.Count > options.MaxColumnsPerRow)
            {
                if (!options.HandleError(
                        "CSV",
                        physicalLine,
                        recordNumber,
                        options.FilePath ?? "",
                        "CsvLimitExceeded",
                        $"Row has {fields.Count} columns (limit {options.MaxColumnsPerRow}).",
                        GetExcerpt(rawRecordSb)))
                {
                    pendingRecord = null;
                    return;
                }
                // Skip behavior: do not emit this record
                pendingRecord = null;
                fields.Clear();
                fieldSb.Clear();
                afterClosingQuote = false;
                atStartOfField = true;
                rawRecordSb?.Clear();
                return;
            }

            // Guard rail: MaxRawRecordLength
            if (options.MaxRawRecordLength > 0 && rawRecordSb != null && rawRecordSb.Length > options.MaxRawRecordLength)
            {
                if (!options.HandleError(
                        "CSV",
                        physicalLine,
                        recordNumber,
                        options.FilePath ?? "",
                        "CsvLimitExceeded",
                        $"Raw record length {rawRecordSb.Length} exceeds limit {options.MaxRawRecordLength}.",
                        GetExcerpt(rawRecordSb)))
                {
                    pendingRecord = null;
                    return;
                }
                // Skip record if continuing
                pendingRecord = null;
                fields.Clear();
                fieldSb.Clear();
                afterClosingQuote = false;
                atStartOfField = true;
                rawRecordSb?.Clear();
                return;
            }

            var arr = fields.ToArray();
            pendingRecord = arr;

            if (rawRecordSb != null)
            {
                string raw = rawRecordSb.ToString();
                if (!options.PreserveLineEndings && options.NormalizeNewlinesInFields)
                    raw = raw.Replace("\r\n", "\n");
                options.RawRecordObserver?.Invoke(recordNumber, raw);
                rawRecordSb.Clear();
            }

            fields.Clear();
            fieldSb.Clear();
            atStartOfField = true;
            afterClosingQuote = false;

            if (options.ShouldEmitProgress())
                options.EmitProgress();
        }

        while ((read = reader.Read(buffer, 0, buffer.Length)) > 0)
        {
            options.CancellationToken.ThrowIfCancellationRequested();

            for (int i = 0; i < read; i++)
            {
                char c = buffer[i];

                // Capture raw characters for auditing if requested
                if (rawRecordSb != null) rawRecordSb.Append(c);

                // Handle CR that might indicate CRLF across buffer boundaries
                if (lastCharWasCR)
                {
                    lastCharWasCR = false;
                    if (c == '\n')
                    {
                        // This LF completes a prior CRLF already processed when committing record.
                        continue;
                    }
                }

                if (inQuotes)
                {
                    if (c == '"')
                    {
                        // Escaped double quote?
                        if (i + 1 < read && buffer[i + 1] == '"')
                        {
                            fieldSb.Append('"');
                            if (rawRecordSb != null) rawRecordSb.Append(buffer[i + 1]);
                            i++;
                            continue;
                        }
                        // Closing quote
                        inQuotes = false;
                        afterClosingQuote = true;
                        continue;
                    }
                    fieldSb.Append(c);
                    continue;
                }
                else if (afterClosingQuote)
                {
                    if (c == options.Separator)
                    {
                        CommitField();
                        continue;
                    }
                    if (c == '\r' || c == '\n')
                    {
                        CommitField();
                        if (c == '\r')
                        {
                            // Lookahead for LF inside current buffer
                            if (i + 1 < read)
                            {
                                if (buffer[i + 1] == '\n')
                                {
                                    if (rawRecordSb != null) rawRecordSb.Append(buffer[i + 1]);
                                    i++;
                                }
                            }
                            else
                            {
                                // CR might be at end-of-buffer; mark to skip next leading LF
                                lastCharWasCR = true;
                            }
                        }
                        EmitRecord();
                        if (pendingRecord != null)
                        {
                            var done = pendingRecord;
                            pendingRecord = null;
                            yield return done;
                        }
                        if (options.Metrics.TerminatedEarly) yield break;
                        continue;
                    }
                    if (options.ErrorOnTrailingGarbageAfterClosingQuote)
                    {
                        if (!options.HandleError(
                                "CSV",
                                physicalLine + 1,
                                recordNumber + 1,
                                options.FilePath ?? "",
                                "CsvQuoteError",
                                $"Illegal character '{Printable(c)}' after closing quote.",
                                GetExcerpt(rawRecordSb)))
                            yield break;
                    }
                    // Treat as literal continuation when leniency chosen
                    fieldSb.Append(c);
                    afterClosingQuote = false;
                    continue;
                }
                else
                {
                    if (atStartOfField && c == '"')
                    {
                        inQuotes = true;
                        atStartOfField = false;
                        continue;
                    }

                    if (c == '"')
                    {
                        switch (options.QuoteMode)
                        {
                            case CsvQuoteMode.RfcStrict:
                            case CsvQuoteMode.ErrorOnIllegalQuote:
                                if (!options.HandleError(
                                        "CSV",
                                        physicalLine + 1,
                                        recordNumber + 1,
                                        options.FilePath ?? "",
                                        "CsvQuoteError",
                                        "Illegal quote character inside unquoted field.",
                                        GetExcerpt(rawRecordSb)))
                                    yield break;
                                if (options.QuoteMode == CsvQuoteMode.RfcStrict)
                                {
                                    // Keep literal quote if in strict-but-non-fatal mode
                                    fieldSb.Append('"');
                                }
                                continue;

                            case CsvQuoteMode.Lenient:
                                inQuotes = true;
                                atStartOfField = false;
                                continue;
                        }
                    }

                    if (c == options.Separator)
                    {
                        CommitField();
                        continue;
                    }

                    if (c == '\r' || c == '\n')
                    {
                        CommitField();
                        if (c == '\r')
                        {
                            if (i + 1 < read)
                            {
                                if (buffer[i + 1] == '\n')
                                {
                                    if (rawRecordSb != null) rawRecordSb.Append(buffer[i + 1]);
                                    i++;
                                }
                            }
                            else
                            {
                                lastCharWasCR = true;
                            }
                        }
                        EmitRecord();
                        if (pendingRecord != null)
                        {
                            var done = pendingRecord;
                            pendingRecord = null;
                            yield return done;
                        }
                        if (options.Metrics.TerminatedEarly) yield break;
                        continue;
                    }

                    fieldSb.Append(c);
                    atStartOfField = false;
                    continue;
                }
            }

            if (pendingRecord != null)
            {
                var done = pendingRecord;
                pendingRecord = null;
                yield return done;
                if (options.Metrics.TerminatedEarly) yield break;
            }
        }

        // EOF handling
        if (inQuotes)
        {
            if (!options.HandleError(
                    "CSV",
                    physicalLine,
                    recordNumber + 1,
                    options.FilePath ?? "",
                    "CsvQuoteError",
                    "Unterminated quoted field at EOF.",
                    GetExcerpt(rawRecordSb)))
                yield break;
        }

        // Emit any final (possibly empty) last record
        if (fieldSb.Length > 0 || fields.Count > 0 || !atStartOfField)
        {
            CommitField();
            EmitRecord();
            if (pendingRecord != null)
            {
                var done = pendingRecord;
                pendingRecord = null;
                yield return done;
            }
        }
    }

    internal static async IAsyncEnumerable<string[]> ParseAsync(StreamReader reader, CsvReadOptions options, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        var fields = new List<string>(32);
        var fieldSb = new StringBuilder(256);
        var rawRecordSb = options.CaptureRawRecord || options.RawRecordObserver != null ? new StringBuilder(512) : null;

        char[] buffer = new char[BufferSize];
        int read;
        bool inQuotes = false;
        bool afterClosingQuote = false;
        bool atStartOfField = true;

        long recordNumber = 0;
        long physicalLine = 0;
        bool lastCharWasCR = false;

        void CommitField()
        {
            string val = options.TrimWhitespace ? fieldSb.ToString().Trim() : fieldSb.ToString();
            fields.Add(val);
            fieldSb.Clear();
            atStartOfField = true;
            afterClosingQuote = false;
        }

        string[] MakeRecord()
        {
            recordNumber++;
            physicalLine++;
            options.Metrics.RawRecordsParsed = recordNumber;
            options.Metrics.LinesRead = physicalLine;

            // Guard rail: MaxColumnsPerRow
            if (options.MaxColumnsPerRow > 0 && fields.Count > options.MaxColumnsPerRow)
            {
                if (!options.HandleError("CSV",
                                         physicalLine,
                                         recordNumber,
                                         options.FilePath ?? "",
                                         "CsvLimitExceeded",
                                         $"Row has {fields.Count} columns (limit {options.MaxColumnsPerRow}).",
                                         GetExcerpt(rawRecordSb)))
                {
                    return Array.Empty<string>(); // termination logic handled by caller via Metrics.TerminatedEarly
                }
                // Skip this record
                fields.Clear();
                fieldSb.Clear();
                atStartOfField = true;
                afterClosingQuote = false;
                rawRecordSb?.Clear();
                return Array.Empty<string>();
            }

            // Guard rail: MaxRawRecordLength
            if (options.MaxRawRecordLength > 0 && rawRecordSb != null && rawRecordSb.Length > options.MaxRawRecordLength)
            {
                if (!options.HandleError("CSV",
                                         physicalLine,
                                         recordNumber,
                                         options.FilePath ?? "",
                                         "CsvLimitExceeded",
                                         $"Raw record length {rawRecordSb.Length} exceeds limit {options.MaxRawRecordLength}.",
                                         GetExcerpt(rawRecordSb)))
                {
                    return Array.Empty<string>();
                }
                // Skip
                fields.Clear();
                fieldSb.Clear();
                atStartOfField = true;
                afterClosingQuote = false;
                rawRecordSb?.Clear();
                return Array.Empty<string>();
            }
    
            var arr = fields.ToArray();

            if (rawRecordSb != null)
            {
                string raw = rawRecordSb.ToString();
                if (!options.PreserveLineEndings && options.NormalizeNewlinesInFields)
                    raw = raw.Replace("\r\n", "\n");
                options.RawRecordObserver?.Invoke(recordNumber, raw);
                rawRecordSb.Clear();
            }

            fields.Clear();
            fieldSb.Clear();
            atStartOfField = true;
            afterClosingQuote = false;

            if (options.ShouldEmitProgress()) options.EmitProgress();

            return arr;
        }

        while ((read = await reader.ReadAsync(buffer.AsMemory(0, buffer.Length), ct)) > 0)
        {
            ct.ThrowIfCancellationRequested();
            options.CancellationToken.ThrowIfCancellationRequested();

            for (int i = 0; i < read; i++)
            {
                char c = buffer[i];
                if (rawRecordSb != null) rawRecordSb.Append(c);

                if (lastCharWasCR)
                {
                    lastCharWasCR = false;
                    if (c == '\n')
                    {
                        continue; // consumed as part of previous CRLF
                    }
                }

                if (inQuotes)
                {
                    if (c == '"')
                    {
                        if (i + 1 < read && buffer[i + 1] == '"')
                        {
                            fieldSb.Append('"');
                            if (rawRecordSb != null) rawRecordSb.Append(buffer[i + 1]);
                            i++;
                            continue;
                        }
                        inQuotes = false;
                        afterClosingQuote = true;
                        continue;
                    }
                    fieldSb.Append(c);
                    continue;
                }
                else if (afterClosingQuote)
                {
                    if (c == options.Separator)
                    {
                        CommitField();
                        continue;
                    }
                    if (c == '\r' || c == '\n')
                    {
                        CommitField();
                        if (c == '\r')
                        {
                            if (i + 1 < read)
                            {
                                if (buffer[i + 1] == '\n')
                                {
                                    if (rawRecordSb != null) rawRecordSb.Append(buffer[i + 1]);
                                    i++;
                                }
                            }
                            else
                            {
                                lastCharWasCR = true;
                            }
                        }
                        var rec = MakeRecord();
                        yield return rec;
                        if (options.Metrics.TerminatedEarly) yield break;
                        continue;
                    }
                    if (options.ErrorOnTrailingGarbageAfterClosingQuote)
                    {
                        if (!options.HandleError("CSV", physicalLine + 1, recordNumber + 1, options.FilePath ?? "",
                            "CsvQuoteError", $"Illegal character '{Printable(c)}' after closing quote.", GetExcerpt(rawRecordSb)))
                            yield break;
                    }
                    fieldSb.Append(c);
                    afterClosingQuote = false;
                    continue;
                }
                else
                {
                    if (atStartOfField && c == '"')
                    {
                        inQuotes = true;
                        atStartOfField = false;
                        continue;
                    }

                    if (c == '"')
                    {
                        switch (options.QuoteMode)
                        {
                            case CsvQuoteMode.RfcStrict:
                            case CsvQuoteMode.ErrorOnIllegalQuote:
                                if (!options.HandleError("CSV", physicalLine + 1, recordNumber + 1, options.FilePath ?? "",
                                    "CsvQuoteError", "Illegal quote character inside unquoted field.", GetExcerpt(rawRecordSb)))
                                    yield break;
                                if (options.QuoteMode == CsvQuoteMode.RfcStrict)
                                    fieldSb.Append('"');
                                continue;
                            case CsvQuoteMode.Lenient:
                                inQuotes = true;
                                atStartOfField = false;
                                continue;
                        }
                    }

                    if (c == options.Separator)
                    {
                        CommitField();
                        continue;
                    }

                    if (c == '\r' || c == '\n')
                    {
                        CommitField();
                        if (c == '\r')
                        {
                            if (i + 1 < read)
                            {
                                if (buffer[i + 1] == '\n')
                                {
                                    if (rawRecordSb != null) rawRecordSb.Append(buffer[i + 1]);
                                    i++;
                                }
                            }
                            else
                            {
                                lastCharWasCR = true;
                            }
                        }
                        var rec = MakeRecord();
                        yield return rec;
                        if (options.Metrics.TerminatedEarly) yield break;
                        continue;
                    }

                    fieldSb.Append(c);
                    atStartOfField = false;
                    continue;
                }
            }

            if (options.Metrics.TerminatedEarly)
                yield break;
        }

        if (inQuotes)
        {
            if (!options.HandleError("CSV", physicalLine, recordNumber + 1, options.FilePath ?? "",
                "CsvQuoteError", "Unterminated quoted field at EOF.", GetExcerpt(rawRecordSb)))
                yield break;
        }

        if (fieldSb.Length > 0 || fields.Count > 0 || !atStartOfField)
        {
            CommitField();
            var rec = MakeRecord();
            yield return rec;
        }
    }


    #region Core Sync / Async Shared

   
    // NOTE: Due to complexity of nested iterator yielding inside local methods with side-effects,
    //       we re-implemented async path separately for clarity & correctness.

    private static bool PeekNextIsLf(char[] buffer, int i, int read, TextReader tr, ref StringBuilder? rawRecord)
    {
        if (i + 1 < read)
        {
            if (buffer[i + 1] == '\n')
            {
                if (rawRecord != null) rawRecord.Append('\n');
                return true;
            }
            return false;
        }
        // Need to peek underlying reader (sync only path uses this; async path ensures lookahead within buffer)
        tr.Peek(); // no direct append because we don't consume here
        return false;
    }

    private static string Printable(char c) => c switch
    {
        '\r' => "\\r",
        '\n' => "\\n",
        '\t' => "\\t",
        _ => c.ToString()
    };

    private static string GetExcerpt(StringBuilder? sb)
    {
        if (sb == null) return "";
        if (sb.Length <= 128) return sb.ToString();
        return sb.ToString(0, 128);
    }

    #endregion
}
