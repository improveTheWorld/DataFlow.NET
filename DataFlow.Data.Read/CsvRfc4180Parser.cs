using System.Text;

namespace DataFlow.Data;

internal static class CsvRfc4180Parser
{
    private const int DefaultBufferSize = 64 * 1024;

  
    // New sync API with external cancellation token
    public static IEnumerable<string[]> Parse(
        TextReader reader,
        CsvReadOptions options,
        int bufferSize =  DefaultBufferSize,
        CancellationToken ct = default)
    {
        if (reader == null) throw new ArgumentNullException(nameof(reader));
        if (options == null) throw new ArgumentNullException(nameof(options));
        if (bufferSize <= 0) throw new ArgumentOutOfRangeException(nameof(bufferSize));

        var core = new CsvParserCore(options, ct);
        char[] buffer = new char[bufferSize];
        var output = new List<string[]>(64);

        // Early deterministic cancellation (covers pre-cancel)
        CancelUtil.ThrowIfRequested(options.CancellationToken, ct);

        while (true)
        {
            CancelUtil.ThrowIfRequested(options.CancellationToken, ct);

            int read = reader.Read(buffer, 0, buffer.Length);
            if (read <= 0) break;

            core.Process(buffer.AsSpan(0, read), isFinal: false, output);

            // Yield any accumulated good rows
            for (int i = 0; i < output.Count; i++)
            {
                CancelUtil.ThrowIfRequested(options.CancellationToken, ct);
                yield return output[i];
            }
            output.Clear();

            // Throw if fatal
            if (core.FatalException != null)
                throw core.FatalException;

            if (options.Metrics.TerminatedEarly || core.StopRequested)
                yield break;
        }

        // Final cancellation check before flush
        CancelUtil.ThrowIfRequested(options.CancellationToken, ct);

        core.Process(ReadOnlySpan<char>.Empty, isFinal: true, output);
        for (int i = 0; i < output.Count; i++)
        {
            CancelUtil.ThrowIfRequested(options.CancellationToken, ct);
            yield return output[i];
        }
        if (core.FatalException != null)
            throw core.FatalException;
    }

    public static async IAsyncEnumerable<string[]> ParseAsync(
        TextReader reader,
        CsvReadOptions options,
        int bufferSize = DefaultBufferSize,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        if (reader == null) throw new ArgumentNullException(nameof(reader));
        if (options == null) throw new ArgumentNullException(nameof(options));
        if (bufferSize <= 0) throw new ArgumentOutOfRangeException(nameof(bufferSize));

        var core = new CsvParserCore(options, ct);
        char[] buffer = new char[bufferSize];
        var output = new List<string[]>(64);

        // Early deterministic cancellation (pre-canceled tokens => OperationCanceledException, not TaskCanceledException)
        CancelUtil.ThrowIfRequested(options.CancellationToken, ct);

        while (true)
        {
            // Check before initiating I/O
            CancelUtil.ThrowIfRequested(options.CancellationToken, ct);

            int read;
            try
            {
#if NETSTANDARD2_0
                read = await reader.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false);
#else
                read = await reader.ReadAsync(buffer.AsMemory(0, buffer.Length), ct).ConfigureAwait(false);
#endif
            }
            catch (TaskCanceledException ex) when (ct.IsCancellationRequested || options.CancellationToken.IsCancellationRequested)
            {
                // Normalize to OperationCanceledException (tests expect exact OCE)
                throw CancelUtil.CreateNormalizedOce(ct, options.CancellationToken, ex);
            }

            // Post-read check (in case cancellation happened just after read completed)
            CancelUtil.ThrowIfRequested(options.CancellationToken, ct);

            if (read <= 0) break;

            core.Process(buffer.AsSpan(0, read), isFinal: false, output);

            // Yield any accumulated rows BEFORE throwing fatal (so previously good rows are not lost)
            for (int i = 0; i < output.Count; i++)
            {
                CancelUtil.ThrowIfRequested(options.CancellationToken, ct);
                yield return output[i];
            }
            output.Clear();

            if (core.FatalException != null)
                throw core.FatalException;

            if (options.Metrics.TerminatedEarly || core.StopRequested)
                yield break;
        }

        // Final cancellation check before flush
        CancelUtil.ThrowIfRequested(options.CancellationToken, ct);

