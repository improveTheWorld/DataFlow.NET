# DataFlow Data Layer
 
The DataFlow Data aspect (namespace `DataFlow`) provides the foundation for **reading and writing data** in the DataFlow.NET framework, with full support for both synchronous (`IEnumerable`) and asynchronous (`IAsyncEnumerable`) streaming.

## Table of Contents

1. [Naming Convention](#naming-convention)
2. [Read Class](#read-class)
3. [Writers Class](#writers-class)
4. [Processing Patterns](#processing-patterns)
5. [Error Handling](#error-handling)
6. [Performance Best Practices](#performance-best-practices)

---

## Naming Convention

> **Default method names are ASYNCHRONOUS. Synchronous variants use the `Sync` suffix.**

| Async (default) | Sync |
|-----------------|------|
| `Read.Csv<T>()` | `Read.CsvSync<T>()` |
| `Read.Json<T>()` | `Read.JsonSync<T>()` |
| `Read.Yaml<T>()` | `Read.YamlSync<T>()` |
| `Read.Text()` | `Read.TextSync()` |
| `WriteCsv()` | `WriteCsvSync()` |

---

## Read Class

The `Read` class provides static methods for reading data from files, streams, and strings with built-in lazy evaluation.

### Supported Formats

| Format | Method | Returns |
|--------|--------|---------|
| Text | `Read.Text()` | `IAsyncEnumerable<string>` |
| CSV | `Read.Csv<T>()` | `IAsyncEnumerable<T>` |
| JSON | `Read.Json<T>()` | `IAsyncEnumerable<T>` |
| YAML | `Read.Yaml<T>()` | `IAsyncEnumerable<T>` |

### Text Reading

```csharp
// Async (default)
await foreach (var line in Read.Text("data.txt"))
{
    Console.WriteLine(line);
}

// Sync
foreach (var line in Read.TextSync("data.txt"))
{
    Console.WriteLine(line);
}

// From stream
await foreach (var line in Read.Text(myStream))
{
    // Process line
}
```

### CSV Reading

#### Simple API

```csharp
public record Employee(string FirstName, string LastName, int Age, decimal Salary);

// Async with default comma separator
await foreach (var emp in Read.Csv<Employee>("employees.csv"))
{
    Console.WriteLine(emp.FirstName);
}

// Sync with custom separator
foreach (var emp in Read.CsvSync<Employee>("data.csv", separator: ";"))
{
    Console.WriteLine(emp.LastName);
}
```

#### Options-Based API

```csharp
var options = new CsvReadOptions
{
    HasHeader = true,
    Separator = ',',
    ErrorAction = ReaderErrorAction.Skip,
    ErrorSink = new JsonLinesFileErrorSink("errors.ndjson"),
    Progress = new Progress<ReaderProgress>(p => Console.WriteLine($"Read {p.RecordsRead} rows"))
};

await foreach (var record in Read.Csv<MyRecord>("data.csv", options))
{
    // Process record
}

// Check metrics after
Console.WriteLine($"Total: {options.Metrics.RecordsEmitted}, Errors: {options.Metrics.ErrorCount}");
```

> [!NOTE]
> For full CSV configuration (schema inference, type inference, guard rails, quoting modes), see [DataFlow-Data-Reading-Infrastructure.md](DataFlow-Data-Reading-Infrastructure.md).

### JSON Reading

```csharp
// Simple API
await foreach (var item in Read.Json<MyType>("data.json"))
{
    Console.WriteLine(item);
}

// Options-based
var jsonOptions = new JsonReadOptions<MyType>
{
    RequireArrayRoot = true,
    AllowSingleObject = true,
    ValidateElements = true,
    ElementValidator = e => e.TryGetProperty("id", out _),
    ErrorAction = ReaderErrorAction.Skip
};

await foreach (var item in Read.Json<MyType>("data.json", jsonOptions))
{
    // Process validated item
}
```

### YAML Reading

```csharp
// Simple API
await foreach (var doc in Read.Yaml<MyConfig>("config.yaml"))
{
    Console.WriteLine(doc);
}

// Options-based with security restrictions
var yamlOptions = new YamlReadOptions<MyConfig>
{
    RestrictTypes = true,
    AllowedTypes = new HashSet<Type> { typeof(MyConfig) },
    DisallowAliases = true,
    MaxDepth = 64
};

await foreach (var doc in Read.Yaml<MyConfig>("config.yaml", yamlOptions))
{
    // Process document
}
```

### String-Based Reading

Parse data directly from in-memory strings:

```csharp
// CSV from string
string csvText = "Name,Age\nJohn,30\nJane,25";
foreach (var record in csvText.AsCsvSync<Person>())
{
    Console.WriteLine(record.Name);
}

// JSON from string
string jsonText = "[{\"id\":1},{\"id\":2}]";
foreach (var item in jsonText.AsJsonSync<MyItem>())
{
    Console.WriteLine(item.Id);
}

// YAML from string
string yamlText = "name: John\nage: 30";
foreach (var doc in yamlText.AsYaml<Person>())
{
    Console.WriteLine(doc.Name);
}
```

---

## Writers Class

Extension methods for writing collections to various output formats.

### Text Writing

```csharp
// Sync
lines.WriteTextSync("output.txt");

// Async from IEnumerable
await lines.WriteText("output.txt");

// Async from IAsyncEnumerable
await asyncLines.WriteText("output.txt");
```

### CSV Writing

```csharp
public record Product(string Name, decimal Price, int Quantity);

var products = new List<Product>
{
    new("Laptop", 999.99m, 10),
    new("Mouse", 29.99m, 50)
};

// Sync
products.WriteCsvSync("products.csv");

// Async with options
await products.WriteCsv("products.csv", withTitle: true, separator: ",");

// From async stream
await asyncProducts.WriteCsv("products.csv");
```

### JSON Writing

```csharp
// Sync
records.WriteJsonSync("data.json");

// Async
await records.WriteJson("data.json");
await asyncRecords.WriteJson("data.json");
```

### YAML Writing

```csharp
// Sync
configs.WriteYamlSync("config.yaml");

// Async
await configs.WriteYaml("config.yaml");
await asyncConfigs.WriteYaml("config.yaml");

// Batched (multi-document)
await asyncLargeDataset.WriteYamlBatched("output.yaml", batchSize: 1000);
```

---

## Processing Patterns

### Streaming Pattern

Process large files with constant memory usage:

```csharp
await Read.Text("huge-log.txt")
    .Where(line => line.Contains("ERROR"))
    .Select(line => $"{DateTime.Now}: {line}")
    .WriteText("errors.txt");
```

### Transformation Pipeline

Chain operations for complex data processing:

```csharp
await Read.Csv<SalesRecord>("sales.csv")
    .Where(r => r.Date >= DateTime.Today.AddDays(-30))
    .GroupBy(r => r.ProductId)
    .Select(g => new SalesSummary 
    { 
        ProductId = g.Key, 
        TotalSales = g.Sum(r => r.Amount)
    })
    .WriteCsv("monthly_summary.csv");
```

### Format Conversion

```csharp
// CSV to JSON
await Read.Csv<Employee>("employees.csv")
    .WriteJson("employees.json");

// JSON to YAML
await Read.Json<Config>("config.json")
    .WriteYaml("config.yaml");
```

### Integration with Cases Pattern

```csharp
await Read.Csv<LogEntry>("logs.csv")
    .Cases(
        log => log.Level == "ERROR",
        log => log.Level == "WARNING"
    )
    .SelectCase(
        error => $"[E] {error.Message}",
        warning => $"[W] {warning.Message}",
        info => $"[I] {info.Message}"
    )
    .AllCases()
    .WriteText("categorized.log");
```

---

## Error Handling

### Options-Based Error Handling

```csharp
var options = new CsvReadOptions
{
    ErrorAction = ReaderErrorAction.Skip,  // Skip bad rows
    ErrorSink = new JsonLinesFileErrorSink("errors.ndjson")
};

await foreach (var record in Read.Csv<MyRecord>("data.csv", options)) { }

// Check results
if (options.Metrics.ErrorCount > 0)
{
    Console.WriteLine($"Skipped {options.Metrics.ErrorCount} bad rows");
}
```

### Simple Delegate Error Handling

```csharp
await foreach (var record in Read.Csv<MyRecord>(
    "data.csv",
    onError: (rawLine, ex) => Console.WriteLine($"Failed: {ex.Message}")))
{
    // Process valid records
}
```

---

## Performance Best Practices

1. **Use streaming**: Avoid `.ToList()` unless necessary
2. **Chain operations**: Combine transformations in a single pipeline
3. **Use async**: Prefer `Read.Csv()` over `Read.CsvSync()` for I/O-bound work
4. **Configure options once**: Reuse options objects when reading multiple files
5. **Handle errors at boundary**: Configure error handling at read time, not in processing

---

## See Also

- [DataFlow-Data-Reading-Infrastructure.md](DataFlow-Data-Reading-Infrastructure.md) — Full reading configuration options
- [DataFlow-Data-Writing-Infrastructure.md](DataFlow-Data-Writing-Infrastructure.md) — Full writing configuration options
- [Cases-Pattern.md](Cases-Pattern.md) — Cases/SelectCase pattern
- [Stream-Merging.md](Stream-Merging.md) — Multi-source streaming
