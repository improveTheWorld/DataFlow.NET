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
        long physicalLine = 0;                 // counts physical newline terminators (CR, LF, CRLF -> 1)
        bool suppressLfForCrLf = false;        // true when a CR in this buffer was followed by LF we must not double-count
        bool pendingCrAcrossBuffer = false;    // true if previous buffer ended with a CR (possible CRLF split)
        bool skipInitialLfThisBuffer = false;  // set at start of buffer if first char is LF completing cross-buffer CRLF


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
            options.Metrics.RawRecordsParsed = recordNumber;
            // LinesRead already updated at newline detection sites; leave as-is.
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

            // Handle CRLF split across buffers:
            // If last buffer ended with a CR and THIS buffer starts with LF, suppress counting this first LF.
            if (pendingCrAcrossBuffer)
            {
                if (read > 0 && buffer[0] == '\n')
                    skipInitialLfThisBuffer = true;
                pendingCrAcrossBuffer = false;
            }

            for (int i = 0; i < read; i++)
            {
                char c = buffer[i];

                // Capture raw characters for auditing if requested
                if (rawRecordSb != null) rawRecordSb.Append(c);

                // --- Centralized physical newline counting (before state machine) ---
                // We count:
                //   CRLF -> 1 line (on CR)
                //   Lone CR -> 1 line
                //   Lone LF -> 1 line
                // Implementation:
                //   If char is CR: increment, set lastCharWasCR=true and (if LF follows inside buffer) mark suppress to skip double count.
                //   If char is LF and not suppressed by preceding CR: increment.
                // This is independent of quoting; embedded newlines inside quoted fields are physical line terminators.
                bool suppressedLfThisChar = false;
                if (c == '\r')
                    {
                    physicalLine++;
                    options.Metrics.LinesRead = physicalLine;
                    // If next char (still in this buffer) is LF, suppress counting that LF.
                    if (i + 1 < read && buffer[i + 1] == '\n')
                    {
                        suppressLfForCrLf = true;
                    }
                    else if (i + 1 == read)
                    {
                        // CR is last char of buffer; could be CRLF split across buffers.
                        pendingCrAcrossBuffer = true;
                    }
                }
                else if (c == '\n')
                {
                    if (skipInitialLfThisBuffer)
                    {
                        skipInitialLfThisBuffer = false;
                        suppressedLfThisChar = true;
                    }
                    else if (suppressLfForCrLf)
                    {
                        suppressLfForCrLf = false;
                        suppressedLfThisChar = true;
                    }
                    else
                    {
                        physicalLine++;
                        options.Metrics.LinesRead = physicalLine;
                    }
                }
                else
                {
                    // Non-newline breaks any pending in-buffer suppression except cross-buffer CR (handled separately)
                    suppressLfForCrLf = false;
                    skipInitialLfThisBuffer = false;
                }
                // If this LF was the second half of a CRLF (already counted) and we are NOT inside quotes,
                // skip further processing so it cannot create an empty extra record.
                if (suppressedLfThisChar && !inQuotes)
                    continue;

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
                                physicalLine,
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
                                        physicalLine,
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
        var rawRecordSb = options.CaptureRawRecord || options.RawRecordObserver != null
        ? new StringBuilder(512)
        : null;

        char[] buffer = new char[BufferSize];
        int read;
        bool inQuotes = false;
        bool afterClosingQuote = false;
        bool atStartOfField = true;

        long recordNumber = 0;
        long physicalLine = 0;              // physical newline terminators (CR, LF, CRLF => 1 each pair)
        bool suppressLfForCrLf = false;     // CR just seen, next in-buffer char is LF
        bool pendingCrAcrossBuffer = false; // previous buffer ended with CR
        bool skipInitialLfThisBuffer = false;

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
            options.Metrics.RawRecordsParsed = recordNumber;

            // MaxColumnsPerRow
            if (options.MaxColumnsPerRow > 0 && fields.Count > options.MaxColumnsPerRow)
            {
                if (!options.HandleError("CSV", physicalLine, recordNumber, options.FilePath ?? "",
                    "CsvLimitExceeded", $"Row has {fields.Count} columns (limit {options.MaxColumnsPerRow}).",
                    GetExcerpt(rawRecordSb)))
                {
                    pendingRecord = null;
                    return;
                }
                // Skip
                pendingRecord = null;
                fields.Clear();
                fieldSb.Clear();
                atStartOfField = true;
                afterClosingQuote = false;
                rawRecordSb?.Clear();
                return;
            }

            // MaxRawRecordLength
            if (options.MaxRawRecordLength > 0 && rawRecordSb != null && rawRecordSb.Length > options.MaxRawRecordLength)
            {
                if (!options.HandleError("CSV", physicalLine, recordNumber, options.FilePath ?? "",
                    "CsvLimitExceeded", $"Raw record length {rawRecordSb.Length} exceeds limit {options.MaxRawRecordLength}.",
                    GetExcerpt(rawRecordSb)))
                {
                    pendingRecord = null;
                    return;
                }
                // Skip
                pendingRecord = null;
                fields.Clear();
                fieldSb.Clear();
                atStartOfField = true;
                afterClosingQuote = false;
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

        while ((read = await reader.ReadAsync(buffer.AsMemory(0, buffer.Length), ct)) > 0)
        {
            ct.ThrowIfCancellationRequested();
            options.CancellationToken.ThrowIfCancellationRequested();

            if (pendingCrAcrossBuffer)
            {
                if (read > 0 && buffer[0] == '\n')
                    skipInitialLfThisBuffer = true;
                pendingCrAcrossBuffer = false;
            }

            for (int i = 0; i < read; i++)
            {
                char c = buffer[i];
                if (rawRecordSb != null) rawRecordSb.Append(c);

                bool suppressedLfThisChar = false;

                // Centralized newline counting
                if (c == '\r')
                {
                    physicalLine++;
                    options.Metrics.LinesRead = physicalLine;
                    if (i + 1 < read && buffer[i + 1] == '\n')
                    {
                        suppressLfForCrLf = true;
                    }
                    else if (i + 1 == read)
                    {
                        pendingCrAcrossBuffer = true; // maybe CRLF split
                    }
                }
                else if (c == '\n')
                {
                    if (skipInitialLfThisBuffer)
                    {
                        skipInitialLfThisBuffer = false;
                        suppressedLfThisChar = true;
                    }
                    else if (suppressLfForCrLf)
                    {
                        suppressLfForCrLf = false;
                        suppressedLfThisChar = true;
                    }
                    else
                    {
                        physicalLine++;
                        options.Metrics.LinesRead = physicalLine;
                    }
                }
                else
                {
                    suppressLfForCrLf = false;
                    skipInitialLfThisBuffer = false;
                }

                // Skip state machine for suppressed LF (second char of CRLF) outside quotes
                if (suppressedLfThisChar && !inQuotes)
                    continue;

                // State machine
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
                        if (!options.HandleError("CSV",
                                physicalLine,
                                recordNumber + 1,
                                options.FilePath ?? "",
                                "CsvQuoteError",
                                $"Illegal character '{Printable(c)}' after closing quote.",
                                GetExcerpt(rawRecordSb)))
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
                                if (!options.HandleError("CSV",
                                        physicalLine,
                                        recordNumber + 1,
                                        options.FilePath ?? "",
                                        "CsvQuoteError",
                                        "Illegal quote character inside unquoted field.",
                                        GetExcerpt(rawRecordSb)))
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

        if (inQuotes)
        {
            if (!options.HandleError("CSV",
                    physicalLine,
                    recordNumber + 1,
                    options.FilePath ?? "",
                    "CsvQuoteError",
                    "Unterminated quoted field at EOF.",
                    GetExcerpt(rawRecordSb)))
                yield break;
        }

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


    #region Core Sync / Async Shared


    // NOTE: Due to complexity of nested iterator yielding inside local methods with side-effects,
    //       we re-implemented async path separately for clarity & correctness.


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
