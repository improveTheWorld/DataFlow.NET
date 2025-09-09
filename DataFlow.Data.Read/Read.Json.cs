using System.Buffers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;

namespace DataFlow.Data;

public static partial class Read
{
    // ---------------------------
    // PUBLIC ASYNC (OPTIONS) API
    // ---------------------------
    public static async IAsyncEnumerable<T> Json<T>(
        string path,
        JsonReadOptions<T> options,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        if (options == null) throw new ArgumentNullException(nameof(options));
        options.FilePath = path;

        if (options.ValidateElements && options.ElementValidator == null)
            throw new ArgumentException("ValidateElements is true but ElementValidator is null.", nameof(options));

        bool fastPath = !(options.ValidateElements && options.ElementValidator != null);
        if (options.GuardRailsEnabled) fastPath = false;
        if (options.MaxStringLength > 0) fastPath = false;

        await foreach (var item in JsonStreamCore(path, options, fastPath, ct).ConfigureAwait(false))
            yield return item;
    }

    // ---------------------------
    // PUBLIC ASYNC (SIMPLE) API
    // ---------------------------
    public static async IAsyncEnumerable<T> Json<T>(
        string path,
        JsonSerializerOptions? options = null,
        Action<Exception>? onError = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var readOptions = new JsonReadOptions<T>
        {
            SerializerOptions = options ?? new JsonSerializerOptions { PropertyNameCaseInsensitive = true },
            ErrorAction = onError == null ? ReaderErrorAction.Throw : ReaderErrorAction.Skip,
            ErrorSink = onError == null
                ? NullErrorSink.Instance
                : new DelegatingErrorSink(e => onError(e), path)
        };
        await foreach (var item in Json(path, readOptions, cancellationToken).ConfigureAwait(false))
            yield return item;
    }

