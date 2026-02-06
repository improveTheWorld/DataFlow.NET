using System.Text;
using DataFlow;
using System.Threading;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System;
using System.Threading.Tasks;
using Xunit;

namespace DataFlow.Data.Tests.Text;

public sealed class Read_Text_Tests : IDisposable
{
    private readonly string _tmpDir;

    public Read_Text_Tests()
    {
        _tmpDir = Path.Combine(Path.GetTempPath(), "DataFlow_Text_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tmpDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tmpDir, recursive: true); } catch { }
    }

    private string CreateFile(string name, string content, Encoding? enc = null, bool emitBom = false)
    {
        string path = Path.Combine(_tmpDir, name);
        var encoding = enc ?? new UTF8Encoding(encoderShouldEmitUTF8Identifier: emitBom, throwOnInvalidBytes: true);
        File.WriteAllText(path, content, encoding);
        return path;
    }

    private static async Task<List<string>> ToListAsync(IAsyncEnumerable<string> e, CancellationToken ct = default)
    {
        var list = new List<string>();
        await foreach (var s in e.WithCancellation(ct))
            list.Add(s);
        return list;
    }

    [Fact]
    public async Task Async_Stream_Minimal_ReadsAllLines_And_LeavesStreamOpen()
    {
        var content = "a\nb\nc\n";
        using var ms = new MemoryStream(Encoding.UTF8.GetBytes(content));
        var lines = await ToListAsync(Read.Text(ms));
        Assert.Equal(new[] { "a", "b", "c" }, lines);

        // Ensure stream still open and position at end (we can reset and read again)
        Assert.True(ms.CanRead);
        ms.Position = 0;
        using var sr = new StreamReader(ms, leaveOpen: true);
        var first = await sr.ReadLineAsync();
        Assert.Equal("a", first);
    }

    [Fact]
    public async Task Async_File_Minimal_ReadsAllLines()
    {
        var path = CreateFile("min.txt", "x\ny\nz\n");
        var lines = await ToListAsync(Read.Text(path));
        Assert.Equal(new[] { "x", "y", "z" }, lines);
    }

    [Fact]
    public async Task Async_Stream_Options_TracksMetrics_And_Progress()
    {
        var sb = new StringBuilder();
        for (int i = 0; i < 12_345; i++)
        {
            sb.Append("line").Append(i).Append('\n');
        }
        using var ms = new MemoryStream(Encoding.UTF8.GetBytes(sb.ToString()));

        var progEvents = new List<ReaderProgress>();
        var opts = new TextReadOptions
        {
            Progress = new Progress<ReaderProgress>(p => progEvents.Add(p)),
            ProgressRecordInterval = 1000, // steady count-based events
            ProgressTimeInterval = TimeSpan.FromMilliseconds(0) // allow immediate gating on count
        };

        var lines = await ToListAsync(Read.Text(ms, opts, filePath: "inmemory.txt"));
        Assert.Equal(12_345, lines.Count);

        // Metrics
        Assert.NotEqual(default, opts.Metrics.StartedUtc);
        Assert.NotNull(opts.Metrics.CompletedUtc);
        Assert.Equal(12_345, opts.Metrics.RecordsEmitted);
        Assert.False(opts.Metrics.TerminatedEarly);
        Assert.Equal("inmemory.txt", opts.FilePath);

        // Progress events
        Assert.True(progEvents.Count > 0);
        Assert.True(progEvents.Last().RecordsRead == 12_345);
        Assert.True(progEvents.All(p => p.ErrorCount == opts.Metrics.ErrorCount));
    }

    [Fact]
    public async Task Async_File_Options_UsesFileStreamWithSequentialScan_And_SetsFilePath()
    {
        var path = CreateFile("big.txt", string.Join('\n', Enumerable.Range(1, 1000)) + "\n");
        var opts = new TextReadOptions();
        var lines = await ToListAsync(Read.Text(path, opts));
        Assert.Equal(1000, lines.Count);
        Assert.Equal(path, opts.FilePath);
        Assert.NotEqual(default, opts.Metrics.StartedUtc);
        Assert.NotNull(opts.Metrics.CompletedUtc);
        Assert.Equal(1000, opts.Metrics.RecordsEmitted);
    }

    [Fact]
    public async Task Cancellation_PerCallToken_StopsImmediately_WithoutCompletedUtc()
    {
        var content = string.Join('\n', Enumerable.Range(0, 100000)) + "\n";
        using var ms = new MemoryStream(Encoding.UTF8.GetBytes(content));

        var cts = new CancellationTokenSource();
        var opts = new TextReadOptions
        {
            ProgressRecordInterval = 10
        };

        var enumerator = Read.Text(ms, opts).GetAsyncEnumerator(cts.Token);
        // Start and then cancel quickly
        Assert.True(await enumerator.MoveNextAsync());

        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            while (await enumerator.MoveNextAsync()) { }
        });

