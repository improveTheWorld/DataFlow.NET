using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace DataFlow.Data.Tests.Csv
{
    public sealed class CsvLinesReadTests
    {
        public enum ParseMode
        {
            Sync,
            Async
        }

        // Shared scenarios (each will run in both sync + async modes)
        public static IEnumerable<object[]> NewlineCountingScenarios()
        {
            var cases = new[]
            {
                new {
                    Name = "EmbeddedQuotedNewlines",
                    Rows = 3,
                    Lines = 6,
                    Content =
                        "\"Id\",\"Text\"\n" +
                        "1,\"Hello\nWorld\"\n" +
                        "2,\"LineA\nLineB\nLineC\"\n" +
                        "3,\"NoNewlineEnd\""
                },
                new {
                    Name = "CRLFCountsAsSingle",
                    Rows = 2,
                    Lines = 4,
                    Content =
                        "\"Id\",\"Text\"\r\n" +
                        "1,\"Alpha\"\r\n" +
                        "2,\"Line1\r\nLine2\"\r\n"
                },
                new {
                    Name = "NoTrailingNewline",
                    Rows = 1,
                    Lines = 1,
                    Content =
                        "Id,Text\n" +
                        "1,HelloWorld"
                },
                new {
                    Name = "MixedLineEndings",
                    Rows = 4,
                    Lines = 5,
                    Content =
                        "Id,Text\r\n" +    // CRLF (1)
                        "1,A\n" +          // LF   (2)
                        "2,B\r" +          // CR   (3)
                        "3,\"X\nY\"\r\n" + // embedded LF (4) + CRLF terminator (5)
                        "4,Z"
                },
                new {
                    Name = "MultipleEmbeddedNewlines",
                    Rows = 3,
                    Lines = 6,
                    Content =
                        "Id,Text\n" +          // header terminator (1)
                        "1,\"A\nB\nC\"\n" +    // embedded LF (2), embedded LF (3), terminator (4)
                        "2,\"D\nE\"\n" +       // embedded LF (5), terminator (6)
                        "3,\"Tail\""           // no terminating newline
                },
               new {
                Name = "ConsecutiveCRLFNoBlankRecords",
                Rows = 2,
                Lines = 3,
                Content =
                    "\"Id\",\"Val\"\r\n" +
                    "1,\"A\"\r\n" +
                    "2,\"B\"\r\n"
            }
            };

            foreach (var c in cases)
            {
                yield return new object[] { ParseMode.Sync, c.Name, c.Rows, (long)c.Lines, c.Content };
                yield return new object[] { ParseMode.Async, c.Name, c.Rows, (long)c.Lines, c.Content };
            }
        }

        [Theory(DisplayName = "LinesRead newline counting scenarios (sync + async)")]
        [MemberData(nameof(NewlineCountingScenarios))]
        public async Task LinesRead_Scenarios(ParseMode mode, string scenarioName, int expectedRows, long expectedLinesRead, string content)
        {
            string path = Path.GetTempFileName();
            try
            {
                await File.WriteAllTextAsync(path, content, Encoding.UTF8);

                var opts = new CsvReadOptions
                {
                    HasHeader = true
                };

                int rows = await CountRows(path, opts, mode);

                Assert.Equal(expectedRows, rows);                 // data rows only
                Assert.Equal(expectedLinesRead, opts.Metrics.LinesRead); // physical newline count
            }
            finally
            {
                TryDelete(path);
            }
        }
        [Theory(DisplayName = "Cross-buffer CRLF should count as single line (sync + async)")]
        [InlineData(ParseMode.Sync)]
        [InlineData(ParseMode.Async)]
        public async Task CrossBufferCRLFShouldCountAsSingleLine(ParseMode mode)
        {
            // We deliberately split between CR and LF so the parser receives them in different read operations.
            var sb = new StringBuilder();
            sb.Append("Id,Text\r");     // CR of CRLF in first buffer
            sb.Append("\n1,A\r\n");     // LF of header CRLF starts second buffer + first data line with CRLF
            sb.Append("2,B");           // final line without newline

            string content = sb.ToString();
            int splitIndex = "Id,Text\r".Length;

            var opts = new CsvReadOptions { HasHeader = true };
            int rowCount = 0;
            bool headerSkipped = false;

            if (mode == ParseMode.Sync)
            {
                using var reader = new SplitTextReader(content, splitIndex);
                foreach (var _ in CsvRfc4180Parser.Parse(reader, opts))
                {
                    if (opts.HasHeader && !headerSkipped)
                    {
                        headerSkipped = true;
                        continue;
                    }
                    rowCount++;
                }
            }
            else
            {
                using var reader = new SplitTextReader(content, splitIndex);
                await foreach (var _ in CsvRfc4180Parser.ParseAsync(reader, opts))
                {
                    if (opts.HasHeader && !headerSkipped)
                    {
                        headerSkipped = true;
                        continue;
                    }
                    rowCount++;
                }
            }

            Assert.Equal(2, rowCount);              // rows: 1,A and 2,B
            Assert.Equal(2, opts.Metrics.LinesRead); // header CRLF + first record CRLF
        }
        private static async Task<int> CountRows(string path, CsvReadOptions opts, ParseMode mode)
        {
            int count = 0;
            bool headerSkipped = false;

            if (mode == ParseMode.Sync)
            {
                using (var sr = new StreamReader(path, Encoding.UTF8))
                {
                    foreach (var _ in CsvRfc4180Parser.Parse(sr, opts))
                    {
                        if (opts.HasHeader && !headerSkipped)
                        {
                            headerSkipped = true;
                            continue;
                        }
                        count++;
                    }
                }
            }
            else // Async
            {
                using (var sr = new StreamReader(path, Encoding.UTF8))
                {
                    await foreach (var _ in CsvRfc4180Parser.ParseAsync(sr, opts))
                    {
                        if (opts.HasHeader && !headerSkipped)
                        {
                            headerSkipped = true;
                            continue;
                        }
                        count++;
                    }
                }
            }

            return count;
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path)) File.Delete(path);
            }
            catch
            {
                // ignore
            }
        }

        private sealed class SplitTextReader : TextReader
        {
            private readonly string _first;
            private readonly string _second;
            private int _stage;

            public SplitTextReader(string full, int splitIndex)
            {
                _first = full.Substring(0, splitIndex);
                _second = full.Substring(splitIndex);
                _stage = 0;
            }

            public override int Read(char[] buffer, int index, int count)
            {
                if (_stage == 0)
                {
                    _stage = 1;
                    _first.CopyTo(0, buffer, index, _first.Length);
                    return _first.Length;
                }
                if (_stage == 1)
                {
                    _stage = 2;
                    _second.CopyTo(0, buffer, index, _second.Length);
                    return _second.Length;
                }
                return 0;
            }
        }
    }
}