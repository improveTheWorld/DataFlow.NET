using DataFlow.Data.StringMapper;
using DataFlow.Extensions;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using YamlDotNet.Core;

namespace DataFlow.Data;

/// <summary>
/// Provides static methods for lazily reading data from various file formats,
/// with full support for both synchronous (IEnumerable) and asynchronous (IAsyncEnumerable) streaming.
/// The method sync/async suffixes convention is inverted (default is asynchronous) to encourage the asynchronous file reading reflex.
/// Simple API for nominal cases + Option-based APIs: Csv / CsvSync, Json, Yaml.
/// </summary>
public static partial class Read
{

    // JSON (async) modifications: buffer reset helper + writer disposal and safer error excerpt skip.
    // Updated Json<T> async iterator with fixes: per-element Utf8JsonWriter recreation, metrics updates for validation paths
    // UPDATED ASYNC JSON ITERATOR WITH FIXES
    public static async IAsyncEnumerable<T> Json<T>(
        string path,
        JsonReadOptions<T> options,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        if (options == null) throw new ArgumentNullException(nameof(options));
        options.FilePath = path;

        if (options.ValidateElements && options.ElementValidator == null)
            throw new ArgumentException("ValidateElements is true but ElementValidator is null. Provide ElementValidator or disable ValidateElements.", nameof(options));

        var fileInfo = new FileInfo(path);
        long totalBytes = fileInfo.Exists ? fileInfo.Length : 0;

        var readerOptions = options.MaxDepth > 0
            ? new JsonReaderOptions { MaxDepth = options.MaxDepth, CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true }
            : new JsonReaderOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };

        const int BufferSize = 64 * 1024;
        byte[] buffer = new byte[BufferSize];
        int bytesRead;
        bool isFinalBlock;
        var state = new JsonReaderState(readerOptions);

