# DataFlow.Data Layer Documentation

The DataFlow.Data layer provides the foundation for data access, reading, writing, and transformation operations in the DataFlow.NET framework.

## Overview

This layer abstracts the complexities of file I/O operations and data format handling, providing a clean, intuitive API for working with various data sources and formats.

## Core Components

### Read Class

The `Read` class is the primary entry point for data ingestion operations, providing static methods for reading data from various sources with built-in lazy evaluation.

#### Methods

##### `text(string path, bool autoClose = true)`
Reads a text file line by line, returning an `IEnumerable<string>`.

**Parameters:**
- `path`: File path to read from
- `autoClose`: Whether to automatically close the file stream (default: true)

**Returns:** `IEnumerable<string>` - Lazy enumerable of file lines

**Example:**
```csharp
// Read all lines from a text file
var lines = Read.text("data.txt");

// Process lines with lazy evaluation
var processedLines = Read.text("large-file.txt")
    .Where(line => !line.StartsWith("#"))  // Skip comments
    .Select(line => line.Trim())           // Trim whitespace
    .Where(line => !string.IsNullOrEmpty(line)); // Skip empty lines
```

##### `text(StreamReader file, bool autoClose = true)`
Reads from an existing `StreamReader` instance.

**Parameters:**
- `file`: StreamReader instance to read from
- `autoClose`: Whether to automatically close the stream (default: true)

**Returns:** `IEnumerable<string>` - Lazy enumerable of lines

**Example:**
```csharp
using var reader = new StreamReader("data.txt");
var lines = Read.text(reader);
```

##### `csv<T>(string path, string separator = ";", bool autoClose = true, params string[] schema)`
Reads and parses CSV files into strongly-typed objects.

**Parameters:**
- `path`: CSV file path
- `separator`: Field separator (default: ";")
- `autoClose`: Whether to automatically close the file (default: true)
- `schema`: Optional custom field schema (uses file header if not provided)

**Returns:** `IEnumerable<T?>` - Lazy enumerable of parsed objects

**Example:**
```csharp
public struct Employee
{
    public string FirstName;
    public string LastName;
    public int Age;
    public decimal Salary;
}

// Read CSV with header row
var employees = Read.csv<Employee>("employees.csv", ",");

// Read CSV with custom schema
var customEmployees = Read.csv<Employee>("data.csv", ",", true, 
    "FirstName", "LastName", "Age", "Salary");

// Process the data
var seniorEmployees = employees
    .Where(emp => emp.Age >= 50)
    .OrderBy(emp => emp.LastName);
```

#### Implementation Details

The `Read.csv<T>()` method uses the following process:
1. **Header Processing**: Reads the first line as column headers (unless custom schema provided)
2. **Schema Mapping**: Maps CSV columns to object properties/fields by name
3. **Type Conversion**: Automatically converts string values to target types
4. **Lazy Evaluation**: Processes one row at a time to minimize memory usage

### Writers Class

The `Writers` class provides extension methods for writing data to various output formats.

#### Methods

##### `WriteText(this IEnumerable<string> lines, string path, bool autoFlash = true)`
Writes string enumerable to a text file.

**Parameters:**
- `lines`: Enumerable of strings to write
- `path`: Output file path
- `autoFlash`: Whether to auto-flush writes (default: true)

**Example:**
```csharp
var processedLines = Read.text("input.txt")
    .Select(line => line.ToUpper())
    .Where(line => line.Contains("IMPORTANT"));

processedLines.WriteText("output.txt");
```

##### `WriteText(this IEnumerable<string> lines, StreamWriter file)`
Writes to an existing `StreamWriter` instance.

**Parameters:**
- `lines`: Enumerable of strings to write
- `file`: StreamWriter instance

**Example:**
```csharp
using var writer = new StreamWriter("output.txt");
lines.WriteText(writer);
```

##### `WriteCSV<T>(this IEnumerable<T> records, string path, bool withTitle = true, string separator = ";")`
Writes strongly-typed objects to CSV format.

**Parameters:**
- `records`: Enumerable of objects to write
- `path`: Output CSV file path
- `withTitle`: Whether to include header row (default: true)
- `separator`: Field separator (default: ";")

**Type Constraints:** `where T : struct`

**Example:**
```csharp
public struct Product
{
    public string Name;
    public decimal Price;
    public int Quantity;
}

var products = new List<Product>
{
    new Product { Name = "Laptop", Price = 999.99m, Quantity = 10 },
    new Product { Name = "Mouse", Price = 29.99m, Quantity = 50 }
};

// Write with header
products.WriteCSV("products.csv", withTitle: true, separator: ",");

// Chain with data processing
Read.csv<Product>("input.csv")
    .Where(p => p.Quantity > 0)
    .Select(p => { p.Price *= 1.1m; return p; }) // 10% price increase
    .WriteCSV("updated_products.csv");
```

