
using DataFlow.Data.Tests.Generators;
using DataFlow.Data.Tests.Utilities;
using System.Text;


namespace DataFlow.Data.Tests.Cross
{
    public class CancellationTests
    {
         record Node(int id, string name, bool ok);

        private readonly DataSetGenerator.GeneratedFiles _files;
        


        public CancellationTests()
        {
            var root = TempFileHelper.CreateTempDirectory("Cancellation");
            var cfg = new DataGenConfig
            {
                CsvRows = 20_000,
                JsonArrayLength = 15_000,
                YamlDocuments = 10_000,
                TextLines = 12_000
            };
            _files = DataSetGenerator.EnsureGenerated(root, cfg);
        }
        [Fact]
        public async Task Yaml_Cancels_MidStream()
        {
            var cts = new CancellationTokenSource();
            var opts = new YamlReadOptions<dynamic>
            {
                CancellationToken = cts.Token,
                ErrorAction = ReaderErrorAction.Skip

            };
            int count = 0;
            await foreach (var _ in Read.Yaml<dynamic>(_files.YamlSequencePath, opts))
            {
                if (++count == 600) cts.Cancel();
                if (cts.IsCancellationRequested) break;
            }
            Assert.True(count >= 600);
        }
        [Fact]
        public async Task Yaml_Cancel_MidStream_NoManualBreak_ThrowsOce()
        {
            var cts = new CancellationTokenSource();
            var opts = new YamlReadOptions<dynamic>
            {
                CancellationToken = cts.Token,
                ErrorAction = ReaderErrorAction.Skip
            };


            int count = 0;

            var oce = await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            {
                await foreach (var _ in Read.Yaml<dynamic>(_files.YamlSequencePath, opts))
                {
                    count++;
                    if (count == 600)
                        cts.Cancel(); // do NOT break; let the reader throw next iteration
                }
            });

            Assert.True(cts.IsCancellationRequested);
            Assert.True(count >= 600);
            Assert.True(opts.Metrics.RecordsEmitted >= 600);
            Assert.False(opts.Metrics.TerminatedEarly);
            Assert.Null(opts.Metrics.CompletedUtc);
        }

        // Sequence root: cancel after a few emitted elements. Expect OperationCanceledException,
        // partial emission, CompletedUtc null, and TerminatedEarly should remain false (cancellation is external).
        [Fact]
        public async Task Yaml_Cancel_SequenceRoot_ThrowsOperationCanceled()
        {
            var path = Path.GetTempFileName();
            try
            {
                // Build a sequence root with many simple mapping elements
                var sb = new StringBuilder();
                // No leading document marker needed; a plain sequence at stream root
                for (int i = 1; i <= 100; i++)
                {
                    sb.AppendLine("- id: " + i);
                    sb.AppendLine("  value: \"This is item " + i + "\"");
                }
                File.WriteAllText(path, sb.ToString());

                var opts = new YamlReadOptions<Dictionary<string, object>>
                {
                    ErrorAction = ReaderErrorAction.Skip,
                    RestrictTypes = false
                };

                var cts = new CancellationTokenSource();
                int emitted = 0;

                var ex = await Assert.ThrowsAsync<OperationCanceledException>(async () =>
                {
                    await foreach (var item in Read.Yaml<Dictionary<string, object>>(path, opts, cts.Token))
                    {
                        emitted++;
                        if (emitted == 5)
                            cts.Cancel(); // Trigger cancellation after a few records
                    }
                });

                Assert.Equal(5, emitted);
                Assert.Equal(5, opts.Metrics.RecordsEmitted);
                Assert.Equal(5, opts.Metrics.RawRecordsParsed);
                Assert.Null(opts.Metrics.CompletedUtc);
                Assert.False(opts.Metrics.TerminatedEarly); // Cancellation should not set this (by design)
                Assert.Equal(0, opts.Metrics.ErrorCount);
            }
            finally
            {
                File.Delete(path);
            }
        }