    // ---------------------------
    // PUBLIC SYNC (OPTIONS) API
    // ---------------------------
    public static IEnumerable<T> JsonSync<T>(
        string path,
        JsonReadOptions<T> options,
        CancellationToken ct = default)
    {
        if (options == null) throw new ArgumentNullException(nameof(options));
        options.FilePath = path;

        if (options.ValidateElements && options.ElementValidator == null)
            throw new ArgumentException("ValidateElements is true but ElementValidator is null.", nameof(options));

        bool fastPath = !(options.ValidateElements && options.ElementValidator != null);
        if (options.GuardRailsEnabled) fastPath = false;
        if (options.MaxStringLength > 0) fastPath = false;

        var asyncEnum = JsonStreamCore(path, options, fastPath, ct);
        var e = asyncEnum.GetAsyncEnumerator(ct);
        try
        {
            while (true)
            {
                if (!e.MoveNextAsync().AsTask().GetAwaiter().GetResult()) break;
                yield return e.Current;
            }
        }
        finally
        {
            e.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
    }

    // ---------------------------
    // PUBLIC SYNC (SIMPLE) API
    // ---------------------------
    public static IEnumerable<T> JsonSync<T>(
        string path,
        JsonSerializerOptions? options = null,
        Action<Exception>? onError = null,
        CancellationToken cancellationToken = default)
    {
        var readOptions = new JsonReadOptions<T>
        {
            SerializerOptions = options ?? new JsonSerializerOptions { PropertyNameCaseInsensitive = true },
            ErrorAction = onError == null ? ReaderErrorAction.Throw : ReaderErrorAction.Skip,
            ErrorSink = onError == null
                ? NullErrorSink.Instance
                : new DelegatingErrorSink(e => onError(e), path)
        };
        foreach (var item in JsonSync<T>(path, readOptions, cancellationToken))
            yield return item;
    }
    // ===========================
    // CORE STREAMING IMPLEMENTATION 
    // ===========================
    private static async IAsyncEnumerable<T> JsonStreamCore<T>(
        string path,
        JsonReadOptions<T> options,
        bool fastPath,
        [EnumeratorCancellation] CancellationToken ct)
    {
        var fileInfo = new FileInfo(path);
        long totalBytes = fileInfo.Exists ? fileInfo.Length : 0;

        var readerOptions = options.MaxDepth > 0
            ? new JsonReaderOptions
            {
                MaxDepth = options.MaxDepth,
                CommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true
            }
            : new JsonReaderOptions
            {
                CommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true
            };

        int bufferSize = 64 * 1024;
        byte[] buffer = ArrayPool<byte>.Shared.Rent(bufferSize);
        int bytesInBuffer = 0;
        bool isFinalBlock = false;
        var state = new JsonReaderState(readerOptions);

        await using var fs = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize,
            FileOptions.SequentialScan | FileOptions.Asynchronous);

        bytesInBuffer = await fs.ReadAsync(buffer.AsMemory(0, bufferSize), ct).ConfigureAwait(false);
        isFinalBlock = bytesInBuffer == 0;

        if (isFinalBlock)
        {
            options.Complete();
            ArrayPool<byte>.Shared.Return(buffer);
            yield break;
        }

        long elementIndex = 0;
        bool rootDetermined = false;
        bool rootIsArray = false;
        bool rootFinished = false;
        bool normalCompletion = false;

        var pending = new List<T>(4);

        try
        {
            if (options.Progress != null)
                options.EmitProgress(totalBytes, fs.CanSeek ? fs.Position : null);

            while (!rootFinished)
            {
                ct.ThrowIfCancellationRequested();
                options.CancellationToken.ThrowIfCancellationRequested();

                bool needMoreData = false;
                int consumedBytes = 0;

                // ----- Begin reader scope (ref struct must not survive beyond this block) -----
                {
                    var span = new ReadOnlySpan<byte>(buffer, 0, bytesInBuffer);
                    var reader = new Utf8JsonReader(span, isFinalBlock, state);

                    try
                    {
                        // Root determination
                        if (!rootDetermined)
                        {
                            var rootOutcome = TryDetermineRoot(
                                ref reader,
                                path,
                                fastPath,
                                options,
                                totalBytes,
                                fs.CanSeek ? fs.Position : null,
                                out rootIsArray,
                                out bool singleRootCompleted,
                                out bool singleRootNeedMore,
                                out bool terminate,
                                out ProcessResult<T>? singleRootResult);

                            
                            switch (rootOutcome)
                            {
                                case RootState.NotYet:
                                    needMoreData = true;
                                    break;

                                case RootState.ArrayDetermined:
                                    rootDetermined = true;
                                    break;

                                case RootState.SingleValueNeedMoreData:
                                    needMoreData = true;
                                    break;

                                case RootState.SingleValueProcessed:
                                case RootState.InvalidButContinuable:
                                    rootDetermined = true;
                                    rootFinished = true;

                                    // IMPORTANT:
                                    // We ONLY update RawRecordsParsed for single-root AFTER the element
                                    // has been fully processed (success / skip). This prevents provisional
                                    // increments that would lead to double counting when the element
                                    // spans multiple buffers (incomplete parses).
                                    if (singleRootResult.HasValue)
                                    {
                                        // Processed single root (emitted or not) counts as exactly one raw record.
                                        options.Metrics.RawRecordsParsed = 1;

                                        if (singleRootResult.Value.Emitted &&
                                            singleRootResult.Value.Item is not null)
                                        {
                                            options.Metrics.RecordsEmitted++;
                                            pending.Add(singleRootResult.Value.Item);
                                        }
                                    }

                                    if (terminate || options.Metrics.TerminatedEarly)
                                        rootFinished = true;
                                    break;
                            }

                            consumedBytes = (int)reader.BytesConsumed;                            
                            state = reader.CurrentState;             

                            // Skip array element loop this iteration if root now finished or we need more data
                            if (rootFinished || needMoreData)
                                goto AfterReader;
                        }

                        // SINGLE NON-ARRAY ROOT already handled above
                        if (!rootIsArray && rootDetermined)
                        {
                            rootFinished = true;
                            consumedBytes = (int)reader.BytesConsumed;                           
                            state = reader.CurrentState;
                            goto AfterReader;
                        }

                        // ARRAY PROCESSING
                        
                        bool rewoundIncompleteElement = false;
                        while (true)
                        {
                            long tokenStartBytes = reader.BytesConsumed;
                            var stateBeforeToken = reader.CurrentState;

                        if (!reader.Read())
                                break;

                            ct.ThrowIfCancellationRequested();
                            options.CancellationToken.ThrowIfCancellationRequested();

                            if (reader.TokenType == JsonTokenType.EndArray)
                            {
                                rootFinished = true;
                                break;
                            }

                            if (!IsValueStartToken(reader.TokenType))
                                continue;

                            long nextIndex = elementIndex + 1;

                            if (options.MaxElements > 0 && nextIndex > options.MaxElements)
                            {
                                bool cont = options.HandleError("JSON", -1, nextIndex, path,
                                    "JsonSizeLimit",
                                    $"Element count exceeded limit {options.MaxElements}.",
                                    "");
                                rootFinished = true;
                                if (!cont) options.Metrics.TerminatedEarly = true;
                                break;
                            }

                            var elemResult = ProcessArrayElement(
                                ref reader,
                                path,
                                nextIndex,
                                fastPath,
                                options,
                                isFinalBlock);

                            if (elemResult.IsIncomplete)
                            {
                                // Rewind both buffer consumption and parser state so StartObject is preserved.
                                needMoreData = true;
                                consumedBytes = (int)tokenStartBytes;
                                state = stateBeforeToken;
                                rewoundIncompleteElement = true;
                                break;
                            }

                            elementIndex = nextIndex;
                            options.Metrics.RawRecordsParsed = elementIndex;

                            if (elemResult.Emitted && elemResult.Item is not null)
                            {
                                options.Metrics.RecordsEmitted++;
                                pending.Add(elemResult.Item);
                            }

                            if (elemResult.TerminateStream)
                            {
                                rootFinished = true;
                                break;
                            }

                            if (options.ShouldEmitProgress())
                                options.EmitProgress(totalBytes, fs.CanSeek ? fs.Position : null);

                            if (options.Metrics.TerminatedEarly)
                            {
                                rootFinished = true;
                                break;
                            }
                        }

                        // Only advance consumption/state if we did NOT rewind for an incomplete element
                        if (!rewoundIncompleteElement)
                        {
                            consumedBytes = (int)reader.BytesConsumed;
                            state = reader.CurrentState;
                        }

                    }
                    catch (JsonException)
                    {
                        if (reader.IsFinalBlock)
                            throw;
                        consumedBytes = (int)reader.BytesConsumed;
                        needMoreData = true;
                        state = reader.CurrentState;
                    }
                }
            // ----- End reader scope -----

            AfterReader:

                // Now safe to yield (reader out of scope)
                if (pending.Count > 0)
                {
                    foreach (var itm in pending)
                        yield return itm;
                    pending.Clear();
                }

                if (rootFinished && !needMoreData)
                    break;

                AdjustBufferForNextRead(ref buffer, ref bufferSize, ref bytesInBuffer, consumedBytes, needMoreData, rootFinished, isFinalBlock, options);

                (bytesInBuffer, isFinalBlock) = await ReadMoreAsync(fs, buffer, bytesInBuffer, bufferSize, rootFinished, isFinalBlock, ct)
                    .ConfigureAwait(false);
            }

            normalCompletion = true;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
            if (normalCompletion && !options.Metrics.TerminatedEarly)
                options.Complete();
        }

        if (pending.Count > 0)
        {
            foreach (var itm in pending)
                yield return itm;
            pending.Clear();
        }
    }