##### `WriteCSV<T>(this IEnumerable<T> records, StreamWriter file, bool withTitle = true, string separator = ";")`
Writes to an existing `StreamWriter` instance in CSV format.

### CSV_Mapper Class

The `CSV_Mapper` class provides low-level CSV serialization functionality used internally by the Writers class.

#### Methods

##### `csv<T>(this T csvRecord, string separator = ";")`
Converts a single object to CSV string representation.

**Parameters:**
- `csvRecord`: Object to convert
- `separator`: Field separator

**Returns:** `string` - CSV representation of the object

##### `csv<T>(string separator = ";") where T : struct`
Generates CSV header string from type definition.

**Parameters:**
- `separator`: Field separator

**Returns:** `string` - CSV header row

**Example:**
```csharp
public struct Person
{
    public string Name;
    public int Age;
}

// Generate header
string header = CSV_Mapper.csv<Person>(","); // "Name,Age"

// Convert object to CSV
var person = new Person { Name = "John", Age = 30 };
string csvLine = person.csv(","); // "John,30"
```

## Data Processing Patterns

### Streaming Pattern
Process large files without loading them entirely into memory:

```csharp
// Process a 1GB log file with constant memory usage
Read.text("huge-log.txt")
    .Where(line => line.Contains("ERROR"))
    .Select(line => $"{DateTime.Now}: {line}")
    .WriteText("errors.txt");
```

### Transformation Pipeline
Chain multiple operations for complex data processing:

```csharp
Read.csv<SalesRecord>("sales.csv")
    .Where(record => record.Date >= DateTime.Today.AddDays(-30)) // Last 30 days
    .GroupBy(record => record.ProductId)
    .Select(group => new SalesSummary 
    { 
        ProductId = group.Key, 
        TotalSales = group.Sum(r => r.Amount),
        Count = group.Count()
    })
    .WriteCSV("monthly_summary.csv");
```

### Multi-format Processing
Convert between different data formats:

```csharp
// Convert CSV to formatted text report
Read.csv<Employee>("employees.csv")
    .Select(emp => $"{emp.LastName}, {emp.FirstName} - Age: {emp.Age}")
    .WriteText("employee_report.txt");
```

## Error Handling

### File Access Errors
```csharp
try
{
    var data = Read.text("nonexistent.txt");
    // Process data...
}
catch (FileNotFoundException ex)
{
    Console.WriteLine($"File not found: {ex.Message}");
}
catch (UnauthorizedAccessException ex)
{
    Console.WriteLine($"Access denied: {ex.Message}");
}
```

### CSV Parsing Errors
```csharp
var records = Read.csv<MyRecord>("data.csv")
    .Where(record => record != null) // Filter out failed parses
    .Cast<MyRecord>(); // Safe cast after null check
```

## Performance Considerations

### Memory Usage
- **Lazy Evaluation**: All Read operations use lazy evaluation, processing one item at a time
- **Streaming**: Large files are processed without loading into memory
- **Disposal**: Streams are automatically disposed when enumeration completes

### Processing Speed
- **Single Pass**: Most operations require only one pass through the data
- **Minimal Allocations**: Efficient string handling and object reuse
- **Buffered I/O**: Optimized file access patterns

### Best Practices
1. **Use streaming**: Avoid `.ToList()` or `.ToArray()` unless necessary
2. **Chain operations**: Combine multiple transformations in a single pipeline
3. **Handle large files**: Use the streaming pattern for files > 100MB
4. **Validate data**: Check for null values and parsing errors
5. **Dispose properly**: Use `using` statements for manual stream management

## Integration Examples

### With DataFlow.Extensions
```csharp
Read.text("log.txt")
    .Until(line => line.StartsWith("END"))
    .Cases(
        line => line.Contains("ERROR"),
        line => line.Contains("WARNING")
    )
    .SelectCase(
        line => $"[E] {line}",
        line => $"[W] {line}",
        line => $"[I] {line}"
    )
    .AllCases()
    .WriteText("categorized.log");
```

### With DataFlow.Framework
```csharp
var publisher = new DataPublisher<string>();

// Publish each line to subscribers
Read.text("data.txt")
    .ForEach(async line => await publisher.PublishDataAsync(line))
    .Do(); // Execute the enumeration
```

This completes the comprehensive documentation for the DataFlow.Data layer, covering all major components, usage patterns, and best practices.