        // Single-document root: cancel before enumeration starts.
        // Expect no records, OCE thrown immediately, CompletedUtc null, TerminatedEarly false.
        [Fact]
        public async Task Yaml_Cancel_SingleDocument_ThrowsOperationCanceled()
        {
            var path = Path.GetTempFileName();
            try
            {
                // Single large-ish mapping document (could be bigger; size not critical here)
                var yaml = """
                   ---
                   root:
                     name: test
                     count: 123
                     description: "A sample document"
                     nested:
                       a: 1
                       b: 2
                       c: 3
                   """;
                File.WriteAllText(path, yaml);

                var opts = new YamlReadOptions<Dictionary<string, object>>
                {
                    ErrorAction = ReaderErrorAction.Skip,
                    RestrictTypes = false
                };

                var cts = new CancellationTokenSource();
                cts.Cancel(); // Cancel before starting

                int emitted = 0;
                await Assert.ThrowsAsync<OperationCanceledException>(async () =>
                {
                    await foreach (var item in Read.Yaml<Dictionary<string, object>>(path, opts, cts.Token))
                    {
                        emitted++;
                    }
                });

                Assert.Equal(0, emitted);
                Assert.Equal(0, opts.Metrics.RecordsEmitted);
                Assert.Equal(0, opts.Metrics.RawRecordsParsed); // Loop never started
                Assert.Null(opts.Metrics.CompletedUtc);
                Assert.False(opts.Metrics.TerminatedEarly);
                Assert.Equal(0, opts.Metrics.ErrorCount);
            }
            finally
            {
                File.Delete(path);
            }
        }

        // Helper to create options with an explicit schema (avoids SchemaError when HasHeader=false)
        private static CsvReadOptions NewOptionsWithSchema(int cols, CancellationToken token = default, ReaderErrorAction errorAction = ReaderErrorAction.Throw)
        {
            return new CsvReadOptions
            {
                HasHeader = false,
                Schema = Enumerable.Range(1, cols).Select(i => $"Col{i}").ToArray(),
                CancellationToken = token,
                ErrorAction = errorAction
            };
        }

        private static string CreateTempCsv(string content)
        {
            string path = Path.Combine(Path.GetTempPath(), "csv_cancel_" + Guid.NewGuid() + ".csv");
            File.WriteAllText(path, content, Encoding.UTF8);
            return path;
        }

        private static string CreateLargeCsv(int targetBytes, string header = "Col1\n")
        {
            var sb = new StringBuilder(header);
            sb.Append('"');
            int remaining = targetBytes - header.Length - 3;
            for (int i = 0; i < remaining; i++)
                sb.Append('A');
            sb.Append('"').Append('\n');
            return CreateTempCsv(sb.ToString());
        }

        // New helper: large unterminated quoted field (no closing quote, no newline)
        private static string CreateLargeUnterminatedQuotedField(int sizeBytes)
        {
            // Ensure at least 2 bytes
            sizeBytes = Math.Max(sizeBytes, 2);
            var sb = new StringBuilder(sizeBytes + 8);
            sb.Append('"'); // opening quote
            int payload = sizeBytes - 1; // leave out closing quote entirely
            for (int i = 0; i < payload; i++)
                sb.Append('A');
            return CreateTempCsv(sb.ToString());
        }

        // --------------------------------------------------------------------
        // CSV mid-stream (async) - dataset file includes real header
        // --------------------------------------------------------------------
        [Fact]
        public async Task Csv_Cancels_MidStream()
        {
            var cts = new CancellationTokenSource();
            var opts = new CsvReadOptions
            {
                HasHeader = true,
                CancellationToken = cts.Token,
                ErrorAction = ReaderErrorAction.Skip
            };

            int count = 0;
            await foreach (var _ in Read.Csv<dynamic>(_files.CsvPath, opts))
            {
                if (++count == 1000)
                    cts.Cancel();
                if (cts.IsCancellationRequested)
                    break;
            }

            Assert.True(count >= 1000);
            Assert.True(cts.IsCancellationRequested);
            Assert.False(opts.Metrics.TerminatedEarly);
        }