    // ===========================
    // ROOT HANDLING
    // ===========================
    private enum RootState
    {
        NotYet,
        ArrayDetermined,
        SingleValueProcessed,
        SingleValueNeedMoreData,
        InvalidButContinuable
    }

    private static RootState TryDetermineRoot<T>(
        ref Utf8JsonReader reader,
        string path,
        bool fastPath,
        JsonReadOptions<T> options,
        long totalBytes,
        long? pos,
        out bool rootIsArray,
        out bool singleRootCompleted,
        out bool singleRootNeedsMore,
        out bool terminate,
        out ProcessResult<T>? singleRootResult)
    {
        rootIsArray = false;
        singleRootCompleted = false;
        singleRootNeedsMore = false;
        terminate = false;
        singleRootResult = null;

        while (reader.Read())
        {
            switch (reader.TokenType)
            {
                case JsonTokenType.StartArray:
                    rootIsArray = true;
                    options.Metrics.RawRecordsParsed = 0;
                    return RootState.ArrayDetermined;

                case JsonTokenType.StartObject:
                case JsonTokenType.String:
                case JsonTokenType.Number:
                case JsonTokenType.True:
                case JsonTokenType.False:
                case JsonTokenType.Null:
                    rootIsArray = false;

                    if (options.RequireArrayRoot && !options.AllowSingleObject)
                    {
                        options.HandleError("JSON", -1, 1, path,
                            "JsonRootError",
                            "Single value root encountered but configuration forbids it (RequireArrayRoot && !AllowSingleObject).",
                            "");
                        singleRootCompleted = true;
                        return RootState.InvalidButContinuable;
                    }

                    // DO NOT set RawRecordsParsed here. We only mark it AFTER the value
                    // has been fully processed (so large single objects spanning multiple
                    // buffers are not double-counted).

                    if (fastPath)
                    {
                        var preReader = reader;
                        var result = ProcessOneValueSingleFast(
                            ref reader,
                            path,
                            1,
                            options,
                            totalBytes,
                            pos,
                            reader.IsFinalBlock);

                        if (result.IsIncomplete)
                        {
                            // Leave StartObject consumption intact (preReader consumed it)
                            reader = preReader;
                            singleRootNeedsMore = true;
                            return RootState.SingleValueNeedMoreData;
                        }

                        singleRootResult = result;
                        singleRootCompleted = true;
                        terminate = result.TerminateStream;
                        return RootState.SingleValueProcessed;
                    }
                    else
                    {
                        singleRootResult = ProcessSingleRootValidationPath(path, 1, options);
                        singleRootCompleted = true;
                        terminate = singleRootResult.Value.TerminateStream;
                        return RootState.SingleValueProcessed;
                    }

                default:
                    continue;
            }
        }
        return RootState.NotYet;
    }

