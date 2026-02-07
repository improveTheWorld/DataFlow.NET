using DataFlow;
using DataFlow.Framework;
using System.Text;
using System.Text.Json;

// ============================================================
// DataFlow.Net EXTREME TESTING - Round 3
// YAML Security, JSON Guard Rails, Writing APIs, Boundary Conditions
// ============================================================

Console.WriteLine("=== DataFlow.Net EXTREME TESTS (Round 3) ===\n");
var findings = new List<string>();

// ================================================================
// SECTION A: YAML SECURITY HARDENING
// ================================================================
Console.WriteLine("=== SECTION A: YAML SECURITY HARDENING ===\n");

// TEST A1: DisallowAliases
Console.WriteLine("--- Test A1: DisallowAliases ---");
try
{
    var testYaml = "test_alias.yaml";
    await File.WriteAllTextAsync(testYaml, @"
defaults: &defaults
  name: default
  value: 100
item1:
  <<: *defaults
  id: 1
");
    
    var options = new YamlReadOptions<dynamic>
    {
        DisallowAliases = true,
        ErrorAction = ReaderErrorAction.Skip,
    };
    
    int count = 0;
    await foreach (var item in Read.Yaml<dynamic>(testYaml, options))
    {
        count++;
    }
    
    Console.WriteLine($"  Count: {count}, Errors: {options.Metrics?.ErrorCount}");
    if ((options.Metrics?.ErrorCount ?? 0) > 0 || count == 0)
        Console.WriteLine($"  ✓ DisallowAliases correctly blocked aliases");
    else
        findings.Add("DisallowAliases: Aliases were not blocked");
    
    File.Delete(testYaml);
}
catch (Exception ex)
{
    Console.WriteLine($"  DisallowAliases threw: {ex.Message}");
}

// TEST A2: DisallowCustomTags
Console.WriteLine("\n--- Test A2: DisallowCustomTags ---");
try
{
    var testYaml = "test_tags.yaml";
    await File.WriteAllTextAsync(testYaml, @"!custom_tag
name: test
value: 100
");
    
    var options = new YamlReadOptions<dynamic>
    {
        DisallowCustomTags = true,
        ErrorAction = ReaderErrorAction.Skip,
    };
    
    int count = 0;
    await foreach (var item in Read.Yaml<dynamic>(testYaml, options))
    {
        count++;
    }
    
    Console.WriteLine($"  Count: {count}, Errors: {options.Metrics?.ErrorCount}");
    if ((options.Metrics?.ErrorCount ?? 0) > 0)
        Console.WriteLine($"  ✓ DisallowCustomTags correctly blocked custom tags");
    else
        Console.WriteLine($"  ⚠ Custom tags may have been processed without error");
    
    File.Delete(testYaml);
}
catch (Exception ex)
{
    Console.WriteLine($"  DisallowCustomTags blocked: {ex.Message}");
}

// TEST A3: MaxDepth for YAML
Console.WriteLine("\n--- Test A3: MaxDepth for YAML ---");
try
{
    var testYaml = "test_deep_yaml.yaml";
    var sb = new StringBuilder();
    for (int i = 0; i < 20; i++)
        sb.Append($"level{i}:\n" + new string(' ', (i + 1) * 2));
    sb.Append("value: deep");
    await File.WriteAllTextAsync(testYaml, sb.ToString());
    
    var options = new YamlReadOptions<dynamic>
    {
        MaxDepth = 10,
        ErrorAction = ReaderErrorAction.Skip,
    };
    
    int count = 0;
    await foreach (var item in Read.Yaml<dynamic>(testYaml, options))
    {
        count++;
    }
    
    Console.WriteLine($"  Deep YAML (20 levels, limit 10): count={count}, errors={options.Metrics?.ErrorCount}");
    if ((options.Metrics?.ErrorCount ?? 0) > 0)
        Console.WriteLine($"  ✓ MaxDepth correctly triggered");
    else
        findings.Add("YAML MaxDepth: Did not trigger for deep nesting");
    
    File.Delete(testYaml);
}
catch (Exception ex)
{
    Console.WriteLine($"  MaxDepth error: {ex.Message}");
}

// TEST A4: MaxTotalDocuments
Console.WriteLine("\n--- Test A4: MaxTotalDocuments ---");
try
{
    var testYaml = "test_multi_doc.yaml";
    var sb = new StringBuilder();
    for (int i = 0; i < 20; i++)
        sb.AppendLine($"---\nname: doc{i}\nvalue: {i}");
    await File.WriteAllTextAsync(testYaml, sb.ToString());
    
    var options = new YamlReadOptions<dynamic>
    {
        MaxTotalDocuments = 5,
        ErrorAction = ReaderErrorAction.Stop,
    };
    
    int count = 0;
    await foreach (var item in Read.Yaml<dynamic>(testYaml, options))
    {
        count++;
    }
    
    Console.WriteLine($"  20 YAML docs, limit 5: count={count}, terminated={options.Metrics?.TerminatedEarly}");
    if (count <= 5)
        Console.WriteLine($"  ✓ MaxTotalDocuments correctly limited");
    else
        findings.Add($"MaxTotalDocuments: Expected <=5, got {count}");
    
    File.Delete(testYaml);
}
catch (Exception ex)
{
    Console.WriteLine($"  MaxTotalDocuments error: {ex.Message}");
}

// TEST A5: MaxNodeScalarLength
Console.WriteLine("\n--- Test A5: MaxNodeScalarLength ---");
try
{
    var testYaml = "test_long_scalar.yaml";
    var longValue = new string('X', 5000);
    await File.WriteAllTextAsync(testYaml, $"name: {longValue}");
    
    var options = new YamlReadOptions<dynamic>
    {
        MaxNodeScalarLength = 100,
        ErrorAction = ReaderErrorAction.Skip,
    };
    
    int count = 0;
    await foreach (var item in Read.Yaml<dynamic>(testYaml, options))
    {
        count++;
    }
    
    Console.WriteLine($"  5000 char scalar, limit 100: count={count}, errors={options.Metrics?.ErrorCount}");
    if ((options.Metrics?.ErrorCount ?? 0) > 0 || count == 0)
        Console.WriteLine($"  ✓ MaxNodeScalarLength correctly blocked");
    else
        findings.Add("MaxNodeScalarLength: Did not block long scalar");
    
    File.Delete(testYaml);
}
catch (Exception ex)
{
    Console.WriteLine($"  MaxNodeScalarLength error: {ex.Message}");
}

// ================================================================
// SECTION B: JSON GUARD RAILS
// ================================================================
Console.WriteLine("\n\n=== SECTION B: JSON GUARD RAILS ===\n");

// TEST B1: MaxElements
Console.WriteLine("--- Test B1: MaxElements Guard Rail ---");
try
{
    var testJson = "test_max_elements.json";
    var sb = new StringBuilder("[");
    for (int i = 0; i < 100; i++)
        sb.Append($"{{\"id\":{i}}},");
    sb.Length--; // Remove trailing comma
    sb.Append("]");
    await File.WriteAllTextAsync(testJson, sb.ToString());
    
    var options = new JsonReadOptions<dynamic>
    {
        MaxElements = 10,
        ErrorAction = ReaderErrorAction.Stop,
    };
    
    int count = 0;
    await foreach (var item in Read.Json<dynamic>(testJson, options))
    {
        count++;
    }
    
    Console.WriteLine($"  100 elements, limit 10: count={count}, terminated={options.Metrics?.TerminatedEarly}");
    if (count <= 10)
        Console.WriteLine($"  ✓ MaxElements correctly limited");
    else
        findings.Add($"MaxElements: Expected <=10, got {count}");
    
    File.Delete(testJson);
}
catch (Exception ex)
{
    Console.WriteLine($"  MaxElements error: {ex.Message}");
    findings.Add($"MaxElements: {ex.Message}");
}

// TEST B2: MaxStringLength
Console.WriteLine("\n--- Test B2: MaxStringLength Guard Rail ---");
try
{
    var testJson = "test_max_string.json";
    var longString = new string('X', 5000);
    await File.WriteAllTextAsync(testJson, $"[{{\"value\":\"{longString}\"}}]");
    
    var options = new JsonReadOptions<dynamic>
    {
        MaxStringLength = 100,
        ErrorAction = ReaderErrorAction.Skip,
    };
    
    int count = 0;
    await foreach (var item in Read.Json<dynamic>(testJson, options))
    {
        count++;
    }
    
    Console.WriteLine($"  5000 char string, limit 100: count={count}, errors={options.Metrics?.ErrorCount}");
    if ((options.Metrics?.ErrorCount ?? 0) > 0 || count == 0)
        Console.WriteLine($"  ✓ MaxStringLength correctly blocked");
    else
        findings.Add("MaxStringLength: Did not block long string");
    
    File.Delete(testJson);
}
catch (Exception ex)
{
    Console.WriteLine($"  MaxStringLength error: {ex.Message}");
}

// ================================================================
// SECTION C: WRITING INFRASTRUCTURE
// ================================================================
Console.WriteLine("\n\n=== SECTION C: WRITING INFRASTRUCTURE ===\n");

// TEST C1: WriteCsv with separator option
Console.WriteLine("--- Test C1: WriteCsv with Custom Separator ---");
try
{
    var records = new[] {
        new { Id = 1, Name = "Test1", Value = 100 },
        new { Id = 2, Name = "Test2", Value = 200 },
    };
    
    var outFile = "test_write_sep.csv";
    var options = new CsvWriteOptions { Separator = ";" };
    await records.Async().WriteCsv(outFile, options);
    
    var content = await File.ReadAllTextAsync(outFile);
    Console.WriteLine($"  Written: {content.Substring(0, Math.Min(100, content.Length))}...");
    
    if (content.Contains(";"))
        Console.WriteLine($"  ✓ Custom separator ';' used correctly");
    else
        findings.Add("WriteCsv separator: Custom separator not applied");
    
    Console.WriteLine($"  WriterMetrics: RecordsWritten={options.Metrics?.RecordsWritten}");
    File.Delete(outFile);
}
catch (Exception ex)
{
    Console.WriteLine($"  WriteCsv error: {ex.Message}");
    findings.Add($"WriteCsv: {ex.Message}");
}

// TEST C2: WriteCsv WriteHeader=false
Console.WriteLine("\n--- Test C2: WriteCsv without Header ---");
try
{
    var records = new[] {
        new { Id = 1, Name = "NoHeader" },
    };
    
    var outFile = "test_no_header.csv";
    var options = new CsvWriteOptions { WriteHeader = false };
    await records.Async().WriteCsv(outFile, options);
    
    var lines = await File.ReadAllLinesAsync(outFile);
    Console.WriteLine($"  Lines: {lines.Length}, First: {lines[0]}");
    
    if (lines.Length == 1 && !lines[0].Contains("Id"))
        Console.WriteLine($"  ✓ Header correctly omitted");
    else
        findings.Add("WriteCsv WriteHeader=false: Header was included");
    
    File.Delete(outFile);
}
catch (Exception ex)
{
    Console.WriteLine($"  WriteCsv no header error: {ex.Message}");
    findings.Add($"WriteCsv WriteHeader: {ex.Message}");
}

// TEST C3: WriteCsv with special characters (RFC 4180 quoting on write)
Console.WriteLine("\n--- Test C3: WriteCsv RFC 4180 Quoting ---");
try
{
    var records = new[] {
        new { Id = 1, Name = "Hello, World", Quote = "Say \"Hi\"" },
    };
    
    var outFile = "test_quote_write.csv";
    await records.Async().WriteCsv(outFile);
    
    var content = await File.ReadAllTextAsync(outFile);
    Console.WriteLine($"  Written: {content}");
    
    if (content.Contains("\"Hello, World\"") && content.Contains("\"\""))
        Console.WriteLine($"  ✓ RFC 4180 quoting correctly applied on write");
    else
        findings.Add("WriteCsv quoting: Fields not properly quoted");
    
    File.Delete(outFile);
}
catch (Exception ex)
{
    Console.WriteLine($"  WriteCsv quoting error: {ex.Message}");
    findings.Add($"WriteCsv quoting: {ex.Message}");
}

// TEST C4: WriteJson - standard array format
Console.WriteLine("\n--- Test C4: WriteJson Standard Format ---");
try
{
    var items = new[] {
        new { Id = 1, Name = "Item1" },
        new { Id = 2, Name = "Item2" },
    };
    
    var outFile = "test_write.json";
    await items.Async().WriteJson(outFile);
    
    var content = await File.ReadAllTextAsync(outFile);
    Console.WriteLine($"  JSON length: {content.Length}");
    
    if (content.StartsWith("[") && content.EndsWith("]"))
        Console.WriteLine($"  ✓ JSON array format correct");
    else
        findings.Add("WriteJson: Not array format");
    
    File.Delete(outFile);
}
catch (Exception ex)
{
    Console.WriteLine($"  WriteJson error: {ex.Message}");
    findings.Add($"WriteJson: {ex.Message}");
}

// TEST C5: WriteJson with JsonLinesFormat
// NOTE: The simple path overload WriteJson(path) does NOT accept JsonWriteOptions!
// Only CancellationToken is supported. This is a documentation discrepancy.
Console.WriteLine("\n--- Test C5: JsonLinesFormat (NDJSON) ---");
Console.WriteLine("  ⚠ SKIPPED: WriteJson path overload doesn't accept JsonWriteOptions (only CancellationToken)");
findings.Add("WriteJson API: Path overload doesn't accept JsonWriteOptions, only CancellationToken - docs show options");

// TEST C6: Append mode
Console.WriteLine("\n--- Test C6: Append Mode ---");
try
{
    var outFile = "test_append.csv";
    
    // First write
    var records1 = new[] { new { Id = 1 } };
    await records1.Async().WriteCsv(outFile);
    
    // Append
    var records2 = new[] { new { Id = 2 } };
    var appendOptions = new CsvWriteOptions { Append = true, WriteHeader = false };
    await records2.Async().WriteCsv(outFile, appendOptions);
    
    var lines = await File.ReadAllLinesAsync(outFile);
    Console.WriteLine($"  After append: {lines.Length} lines");
    
    if (lines.Length == 3) // header + 2 data rows
        Console.WriteLine($"  ✓ Append mode works correctly");
    else
        findings.Add($"Append mode: Expected 3 lines, got {lines.Length}");
    
    File.Delete(outFile);
}
catch (Exception ex)
{
    Console.WriteLine($"  Append mode error: {ex.Message}");
    findings.Add($"Append mode: {ex.Message}");
}

// TEST C7: WriteYaml multi-document
Console.WriteLine("\n--- Test C7: WriteYaml ---");
try
{
    var items = new[] {
        new { Name = "Doc1", Value = 1 },
        new { Name = "Doc2", Value = 2 },
    };
    
    var outFile = "test_write.yaml";
    await items.Async().WriteYaml(outFile);
    
    var content = await File.ReadAllTextAsync(outFile);
    Console.WriteLine($"  YAML length: {content.Length}");
    Console.WriteLine($"  ✓ WriteYaml completed");
    
    File.Delete(outFile);
}
catch (Exception ex)
{
    Console.WriteLine($"  WriteYaml error: {ex.Message}");
    findings.Add($"WriteYaml: {ex.Message}");
}

// ================================================================
// SECTION D: EXTREME BOUNDARY CONDITIONS
// ================================================================
Console.WriteLine("\n\n=== SECTION D: EXTREME BOUNDARY CONDITIONS ===\n");

// TEST D1: Zero-length data
Console.WriteLine("--- Test D1: Empty Data Handling ---");
try
{
    var empty = Array.Empty<object>().Async();
    var outFile = "test_empty.csv";
    
    await empty.WriteCsv(outFile);
    var content = await File.ReadAllTextAsync(outFile);
    Console.WriteLine($"  Empty CSV content: '{content}' (len={content.Length})");
    
    File.Delete(outFile);
}
catch (Exception ex)
{
    Console.WriteLine($"  Empty data error: {ex.Message}");
    findings.Add($"Empty data: {ex.Message}");
}

// TEST D2: Very long field names (schema stress)
Console.WriteLine("\n--- Test D2: Long Field Names ---");
try
{
    var testCsv = "test_long_field.csv";
    var longName = new string('X', 1000);
    await File.WriteAllTextAsync(testCsv, $"{longName},Normal\n1,2\n");
    
    var options = new CsvReadOptions 
    { 
        InferSchema = true,
        SchemaInferenceMode = SchemaInferenceMode.ColumnNamesOnly,
    };
    
    int count = 0;
    await foreach (var row in Read.Csv<dynamic>(testCsv, options))
    {
        count++;
    }
    
    Console.WriteLine($"  Long field name: processed {count} rows");
    Console.WriteLine($"  Schema[0] length: {options.Schema?[0]?.Length ?? 0}");
    
    if ((options.Schema?[0]?.Length ?? 0) == 1000)
        Console.WriteLine($"  ✓ Long field names preserved");
    else
        findings.Add("Long field names: Truncated or failed");
    
    File.Delete(testCsv);
}
catch (Exception ex)
{
    Console.WriteLine($"  Long field names error: {ex.Message}");
    findings.Add($"Long field names: {ex.Message}");
}

// TEST D3: Binary/control characters in data
Console.WriteLine("\n--- Test D3: Control Characters ---");
try
{
    var testCsv = "test_control.csv";
    await File.WriteAllTextAsync(testCsv, "Id,Data\n1,\"Hello\x00World\"\n2,\"Tab\tHere\"\n3,\"Bell\x07Char\"\n");
    
    int count = 0;
    await foreach (var row in Read.Csv<dynamic>(testCsv))
    {
        count++;
    }
    
    Console.WriteLine($"  Control chars: processed {count} rows");
    if (count == 3)
        Console.WriteLine($"  ✓ Control characters handled");
    else
        findings.Add($"Control characters: Expected 3 rows, got {count}");
    
    File.Delete(testCsv);
}
catch (Exception ex)
{
    Console.WriteLine($"  Control characters error: {ex.Message}");
    findings.Add($"Control characters: {ex.Message}");
}

// TEST D4: CRLF vs LF line endings
Console.WriteLine("\n--- Test D4: Line Ending Handling ---");
try
{
    // LF only
    var lfFile = "test_lf.csv";
    await File.WriteAllTextAsync(lfFile, "Id,Name\n1,LF\n2,Only\n");
    
    int lfCount = 0;
    await foreach (var _ in Read.Csv<dynamic>(lfFile)) lfCount++;
    
    // CRLF
    var crlfFile = "test_crlf.csv";
    await File.WriteAllTextAsync(crlfFile, "Id,Name\r\n1,CRLF\r\n2,Windows\r\n");
    
    int crlfCount = 0;
    await foreach (var _ in Read.Csv<dynamic>(crlfFile)) crlfCount++;
    
    Console.WriteLine($"  LF: {lfCount} rows, CRLF: {crlfCount} rows");
    if (lfCount == 2 && crlfCount == 2)
        Console.WriteLine($"  ✓ Both line ending styles handled correctly");
    else
        findings.Add($"Line endings: LF={lfCount}, CRLF={crlfCount}");
    
    File.Delete(lfFile);
    File.Delete(crlfFile);
}
catch (Exception ex)
{
    Console.WriteLine($"  Line endings error: {ex.Message}");
    findings.Add($"Line endings: {ex.Message}");
}

// TEST D5: Very wide row (100+ columns dynamically)
Console.WriteLine("\n--- Test D5: Dynamic Wide Row ---");
try
{
    var testCsv = "test_wide2.csv";
    var headers = string.Join(",", Enumerable.Range(1, 200).Select(i => $"Col{i}"));
    var values = string.Join(",", Enumerable.Range(1, 200).Select(i => $"Val{i}"));
    await File.WriteAllTextAsync(testCsv, $"{headers}\n{values}\n");
    
    var options = new CsvReadOptions { InferSchema = true };
    
    int count = 0;
    await foreach (var row in Read.Csv<dynamic>(testCsv, options))
    {
        count++;
    }
    
    Console.WriteLine($"  200 columns: {count} rows, schema={options.Schema?.Length} columns");
    if (options.Schema?.Length == 200)
        Console.WriteLine($"  ✓ Wide schema (200 cols) handled");
    else
        findings.Add($"Wide schema: Expected 200 cols, got {options.Schema?.Length}");
    
    File.Delete(testCsv);
}
catch (Exception ex)
{
    Console.WriteLine($"  Wide schema error: {ex.Message}");
    findings.Add($"Wide schema: {ex.Message}");
}

// TEST D6: SkipWhile operator
Console.WriteLine("\n--- Test D6: SkipWhile Operator ---");
try
{
    var data = Enumerable.Range(1, 10).Async();
    
    var results = new List<int>();
    await foreach (var item in data.SkipWhile(x => x < 5))
    {
        results.Add(item);
    }
    
    Console.WriteLine($"  SkipWhile(x < 5): [{string.Join(", ", results)}]");
    if (results.Count == 6 && results[0] == 5)
        Console.WriteLine($"  ✓ SkipWhile works correctly");
    else
        findings.Add($"SkipWhile: Expected 6 items starting at 5, got {results.Count} starting at {results.FirstOrDefault()}");
}
catch (Exception ex)
{
    Console.WriteLine($"  SkipWhile not available: {ex.Message}");
    findings.Add($"SkipWhile: {ex.Message}");
}

// TEST D7: Aggregate operator
Console.WriteLine("\n--- Test D7: Aggregate Operator ---");
try
{
    var data = Enumerable.Range(1, 5).Async();
    
    var sum = await data.Aggregate((a, b) => a + b);
    Console.WriteLine($"  Aggregate sum 1-5: {sum}");
    
    if (sum == 15)
        Console.WriteLine($"  ✓ Aggregate works correctly");
    else
        findings.Add($"Aggregate: Expected 15, got {sum}");
}
catch (Exception ex)
{
    Console.WriteLine($"  Aggregate error: {ex.Message}");
    findings.Add($"Aggregate: {ex.Message}");
}

// TEST D8: First/FirstOrDefault
Console.WriteLine("\n--- Test D8: First/FirstOrDefault Operators ---");
try
{
    var data = new[] { 10, 20, 30 }.Async();
    var first = await data.First();
    Console.WriteLine($"  First: {first}");
    
    var empty = Array.Empty<int>().Async();
    var firstOrDefault = await empty.FirstOrDefault();
    Console.WriteLine($"  FirstOrDefault on empty: {firstOrDefault}");
    
    if (first == 10 && firstOrDefault == 0)
        Console.WriteLine($"  ✓ First/FirstOrDefault work correctly");
    else
        findings.Add($"First: Got {first}, {firstOrDefault}");
}
catch (Exception ex)
{
    Console.WriteLine($"  First/FirstOrDefault error: {ex.Message}");
    findings.Add($"First/FirstOrDefault: {ex.Message}");
}

// TEST D9: Any with predicate
Console.WriteLine("\n--- Test D9: Any Operator with Predicate ---");
try
{
    var data = new[] { 1, 2, 3, 4, 5 }.Async();
    
    var hasEven = await data.Any(x => x % 2 == 0);
    Console.WriteLine($"  Any(even): {hasEven}");
    
    var hasLarge = await new[] { 1, 2, 3 }.Async().Any(x => x > 100);
    Console.WriteLine($"  Any(>100): {hasLarge}");
    
    if (hasEven && !hasLarge)
        Console.WriteLine($"  ✓ Any with predicate works correctly");
    else
        findings.Add($"Any: hasEven={hasEven}, hasLarge={hasLarge}");
}
catch (Exception ex)
{
    Console.WriteLine($"  Any error: {ex.Message}");
    findings.Add($"Any: {ex.Message}");
}

// Summary
Console.WriteLine("\n\n=== EXTREME TEST SUMMARY ===");
if (findings.Count == 0)
{
    Console.WriteLine("All extreme tests passed! 🎉");
}
else
{
    Console.WriteLine($"Found {findings.Count} issues:");
    foreach (var finding in findings)
    {
        Console.WriteLine($"  • {finding}");
    }
}

Console.WriteLine("\nExtreme testing complete.");