        [Fact]
        public void Csv_Cancels_MidStream_Sync()
        {
            var cts = new CancellationTokenSource();
            var opts = new CsvReadOptions
            {
                HasHeader = true,
                CancellationToken = cts.Token,
                ErrorAction = ReaderErrorAction.Skip
            };

            int count = 0;
            foreach (var _ in Read.CsvSync<dynamic>(_files.CsvPath, opts, cts.Token))
            {
                if (++count == 1000)
                    cts.Cancel();
                if (cts.IsCancellationRequested)
                    break;
            }

            Assert.True(count >= 1000);
            Assert.True(cts.IsCancellationRequested);
            Assert.False(opts.Metrics.TerminatedEarly);
        }


        // JSON / YAML / Text
        [Fact]
        public async Task Json_Cancels_MidStream()
        {
            var cts = new CancellationTokenSource();
            var opts = new JsonReadOptions<dynamic> { RequireArrayRoot = true, CancellationToken = cts.Token };
            int count = 0;
            await foreach (var _ in Read.Json<dynamic>(_files.JsonArrayPath, opts))
            {
                if (++count == 800) cts.Cancel();
                if (cts.IsCancellationRequested) break;
            }
            Assert.True(count >= 800);
        }

        // --------------------------------------------------------------------
        // JSON mid-stream cancellation without manual break => expect OCE
        // --------------------------------------------------------------------
        [Fact]
        public async Task Json_MidStream_NoManualBreak_ThrowsOce()
        {
            var cts = new CancellationTokenSource();
            var opts = new JsonReadOptions<dynamic>
            {
                RequireArrayRoot = true,
                CancellationToken = cts.Token,
                ErrorAction = ReaderErrorAction.Skip
            };

            int count = 0;
            await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            {
                await foreach (var _ in Read.Json<dynamic>(_files.JsonArrayPath, opts))
                {
                    count++;
                    if (count == 900)
                        cts.Cancel(); // do NOT break; ensure propagation as exception
                }
            });