    private static ProcessResult<T> ProcessSingleRootValidationPath<T>(
        string path,
        long elementIndex,
        JsonReadOptions<T> options)
    {
        var pending = new List<T>(1);
        HandleSingleRootFullFile(path, elementIndex, options, ref pending);
        if (pending.Count == 1)
            return ProcessResult<T>.Emit(pending[0]);
        return ProcessResult<T>.SuccessNoEmit();
    }

    private static ProcessResult<T> ProcessOneValueSingleFast<T>(
        ref Utf8JsonReader reader,
        string path,
        long elementIndex,
        JsonReadOptions<T> options,
        long totalBytes,
        long? currentPos,
        bool isFinalBlock)
    {
        var preReader = reader;
        try
        {
            // Probe completeness first (avoid repeated failing Deserialize on partial buffers)
            if (!IsValueFullyAvailable(ref reader))
            {
                // Incomplete value: restore and wait for more data unless this is truly the final block
                reader = preReader;
                if (!isFinalBlock)
                    return ProcessResult<T>.IncompletePartial();
                // If final block AND still incomplete, let JsonSerializer throw (will translate to error)
            }
            var item = JsonSerializer.Deserialize<T>(ref reader, options.SerializerOptions);
            if (options.ShouldEmitProgress())
                options.EmitProgress(totalBytes, currentPos);
            return item is not null ? ProcessResult<T>.Emit(item) : ProcessResult<T>.SuccessNoEmit();
        }
        catch (Exception ex)
        {
            if (ex is JsonException && !isFinalBlock)
            {
                reader = preReader;
                return ProcessResult<T>.IncompletePartial();
            }

            string excerpt = "";
            var excerptReader = preReader;
            try
            {
                excerpt = BuildExcerptFromValue(ref excerptReader, 128);
                reader = excerptReader;
            }
            catch { }

            bool cont = options.HandleError("JSON", -1, elementIndex, path,
                ex.GetType().Name, ex.Message, excerpt);

            if (!cont)
                return ProcessResult<T>.Terminate();
            return ProcessResult<T>.SuccessNoEmit();
        }
    }