        await using var fs = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            BufferSize,
            FileOptions.Asynchronous | FileOptions.SequentialScan);

        bytesRead = await fs.ReadAsync(buffer.AsMemory(0, buffer.Length), ct).ConfigureAwait(false);
        isFinalBlock = bytesRead == 0;
        if (isFinalBlock)
        {
            options.Complete();
            yield break;
        }

        bool rootDetermined = false;
        bool rootIsArray = false;
        bool rootFinished = false;
        long elementIndex = 0;
        options.Metrics.RawRecordsParsed = 0;

        bool fastPath = !(options.ValidateElements && options.ElementValidator != null);
        if (options.GuardRailsEnabled) fastPath = false;

        // Early one-shot path for a single validated non-array root (large object safe)
        if (!fastPath && options.AllowSingleObject)
        {
            var preview = new Utf8JsonReader(new ReadOnlySpan<byte>(buffer, 0, bytesRead), isFinalBlock, state);
            bool decided = false;
            bool previewSawArray = false;
            while (preview.Read())
            {
                switch (preview.TokenType)
                {
                    case JsonTokenType.StartArray:
                        previewSawArray = true;
                        decided = true;
                        break;
                    case JsonTokenType.StartObject:
                    case JsonTokenType.String:
                    case JsonTokenType.Number:
                    case JsonTokenType.True:
                    case JsonTokenType.False:
                    case JsonTokenType.Null:
                        decided = true;
                        break;
                    default:
                        continue;
                }
                if (decided) break;
            }

            if (decided && !previewSawArray &&
                !(options.RequireArrayRoot && !options.AllowSingleObject))
            {
                // Read remainder of file into memory
                using var ms = new MemoryStream((int)(totalBytes > 0 ? totalBytes : bytesRead * 2));
                ms.Write(buffer, 0, bytesRead);
                int r;
                while ((r = await fs.ReadAsync(buffer.AsMemory(0, buffer.Length), ct).ConfigureAwait(false)) > 0)
                    ms.Write(buffer, 0, r);
                ms.Position = 0;

                bool emit = false;
                T? item = default;

                // Parse + validate + deserialize (no yield inside)
                try
                {
                    using var doc = JsonDocument.Parse(ms,
                        new JsonDocumentOptions
                        {
                            AllowTrailingCommas = true,
                            MaxDepth = options.MaxDepth > 0 ? options.MaxDepth : 0
                        });

                    var rootEl = doc.RootElement;

                    elementIndex = 1;
                    options.Metrics.RawRecordsParsed = 1;

                    // Guard rail: MaxStringLength
                    if (options.MaxStringLength > 0 && JsonStringTooLong(rootEl, options.MaxStringLength))
                    {
                        if (!options.HandleError("JSON", -1, 1, path,
                                "JsonSizeLimit",
                                $"Element contains a string exceeding MaxStringLength {options.MaxStringLength}.",
                                Truncate(SafeGetRawText(rootEl), 128)))
                        {
                            // Throw or Stop already handled
                            options.Complete();
                            yield break;
                        }
                        // Skip (Skip mode)
                        options.Complete();
                        yield break;
                    }

                    // Validation
                    if (options.ValidateElements && options.ElementValidator != null)
                    {
                        bool valid;
                        try
                        {
                            valid = options.ElementValidator(rootEl);
                        }
                        catch (Exception exVal)
                        {
                            options.HandleError("JSON", -1, 1, path,
                                "JsonValidationError", exVal.Message,
                                Truncate(SafeGetRawText(rootEl), 128));
                            options.Complete();
                            yield break;
                        }

                        if (!valid)
                        {
                            if (!options.HandleError("JSON", -1, 1, path,
                                    "JsonValidationFailed",
                                    "Element validator returned false.",
                                    Truncate(SafeGetRawText(rootEl), 128)))
                            {
                                // Throw or Stop
                                options.Complete();
                                yield break;
                            }
                            // Skip
                            options.Complete();
                            yield break;
                        }
                    }

                    // Deserialize
                    try
                    {
                        item = rootEl.Deserialize<T>(options.SerializerOptions)!;
                        emit = true;
                    }
                    catch (Exception exDeser)
                    {
                        options.HandleError("JSON", -1, 1, path,
                            exDeser.GetType().Name, exDeser.Message,
                            Truncate(SafeGetRawText(rootEl), 128));
                        // Throw => exits enumeration automatically; Skip => no emit
                    }
                }
                catch (Exception exOuter)
                {
                    // Parsing-level failure
                    options.HandleError("JSON", -1, 1, path,
                        exOuter.GetType().Name, exOuter.Message, "");
                    // Throw stops; Skip just ends
                }

                options.Complete();

                if (emit)
                {
                    options.Metrics.RecordsEmitted = 1;
                    yield return item!;
                }
                yield break;
            }
            // Else: fall through to normal streaming (array or disallowed single root)
        }

        bool maxElementsBreached = false;
        bool initialProgressEmitted = false;

        ArrayBufferWriter<byte>? elementBuffer = null;
        Utf8JsonWriter? elementWriter = null;

        void BeginNewElementCapture()
        {
            elementBuffer ??= new ArrayBufferWriter<byte>(8 * 1024);
            ResetArrayBufferWriter(ref elementBuffer);
            elementWriter?.Dispose();
            elementWriter = new Utf8JsonWriter(elementBuffer);
        }

        bool capturing = false;
        int openContainers = 0;
        bool elementIsPrimitive = false;

        bool CheckElementSizeLimit()
        {
            if (options.MaxElementBytes > 0 &&
                elementBuffer != null &&
                elementBuffer.WrittenCount > options.MaxElementBytes)
            {
                if (!options.HandleError("JSON", -1, elementIndex, path,
                        "JsonSizeLimit",
                        $"Element size {elementBuffer.WrittenCount} bytes exceeds limit {options.MaxElementBytes}.",
                        ""))
                {
                    rootFinished = true; // Stop / Throw
                    return false;
                }
                // Skip (but still must consume element fully)
                ResetArrayBufferWriter(ref elementBuffer);
                capturing = false;
                return false;
            }
            return true;
        }

        void SkipRemainingElement(ref Utf8JsonReader rdr)
        {
            while (openContainers > 0 && rdr.Read())
            {
                switch (rdr.TokenType)
                {
                    case JsonTokenType.StartObject:
                    case JsonTokenType.StartArray:
                        openContainers++;
                        break;
                    case JsonTokenType.EndObject:
                    case JsonTokenType.EndArray:
                        openContainers--;
                        break;
                }
            }
        }

        List<T> pending = new List<T>(1);

        try
        {
            if (!initialProgressEmitted && options.Progress != null)
            {
                options.EmitProgress(totalBytes, fs.CanSeek ? fs.Position : null);
                initialProgressEmitted = true;
            }

            while (!rootFinished)
            {
                var span = new ReadOnlySpan<byte>(buffer, 0, bytesRead);
                var reader = new Utf8JsonReader(span, isFinalBlock, state);

                bool producedThisIteration = false;

                while (reader.Read())
                {
                    ct.ThrowIfCancellationRequested();
                    options.CancellationToken.ThrowIfCancellationRequested();

                    // CONTINUATION: single-root capture across buffer boundaries
                    if (rootDetermined && !rootIsArray && capturing)
                    {
                        // Current token already loaded (first token of new buffer or continuation)
                        // Process current token (reader.TokenType reflects a new token each loop iteration).
                        WriteTokenToElement(ref reader, elementWriter!, ref openContainers, ref elementIsPrimitive);
                        if (!CheckElementSizeLimit())
                        {
                            if (!rootFinished && openContainers > 0)
                                SkipRemainingElement(ref reader);
                            capturing = false;
                            rootFinished = rootFinished || options.Metrics.TerminatedEarly;
                        }
                        if (capturing && openContainers == 0)
                        {
                            capturing = false;
                            elementWriter!.Flush();
                            if (TryFinalizeCapturedElement(elementBuffer!, options, path, elementIndex, out var item))
                            {
                                options.Metrics.RawRecordsParsed = 1;
                                if (options.ShouldEmitProgress())
                                    options.EmitProgress(totalBytes, fs.CanSeek ? fs.Position : null);
                                pending.Add(item);
                                producedThisIteration = true;
                            }
                            else if (options.Metrics.TerminatedEarly)
                            {
                                rootFinished = true;
                            }
                            rootFinished = true;
                        }
                        if (rootFinished || producedThisIteration)
                            break;
                        // Continue reading further tokens (loop continues)
                        continue;
                    }

                    if (!rootDetermined)
                    {
                        switch (reader.TokenType)
                        {
                            case JsonTokenType.StartArray:
                                rootIsArray = true;
                                rootDetermined = true;
                                continue;

                            case JsonTokenType.StartObject:
                            case JsonTokenType.String:
                            case JsonTokenType.Number:
                            case JsonTokenType.True:
                            case JsonTokenType.False:
                            case JsonTokenType.Null:
                                rootIsArray = false;
                                rootDetermined = true;

                                if (options.RequireArrayRoot && !options.AllowSingleObject)
                                {
                                    if (!options.HandleError("JSON", -1, -1, path,
                                            "JsonFormatError", "Root element is not an array.", ""))
                                        rootFinished = true;
                                    else
                                        rootFinished = true;
                                    break;
                                }

                                elementIndex = 1;
                                options.Metrics.RawRecordsParsed = 1;
                                if (fastPath)
                                {
                                    var valueReader = reader;
                                    bool success = false;
                                    T item = default;
                                    try
                                    {
                                        item = JsonSerializer.Deserialize<T>(ref valueReader, options.SerializerOptions)!;
                                        success = true;
                                        reader = valueReader;
                                    }
                                    catch (Exception ex)
                                    {
                                        string excerpt = BuildExcerptAndSkip(ref reader, 128);
                                        if (!options.HandleError("JSON", -1, 1, path,
                                                ex.GetType().Name, ex.Message, excerpt))
                                            rootFinished = true;
                                    }

                                    if (success)
                                    {
                                        options.Metrics.RawRecordsParsed = 1;
                                        if (options.ShouldEmitProgress())
                                            options.EmitProgress(totalBytes, fs.CanSeek ? fs.Position : null);
                                        pending.Add(item);
                                        producedThisIteration = true;
                                    }
                                    rootFinished = true;
                                }
                                else
                                {
                                    // Begin capturing single root element; do NOT set rootFinished yet.
                                    BeginNewElementCapture();
                                    capturing = true;
                                    openContainers = 0;
                                    elementIsPrimitive = false;

                                    WriteTokenToElement(ref reader, elementWriter!, ref openContainers, ref elementIsPrimitive);
                                    if (!CheckElementSizeLimit())
                                    {
                                        if (!rootFinished && openContainers > 0)
                                            SkipRemainingElement(ref reader);
                                        capturing = false;
                                        rootFinished = rootFinished || options.Metrics.TerminatedEarly;
                                    }

                                    if (capturing && elementIsPrimitive)
                                    {
                                        capturing = false;
                                        elementWriter!.Flush();
                                        if (TryFinalizeCapturedElement(elementBuffer!, options, path, elementIndex, out var item))
                                        {
                                            options.Metrics.RawRecordsParsed = 1;
                                            if (options.ShouldEmitProgress())
                                                options.EmitProgress(totalBytes, fs.CanSeek ? fs.Position : null);
                                            pending.Add(item);
                                            producedThisIteration = true;
                                        }
                                        else if (options.Metrics.TerminatedEarly)
                                        {
                                            rootFinished = true;
                                        }
                                        rootFinished = true;
                                    }
                                    // If not primitive and still capturing, we'll continue on next tokens (possibly next buffer).
                                }
                                break;

                            default:
                                continue;
                        }

                        if (rootFinished || producedThisIteration) break;
                        if (!rootIsArray) break; // break inner loop to refill buffer if needed during ongoing capture
                        continue;
                    }

                    // Continuation of a previously started (validation path) array element spanning buffers
                    if (capturing)
                    {
                        // We are mid-element; keep writing tokens until element closes.
                        WriteTokenToElement(ref reader, elementWriter!, ref openContainers, ref elementIsPrimitive);
                        if (!CheckElementSizeLimit())
                        {
                            if (!rootFinished && openContainers > 0)
                                SkipRemainingElement(ref reader);
                            capturing = false;
                            if (options.Metrics.TerminatedEarly)
                                rootFinished = true;
                            continue;
                        }
                        if (openContainers == 0)
                        {
                            // Element finished; finalize
                            capturing = false;
                            elementWriter!.Flush();
                            if (TryFinalizeCapturedElement(elementBuffer!, options, path, elementIndex, out var item))
                            {
                                if (options.ShouldEmitProgress())
                                    options.EmitProgress(totalBytes, fs.CanSeek ? fs.Position : null);
                                pending.Add(item);
                                producedThisIteration = true;
                                if (options.Metrics.TerminatedEarly)
                                    rootFinished = true;
                            }
                            else if (options.Metrics.TerminatedEarly)
                            {
                                rootFinished = true;
                            }
                        }
                        if (producedThisIteration || rootFinished)
                            break;
                        continue; // Continue reading next token (same element)
                    }


                    if (!rootIsArray)
                        break; // (handled by continuation branch)

                    // ARRAY ROOT
                    if (reader.TokenType == JsonTokenType.EndArray)
                    {
                        rootFinished = true;
                        break;
                    }
                    // Abort before counting another element if a Stop or external termination occurred
                    if (options.Metrics.TerminatedEarly)
                    {
                        rootFinished = true;
                        break;
                    }

                    elementIndex++;
                    options.Metrics.RawRecordsParsed = elementIndex;

                    if (options.MaxElements > 0 &&
                        elementIndex > options.MaxElements &&
                        !maxElementsBreached)
                    {
                        maxElementsBreached = true;
                        if (!options.HandleError("JSON", -1, elementIndex, path,
                                "JsonSizeLimit",
                                $"Element count exceeded limit {options.MaxElements}.",
                                ""))
                        {
                            rootFinished = true;
                            break;
                        }
                        rootFinished = true;
                        break;
                    }

                    if (fastPath)
                    {
                        var valueReader = reader;
                        bool success = false;
                        T item = default;
                        try
                        {
                            item = JsonSerializer.Deserialize<T>(ref valueReader, options.SerializerOptions)!;
                            success = true;
                            reader = valueReader;
                        }
                        catch (Exception exElem)
                        {
                            string excerpt = BuildExcerptAndSkip(ref reader, 128);
                            if (!options.HandleError("JSON", -1, elementIndex, path,
                                    exElem.GetType().Name, exElem.Message, excerpt))
                            {
                                rootFinished = true;
                                break;
                            }
                            if (options.Metrics.TerminatedEarly)
                            {
                                rootFinished = true;
                                break;
                            }
                            continue; // skip this element
                        }

                        if (success)
                        {
                            
                            if (options.ShouldEmitProgress())
                                options.EmitProgress(totalBytes, fs.CanSeek ? fs.Position : null);
                            pending.Add(item);
                            producedThisIteration = true;
                            if (options.Metrics.TerminatedEarly)
                                rootFinished = true;
                        }
                    }
                    else
                    {
                        // Validation (capture) path
                        capturing = true;
                        openContainers = 0;
                        elementIsPrimitive = false;
                        BeginNewElementCapture();

                        WriteTokenToElement(ref reader, elementWriter!, ref openContainers, ref elementIsPrimitive);
                        if (!CheckElementSizeLimit())
                        {
                            if (!rootFinished && openContainers > 0)
                                SkipRemainingElement(ref reader);
                            capturing = false;
                            if (options.Metrics.TerminatedEarly)
                                rootFinished = true;
                            continue;
                        }

                        if (elementIsPrimitive)
                        {
                            capturing = false;
                            elementWriter!.Flush();
                        }
                        else
                        {
                            while (capturing && reader.Read())
                            {
                                WriteTokenToElement(ref reader, elementWriter!, ref openContainers, ref elementIsPrimitive);
                                if (!CheckElementSizeLimit())
                                {
                                    if (!rootFinished && openContainers > 0)
                                        SkipRemainingElement(ref reader);
                                    capturing = false;
                                    break;
                                }
                                if (openContainers == 0)
                                {
                                    capturing = false;
                                    elementWriter!.Flush();
                                    break;
                                }
                            }
                        }

                        if (!capturing)
                        {
                            if (TryFinalizeCapturedElement(elementBuffer!, options, path, elementIndex, out var item))
                            {
                                
                                if (options.ShouldEmitProgress())
                                    options.EmitProgress(totalBytes, fs.CanSeek ? fs.Position : null);
                                pending.Add(item);
                                producedThisIteration = true;
                                if (options.Metrics.TerminatedEarly)
                                    rootFinished = true;
                            }
                            else if (options.Metrics.TerminatedEarly)
                            {
                                rootFinished = true;
                            }
                        }
                    }

                    if (producedThisIteration) break;
                }

                state = reader.CurrentState;
                int consumedBytes = (int)reader.BytesConsumed;
                int remaining = bytesRead - consumedBytes;
                if (remaining > 0)
                    Buffer.BlockCopy(buffer, consumedBytes, buffer, 0, remaining);

                if (!rootFinished)
                {
                    int read = await fs.ReadAsync(buffer.AsMemory(remaining, buffer.Length - remaining), ct).ConfigureAwait(false);
                    bytesRead = remaining + read;
                    isFinalBlock = read == 0;
                    if (bytesRead == 0)
                        rootFinished = true;
                }

                if (pending.Count > 0)
                {
                    foreach (var itm in pending)
                    {
                        options.Metrics.RecordsEmitted++; // increment at yield only
                        yield return itm;
                    }
                    pending.Clear();
                }
            }
        }
        finally
        {
            elementWriter?.Dispose();
        }

        options.Complete();
    }


    // UPDATED SYNC JSON ITERATOR WITH SAME FIXES
    public static IEnumerable<T> JsonSync<T>(
        string path,
        JsonReadOptions<T> options,
        CancellationToken cancellationToken = default)
    {
        if (options == null) throw new ArgumentNullException(nameof(options));
        options.FilePath = path;

        if (options.ValidateElements && options.ElementValidator == null)
            throw new ArgumentException("ValidateElements is true but ElementValidator is null. Provide ElementValidator or disable ValidateElements.", nameof(options));

        var fileInfo = new FileInfo(path);
        long totalBytes = fileInfo.Exists ? fileInfo.Length : 0;

        var readerOptions = options.MaxDepth > 0
            ? new JsonReaderOptions { MaxDepth = options.MaxDepth, CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true }
            : new JsonReaderOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };

        const int BufferSize = 64 * 1024;
        byte[] buffer = new byte[BufferSize];
        int bytesRead;
        bool isFinalBlock;
        var state = new JsonReaderState(readerOptions);

        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read,
                                      BufferSize, FileOptions.SequentialScan);

        bytesRead = fs.Read(buffer, 0, buffer.Length);
        isFinalBlock = bytesRead == 0;
        if (isFinalBlock)
        {
            options.Complete();
            yield break;
        }

        bool rootDetermined = false;
        bool rootIsArray = false;
        bool rootFinished = false;
        long elementIndex = 0;
        options.Metrics.RawRecordsParsed = 0;

        bool fastPath = !(options.ValidateElements && options.ElementValidator != null);
        if (options.GuardRailsEnabled) fastPath = false;

        // Early one-shot path for a single validated non-array root (large object safe)
        if (!fastPath && options.AllowSingleObject)
        {
            var preview = new Utf8JsonReader(new ReadOnlySpan<byte>(buffer, 0, bytesRead), isFinalBlock, state);
            bool decided = false;
            bool previewSawArray = false;
            while (preview.Read())
            {
                switch (preview.TokenType)
                {
                    case JsonTokenType.StartArray:
                        previewSawArray = true;
                        decided = true;
                        break;
                    case JsonTokenType.StartObject:
                    case JsonTokenType.String:
                    case JsonTokenType.Number:
                    case JsonTokenType.True:
                    case JsonTokenType.False:
                    case JsonTokenType.Null:
                        decided = true;
                        break;
                    default:
                        continue;
                }
                if (decided) break;
            }

            if (decided && !previewSawArray &&
                !(options.RequireArrayRoot && !options.AllowSingleObject))
            {
                // Read remainder of file into memory
                using var ms = new MemoryStream((int)(totalBytes > 0 ? totalBytes : bytesRead * 2));
                ms.Write(buffer, 0, bytesRead);
                int r;
                while ((r = fs.Read(buffer, 0, buffer.Length)) > 0)
                    ms.Write(buffer, 0, r);
                ms.Position = 0;

                bool emit = false;
                T? item = default;

                // Parse + validate + deserialize (no yield inside)
                try
                {
                    using var doc = JsonDocument.Parse(ms,
                        new JsonDocumentOptions
                        {
                            AllowTrailingCommas = true,
                            MaxDepth = options.MaxDepth > 0 ? options.MaxDepth : 0
                        });

                    var rootEl = doc.RootElement;

                    elementIndex = 1;
                    options.Metrics.RawRecordsParsed = 1;

                    // Guard rail: MaxStringLength
                    if (options.MaxStringLength > 0 && JsonStringTooLong(rootEl, options.MaxStringLength))
                    {
                        if (!options.HandleError("JSON", -1, 1, path,
                                "JsonSizeLimit",
                                $"Element contains a string exceeding MaxStringLength {options.MaxStringLength}.",
                                Truncate(SafeGetRawText(rootEl), 128)))
                        {
                            // Throw or Stop already handled
                            options.Complete();
                            yield break;
                        }
                        // Skip (Skip mode)
                        options.Complete();
                        yield break;
                    }

                    // Validation
                    if (options.ValidateElements && options.ElementValidator != null)
                    {
                        bool valid;
                        try
                        {
                            valid = options.ElementValidator(rootEl);
                        }
                        catch (Exception exVal)
                        {
                            options.HandleError("JSON", -1, 1, path,
                                "JsonValidationError", exVal.Message,
                                Truncate(SafeGetRawText(rootEl), 128));
                            options.Complete();
                            yield break;
                        }

                        if (!valid)
                        {
                            if (!options.HandleError("JSON", -1, 1, path,
                                    "JsonValidationFailed",
                                    "Element validator returned false.",
                                    Truncate(SafeGetRawText(rootEl), 128)))
                            {
                                // Throw or Stop
                                options.Complete();
                                yield break;
                            }
                            // Skip
                            options.Complete();
                            yield break;
                        }
                    }

                    // Deserialize
                    try
                    {
                        item = rootEl.Deserialize<T>(options.SerializerOptions)!;
                        emit = true;
                    }
                    catch (Exception exDeser)
                    {
                        options.HandleError("JSON", -1, 1, path,
                            exDeser.GetType().Name, exDeser.Message,
                            Truncate(SafeGetRawText(rootEl), 128));
                        // Throw => exits enumeration automatically; Skip => no emit
                    }
                }
                catch (Exception exOuter)
                {
                    // Parsing-level failure
                    options.HandleError("JSON", -1, 1, path,
                        exOuter.GetType().Name, exOuter.Message, "");
                    // Throw stops; Skip just ends
                }

                options.Complete();

                if (emit)
                {
                    options.Metrics.RecordsEmitted = 1;
                    yield return item!;
                }
                yield break;
            }
            // Else: fall through to normal streaming (array or disallowed single root)
        }

        bool maxElementsBreached = false;
        bool initialProgressEmitted = false; // FIX

        ArrayBufferWriter<byte>? elementBuffer = null;
        Utf8JsonWriter? elementWriter = null;

        void BeginNewElementCapture()
        {
            elementBuffer ??= new ArrayBufferWriter<byte>(8 * 1024);
            ResetArrayBufferWriter(ref elementBuffer);
            elementWriter?.Dispose();
            elementWriter = new Utf8JsonWriter(elementBuffer);
        }

        bool capturing = false;
        int openContainers = 0;
        bool elementIsPrimitive = false;

        bool CheckElementSizeLimit()
        {
            if (options.MaxElementBytes > 0 && elementBuffer != null &&
                elementBuffer.WrittenCount > options.MaxElementBytes)
            {
                if (!options.HandleError("JSON", -1, elementIndex, path,
                        "JsonSizeLimit",
                        $"Element size {elementBuffer.WrittenCount} bytes exceeds limit {options.MaxElementBytes}.",
                        ""))
                {
                    rootFinished = true;
                    return false;
                }
                ResetArrayBufferWriter(ref elementBuffer);
                capturing = false;
                return false;
            }
            return true;
        }

        void SkipRemainingElement(ref Utf8JsonReader rdr)
        {
            while (openContainers > 0 && rdr.Read())
            {
                switch (rdr.TokenType)
                {
                    case JsonTokenType.StartObject:
                    case JsonTokenType.StartArray:
                        openContainers++;
                        break;
                    case JsonTokenType.EndObject:
                    case JsonTokenType.EndArray:
                        openContainers--;
                        break;
                }
            }
        }

        List<T> pending = new List<T>(1);

        try
        {
            if (!initialProgressEmitted && options.Progress != null)
            {
                options.EmitProgress(totalBytes, fs.CanSeek ? fs.Position : null);
                initialProgressEmitted = true;
            }

            while (!rootFinished)
            {
                var span = new ReadOnlySpan<byte>(buffer, 0, bytesRead);
                var reader = new Utf8JsonReader(span, isFinalBlock, state);

                bool producedThisIteration = false;

                while (reader.Read())
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    options.CancellationToken.ThrowIfCancellationRequested();

                    if (!rootDetermined)
                    {
                        switch (reader.TokenType)
                        {
                            case JsonTokenType.StartArray:
                                rootIsArray = true;
                                rootDetermined = true;
                                continue;

                            case JsonTokenType.StartObject:
                            case JsonTokenType.String:
                            case JsonTokenType.Number:
                            case JsonTokenType.True:
                            case JsonTokenType.False:
                            case JsonTokenType.Null:
                                rootIsArray = false;
                                rootDetermined = true;

                                if (options.RequireArrayRoot && !options.AllowSingleObject)
                                {
                                    if (!options.HandleError("JSON", -1, -1, path,
                                            "JsonFormatError", "Root element is not an array.", ""))
                                    {
                                        rootFinished = true;
                                    }
                                    else
                                    {
                                        rootFinished = true;
                                    }
                                    break;
                                }

                                elementIndex = 1;
                                options.Metrics.RawRecordsParsed = 1;

                                if (fastPath)
                                {
                                    var valueReader = reader;
                                    bool success = false;
                                    T item = default;
                                    try
                                    {
                                        item = JsonSerializer.Deserialize<T>(ref valueReader, options.SerializerOptions)!;
                                        success = true;
                                        reader = valueReader;
                                    }
                                    catch (Exception ex)
                                    {
                                        string excerpt = BuildExcerptAndSkip(ref reader, 128);
                                        if (!options.HandleError("JSON", -1, 1, path,
                                                ex.GetType().Name, ex.Message, excerpt))
                                        {
                                            rootFinished = true;
                                        }
                                    }

                                    if (success)
                                    {
                                        options.Metrics.RawRecordsParsed = 1;
                                        if (options.ShouldEmitProgress())
                                            options.EmitProgress(totalBytes, fs.CanSeek ? fs.Position : null);
                                        pending.Add(item);
                                        producedThisIteration = true;
                                        rootFinished = true;
                                    }
                                }
                                else
                                {
                                    BeginNewElementCapture();
                                    capturing = true;
                                    openContainers = 0;
                                    elementIsPrimitive = false;

                                    WriteTokenToElement(ref reader, elementWriter!, ref openContainers, ref elementIsPrimitive);
                                    if (!CheckElementSizeLimit())
                                    {
                                        if (!rootFinished && !elementIsPrimitive)
                                            SkipRemainingElement(ref reader);
                                        if (rootFinished) break;
                                    }

                                    if (elementIsPrimitive)
                                    {
                                        capturing = false;
                                        elementWriter!.Flush();
                                        if (TryFinalizeCapturedElement(elementBuffer!, options, path, elementIndex, out var item))
                                        {
                                            options.Metrics.RawRecordsParsed = 1;
                                            if (options.ShouldEmitProgress())
                                                options.EmitProgress(totalBytes, fs.CanSeek ? fs.Position : null);
                                            pending.Add(item);
                                            producedThisIteration = true;
                                        }
                                        else if (options.Metrics.TerminatedEarly)
                                        {
                                            rootFinished = true;
                                        }
                                        rootFinished = true;
                                    }
                                    // else continue across buffers
                                }
                                break;

                            default:
                                continue;
                        }

                        if (rootFinished || producedThisIteration) break;
                        if (!rootIsArray) break;
                        continue;
                    }

                    if (!rootIsArray)
                    {
                        if (capturing)
                        {
                            while (capturing && reader.Read())
                            {
                                WriteTokenToElement(ref reader, elementWriter!, ref openContainers, ref elementIsPrimitive);
                                if (!CheckElementSizeLimit())
                                {
                                    if (!rootFinished && !elementIsPrimitive)
                                        SkipRemainingElement(ref reader);
                                    capturing = false;
                                    break;
                                }
                                if (openContainers == 0)
                                {
                                    capturing = false;
                                    elementWriter!.Flush();
                                }
                            }

                            if (!capturing)
                            {
                                if (TryFinalizeCapturedElement(elementBuffer!, options, path, elementIndex, out var item))
                                {
                                    options.Metrics.RawRecordsParsed = 1;
                                    if (options.ShouldEmitProgress())
                                        options.EmitProgress(totalBytes, fs.CanSeek ? fs.Position : null);
                                    pending.Add(item);
                                    producedThisIteration = true;
                                }
                                else if (options.Metrics.TerminatedEarly)
                                {
                                    rootFinished = true;
                                }
                                rootFinished = true;
                            }
                        }
                        break;
                    }

                    if (reader.TokenType == JsonTokenType.EndArray)
                    {
                        rootFinished = true;
                        break;
                    }

                    // Continuation of multi-buffer array element (validation path)
                    if (capturing)
                    {
                        WriteTokenToElement(ref reader, elementWriter!, ref openContainers, ref elementIsPrimitive);
                        if (!CheckElementSizeLimit())
                        {
                            if (!rootFinished && openContainers > 0)
                                SkipRemainingElement(ref reader);
                            capturing = false;
                            if (options.Metrics.TerminatedEarly)
                                rootFinished = true;
                            continue;
                        }
                        if (openContainers == 0)
                        {
                            capturing = false;
                            elementWriter!.Flush();
                            if (TryFinalizeCapturedElement(elementBuffer!, options, path, elementIndex, out var item))
                            {
                                if (options.ShouldEmitProgress())
                                    options.EmitProgress(totalBytes, fs.CanSeek ? fs.Position : null);
                                pending.Add(item);
                                producedThisIteration = true;
                                if (options.Metrics.TerminatedEarly)
                                    rootFinished = true;
                            }
                            else if (options.Metrics.TerminatedEarly)
                            {
                                rootFinished = true;
                            }
                        }
                        if (producedThisIteration || rootFinished)
                            break;
                        continue;
                    }
                    // Abort before counting another element if a Stop or external termination occurred
                    if (options.Metrics.TerminatedEarly)
                    {
                        rootFinished = true;
                        break;
                    }
                    // New element start (not a continuation): count attempted element
                    elementIndex++;
                    options.Metrics.RawRecordsParsed = elementIndex;
                    if (options.MaxElements > 0 && elementIndex > options.MaxElements && !maxElementsBreached)
                    {
                        maxElementsBreached = true;
                        if (!options.HandleError("JSON", -1, elementIndex, path, "JsonSizeLimit",
                                $"Element count exceeded limit {options.MaxElements}.", ""))
                        {
                            rootFinished = true;
                            break;
                        }
                        rootFinished = true;
                        break;
                    }

                    if (fastPath)
                    {
                        var valueReader = reader;
                        bool success = false;
                        T item = default;
                        try
                        {
                            item = JsonSerializer.Deserialize<T>(ref valueReader, options.SerializerOptions)!;
                            success = true;
                            reader = valueReader;
                        }
                        catch (Exception exElem)
                        {
                            string excerpt = BuildExcerptAndSkip(ref reader, 128);
                            if (!options.HandleError("JSON", -1, elementIndex, path,
                                    exElem.GetType().Name, exElem.Message, excerpt))
                            {
                                rootFinished = true;
                                break;
                            }
                            if (options.Metrics.TerminatedEarly)
                            {
                                rootFinished = true;
                                break;
                            }
                            continue;
                        }

                        if (success)
                        {
                            if (options.ShouldEmitProgress())
                                options.EmitProgress(totalBytes, fs.CanSeek ? fs.Position : null);
                            pending.Add(item);
                            producedThisIteration = true;
                            if (options.Metrics.TerminatedEarly)
                                rootFinished = true;
                        }
                    }
                    else
                    {
                        capturing = true;
                        openContainers = 0;
                        elementIsPrimitive = false;
                        BeginNewElementCapture();

                        WriteTokenToElement(ref reader, elementWriter!, ref openContainers, ref elementIsPrimitive);
                        if (!CheckElementSizeLimit())
                        {
                            if (!rootFinished && !elementIsPrimitive)
                                SkipRemainingElement(ref reader);
                            capturing = false;
                            if (options.Metrics.TerminatedEarly)
                                rootFinished = true;
                            continue;
                        }

                        if (elementIsPrimitive)
                        {
                            capturing = false;
                            elementWriter!.Flush();
                        }
                        else
                        {
                            while (capturing && reader.Read())
                            {
                                WriteTokenToElement(ref reader, elementWriter!, ref openContainers, ref elementIsPrimitive);
                                if (!CheckElementSizeLimit())
                                {
                                    if (!rootFinished && !elementIsPrimitive)
                                        SkipRemainingElement(ref reader);
                                    capturing = false;
                                    break;
                                }
                                if (openContainers == 0)
                                {
                                    capturing = false;
                                    elementWriter!.Flush();
                                }
                            }
                        }

                        if (!capturing)
                        {
                            if (TryFinalizeCapturedElement(elementBuffer!, options, path, elementIndex, out var item))
                            {
                                if (options.ShouldEmitProgress())
                                    options.EmitProgress(totalBytes, fs.CanSeek ? fs.Position : null);
                                pending.Add(item);
                                producedThisIteration = true;
                                if (options.Metrics.TerminatedEarly)
                                    rootFinished = true;
                            }
                            else if (options.Metrics.TerminatedEarly)
                            {
                                rootFinished = true;
                            }
                        }
                    }

                    if (producedThisIteration) break;
                }

                state = reader.CurrentState;
                int consumed = (int)reader.BytesConsumed;
                int rem = bytesRead - consumed;
                if (rem > 0) Buffer.BlockCopy(buffer, consumed, buffer, 0, rem);

                if (!rootFinished)
                {
                    int read = fs.Read(buffer, rem, buffer.Length - rem);
                    bytesRead = rem + read;
                    isFinalBlock = read == 0;
                    if (bytesRead == 0) rootFinished = true;
                }

                if (pending.Count > 0)
                {
                    foreach (var itm in pending)
                    {
                        options.Metrics.RecordsEmitted++;
                        yield return itm;
                    }
                    pending.Clear();
                }
            }
        }
        finally
        {
            elementWriter?.Dispose();
        }

        options.Complete();
    }

    /// <summary>
    /// Finalize a captured element: validate (if required) and deserialize.
    /// Returns true + item when successful; false otherwise.
    /// </summary>
    private static bool TryFinalizeCapturedElement<T>(
        ArrayBufferWriter<byte> elementBuffer,
        JsonReadOptions<T> options,
        string path,
        long elementIndex,
        out T item)
    {
        item = default;
        bool success = false;

        try
        {
            ReadOnlyMemory<byte> mem = elementBuffer.WrittenMemory;
            ReadOnlySequence<byte> seq;
            if (MemoryMarshal.TryGetArray(mem, out ArraySegment<byte> segment) && segment.Array != null)
                seq = new ReadOnlySequence<byte>(segment.Array, segment.Offset, segment.Count);
            else
                seq = new ReadOnlySequence<byte>(mem.ToArray());

            using var doc = JsonDocument.Parse(seq);
            var root = doc.RootElement;

            if (options.MaxStringLength > 0)
            {
                if (JsonStringTooLong(root, options.MaxStringLength))
                {
                    if (!options.HandleError("JSON", -1, elementIndex, path,
                            "JsonSizeLimit",
                            $"Element contains a string exceeding MaxStringLength {options.MaxStringLength}.",
                            Truncate(SafeGetRawText(root), 128)))
                    {
                        ResetArrayBufferWriter(ref elementBuffer);
                        return false;
                    }
                    ResetArrayBufferWriter(ref elementBuffer);
                    return false;
                }
            }

            if (options.ValidateElements && options.ElementValidator != null)
            {
                bool valid;
                try
                {
                    valid = options.ElementValidator(root);
                }
                catch (Exception exVal)
                {
                    if (!options.HandleError("JSON", -1, elementIndex, path,
                            "JsonValidationError", exVal.Message,
                            Truncate(SafeGetRawText(root), 128)))
                        return false;
                    return false;
                }

                if (!valid)
                {
                    if (!options.HandleError("JSON", -1, elementIndex, path,
                            "JsonValidationFailed", "Element validator returned false.",
                            Truncate(SafeGetRawText(root), 128)))
                        return false;
                    return false;
                }
            }

            try
            {
                item = root.Deserialize<T>(options.SerializerOptions)!;
                success = true;
            }
            catch (Exception exDeser)
            {
                options.HandleError("JSON", -1, elementIndex, path,
                    exDeser.GetType().Name, exDeser.Message,
                    Truncate(SafeGetRawText(root), 128));
            }
        }
        catch (Exception exOuter)
        {
            options.HandleError("JSON", -1, elementIndex, path,
                exOuter.GetType().Name, exOuter.Message, "");
        }

        ResetArrayBufferWriter(ref elementBuffer);
        return success;
    }

    // Scan for string length breach
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
                    if (el.GetString()?.Length > maxLen)
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

    /* Helper: build a small excerpt while skipping a value (starting with reader on first token of the value).
     * Returns truncated JSON-like text (best effort). Advances reader past the entire value. */
    private static string BuildExcerptAndSkip(ref Utf8JsonReader reader, int maxChars)
    {
        var sb = new StringBuilder(Math.Min(maxChars, 256));
        int startDepth = 0;
        bool firstToken = true;

        void Append(string s)
        {
            if (sb.Length >= maxChars) return;
            int space = maxChars - sb.Length;
            if (s.Length <= space) sb.Append(s);
            else sb.Append(s.AsSpan(0, space));
        }

        try
        {
            if (reader.TokenType == JsonTokenType.StartObject || reader.TokenType == JsonTokenType.StartArray)
                startDepth = 1;

            AppendToken(ref reader, Append, firstToken);
            firstToken = false;

            if (startDepth == 0)
                return sb.ToString();

            while (startDepth > 0 && reader.Read())
            {
                if (reader.TokenType == JsonTokenType.StartObject || reader.TokenType == JsonTokenType.StartArray)
                    startDepth++;
                else if (reader.TokenType == JsonTokenType.EndObject || reader.TokenType == JsonTokenType.EndArray)
                    startDepth--;

                if (startDepth < 0) break; // Defensive: corrupted depth
                AppendToken(ref reader, Append, firstToken);
                firstToken = false;
            }
        }
        catch
        {
            // If reader state corrupt, return best-effort excerpt so far.
        }

        return sb.ToString();

        static void AppendToken(ref Utf8JsonReader r, Action<string> add, bool first)
        {
            switch (r.TokenType)
            {
                case JsonTokenType.StartObject: add("{"); break;
                case JsonTokenType.EndObject: add("}"); break;
                case JsonTokenType.StartArray: add("["); break;
                case JsonTokenType.EndArray: add("]"); break;
                case JsonTokenType.PropertyName:
                    if (!first) add(",");
                    add("\"" + JsonEscape(r.GetString()!) + "\":");
                    break;
                case JsonTokenType.String:
                    add("\"" + JsonEscape(r.GetString()!) + "\"");
                    break;
                case JsonTokenType.Number:
                    add(r.GetRawString());
                    break;
                case JsonTokenType.True: add("true"); break;
                case JsonTokenType.False: add("false"); break;
                case JsonTokenType.Null: add("null"); break;
            }
        }
    }

    // Helper to reset ArrayBufferWriter across TFMs (fallback reallocation if Clear not supported)
    private static void ResetArrayBufferWriter(ref ArrayBufferWriter<byte> buffer)
    {
#if NETSTANDARD2_0
    // If Clear() not available in target TFMs (defensive), reallocate preserving capacity heuristic.
    int capacity = Math.Max(buffer.WrittenCount, 1024);
    buffer = new ArrayBufferWriter<byte>(capacity);
#else
        buffer.Clear();
#endif
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
    private static void WriteTokenToElement(ref Utf8JsonReader reader,
                                            Utf8JsonWriter writer,
                                            ref int openContainers,
                                            ref bool elementIsPrimitive)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.StartObject:
                writer.WriteStartObject();
                openContainers++;
                break;
            case JsonTokenType.EndObject:
                writer.WriteEndObject();
                openContainers--;
                break;
            case JsonTokenType.StartArray:
                writer.WriteStartArray();
                openContainers++;
                break;
            case JsonTokenType.EndArray:
                writer.WriteEndArray();
                openContainers--;
                break;
            case JsonTokenType.PropertyName:
                writer.WritePropertyName(reader.GetString());
                break;
            case JsonTokenType.String:
                writer.WriteStringValue(reader.GetString());
                elementIsPrimitive = openContainers == 0;
                break;
            case JsonTokenType.Number:
#if NET8_0_OR_GREATER
                // Preserve exact numeric text
                writer.WriteRawValue(reader.ValueSpan, skipInputValidation: true);
#else
            if (reader.TryGetInt64(out long l))
                writer.WriteNumberValue(l);
            else if (reader.TryGetDecimal(out decimal dec))
                writer.WriteNumberValue(dec);
            else if (reader.TryGetDouble(out double dbl))
                writer.WriteNumberValue(dbl);
            else
                writer.WriteStringValue(reader.GetString());
#endif
                elementIsPrimitive = openContainers == 0;
                break;
            case JsonTokenType.True:
                writer.WriteBooleanValue(true);
                elementIsPrimitive = openContainers == 0;
                break;
            case JsonTokenType.False:
                writer.WriteBooleanValue(false);
                elementIsPrimitive = openContainers == 0;
                break;
            case JsonTokenType.Null:
                writer.WriteNullValue();
                elementIsPrimitive = openContainers == 0;
                break;
            default:
                break;
        }
    }

    private static string JsonEscape(string s)
    {
        // Minimal fast-path escape for excerpt (not full JSON spec)
        if (string.IsNullOrEmpty(s)) return s;
        var sb = new StringBuilder(s.Length + 4);
        foreach (char c in s)
        {
            switch (c)
            {
                case '\\': sb.Append("\\\\"); break;
                case '"': sb.Append("\\\""); break;
                case '\r': sb.Append("\\r"); break;
                case '\n': sb.Append("\\n"); break;
                case '\t': sb.Append("\\t"); break;
                default:
                    if (c < 32)
                        sb.Append("\\u").Append(((int)c).ToString("x4"));
                    else
                        sb.Append(c);
                    break;
            }
        }
        return sb.ToString();
    }

    // Extension: get raw numeric text (Utf8JsonReader does not expose a direct method; ValueSpan safe for numbers)
    private static string GetRawString(this ref Utf8JsonReader reader)
    {
        return reader.HasValueSequence
            ? Encoding.UTF8.GetString(reader.ValueSequence.ToArray())
            : Encoding.UTF8.GetString(reader.ValueSpan);
    }

    // Simple Json APIs
    public static async IAsyncEnumerable<T> Json<T>(string path, JsonSerializerOptions? options = null, Action<Exception>? onError = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var readOptions = new JsonReadOptions<T>
        {
            SerializerOptions = options ?? new JsonSerializerOptions { PropertyNameCaseInsensitive = true },
            ErrorAction = onError == null ? ReaderErrorAction.Throw : ReaderErrorAction.Skip,
            ErrorSink = onError == null ? NullErrorSink.Instance : new DelegatingErrorSink(e => onError(e), path)
        };
        await foreach (var item in Json<T>(path, readOptions, cancellationToken))
            yield return item;
    }

    public static IEnumerable<T> JsonSync<T>(string path, JsonSerializerOptions? options = null, Action<Exception>? onError = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var readOptions = new JsonReadOptions<T>
        {
            SerializerOptions = options ?? new JsonSerializerOptions { PropertyNameCaseInsensitive = true },
            ErrorAction = onError == null ? ReaderErrorAction.Throw : ReaderErrorAction.Skip,
            ErrorSink = onError == null ? NullErrorSink.Instance : new DelegatingErrorSink(e => onError(e), path)
        };
        foreach (var item in JsonSync<T>(path, readOptions, cancellationToken))
            yield return item;
    }

}