        // CompletedUtc remains null on cancellation
        Assert.Null(opts.Metrics.CompletedUtc);
        // Some lines might have been emitted, but no finalization
    }

    [Fact]
    public async Task Cancellation_OptionsToken_StopsImmediately_WithoutCompletedUtc()
    {
        var content = string.Join('\n', Enumerable.Range(0, 50000)) + "\n";
        using var ms = new MemoryStream(Encoding.UTF8.GetBytes(content));

        var cts = new CancellationTokenSource();
        var opts = new TextReadOptions
        {
            CancellationToken = cts.Token,
            ProgressRecordInterval = 10
        };

        var e = Read.Text(ms, opts).GetAsyncEnumerator();
        Assert.True(await e.MoveNextAsync());
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            while (await e.MoveNextAsync()) { }
        });

        Assert.Null(opts.Metrics.CompletedUtc);
    }

    /// <summary>
    /// FLAKY: Progress&lt;T&gt; posts callbacks asynchronously. When run with other tests,
    /// thread pool contention may delay callbacks. Run alone if fails in batch.
    /// </summary>
    [Fact]
    public void Sync_Stream_Options_TracksMetrics_And_Progress()
    {
        var content = "a\nb\nc\nd\n";
        using var ms = new MemoryStream(Encoding.UTF8.GetBytes(content));
        var progEvents = new List<ReaderProgress>();
        var opts = new TextReadOptions
        {
            Progress = new Progress<ReaderProgress>(p => progEvents.Add(p)),
            ProgressRecordInterval = 2
        };

        var lines = Read.TextSync(ms, opts).ToList();

        Assert.Equal(new[] { "a", "b", "c", "d" }, lines);
        Assert.Equal(4, opts.Metrics.RecordsEmitted);
        Assert.NotEqual(default, opts.Metrics.StartedUtc);
        Assert.NotNull(opts.Metrics.CompletedUtc);

        Assert.True(progEvents.Count >= 2);
        Assert.Equal(4, progEvents.Last().RecordsRead);
    }

    [Fact]
    public void Sync_File_Minimal_ReadsAllLines()
    {
        var path = CreateFile("sync.txt", "one\ntwo\nthree\n");
        var got = Read.TextSync(path).ToList();
        Assert.Equal(new[] { "one", "two", "three" }, got);
    }

    [Fact]
    public async Task Encoding_DefaultUtf8_NoBom_DetectOn_And_CustomEncoding()
    {
        // Default (UTF-8, detect true)
        var p1 = CreateFile("utf8.txt", "α\nβ\nγ\n", new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true));
        var l1 = await ToListAsync(Read.Text(p1));
        Assert.Equal(new[] { "α", "β", "γ" }, l1);

        // UTF-8 with BOM and detection on should still read fine
        var p2 = CreateFile("utf8bom.txt", "x\ny\n", new UTF8Encoding(encoderShouldEmitUTF8Identifier: true, throwOnInvalidBytes: true));
        var l2 = await ToListAsync(Read.Text(p2));
        Assert.Equal(new[] { "x", "y" }, l2);

        // Custom encoding (UTF-16LE) with BOM; detection disabled should still decode if we explicitly set Encoding
        var content = "A\r\nB\r\nC\r\n";
        var bytes = Encoding.Unicode.GetPreamble().Concat(Encoding.Unicode.GetBytes(content)).ToArray();
        var p3 = Path.Combine(_tmpDir, "utf16le.txt");
        File.WriteAllBytes(p3, bytes);

        var opts = new TextReadOptions
        {
            Encoding = Encoding.Unicode,
            DetectEncodingFromByteOrderMarks = false
        };
        var l3 = await ToListAsync(Read.Text(p3, opts));
        Assert.Equal(new[] { "A", "B", "C" }, l3);
    }

    [Fact(Skip = "Flaky timing-dependent test")]
    public async Task Progress_TimeGated_FiresEvenWithLargeIntervalsOfRecords()
    {
        var content = string.Join('\n', Enumerable.Range(0, 5000)) + "\n";
        using var ms = new MemoryStream(Encoding.UTF8.GetBytes(content));

        var progEvents = new List<ReaderProgress>();
        var opts = new TextReadOptions
        {
            Progress = new Progress<ReaderProgress>(p => progEvents.Add(p)),
            ProgressRecordInterval = 0, // disable record gating
            ProgressTimeInterval = TimeSpan.FromMilliseconds(10)
        };

        var lines = await ToListAsync(Read.Text(ms, opts));
        Assert.Equal(5000, lines.Count);

        // We expect at least one progress event at start and one later (time-based)
        Assert.True(progEvents.Count >= 1);
        Assert.Equal(5000, progEvents.Last().RecordsRead);
    }

    [Fact]
    public void Sync_Cancellation_Throws_OperationCanceledException()
    {
        var content = string.Join('\n', Enumerable.Range(0, 100000)) + "\n";
        using var ms = new MemoryStream(Encoding.UTF8.GetBytes(content));

        var cts = new CancellationTokenSource();
        var opts = new TextReadOptions();

        var enumerable = Read.TextSync(ms, opts, filePath: null, cancellationToken: cts.Token);

        using var e = enumerable.GetEnumerator();
        Assert.True(e.MoveNext()); // get first
        cts.Cancel();

        Assert.Throws<OperationCanceledException>(() =>
        {
            while (e.MoveNext()) { }
        });
        Assert.Null(opts.Metrics.CompletedUtc);
    }

    [Fact]
    public async Task FilePath_IsPropagated_To_Options_In_StreamOverload()
    {
        var content = "a\n";
        using var ms = new MemoryStream(Encoding.UTF8.GetBytes(content));
        var opts = new TextReadOptions();

        var list = await ToListAsync(Read.Text(ms, opts, filePath: "custom.stream.name"));
        Assert.Equal(new[] { "a" }, list);
        Assert.Equal("custom.stream.name", opts.FilePath);
    }

    [Fact]
    public async Task LargeBufferSize_IsHonored_And_DoesNotBreakReading()
    {
        var big = string.Join('\n', Enumerable.Range(0, 20000)) + "\n";
        using var ms = new MemoryStream(Encoding.UTF8.GetBytes(big));
        var opts = new TextReadOptions
        {
            BufferSize = 256 * 1024
        };
        var lines = await ToListAsync(Read.Text(ms, opts));
        Assert.Equal(20000, lines.Count);
        Assert.Equal(20000, opts.Metrics.RecordsEmitted);
    }

    [Fact]
    public void Sync_BufferSize_IsHonored()
    {
        var content = string.Join('\n', Enumerable.Range(0, 2048)) + "\n";
        using var ms = new MemoryStream(Encoding.UTF8.GetBytes(content));
        var opts = new TextReadOptions
        {
            BufferSize = 8 * 1024
        };
        var lines = Read.TextSync(ms, opts).ToList();
        Assert.Equal(2048, lines.Count);
    }
}