    // ===========================
    // ARRAY ELEMENT PROCESSING
    // ===========================
    private static ProcessResult<T> ProcessArrayElement<T>(
        ref Utf8JsonReader reader,
        string path,
        long elementIndex,
        bool fastPath,
        JsonReadOptions<T> options,
        bool isFinalBlock)
    {
        // We take a snapshot so we can restore the reader if the element is incomplete
        var valueReader = reader;
        var result = ProcessOneArrayElement(
            ref valueReader,
            path,
            elementIndex,
            fastPath,
            options,
            (long)reader.BytesConsumed,
            isFinalBlock);

        // ALWAYS assign back. For incomplete parses valueReader is rewound
        // inside the fast-path catch to the pre-deserialize state (StartObject / StartArray),
        // ensuring the next loop re-attempt starts at the correct token.
        reader = valueReader;
        return result;

    }

    // ===========================
    // BUFFER MANAGEMENT
    // ===========================
    private static void AdjustBufferForNextRead<T>(
        ref byte[] buffer,
        ref int bufferSize,
        ref int bytesInBuffer,
        int consumedBytes,
        bool needMoreData,
        bool rootFinished,
        bool isFinalBlock,
        JsonReadOptions<T> options)
    {
        int remaining = bytesInBuffer - consumedBytes;
        if (remaining > 0)
            Buffer.BlockCopy(buffer, consumedBytes, buffer, 0, remaining);

        if (!rootFinished && !isFinalBlock)
        {
            bool grow = false;
            bool noFree = remaining == bufferSize;
            bool almostFull = bufferSize - remaining < 1024;
            if (needMoreData && (noFree || almostFull))
                grow = true;

            if (grow)
            {
                int desired = bufferSize * 2;
                if (options.MaxElementBytes > 0 && desired > options.MaxElementBytes)
                    desired = (int)Math.Min(desired, options.MaxElementBytes);

                if (desired > bufferSize)
                {
                    var newBuf = ArrayPool<byte>.Shared.Rent(desired);
                    Buffer.BlockCopy(buffer, 0, newBuf, 0, remaining);
                    ArrayPool<byte>.Shared.Return(buffer);
                    buffer = newBuf;
                    bufferSize = desired;
                }
            }
        }

        bytesInBuffer = remaining;
    }

    private static async ValueTask<(int bytesInBuffer, bool isFinalBlock)> ReadMoreAsync(
        FileStream fs,
        byte[] buffer,
        int currentBytes,
        int bufferSize,
        bool rootFinished,
        bool alreadyFinal,
        CancellationToken ct)
    {
        if (rootFinished) return (currentBytes, alreadyFinal);

        int read = await fs.ReadAsync(buffer.AsMemory(currentBytes, bufferSize - currentBytes), ct).ConfigureAwait(false);
        int total = currentBytes + read;
        bool eof = read == 0;
        if (fs.CanSeek)
            eof = eof || fs.Position >= fs.Length;
        return (total, eof);
    }

    // ===========================
    // PROCESS RESULT STRUCT
    // ===========================
    private readonly struct ProcessResult<T>
    {
        public bool Succeeded { get; }
        public bool Emitted { get; }
        public bool TerminateStream { get; }
        public bool IsIncomplete { get; }
        public T? Item { get; }

        private ProcessResult(bool succeeded, bool emitted, bool terminate, bool isIncomplete, T? item)
        {
            Succeeded = succeeded;
            Emitted = emitted;
            TerminateStream = terminate;
            IsIncomplete = isIncomplete;
            Item = item;
        }

        public static ProcessResult<T> Emit(T item) => new(true, true, false, false, item);
        public static ProcessResult<T> SuccessNoEmit() => new(true, false, false, false, default);
        public static ProcessResult<T> Terminate() => new(true, false, true, false, default);
        public static ProcessResult<T> IncompletePartial() => new(false, false, false, true, default);
        public static ProcessResult<T> FailureSkip() => new(true, false, false, false, default);
    }