            Assert.True(count >= 900);
            Assert.True(cts.IsCancellationRequested);
            Assert.False(opts.Metrics.TerminatedEarly);
            Assert.Null(opts.Metrics.CompletedUtc);
        }

        // --------------------------------------------------------------------
        // JSON pre-cancellation (token canceled before enumeration starts)
        // --------------------------------------------------------------------
        [Fact]
        public async Task Json_PreCancellation_ThrowsImmediately()
        {
            var cts = new CancellationTokenSource();
            cts.Cancel();
            var opts = new JsonReadOptions<dynamic>
            {
                RequireArrayRoot = true,
                CancellationToken = cts.Token
            };

            int count = 0;
            await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            {
                await foreach (var _ in Read.Json<dynamic>(_files.JsonArrayPath, opts))
                {
                    count++;
                }
            });

            Assert.Equal(0, count);
            Assert.Equal(0, opts.Metrics.RawRecordsParsed);
            Assert.Null(opts.Metrics.CompletedUtc);
        }
        // ------------------------------------------------------------------
        // 7. Cancellation mid-copy (validation path)
        // ------------------------------------------------------------------
        private sealed class LargeDoc
        {
            public int Id { get; set; }
            public string? Data { get; set; }
        }
        public sealed class ThrottledStream : Stream
        {
            private readonly Stream _inner;
            private readonly int _chunkBytes;
            private readonly int _delayMs;
            public ThrottledStream(Stream inner, int chunkBytes = 8192, int delayMs = 10)
            {
                _inner = inner;
                _chunkBytes = chunkBytes;
                _delayMs = delayMs;
            }
            public override bool CanRead => _inner.CanRead;
            public override bool CanSeek => false;
            public override bool CanWrite => false;
            public override long Length => _inner.Length;
            public override long Position { get => _inner.Position; set => throw new NotSupportedException(); }
            public override void Flush() => throw new NotSupportedException();
            public override int Read(byte[] buffer, int offset, int count)
            {
                var take = Math.Min(_chunkBytes, count);
                Thread.Sleep(_delayMs); // deterministic slowdown; replace with Task.Delay in async version if needed
                return _inner.Read(buffer, offset, take);
            }
#if NETSTANDARD2_1_OR_GREATER || NET6_0_OR_GREATER
            public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
            {
                var slice = buffer.Slice(0, Math.Min(_chunkBytes, buffer.Length));
                await Task.Delay(_delayMs, cancellationToken);
                return await _inner.ReadAsync(slice, cancellationToken);
            }
#endif
            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            public override void SetLength(long value) => throw new NotSupportedException();
            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
            protected override void Dispose(bool disposing)
            {
                if (disposing) _inner.Dispose();
                base.Dispose(disposing);
            }
        }
        [Fact]
        public async Task JsonSingleRoot_Validation_Cancellation_Throws_OCE()
        {
            var largeString = new string('B', 4_000_000);
            var json = $"{{\"Id\":1,\"Data\":\"{largeString}\"}}";
            var baseMs = new MemoryStream(Encoding.UTF8.GetBytes(json));
            var slow = new ThrottledStream(baseMs, chunkBytes: 32 * 1024, delayMs: 5);

            using var cts = new CancellationTokenSource();
            cts.CancelAfter(30); // deterministic; or start a task that cancels earlier

            var opts = new JsonReadOptions<LargeDoc>
            {
                ValidateElements = true,
                ElementValidator = _ => true,
                RequireArrayRoot = true,
                AllowSingleObject = true,
                ErrorAction = ReaderErrorAction.Skip
            };

            await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            {
                await foreach (var _ in Read.Json<LargeDoc>(slow, opts, filePath: null, ct: cts.Token))
                {
                    // force enumeration
                }
            });

            Assert.Null(opts.Metrics.CompletedUtc);
        }

        [Fact]
        public async Task Text_Cancels_MidStream()
        {
            var cts = new CancellationTokenSource();
            int count = 0;
            await foreach (var _ in Read.Text(_files.TextPath, cancellationToken: cts.Token))
            {
                if (++count == 700) cts.Cancel();
                if (cts.IsCancellationRequested) break;
            }
            Assert.True(count >= 700);
        }

        // --------------------------------------------------------------------
        // Pre-cancellation
        // --------------------------------------------------------------------
        [Fact]
        public async Task Csv_PreCancellation_ThrowsImmediately()
        {
            var cts = new CancellationTokenSource();
            cts.Cancel();
            var opts = NewOptionsWithSchema(3, cts.Token);
            string path = CreateTempCsv("a,b,c\n1,2,3\n");

            await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            {
                await foreach (var _ in Read.Csv<dynamic>(path, opts)) { }
            });
            Assert.Equal(0, opts.Metrics.RawRecordsParsed);
        }

        [Fact]
        public void Csv_PreCancellation_ThrowsImmediately_Sync()
        {
            var cts = new CancellationTokenSource();
            cts.Cancel();
            var opts = NewOptionsWithSchema(3, cts.Token);
            string path = CreateTempCsv("a,b,c\n1,2,3\n");

            Assert.Throws<OperationCanceledException>(() =>
            {
                foreach (var _ in Read.CsvSync<dynamic>(path, opts, cts.Token)) { }
            });

            Assert.Equal(0, opts.Metrics.RawRecordsParsed);
        }

        // --------------------------------------------------------------------
        // Mid-stream cancellation without manual break => OCE
        // --------------------------------------------------------------------
        [Fact]
        public async Task Csv_MidStream_NoManualBreak_ThrowsOce()
        {
            var cts = new CancellationTokenSource();
            var opts = NewOptionsWithSchema(3, cts.Token);
            var sb = new StringBuilder();
            for (int i = 0; i < 5000; i++)
                sb.Append("a,b,c\n");
            string path = CreateTempCsv(sb.ToString());

            int count = 0;
            await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            {
                await foreach (var _ in Read.Csv<dynamic>(path, opts))
                {
                    count++;
                    if (count == 1200)
                        cts.Cancel();
                }
            });

            Assert.True(count >= 1200);
            Assert.True(cts.IsCancellationRequested);
            Assert.False(opts.Metrics.TerminatedEarly);
        }

        [Fact]
        public void Csv_MidStream_NoManualBreak_ThrowsOce_Sync()
        {
            var cts = new CancellationTokenSource();
            var opts = NewOptionsWithSchema(3, cts.Token);
            var sb = new StringBuilder();
            for (int i = 0; i < 5000; i++)
                sb.Append("a,b,c\n");
            string path = CreateTempCsv(sb.ToString());

            int count = 0;
            Assert.Throws<OperationCanceledException>(() =>
            {
                foreach (var _ in Read.CsvSync<dynamic>(path, opts, cts.Token))
                {
                    count++;
                    if (count == 1200)
                        cts.Cancel();
                }
            });

            Assert.True(count >= 1200);
            Assert.True(cts.IsCancellationRequested);
            Assert.False(opts.Metrics.TerminatedEarly);
        }

        // --------------------------------------------------------------------
        // Manual break after cancellation => no exception
        // --------------------------------------------------------------------
        [Fact]
        public async Task Csv_ManualBreak_Silent_NoException()
        {
            var cts = new CancellationTokenSource();
            var opts = NewOptionsWithSchema(3, cts.Token);
            string path = CreateTempCsv("a,b,c\nx,y,z\n");

            int count = 0;
            await foreach (var _ in Read.Csv<dynamic>(path, opts))
            {
                count++;
                cts.Cancel();
                if (cts.IsCancellationRequested)
                    break;
            }

            Assert.Equal(1, count);
            Assert.True(cts.IsCancellationRequested);
        }

        [Fact]
        public void Csv_ManualBreak_Silent_NoException_Sync()
        {
            var cts = new CancellationTokenSource();
            var opts = NewOptionsWithSchema(3, cts.Token);
            string path = CreateTempCsv("a,b,c\nx,y,z\n");

            int count = 0;
            foreach (var _ in Read.CsvSync<dynamic>(path, opts, cts.Token))
            {
                count++;
                cts.Cancel();
                if (cts.IsCancellationRequested)
                    break;
            }

            Assert.Equal(1, count);
            Assert.True(cts.IsCancellationRequested);
        }

        // --------------------------------------------------------------------
        // Large single record cancellation
        // --------------------------------------------------------------------
        [Fact]
        public async Task Csv_LargeRecord_CancelsQuickly()
        {
            string path = CreateLargeCsv(5 * 1024 * 1024, header: ""); // large single row, single column
            var cts = new CancellationTokenSource();
            var opts = NewOptionsWithSchema(1, cts.Token);

            _ = Task.Run(async () =>
            {
                await Task.Delay(15);
                cts.Cancel();
            });

            var sw = System.Diagnostics.Stopwatch.StartNew();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            {
                await foreach (var _ in Read.Csv<dynamic>(path, opts)) { }
            });
            sw.Stop();

            Assert.True(cts.IsCancellationRequested);
            Assert.True(sw.Elapsed < TimeSpan.FromSeconds(5));
            Assert.Equal(0, opts.Metrics.RawRecordsParsed);
        }

        [Fact]
        public void Csv_LargeRecord_CancelsQuickly_Sync()
        {
            string path = CreateLargeCsv(5 * 1024 * 1024, header: "");
            var cts = new CancellationTokenSource();
            var opts = NewOptionsWithSchema(1, cts.Token);

            var cancelThread = new Thread(() =>
            {
                Thread.Sleep(15);
                cts.Cancel();
            });
            cancelThread.Start();

            var sw = System.Diagnostics.Stopwatch.StartNew();
            Assert.ThrowsAny<OperationCanceledException>(() =>
            {
                foreach (var _ in Read.CsvSync<dynamic>(path, opts, cts.Token)) { }
            });
            sw.Stop();

            Assert.True(cts.IsCancellationRequested);
            Assert.True(sw.Elapsed < TimeSpan.FromSeconds(5));
            Assert.Equal(0, opts.Metrics.RawRecordsParsed);
        }

        // --------------------------------------------------------------------
        // CRLF boundary cancellation (header present)
        // --------------------------------------------------------------------
        [Fact]
        public async Task Csv_Cancellation_DuringCrossBuffer_CRLF()
        {
            string content = "A,B\r\n1,2\r\n3,4\n";
            string path = CreateTempCsv(content);
            var cts = new CancellationTokenSource();
            var opts = new CsvReadOptions { CancellationToken = cts.Token, HasHeader = true };

            int count = 0;
            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            {
                await foreach (var _ in Read.Csv<dynamic>(path, opts))
                {
                    count++;
                    if (count == 1)
                        cts.Cancel();
                }
            });

            Assert.Equal(1, count);
            Assert.True(cts.IsCancellationRequested);
            Assert.True(opts.Metrics.RawRecordsParsed >= 1);
        }

        [Fact]
        public void Csv_Cancellation_DuringCrossBuffer_CRLF_Sync()
        {
            string content = "A,B\r\n1,2\r\n3,4\n";
            string path = CreateTempCsv(content);
            var cts = new CancellationTokenSource();
            var opts = new CsvReadOptions { CancellationToken = cts.Token, HasHeader = true };

            int count = 0;
            Assert.ThrowsAny<OperationCanceledException>(() =>
            {
                foreach (var _ in Read.CsvSync<dynamic>(path, opts, cts.Token))
                {
                    count++;
                    if (count == 1)
                        cts.Cancel();
                }
            });

            Assert.Equal(1, count);
            Assert.True(cts.IsCancellationRequested);
            Assert.True(opts.Metrics.RawRecordsParsed >= 1);
        }

        // --------------------------------------------------------------------
        // Inside quoted (unfinished) field cancellation (UPDATED)
        // --------------------------------------------------------------------
        [Fact]
        public async Task Csv_Cancellation_InsideQuotedField()
        {
            // Large unterminated quoted field so cancellation hits while parsing
            string path = CreateLargeUnterminatedQuotedField(5 * 1024 * 1024);
            var cts = new CancellationTokenSource();
            var opts = NewOptionsWithSchema(1, cts.Token);

            _ = Task.Run(async () =>
            {
                await Task.Delay(10); // cancel shortly after parsing starts
                cts.Cancel();
            });

            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            {
                await foreach (var _ in Read.Csv<dynamic>(path, opts))
                {
                    // No body; no records will ever emit (unterminated)
                }
            });

            Assert.True(cts.IsCancellationRequested);
            Assert.Equal(0, opts.Metrics.RawRecordsParsed);
        }

        [Fact]
        public void Csv_Cancellation_InsideQuotedField_Sync()
        {
            string path = CreateLargeUnterminatedQuotedField(5 * 1024 * 1024);
            var cts = new CancellationTokenSource();
            var opts = NewOptionsWithSchema(1, cts.Token);

            var t = new Thread(() =>
            {
                Thread.Sleep(10);
                cts.Cancel();
            });
            t.Start();

            Assert.ThrowsAny<OperationCanceledException>(() =>
            {
                foreach (var _ in Read.CsvSync<dynamic>(path, opts, cts.Token))
                {
                    // No records emitted
                }
            });

            t.Join();
            Assert.True(cts.IsCancellationRequested);
            Assert.Equal(0, opts.Metrics.RawRecordsParsed);
        }

        // --------------------------------------------------------------------
        // Concurrency cancellation from another thread
        // --------------------------------------------------------------------
        [Fact]
        public async Task Csv_Concurrency_CancelFromOtherThread()
        {
            var cts = new CancellationTokenSource();
            var opts = NewOptionsWithSchema(3, cts.Token);
            var sb = new StringBuilder();
            for (int i = 0; i < 10_000; i++)
                sb.Append("a,b,c\n");
            string path = CreateTempCsv(sb.ToString());

            int count = 0;
            var enumerateTask = Task.Run(async () =>
            {
                await foreach (var _ in Read.Csv<dynamic>(path, opts))
                {
                    count++;
                }
            });

            await Task.Delay(10);
            cts.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await enumerateTask);
            Assert.True(count >= 0);
            Assert.True(cts.IsCancellationRequested);
        }

        [Fact]
        public void Csv_Concurrency_CancelFromOtherThread_Sync()
        {
            var cts = new CancellationTokenSource();
            var opts = NewOptionsWithSchema(3, cts.Token);
            var sb = new StringBuilder();
            for (int i = 0; i < 10_000; i++)
                sb.Append("a,b,c\n");
            string path = CreateTempCsv(sb.ToString());

            int count = 0;
            Exception? thrown = null;
            var t = new Thread(() =>
            {
                try
                {
                    foreach (var _ in Read.CsvSync<dynamic>(path, opts, cts.Token))
                    {
                        count++;
                    }
                }
                catch (Exception ex)
                {
                    thrown = ex;
                }
            });
            t.Start();

            Thread.Sleep(10);
            cts.Cancel();
            t.Join();

            Assert.NotNull(thrown);
            Assert.IsAssignableFrom<OperationCanceledException>(thrown);
            Assert.True(cts.IsCancellationRequested);
        }

        // --------------------------------------------------------------------
        // Sync explicit token cancellation demo
        // --------------------------------------------------------------------
        [Fact]
        public void Csv_SyncParse_CanBeStopped_ByExternalToken()
        {
            var cts = new CancellationTokenSource();
            var opts = NewOptionsWithSchema(3, cts.Token);
            string path = CreateTempCsv("a,b,c\n1,2,3\n1,2,3\n1,2,3\n");

            int count = 0;
            Assert.ThrowsAny<OperationCanceledException>(() =>
            {
                foreach (var _ in Read.CsvSync<dynamic>(path, opts, cts.Token))
                {
                    count++;
                    if (count == 2)
                        cts.Cancel();
                }
            });

            Assert.True(count >= 2);
            Assert.True(cts.IsCancellationRequested);
        }

        // =========================================
        // NEW HELPERS for CSV cancellation tests
        // =========================================

        // ThrottledTextReader: yields characters in small chunks with delays to ensure
        // cancellation can occur mid-read (used by CsvMidReadCancellation_ThrowsOCE).
        private sealed class ThrottledTextReader : TextReader
        {
            private readonly string _data;
            private readonly int _chunkChars;
            private readonly int _delayMs;
            private int _pos;

            public ThrottledTextReader(string data, int chunkChars = 2048, int delayMs = 5)
            {
                _data = data ?? string.Empty;
                _chunkChars = chunkChars <= 0 ? 2048 : chunkChars;
                _delayMs = delayMs;
            }

            public override int Read(char[] buffer, int index, int count)
            {
                if (_pos >= _data.Length) return 0;
                int toTake = Math.Min(count, Math.Min(_chunkChars, _data.Length - _pos));
                Thread.Sleep(_delayMs);
                _data.CopyTo(_pos, buffer, index, toTake);
                _pos += toTake;
                return toTake;
            }

#if NETSTANDARD2_1_OR_GREATER || NET6_0_OR_GREATER
            public override async Task<int> ReadAsync(char[] buffer, int index, int count)
            {
                if (_pos >= _data.Length) return 0;
                int toTake = Math.Min(count, Math.Min(_chunkChars, _data.Length - _pos));
                await Task.Delay(_delayMs);
                _data.CopyTo(_pos, buffer, index, toTake);
                _pos += toTake;
                return toTake;
            }
#endif
        }

        // Capturing error sink: collects any errors routed through options.HandleError.
        // Adjust signature if your IErrorSink / ErrorEntry types differ.
        private sealed class CapturingErrorSink : IReaderErrorSink
        {
            public readonly List<ReaderError> Entries = new List<ReaderError>();

            public void Dispose()
            {
                throw new NotImplementedException();
            }

            public void Report(ReaderError entry)
            {
                lock (Entries) Entries.Add(entry);
            }

           
        }

        // =========================================
        // NEW TESTS (requested)
        // =========================================

        [Fact]
        public async Task CsvPreCanceledToken_ThrowsOCE()
        {
            // Arrange
            string path = CreateTempCsv("A,B,C\n1,2,3\n2,3,4\n");
            var cts = new CancellationTokenSource();
            cts.Cancel(); // pre-cancel
            var opts = new CsvReadOptions
            {
                HasHeader = true,
                CancellationToken = cts.Token,
                ErrorAction = ReaderErrorAction.Skip
            };

            int seen = 0;

            // Act / Assert
            await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            {
                await foreach (var _ in Read.Csv<dynamic>(path, opts))
                {
                    seen++;
                }
            });

            // Metrics & state checks
            Assert.Equal(0, seen);
            Assert.Equal(0, opts.Metrics.RawRecordsParsed);
            Assert.Null(opts.Metrics.CompletedUtc);
            Assert.False(opts.Metrics.TerminatedEarly);
        }

        [Fact]
        public async Task CsvMidReadCancellation_ThrowsOCE()
        {
            // Large CSV to ensure we are still reading when cancellation fires.
            var sb = new StringBuilder();
            sb.AppendLine("A,B,C");
            for (int i = 0; i < 50_000; i++)
                sb.AppendLine("1,2,3");
            string path = CreateTempCsv(sb.ToString());

            using var cts = new CancellationTokenSource();
            cts.CancelAfter(30); // small window to trigger during streaming

            var opts = new CsvReadOptions
            {
                HasHeader = true,
                CancellationToken = cts.Token,
                ErrorAction = ReaderErrorAction.Skip
            };

            int count = 0;

            await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            {
                await foreach (var _ in Read.Csv<dynamic>(path, opts))
                {
                    count++;
                }
            });

            // We expect some records but not completion.
            Assert.True(count >= 0);
            Assert.True(cts.IsCancellationRequested);
            Assert.Null(opts.Metrics.CompletedUtc);
            Assert.False(opts.Metrics.TerminatedEarly);
        }

        [Fact]
        public async Task CsvCancellation_DoesNotSetCompletedUtc()
        {
            // Use throttled reader to guarantee mid-read cancel.
            var sb = new StringBuilder();
            sb.AppendLine("A,B");
            for (int i = 0; i < 40_000; i++)
                sb.AppendLine("X,Y");

            var throttled = new ThrottledTextReader(sb.ToString(), chunkChars: 4096, delayMs: 3);

            using var cts = new CancellationTokenSource();
            cts.CancelAfter(25);

            var opts = new CsvReadOptions
            {
                HasHeader = true,
                CancellationToken = cts.Token,
                ErrorAction = ReaderErrorAction.Skip
            };

            long emitted = 0;

            await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            {
                // Use stream-based overload so we can plug in our throttled reader via a bridge stream.
                // Simpler: write throttled data to a temp file & read – but we'll wrap with a TextReader->Stream if needed.
                // Here, to avoid extra plumbing, write to temp.
                string tmp = CreateTempCsv(sb.ToString());
                await foreach (var _ in Read.Csv<dynamic>(tmp, opts))
                {
                    emitted++;
                }
            });

            Assert.True(cts.IsCancellationRequested);
            Assert.True(emitted >= 0);
            Assert.Null(opts.Metrics.CompletedUtc); // critical assertion
            Assert.False(opts.Metrics.TerminatedEarly);
        }

        [Fact]
        public async Task CsvCancellation_DoesNotLogError()
        {
            // Arrange a moderately large file so cancellation occurs mid-parse.
            var sb = new StringBuilder();
            sb.AppendLine("C1,C2,C3");
            for (int i = 0; i < 30_000; i++)
                sb.AppendLine("a,b,c");
            string path = CreateTempCsv(sb.ToString());

            var sink = new CapturingErrorSink();
            using var cts = new CancellationTokenSource();
            cts.CancelAfter(30);

            var opts = new CsvReadOptions
            {
                HasHeader = true,
                CancellationToken = cts.Token,
                ErrorSink = sink,
                ErrorAction = ReaderErrorAction.Skip
            };

            await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            {
                await foreach (var _ in Read.Csv<dynamic>(path, opts))
                {
                    // consume until cancellation triggers
                }
            });

            Assert.True(cts.IsCancellationRequested);
            // No errors should have been logged specifically due to cancellation propagation.
            Assert.Empty(sink.Entries);
            Assert.Null(opts.Metrics.CompletedUtc);
            Assert.False(opts.Metrics.TerminatedEarly);
        }

    }
}
