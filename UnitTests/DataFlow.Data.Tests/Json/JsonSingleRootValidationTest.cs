using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DataFlow.Data.Tests.Json
{
    public class JsonSingleRootValidationStreamTests
    {
        // DTOs
        private sealed class MyDoc
        {
            public int Id { get; set; }
            public string? Name { get; set; }
        }

        private sealed class LargeDoc
        {
            public int Id { get; set; }
            public string? Data { get; set; }
        }

        private sealed class ValidatorFailDoc
        {
            public int Id { get; set; }
            public string? Flag { get; set; }
        }

        // ------------------------------------------------------------------
        // 1. Original (Enhanced) Non-Seekable Stream + Validation Path
        // ------------------------------------------------------------------
        [Fact]
        public async Task SingleRoot_Object_With_Validation_From_NonSeekable_Stream_Works()
        {
            const string json = "{ \"Id\": 42, \"Name\": \"Widget\" }";
            var bytes = Encoding.UTF8.GetBytes(json);

            using var nonSeekable = new NonSeekableStream(bytes);

            bool validatorCalled = false;
            var options = new JsonReadOptions<MyDoc>
            {
                ValidateElements = true,
                ElementValidator = elem =>
                {
                    validatorCalled = true;
                    return elem.TryGetProperty("Id", out _);
                },
                RequireArrayRoot = true,
                AllowSingleObject = true,
                ErrorAction = ReaderErrorAction.Throw
            };

            var results = new List<MyDoc>();

            await foreach (var doc in Read.Json<MyDoc>(nonSeekable, options, filePath: null))
                results.Add(doc);

            Assert.True(validatorCalled);
            Assert.Single(results);
            Assert.Equal(42, results[0].Id);
            Assert.Equal("Widget", results[0].Name);

            Assert.Equal(1, options.Metrics.RawRecordsParsed);
            Assert.Equal(1, options.Metrics.RecordsEmitted);
            Assert.False(options.Metrics.TerminatedEarly);
            Assert.NotNull(options.Metrics.CompletedUtc);
        }

        // ------------------------------------------------------------------
        // 2. Single root + Validation Path from FileStream (seekable)
        // ------------------------------------------------------------------
        [Fact]
        public async Task SingleRoot_Object_With_Validation_From_FileStream_Works()
        {
            var tmp = Path.GetTempFileName();
            try
            {
                await File.WriteAllTextAsync(tmp, "{ \"Id\": 7, \"Name\": \"FileBacked\" }");

                var options = new JsonReadOptions<MyDoc>
                {
                    ValidateElements = true,
                    ElementValidator = e => e.TryGetProperty("Id", out _),
                    RequireArrayRoot = true,
                    AllowSingleObject = true,
                    ErrorAction = ReaderErrorAction.Throw
                };

                var result = await Read.Json<MyDoc>(tmp, options).SingleAsync();

                Assert.Equal(7, result.Id);
                Assert.Equal("FileBacked", result.Name);
                Assert.Equal(1, options.Metrics.RawRecordsParsed);
                Assert.Equal(1, options.Metrics.RecordsEmitted);
                Assert.False(options.Metrics.TerminatedEarly);
                Assert.NotNull(options.Metrics.CompletedUtc);
            }
            finally
            {
                try { File.Delete(tmp); } catch { }
            }
        }

        // ------------------------------------------------------------------
        // 3. Single root with MaxStringLength > 0 (forces validation)
        // ------------------------------------------------------------------
        [Fact]
        public async Task SingleRoot_Object_With_MaxStringLength_ValidationPath_Works()
        {
            var json = "{ \"Id\": 99, \"Name\": \"Short\" }";
            var ms = new MemoryStream(Encoding.UTF8.GetBytes(json));

            var options = new JsonReadOptions<MyDoc>
            {
                MaxStringLength = 100, // Forces validation path
                RequireArrayRoot = true,
                AllowSingleObject = true,
                ErrorAction = ReaderErrorAction.Throw
            };

            var item = await Read.Json<MyDoc>(ms, options).SingleAsync();

            Assert.Equal(99, item.Id);
            Assert.Equal("Short", item.Name);
            Assert.Equal(1, options.Metrics.RawRecordsParsed);
            Assert.Equal(1, options.Metrics.RecordsEmitted);
            Assert.False(options.Metrics.TerminatedEarly);
        }

        // ------------------------------------------------------------------
        // 4. Single root with failing validator (Skip)
        // ------------------------------------------------------------------
        [Fact]
        public async Task SingleRoot_FailingValidator_Skip_RecordsError_NoEmission()
        {
            var sink = new TestErrorSink();
            var json = "{ \"Id\": 1, \"Flag\": \"X\" }";
            var ms = new MemoryStream(Encoding.UTF8.GetBytes(json));

            var options = new JsonReadOptions<ValidatorFailDoc>
            {
                ValidateElements = true,
                ElementValidator = e => false,  // always fails
                RequireArrayRoot = true,
                AllowSingleObject = true,
                ErrorAction = ReaderErrorAction.Skip,
                ErrorSink = sink
            };

            var list = new List<ValidatorFailDoc>();
            await foreach (var doc in Read.Json<ValidatorFailDoc>(ms, options))
                list.Add(doc);

            Assert.Empty(list);
            Assert.Equal(1, options.Metrics.RawRecordsParsed); // processed once
            Assert.Equal(0, options.Metrics.RecordsEmitted);
            Assert.False(options.Metrics.TerminatedEarly);
            Assert.Single(sink.Errors);
            Assert.Equal("JsonValidationFailed", sink.Errors[0].ErrorType);
            Assert.NotNull(options.Metrics.CompletedUtc);
        }

        // ------------------------------------------------------------------
        // 5. Large single root object (multi-MB) ensures buffering & progress
        // ------------------------------------------------------------------
        [Fact]
        public async Task SingleRoot_LargeObject_Validation_Buffering_Works()
        {
            var largeString = new string('A', 2_500_000); // ~2.5MB
            var json = $"{{\"Id\":123,\"Data\":\"{largeString}\"}}";
            var ms = new MemoryStream(Encoding.UTF8.GetBytes(json));

            var progressEvents = new List<ReaderProgress>();
            var options = new JsonReadOptions<LargeDoc>
            {
                ValidateElements = true,
                ElementValidator = e => e.TryGetProperty("Data", out _),
                RequireArrayRoot = true,
                AllowSingleObject = true,
                ErrorAction = ReaderErrorAction.Throw,
                Progress = new Progress<ReaderProgress>(p => progressEvents.Add(p))
            };

            var item = await Read.Json<LargeDoc>(ms, options).SingleAsync();

            Assert.Equal(123, item.Id);
            Assert.Equal(2_500_000, item.Data?.Length);
            Assert.Equal(1, options.Metrics.RawRecordsParsed);
            Assert.Equal(1, options.Metrics.RecordsEmitted);
            Assert.NotNull(options.Metrics.CompletedUtc);

            // Progress: expect at least one event; for large files maybe >=1 initial + final.
            Assert.True(progressEvents.Count >= 1, "Expected at least one progress event.");
            var final = progressEvents.Last();
            Assert.Equal(options.Metrics.RecordsEmitted, final.RecordsRead);
        }

        // ------------------------------------------------------------------
        // 6. Fast path single root (ValidateElements=false) still works
        // ------------------------------------------------------------------
        [Fact]
        public async Task SingleRoot_FastPath_Emit_Works()
        {
            var json = "{ \"Id\": 5, \"Name\": \"Fast\" }";
            var ms = new MemoryStream(Encoding.UTF8.GetBytes(json));

            var opts = new JsonReadOptions<MyDoc>
            {
                ValidateElements = false,  // fast path
                RequireArrayRoot = true,
                AllowSingleObject = true,
                ErrorAction = ReaderErrorAction.Throw
            };

            var doc = await Read.Json<MyDoc>(ms, opts).SingleAsync();

            Assert.Equal(5, doc.Id);
            Assert.Equal("Fast", doc.Name);
            Assert.Equal(1, opts.Metrics.RawRecordsParsed);
            Assert.Equal(1, opts.Metrics.RecordsEmitted);
        }

       

        // ------------------------------------------------------------------
        // 8. ErrorAction=Stop when validator returns false
        // ------------------------------------------------------------------
        [Fact]
        public async Task SingleRoot_Validation_ErrorAction_Stop_Sets_TerminatedEarly()
        {
            var sink = new TestErrorSink();
            var json = "{ \"Id\": 10, \"Flag\": \"Y\" }";
            var ms = new MemoryStream(Encoding.UTF8.GetBytes(json));

            var opts = new JsonReadOptions<ValidatorFailDoc>
            {
                ValidateElements = true,
                ElementValidator = e => false,
                RequireArrayRoot = true,
                AllowSingleObject = true,
                ErrorAction = ReaderErrorAction.Stop,
                ErrorSink = sink
            };

            var list = new List<ValidatorFailDoc>();
            await foreach (var d in Read.Json<ValidatorFailDoc>(ms, opts))
                list.Add(d);

            Assert.Empty(list);
            Assert.Equal(1, opts.Metrics.RawRecordsParsed);
            Assert.Equal(0, opts.Metrics.RecordsEmitted);
            Assert.True(opts.Metrics.TerminatedEarly);
            Assert.Null(opts.Metrics.CompletedUtc);
            Assert.Single(sink.Errors);
            Assert.Equal("JsonValidationFailed", sink.Errors[0].ErrorType);
        }

        // ------------------------------------------------------------------
        // 9. ErrorAction=Throw on deserialization error (fast path) sets TerminatedEarly (assuming HandleError)
        // ------------------------------------------------------------------
        [Fact]
        public async Task SingleRoot_FastPath_DeserializationError_Throws_And_Terminated()
        {
            var json = "{ \"Id\": \"not-an-int\", \"Name\": \"Bad\" }";
            var ms = new MemoryStream(Encoding.UTF8.GetBytes(json));


            var opts = new JsonReadOptions<MyDoc>
            {
                ValidateElements = false,
                RequireArrayRoot = true,
                AllowSingleObject = true,
                ErrorAction = ReaderErrorAction.Throw
            };

            var ex = await Assert.ThrowsAsync<InvalidDataException>(async () =>
            {
                await foreach (var _ in Read.Json<MyDoc>(ms, opts)) { }
            });

            Assert.Contains("could not be converted", ex.Message, StringComparison.OrdinalIgnoreCase); // generic message
            Assert.True(opts.Metrics.TerminatedEarly);           // assuming HandleError sets it on Throw
            Assert.Null(opts.Metrics.CompletedUtc);
            Assert.Equal(0, opts.Metrics.RawRecordsParsed);      // value not fully processed
            Assert.Equal(0, opts.Metrics.RecordsEmitted);
        }

        // ------------------------------------------------------------------
        // Helpers
        // ------------------------------------------------------------------

        // Simple error sink capturing errors.
        private sealed class TestErrorSink : IReaderErrorSink
        {
            public List<ReaderError> Errors { get; } = new();
            public void Report(ReaderError error) => Errors.Add(error);
            public void Dispose() { }
        }

        // Wraps a byte[] in a non-seekable stream
        private sealed class NonSeekableStream : Stream
        {
            private readonly MemoryStream _inner;
            public NonSeekableStream(byte[] data) => _inner = new MemoryStream(data, writable: false);

            public override bool CanRead => true;
            public override bool CanSeek => false;
            public override bool CanWrite => false;
            public override long Length => throw new NotSupportedException();
            public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

            public override void Flush() => throw new NotSupportedException();

            public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);

#if NET6_0_OR_GREATER
            public override int Read(Span<byte> destination) => _inner.Read(destination);
            public override ValueTask<int> ReadAsync(Memory<byte> destination, CancellationToken cancellationToken = default)
                => _inner.ReadAsync(destination, cancellationToken);
#endif
            public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
                => _inner.ReadAsync(buffer, offset, count, cancellationToken);

            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            public override void SetLength(long value) => throw new NotSupportedException();
            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

            protected override void Dispose(bool disposing)
            {
                if (disposing) _inner.Dispose();
                base.Dispose(disposing);
            }
        }
    }

    // Small LINQ convenience for IAsyncEnumerable SingleAsync (in case not already available)
    internal static class AsyncEnumerableExt
    {
        public static async Task<T> SingleAsync<T>(this IAsyncEnumerable<T> source, CancellationToken ct = default)
        {
            await using var e = source.GetAsyncEnumerator(ct);
            if (!await e.MoveNextAsync())
                throw new InvalidOperationException("Sequence was empty.");
            var first = e.Current;
            if (await e.MoveNextAsync())
                throw new InvalidOperationException("Sequence contained more than one element.");
            return first;
        }
    }
}