    // ===========================
    // UPDATED VALUE / ELEMENT HELPERS
    // ===========================
    private static ProcessResult<T> ProcessOneArrayElement<T>(
        ref Utf8JsonReader valueReader,
        string path,
        long elementIndex,
        bool fastPath,
        JsonReadOptions<T> options,
        long elementStartBytesConsumed,
        bool isFinalBlock)
    {
        if (fastPath)
        {
            var preReader = valueReader;
            try
            {
                // Ensure full element is buffered before attempting fast-path deserialize
                if (!IsValueFullyAvailable(ref valueReader))
                {
                    valueReader = preReader;
                    if (!isFinalBlock)
                        return ProcessResult<T>.IncompletePartial();
                    // If final block but still incomplete, we'll fall through and let Deserialize throw -> real error.
                }

                var item = JsonSerializer.Deserialize<T>(ref valueReader, options.SerializerOptions);
                return item is not null ? ProcessResult<T>.Emit(item) : ProcessResult<T>.SuccessNoEmit();
            }
            catch (Exception ex)
            {
                if (ex is JsonException && !isFinalBlock)
                {
                    // Incomplete element: restore reader so caller keeps full element bytes
                    valueReader = preReader;
                    return ProcessResult<T>.IncompletePartial();
                }
                string excerpt = "";
                var excerptReader = preReader;
                try
                {
                    excerpt = BuildExcerptFromValue(ref excerptReader, 128);
                    valueReader = excerptReader;
                }
                catch { }

                bool cont = options.HandleError("JSON", -1, elementIndex, path,
                    ex.GetType().Name, ex.Message, excerpt);
                return cont ? ProcessResult<T>.FailureSkip() : ProcessResult<T>.Terminate();
            }
        }

        try
        {
            using var doc = JsonDocument.ParseValue(ref valueReader);
            var rootEl = doc.RootElement;

            long elementBytes = (long)valueReader.BytesConsumed - elementStartBytesConsumed;
            if (options.MaxElementBytes > 0 && elementBytes > options.MaxElementBytes)
            {
                bool cont = options.HandleError("JSON", -1, elementIndex, path,
                    "JsonSizeLimit",
                    $"Element size {elementBytes} bytes exceeds limit {options.MaxElementBytes}.",
                    "");
                return cont ? ProcessResult<T>.FailureSkip() : ProcessResult<T>.Terminate();
            }

            if (!CheckStringLength(rootEl, options, path, elementIndex))
                return ProcessResult<T>.FailureSkip();

            if (options.ValidateElements && options.ElementValidator != null)
            {
                bool valid;
                try { valid = options.ElementValidator(rootEl); }
                catch (Exception exVal)
                {
                    bool cont = options.HandleError("JSON", -1, elementIndex, path,
                        "JsonValidationError", exVal.Message,
                        Truncate(SafeGetRawText(rootEl), 128));
                    return cont ? ProcessResult<T>.FailureSkip() : ProcessResult<T>.Terminate();
                }

                if (!valid)
                {
                    bool cont = options.HandleError("JSON", -1, elementIndex, path,
                        "JsonValidationFailed", "Element validator returned false.",
                        Truncate(SafeGetRawText(rootEl), 128));
                    return cont ? ProcessResult<T>.FailureSkip() : ProcessResult<T>.Terminate();
                }
            }

            try
            {
                var item = rootEl.Deserialize<T>(options.SerializerOptions);
                return item is not null ? ProcessResult<T>.Emit(item) : ProcessResult<T>.SuccessNoEmit();
            }
            catch (Exception exDeser)
            {
                bool cont = options.HandleError("JSON", -1, elementIndex, path,
                    exDeser.GetType().Name, exDeser.Message,
                    Truncate(SafeGetRawText(rootEl), 128));
                return cont ? ProcessResult<T>.FailureSkip() : ProcessResult<T>.Terminate();
            }
        }
        catch (Exception exOuter)
        {
            bool cont = options.HandleError("JSON", -1, elementIndex, path,
                exOuter.GetType().Name, exOuter.Message, "");
            return cont ? ProcessResult<T>.FailureSkip() : ProcessResult<T>.Terminate();
        }
    }