        core.Process(ReadOnlySpan<char>.Empty, isFinal: true, output);
        // Final flush: yield first, then throw if fatal occurred during finalization
        for (int i = 0; i < output.Count; i++)
        {
            CancelUtil.ThrowIfRequested(options.CancellationToken, ct);
            yield return output[i];
        }
        if (core.FatalException != null)
            throw core.FatalException;

    }

    private static class CancelUtil
    {
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public static void ThrowIfRequested(CancellationToken a, CancellationToken b)
        {
            if (a.IsCancellationRequested)
                throw new OperationCanceledException(a);
            if (b.IsCancellationRequested)
                throw new OperationCanceledException(b);
        }

        public static OperationCanceledException CreateNormalizedOce(CancellationToken a, CancellationToken b, Exception? inner = null)
        {
            if (a.IsCancellationRequested)
                return new OperationCanceledException("CSV read canceled.", inner, a);
            if (b.IsCancellationRequested)
                return new OperationCanceledException("CSV read canceled.", inner, b);
            // Fallback (should not happen if used correctly)
            return new OperationCanceledException("CSV read canceled.", inner, CancellationToken.None);
        }
    }

    private sealed class CsvParserCore
    {
        private readonly CsvReadOptions _options;
        private readonly CancellationToken _externalToken;

        private readonly List<string> _fields = new List<string>(32);
        private readonly StringBuilder _fieldSb = new StringBuilder(256);
        // Full/raw capture toggle and buffers
        private readonly bool _enableFullRaw;
        private StringBuilder? _rawFull;              // only allocated when full capture is enabled
        private readonly char[] _rawPrefix = new char[128];
        private int _rawPrefixLen = 0;
        private int _rawLength = 0;

        private bool _inQuotes;
        private bool _afterClosingQuote;
        private bool _atStartOfField = true;

        private long _recordNumber;
        private long _physicalLine;

        private bool _stopRequested;

        // Newline / CRLF handling
        private bool _suppressLfForCrLf;
        private bool _pendingCrAcrossBuffer;
        private bool _skipInitialLfThisBuffer;

        private string[]? _pendingRecord;

        private const int CancellationCheckMask = 0x1FFF; // every 8192 chars

        public bool StopRequested => _stopRequested;

        private Exception? _fatal;
        public Exception? FatalException => _fatal;

        private long LogicalRecordNumber => _options.HasHeader ? _recordNumber - 1 : _recordNumber;
        public CsvParserCore(CsvReadOptions options, CancellationToken externalToken)
        {
            _options = options;
            _externalToken = externalToken;
            // Always allocate for consistent excerpts (guard rails & quote errors)
            // Enable full capture only when an observer is provided.
            _enableFullRaw = options.RawRecordObserver != null;
            if (_enableFullRaw)
                _rawFull = new StringBuilder(512);
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private void CheckCancel()
        {
            if (_options.CancellationToken.IsCancellationRequested)
                throw new OperationCanceledException(_options.CancellationToken);
            if (_externalToken.IsCancellationRequested)
                throw new OperationCanceledException(_externalToken);
        }

        public void Process(ReadOnlySpan<char> span, bool isFinal, List<string[]> output)
        {
            if (_stopRequested || _fatal != null) return;

            // If previous buffer ended with CR and current starts with LF, suppress LF counting
            if (_pendingCrAcrossBuffer)
            {
                if (span.Length > 0 && span[0] == '\n')
                    _skipInitialLfThisBuffer = true;
                _pendingCrAcrossBuffer = false;
            }

            for (int idx = 0; idx < span.Length; idx++)
            {
                if (_stopRequested || _fatal != null)
                    break;

                if ((idx & CancellationCheckMask) == 0)
                    CheckCancel();

                char c = span[idx];
                // Raw accumulation (prefix + optional full)
                _rawLength++;
                if (_rawPrefixLen < _rawPrefix.Length)
                    _rawPrefix[_rawPrefixLen++] = c;
                if (_enableFullRaw) _rawFull!.Append(c);

                bool suppressedLfThisChar = false;

                if (c == '\r')
                {
                    _physicalLine++;
                    _options.Metrics.LinesRead = _physicalLine;

                    if (idx + 1 < span.Length && span[idx + 1] == '\n')
                    {
                        _suppressLfForCrLf = true;
                    }
                    else if (idx + 1 == span.Length)
                    {
                        _pendingCrAcrossBuffer = true;
                    }
                }
                else if (c == '\n')
                {
                    if (_skipInitialLfThisBuffer)
                    {
                        _skipInitialLfThisBuffer = false;
                        suppressedLfThisChar = true;
                    }
                    else if (_suppressLfForCrLf)
                    {
                        _suppressLfForCrLf = false;
                        suppressedLfThisChar = true;
                    }
                    else
                    {
                        _physicalLine++;
                        _options.Metrics.LinesRead = _physicalLine;
                    }
                }
                else
                {
                    _suppressLfForCrLf = false;
                    _skipInitialLfThisBuffer = false;
                }

                if (suppressedLfThisChar && !_inQuotes)
                    continue;

                if (_inQuotes)
                {
                    if (c == '"')
                    {
                        if (idx + 1 < span.Length && span[idx + 1] == '"')
                        {
                            _fieldSb.Append('"');
                            // Also reflect doubled quote into raw accumulators
                            _rawLength++;
                            if (_rawPrefixLen < _rawPrefix.Length)
                                _rawPrefix[_rawPrefixLen++] = span[idx + 1];
                            if (_enableFullRaw) _rawFull!.Append(span[idx + 1]);
                            idx++;
                            continue;
                        }
                        _inQuotes = false;
                        _afterClosingQuote = true;
                        continue;
                    }
                    _fieldSb.Append(c);
                    continue;
                }
                else if (_afterClosingQuote)
                {
                    if (c == _options.Separator)
                    {
                        CommitField();
                        continue;
                    }
                    if (c == '\r' || c == '\n')
                    {
                        CommitField();
                        EmitRecord();
                        FlushPending(output);
                        if (_options.Metrics.TerminatedEarly) return;
                        continue;
                    }
                    if (_options.ErrorOnTrailingGarbageAfterClosingQuote)
                    {
                        if (!_options.HandleError("CSV",
                                _physicalLine,
                                LogicalRecordNumber + 1 <= 0 ? 1 : LogicalRecordNumber + 1,
                                _options.FilePath ?? "",
                                "CsvQuoteError",
                                $"Illegal character '{Printable(c)}' after closing quote.",
                                GetExcerpt()))
                            return;
                    }
                    _fieldSb.Append(c);
                    _afterClosingQuote = false;
                    continue;
                }
                else
                {
                    if (_atStartOfField && c == '"')
                    {
                        _inQuotes = true;
                        _atStartOfField = false;
                        continue;
                    }

                    if (c == '"')
                    {
                        switch (_options.QuoteMode)
                        {
                            case CsvQuoteMode.RfcStrict:
                            case CsvQuoteMode.ErrorOnIllegalQuote:
                                if (!_options.HandleError("CSV",
                                        _physicalLine,
                                        LogicalRecordNumber + 1 <= 0 ? 1 : LogicalRecordNumber + 1,
                                        _options.FilePath ?? "",
                                        "CsvQuoteError",
                                        "Illegal quote character inside unquoted field.",
                                        GetExcerpt()))
                                    return;
                                if (_options.QuoteMode == CsvQuoteMode.RfcStrict)
                                    _fieldSb.Append('"');
                                continue;
                            case CsvQuoteMode.Lenient:
                                _inQuotes = true;
                                _atStartOfField = false;
                                continue;
                        }
                    }

                    if (c == _options.Separator)
                    {
                        CommitField();
                        continue;
                    }

                    if (c == '\r' || c == '\n')
                    {
                        CommitField();
                        EmitRecord();
                        FlushPending(output);
                        if (_options.Metrics.TerminatedEarly) return;
                        continue;
                    }

                    _fieldSb.Append(c);
                    _atStartOfField = false;
                }
            }

            FlushPending(output);

            if (_stopRequested || _fatal != null)
            {
                // Ensure any pending completed record prior to stop is flushed
                FlushPending(output);
                return;
            }

            if (isFinal)
            {
                // Cancellation takes precedence over format errors
                CheckCancel();

                if (_inQuotes)
                {
                    if (_stopRequested || _fatal != null)
                    {
                        FlushPending(output);
                        return;
                    }

                    CheckCancel(); // re-check before raising error
                    if (!_options.HandleError("CSV",
                            _physicalLine,
                             LogicalRecordNumber + 1 <= 0 ? 1 : LogicalRecordNumber + 1,
                            _options.FilePath ?? "",
                            "CsvQuoteError",
                            "Unterminated quoted field at EOF.",
                            GetExcerpt()))
                        return;
                }

                if (_fieldSb.Length > 0 || _fields.Count > 0 || !_atStartOfField)
                {
                    CheckCancel();
                    CommitField();
                    EmitRecord();
                    FlushPending(output);
                }
            }
        }

        private void CommitField()
        {
            string val = _options.TrimWhitespace ? _fieldSb.ToString().Trim() : _fieldSb.ToString();
            _fields.Add(val);
            _fieldSb.Clear();
            _atStartOfField = true;
            _afterClosingQuote = false;
        }

        private void EmitRecord()
        {
            _recordNumber++;
            long logicalNumber = LogicalRecordNumber;
            // Only count data rows (exclude header)
            if (!(_options.HasHeader && _recordNumber == 1))
                _options.Metrics.RawRecordsParsed = logicalNumber;

            // Guard rails use logicalNumber for user-facing record index
            if (_options.MaxColumnsPerRow > 0 && _fields.Count > _options.MaxColumnsPerRow)
            {
                GuardRailError(logicalNumber,
                    "Row has " + _fields.Count + " columns (limit " + _options.MaxColumnsPerRow + ").");
                ResetRecordState();
                return;
            }

            if (_options.MaxRawRecordLength > 0 && _rawLength > _options.MaxRawRecordLength)
            {
                GuardRailError(logicalNumber,
                    "Raw record length " + _rawLength + " exceeds limit " + _options.MaxRawRecordLength + ".");
                ResetRecordState();
                return;
            }

            var arr = _fields.ToArray();
            _pendingRecord = arr;

            if (_enableFullRaw && _rawFull != null)
            {
                string raw = _rawFull.ToString();
                if (!_options.PreserveLineEndings && _options.NormalizeNewlinesInFields)
                    raw = raw.Replace("\r\n", "\n");
                if (logicalNumber > 0) // skip header
                    _options.RawRecordObserver?.Invoke(logicalNumber, raw);
                _rawFull.Clear();
            }

            _fields.Clear();
            _fieldSb.Clear();
            _atStartOfField = true;
            _afterClosingQuote = false;
            _rawPrefixLen = 0;
            _rawLength = 0;

            if (_options.ShouldEmitProgress())
                _options.EmitProgress();

            if ((logicalNumber & 0xFF) == 0)
                CheckCancel();
        }

        private void GuardRailError(long logicalNumber, string message)
        {
            try
            {
                if (logicalNumber <= 0) logicalNumber = 1;
                bool continueParsing = _options.HandleError(
                    "CSV",
                    _physicalLine,
                    logicalNumber,
                    _options.FilePath ?? "",
                    "CsvLimitExceeded",
                    message,
                    GetExcerpt());

                if (!continueParsing)
                {
                    // STOP behavior: do not parse further, but do NOT discard already flushed rows
                    _stopRequested = true;
                }
            }
            catch (InvalidDataException ex)
            {
                // THROW behavior: record fatal; outer loop will throw before yielding
                _fatal = ex;
            }
        }

        private void ResetRecordState()
        {
            _fields.Clear();
            _fieldSb.Clear();
            _afterClosingQuote = false;
            _atStartOfField = true;
            _rawFull?.Clear();
            _rawPrefixLen = 0;
            _rawLength = 0;
            _pendingRecord = null;
        }

        private void FlushPending(List<string[]> output)
        {
            if (_pendingRecord != null)
            {
                // REMOVE the conditional that skipped header
                // if (!(_options.HasHeader && _recordNumber == 1))
                output.Add(_pendingRecord);
                _pendingRecord = null;
            }
        }
        private string GetExcerpt()
        {
            if (_rawPrefixLen == 0) return "";
            return new string (_rawPrefix, 0, _rawPrefixLen);
        }
    }

    private static string Printable(char c) => c switch
    {
        '\r' => "\\r",
        '\n' => "\\n",
        '\t' => "\\t",
        _ => c < ' ' ? $"0x{(int)c:X2}" : c.ToString()
    };

}