    // ===========================
    // SUPPORT UTILITIES
    // ===========================
    // Returns true if the complete JSON value starting at the current reader position
    // is fully contained in the current buffer segment (so a fast-path deserialize is safe).
    private static bool IsValueFullyAvailable(ref Utf8JsonReader reader)
    {
        var probe = reader; // struct copy
        try
        {
            // TrySkip efficiently advances over the entire value if fully present.
            return probe.TrySkip();
        }
        catch
        {
            // Any failure => treat as not fully available (force more buffering).
            return false;
        }
    }
    private static void HandleSingleRootFullFile<T>(
        string path,
        long elementIndex,
        JsonReadOptions<T> options,
        ref List<T> pending)
    {
        try
        {
            using var fullFs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 8192, FileOptions.SequentialScan);
            using var doc = JsonDocument.Parse(fullFs, new JsonDocumentOptions { MaxDepth = options.MaxDepth > 0 ? options.MaxDepth : 0 });
            var rootEl = doc.RootElement;

            if (!CheckStringLength(rootEl, options, path, elementIndex))
                return;

            if (options.ValidateElements && options.ElementValidator != null)
            {
                bool valid;
                try { valid = options.ElementValidator(rootEl); }
                catch (Exception exVal)
                {
                    options.HandleError("JSON", -1, elementIndex, path,
                        "JsonValidationError", exVal.Message, Truncate(SafeGetRawText(rootEl), 128));
                    return;
                }
                if (!valid)
                {
                    options.HandleError("JSON", -1, elementIndex, path,
                        "JsonValidationFailed", "Element validator returned false.",
                        Truncate(SafeGetRawText(rootEl), 128));
                    return;
                }
            }

            try
            {
                var item = rootEl.Deserialize<T>(options.SerializerOptions);
                if (item is not null)
                {
                    // Do NOT touch metrics here: emission is centralized in the main loop
                    pending.Add(item);
                }
            }
            catch (Exception exDeser)
            {
                options.HandleError("JSON", -1, elementIndex, path,
                    exDeser.GetType().Name, exDeser.Message, Truncate(SafeGetRawText(rootEl), 128));
            }
        }
        catch (Exception exOuter)
        {
            options.HandleError("JSON", -1, elementIndex, path,
                exOuter.GetType().Name, exOuter.Message, "");
        }
    }

    private static string BuildExcerptFromValue(ref Utf8JsonReader readerCopy, int maxChars)
    {
        try
        {
            using var doc = JsonDocument.ParseValue(ref readerCopy);
            return Truncate(doc.RootElement.GetRawText(), maxChars);
        }
        catch
        {
            return "";
        }
    }

    private static bool IsValueStartToken(JsonTokenType t) =>
        t == JsonTokenType.StartObject ||
        t == JsonTokenType.StartArray ||
        IsPrimitiveToken(t);

    private static bool IsPrimitiveToken(JsonTokenType t) =>
        t == JsonTokenType.String ||
        t == JsonTokenType.Number ||
        t == JsonTokenType.True ||
        t == JsonTokenType.False ||
        t == JsonTokenType.Null;

    private static bool CheckStringLength<T>(JsonElement rootEl, JsonReadOptions<T> options, string path, long elementIndex)
    {
        if (options.MaxStringLength <= 0) return true;
        if (JsonStringTooLong(rootEl, options.MaxStringLength))
        {
            options.HandleError("JSON", -1, elementIndex, path,
                "JsonSizeLimit",
                $"Element contains a string exceeding MaxStringLength {options.MaxStringLength}.",
                Truncate(SafeGetRawText(rootEl), 128));
            return false;
        }
        return true;
    }

    private static bool JsonStringTooLong(JsonElement root, int maxLen)
    {
        var stack = new Stack<JsonElement>();
        stack.Push(root);
        while (stack.Count > 0)
        {
            var el = stack.Pop();
            switch (el.ValueKind)
            {
                case JsonValueKind.String:
                    var s = el.GetString();
                    if (s != null && s.Length > maxLen)
                        return true;
                    break;
                case JsonValueKind.Object:
                    foreach (var prop in el.EnumerateObject())
                        stack.Push(prop.Value);
                    break;
                case JsonValueKind.Array:
                    foreach (var item in el.EnumerateArray())
                        stack.Push(item);
                    break;
            }
        }
        return false;
    }

    private static string Truncate(string? s, int max)
    {
        if (string.IsNullOrEmpty(s)) return "";
        return s.Length <= max ? s : s.Substring(0, max);
    }

    private static string SafeGetRawText(JsonElement e)
    {
        try { return e.GetRawText(); }
        catch { return ""; }
    }
}
