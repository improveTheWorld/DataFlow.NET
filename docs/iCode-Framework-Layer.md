# DataFlow.Framework Layer Documentation

The DataFlow.Framework layer provides the core infrastructure components of the DataFlow.NET framework, including async processing capabilities, data publishing patterns, validation utilities, regular expression helpers, and syntax parsing functionality.

## Overview

This layer contains the foundational abstractions and implementations that enable the framework's advanced features like asynchronous data streams, publisher-subscriber patterns, defensive programming utilities, and simplified regular expression processing.

## Core Components

### Guard Class

The `Guard` class provides defensive programming utilities with comprehensive argument validation to ensure robust applications.

#### Methods

##### `AgainstNullArgument<TArgument>(string parameterName, TArgument argument)`
Validates that reference type arguments are not null.

**Type Constraints:** `where TArgument : class`

**Throws:** `ArgumentNullException` if argument is null

**Example:**
```csharp
public void ProcessData(string data, IEnumerable<int> numbers)
{
    Guard.AgainstNullArgument(nameof(data), data);
    Guard.AgainstNullArgument(nameof(numbers), numbers);
    
    // Safe to use data and numbers here
}
```

##### `AgainstOutOfRange(string parameterName, int argument, int start, int end)`
Validates that integer arguments are within specified range.

**Throws:** `ArgumentOutOfRangeException` if argument is outside range

**Example:**
```csharp
public string GetSubstring(string text, int startIndex, int length)
{
    Guard.AgainstNullArgument(nameof(text), text);
    Guard.AgainstOutOfRange(nameof(startIndex), startIndex, 0, text.Length - 1);
    Guard.AgainstOutOfRange(nameof(length), length, 1, text.Length - startIndex);
    
    return text.Substring(startIndex, length);
}
```

##### `AgainstNullArgumentIfNullable<TArgument>(string parameterName, TArgument argument)`
Validates nullable type arguments.

**Example:**
```csharp
public void ProcessValue<T>(T? value) where T : struct
{
    Guard.AgainstNullArgumentIfNullable(nameof(value), value);
    
    // Safe to use value.Value here
}
```

##### `AgainstNullArgumentProperty<TProperty>(string parameterName, string propertyName, TProperty argumentProperty)`
Validates that object properties are not null.

**Example:**
```csharp
public void ProcessUser(User user)
{
    Guard.AgainstNullArgument(nameof(user), user);
    Guard.AgainstNullArgumentProperty(nameof(user), nameof(user.Email), user.Email);
    Guard.AgainstNullArgumentProperty(nameof(user), nameof(user.Profile), user.Profile);
    
    // Safe to use user.Email and user.Profile
}
```

### DataPublisher<T>

The `DataPublisher<T>` class implements the publisher-subscriber pattern for distributing data to multiple consumers with optional filtering.

#### Key Features
- Multiple subscriber support
- Conditional data filtering per subscriber
- Thread-safe operations
- Automatic resource management
- Integration with .NET Channels

#### Methods

##### `AddWriter(ChannelWriter<T> channelWriter, Func<T, bool>? condition = null)`
Adds a subscriber with optional filtering condition.

**Parameters:**
- `channelWriter`: Channel writer for the subscriber
- `condition`: Optional filter predicate (null means accept all data)

**Example:**
```csharp
var publisher = new DataPublisher<LogEntry>();

// Subscribe to all log entries
publisher.AddWriter(allLogsChannel.Writer);

// Subscribe only to error logs
publisher.AddWriter(errorLogsChannel.Writer, log => log.Level == LogLevel.Error);

// Subscribe only to recent logs
publisher.AddWriter(recentLogsChannel.Writer, log => log.Timestamp > DateTime.Now.AddMinutes(-5));
```

##### `RemoveWriter(ChannelWriter<T> channelWriter)`
Removes a subscriber from the publisher.

##### `PublishDataAsync(T newData)`
Publishes data to all matching subscribers asynchronously.

**Example:**
```csharp
var logEntry = new LogEntry 
{ 
    Level = LogLevel.Error, 
    Message = "Database connection failed",
    Timestamp = DateTime.Now
};

await publisher.PublishDataAsync(logEntry);
```

##### `Count()`
Returns the number of active subscribers.

##### `Dispose()`
Properly disposes all channels and clears subscribers.

#### Usage Patterns

##### Basic Publisher-Subscriber
```csharp
var publisher = new DataPublisher<string>();
var channel1 = Channel.CreateUnbounded<string>();
var channel2 = Channel.CreateUnbounded<string>();

publisher.AddWriter(channel1.Writer);
publisher.AddWriter(channel2.Writer, msg => msg.StartsWith("IMPORTANT"));

// Publish data
await publisher.PublishDataAsync("Hello World");           // Goes to channel1 only
await publisher.PublishDataAsync("IMPORTANT: Alert!");     // Goes to both channels
```

##### Real-time Data Distribution
```csharp
var sensorPublisher = new DataPublisher<SensorReading>();

// Critical alerts channel
sensorPublisher.AddWriter(alertsChannel.Writer, 
    reading => reading.Temperature > 80 || reading.Pressure < 10);

// Historical data channel
sensorPublisher.AddWriter(historyChannel.Writer);

// Real-time dashboard channel (recent data only)
sensorPublisher.AddWriter(dashboardChannel.Writer, 
    reading => DateTime.Now - reading.Timestamp < TimeSpan.FromSeconds(30));

// Simulate sensor data
while (sensorActive)
{
    var reading = await ReadSensorAsync();
    await sensorPublisher.PublishDataAsync(reading);
    await Task.Delay(1000);
}
```

### DataFlow<T>

The `DataFlow<T>` class provides asynchronous enumeration capabilities for data streams, enabling efficient processing of data from multiple sources.

#### Key Features
- Multiple data source subscription
- Conditional data filtering
- Automatic channel management
- Proper disposal pattern
- Integration with `IAsyncEnumerable<T>`

#### Constructors

##### `DataFlow(IDataSource<T> dataSource, Func<T, bool>? condition = null, ChannelOptions? options = null)`
Creates an async enumerable from a single data source.

##### `DataFlow(Func<T, bool>? condition = null, ChannelOptions? options = null, params IDataSource<T>[] dataSources)`
Creates an async enumerable from multiple data sources.

##### `DataFlow(DataFlow<T> source, Func<T, bool>? condition = null, ChannelOptions? options = null)`
Creates an async enumerable by copying subscriptions from another instance.

#### Methods

##### `ListenTo(IDataSource<T> dataSource, Func<T, bool>? condition = null, ChannelOptions? options = null)`
Adds a subscription to a data source.

**Example:**
```csharp
var asyncEnum = new DataFlow<LogEntry>();

asyncEnum
    .ListenTo(fileLogSource, log => log.Level >= LogLevel.Warning)
    .ListenTo(networkLogSource, log => log.Source == "Network")
    .ListenTo(databaseLogSource);
```

##### `Unlisten(IDataSource<T> dataSource)`
Removes subscription from a data source.

##### `GetAsyncEnumerator()`
Gets the async enumerator for iteration.

#### Usage Patterns

##### Basic Async Processing
```csharp
var publisher = new DataPublisher<string>();
var asyncEnum = new DataFlow<string>(publisher);

// Start publishing data in background
_ = Task.Run(async () =>
{
    for (int i = 0; i < 100; i++)
    {
        await publisher.PublishDataAsync($"Message {i}");
        await Task.Delay(100);
    }
});

// Process data asynchronously
await foreach (var message in asyncEnum)
{
    Console.WriteLine($"Received: {message}");
    
    if (message.Contains("50"))
        break; // Stop processing
}
```

##### Multi-source Data Processing
```csharp
var filePublisher = new DataPublisher<LogEntry>();
var networkPublisher = new DataPublisher<LogEntry>();
var databasePublisher = new DataPublisher<LogEntry>();

var combinedLogs = new DataFlow<LogEntry>(
    condition: log => log.Level >= LogLevel.Warning,
    dataSources: filePublisher, networkPublisher, databasePublisher);

await foreach (var logEntry in combinedLogs)
{
    await ProcessCriticalLog(logEntry);
}
```

### AsyncEnumerator<T>

The `AsyncEnumerator<T>` class handles the low-level async enumeration logic, managing multiple channel readers and coordinating data flow.

#### Key Features
- Multiple channel reader management
- Task coordination and synchronization
- Proper cleanup and disposal
- Logging integration support

#### Methods

##### `MoveNextAsync()`
Advances to the next item asynchronously, handling multiple data sources.

##### `Unlisten(ChannelReader<T> reader)`
Removes a channel reader from the enumeration.

#### Internal Architecture

The `AsyncEnumerator<T>` uses the following pattern:
1. **Task Management**: Maintains a list of read tasks from multiple channels
2. **Coordination**: Uses `Task.WhenAny()` to wait for the first available data
3. **Cleanup**: Automatically removes closed channels
4. **Token Management**: Uses `ControlledTask` for coordination

### EnumerableWithNote<T, P>

The `EnumerableWithNote<T, P>` class extends `IEnumerable<T>` with additional metadata or context information.

#### Key Features
- Maintains enumerable behavior
- Carries additional context (`_Plus` property)
- Supports specialized extension methods
- Enables context-aware processing

#### Usage Example
```csharp
var dataWithContext = items.Plus(new ProcessingContext 
{ 
    StartTime = DateTime.Now,
    BatchId = Guid.NewGuid(),
    Options = processingOptions
});

var result = dataWithContext
    .Where(item => item.IsValid())
    .Select(item => Transform(item, dataWithContext._Plus.Options))
    .Minus(context => context.LogCompletion()); // Execute cleanup
```

### Regular Expression Utilities

#### Regex Class

The `Regex` class provides simplified regular expression pattern building with intuitive constants and methods.

##### Constants
```csharp
public const string CHAR = ".";           // Any character
public const string SPACE = @"\s";        // Single whitespace
public const string ALPHNUM = @"\w";      // Alphanumeric character
public const string NUM = @"\d";          // Single digit
public const string ALPHA = "[a-zA-Z]";   // Single letter

public const string ANY_CHARS = ".*";     // Any characters
public const string SPACES = @"\s+";      // One or more spaces
public const string MAYBE_SPACES = @"\s*"; // Zero or more spaces
public const string ALPHNUMS = @"\w+";    // One or more alphanumeric
public const string NUMS = @"\d+";        // One or more digits
public const string ALPHAS = "[a-zA-Z]+"; // One or more letters
public const string WORD = MAYBE_SPACES + ALPHNUMS + MAYBE_SPACES;
public static string WORDS = MAYBE_SPACES + ALPHNUMS + Many(SPACE + ALPHNUMS) + MAYBE_SPACES;
```

##### Methods

##### `Group(this string input)`
Wraps pattern in non-capturing group if needed.

##### `Any(this string input)` / `Many(this string input)` / `MayBe(this string input)`
Applies quantifiers (* + ?) to patterns.

##### `As(this string input, string groupName = "")`
Creates named or unnamed capture groups.

**Example:**
```csharp
string pattern = NUMS.As("number") + SPACES + ALPHAS.As("word");
// Result: "(?<number>\d+)\s+(?<word>[a-zA-Z]+)"
```

##### `OneOf(params string[] parameters)`
Creates alternation patterns.

**Example:**
```csharp
string pattern = OneOf("cat", "dog", "bird");
// Result: "cat|dog|bird"
```

##### `Many(this string input, int limitInf, int limitSup)`
Creates bounded quantifiers.

**Example:**
```csharp
string pattern = NUMS.Many(2, 4); // 2 to 4 digits
// Result: "\d{2,4}"
```

#### Regxes Class

The `Regxes` class provides advanced pattern matching with multiple regex support and automatic slice extraction.

##### Key Features
- Multiple regex pattern support
- Automatic text slicing and mapping
- Integration with Cases/SelectCase pattern
- Unmatched content handling

##### Constructors
```csharp
public Regxes(params Regex[] regs)
public Regxes(params string[] patterns)
```

##### Methods

##### `Add(Regex regex)` / `Add(string pattern)`
Adds additional patterns to the matcher.

##### `Map(string line)`
Maps a line to named groups and unmatched slices.

**Returns:** `IEnumerable<(string groupName, string subpart)>`

##### `Slices(string line)`
Returns position information for matched groups.

**Returns:** `IEnumerable<(string groupName, (int startIndex, int Length) slice)>`

#### Usage Examples

##### Basic Pattern Matching
```csharp
using static DataFlow.Framework.Regex;

// Simple email pattern
string emailPattern = ALPHNUMS.As("user") + "@" + ALPHNUMS.As("domain") + "." + ALPHAS.As("tld");

var regxs = new Regxes(emailPattern);
var result = regxs.Map("contact: john@example.com");

foreach (var (groupName, value) in result)
{
    Console.WriteLine($"{groupName}: {value}");
}
// Output:
// user: john
// domain: example
// tld: com
```

##### Log Processing with Regxes
```csharp
var logPattern = $"{NUMS.As("timestamp")} {OneOf("ERROR", "WARNING", "INFO").As("level")} {ANY_CHARS.As("message")}";
var regxs = new Regxes(logPattern);

Read.text("application.log")
    .SelectMany(line => regxs.Map(line))
    .Cases("timestamp", "level", "message")
    .SelectCase(
        ts => DateTime.FromBinary(long.Parse(ts)).ToString("yyyy-MM-dd HH:mm:ss"),
        level => level.ToUpper(),
        msg => msg.Trim(),
        x => x // Unmatched content
    )
    .AllCases()
    .Display("Parsed Log Entries");
```

##### Advanced Pattern with Multiple Formats
```csharp
var patterns = new Regxes(
    $"HTTP {NUMS.As("status")} {ANY_CHARS.As("url")}",
    $"User {ALPHNUMS.As("username")} logged {OneOf("in", "out").As("action")}",
    $"Error: {ANY_CHARS.As("error_message")}"
);

Read.text("mixed.log")
    .SelectMany(line => patterns.Map(line))
    .Cases("status", "username", "error_message", Regxes.UNMATCHED.LINE)
    .SelectCase(
        status => $"HTTP Status: {status}",
        user => $"User Activity: {user}",
        error => $"Error Detected: {error}",
        line => $"Unrecognized: {line}"
    )
    .ForEachCase(
        httpLog => httpLogger.WriteLine(httpLog),
        userLog => userLogger.WriteLine(userLog),
        errorLog => errorLogger.WriteLine(errorLog),
        unknownLog => unknownLogger.WriteLine(unknownLog)
    )
    .AllCases()
    .WriteText("categorized.log");
```

### Syntax Parsing Framework

The framework includes a basic syntax parsing system for handling structured text formats.

#### ITokenEater Interface

Defines the contract for token processing entities.

```csharp
public interface ITokenEater
{
    TokenDigestion AcceptToken(string token);
    void Activate();
}
```

#### TokenDigestion Enum

Represents the outcome of token processing:
- `None`: Token not matched
- `Digested`: Token matched and processed
- `Completed`: Token matched and completed current expectation
- `Propagate`: Token should be propagated to other processors

#### GrammarElem Class

Represents grammar elements that can process tokens according to defined rules.

#### TerminalGrammElem Class

Represents terminal grammar elements that match specific tokens exactly.

**Example:**
```csharp
var terminal = new TerminalGrammElem("BEGIN");
var result = terminal.AcceptToken("BEGIN"); // Returns TokenDigestion.Completed
```

#### Usage Example
```csharp
// Define grammar rules for a simple language
var rules = new Rule[]
{
    new Rule("STATEMENT", "BEGIN", "COMMANDS", "END"),
    new Rule("COMMANDS", "COMMAND", "COMMANDS"),
    new Rule("COMMANDS", "COMMAND"),
    new Rule("COMMAND", "PRINT", "STRING"),
    new Rule("COMMAND", "SET", "VARIABLE", "VALUE")
};

var grammar = GrammarElem.Builder.Build(rules);

// Process tokens
string[] tokens = { "BEGIN", "PRINT", "Hello", "END" };
foreach (var token in tokens)
{
    var result = grammar.AcceptToken(token);
    Console.WriteLine($"Token: {token}, Result: {result}");
}
```

## Integration Patterns

### Async Data Processing Pipeline
```csharp
// Create publishers for different data sources
var filePublisher = new DataPublisher<LogEntry>();
var networkPublisher = new DataPublisher<LogEntry>();

// Create async enumerable that combines both sources
var combinedLogs = new DataFlow<LogEntry>(
    condition: log => log.Level >= LogLevel.Warning,
    dataSources: filePublisher, networkPublisher);

// Process data asynchronously with regex patterns
var logPattern = new Regxes($"{NUMS.As("timestamp")} {ALPHAS.As("level")} {ANY_CHARS.As("message")}");

await foreach (var logEntry in combinedLogs)
{
    var parsedData = logPattern.Map(logEntry.RawMessage)
        .Cases("timestamp", "level", "message")
        .SelectCase(
            ts => DateTimeOffset.FromUnixTimeSeconds(long.Parse(ts)),
            level => Enum.Parse<LogLevel>(level),
            msg => msg.Trim(),
            x => x
        )
        .AllCases();
    
    await ProcessParsedLog(parsedData);
}
```

### Defensive Programming with Guards
```csharp
public class DataProcessor
{
    private readonly ILogger _logger;
    private readonly DataPublisher<ProcessedData> _publisher;
    
    public DataProcessor(ILogger logger, DataPublisher<ProcessedData> publisher)
    {
        Guard.AgainstNullArgument(nameof(logger), logger);
        Guard.AgainstNullArgument(nameof(publisher), publisher);
        
        _logger = logger;
        _publisher = publisher;
    }
    
    public async Task ProcessAsync(IEnumerable<RawData> data, ProcessingOptions options)
    {
        Guard.AgainstNullArgument(nameof(data), data);
        Guard.AgainstNullArgument(nameof(options), options);
        Guard.AgainstNullArgumentProperty(nameof(options), nameof(options.OutputPath), options.OutputPath);
        Guard.AgainstOutOfRange(nameof(options.BatchSize), options.BatchSize, 1, 10000);
        
        foreach (var item in data)
        {
            try
            {
                var processed = await ProcessItem(item, options);
                await _publisher.PublishDataAsync(processed);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process item: {Item}", item);
            }
        }
    }
    
    private async Task<ProcessedData> ProcessItem(RawData item, ProcessingOptions options)
    {
        Guard.AgainstNullArgument(nameof(item), item);
        Guard.AgainstNullArgumentProperty(nameof(item), nameof(item.Content), item.Content);
        
        // Processing logic here
        return new ProcessedData { /* ... */ };
    }
}
```

### Complex Regex Processing with Context
```csharp
public class LogAnalyzer
{
    private readonly Regxes _logPatterns;
    private readonly DataPublisher<AnalyzedLog> _publisher;
    
    public LogAnalyzer()
    {
        _logPatterns = new Regxes(
            // Apache access log format
            $"{ALPHNUMS.As("ip")} - - \\[{ANY_CHARS.As("timestamp")}\\] \"{ALPHAS.As("method")} {ANY_CHARS.As("url")} HTTP/{NUMS}.{NUMS}\" {NUMS.As("status")} {NUMS.As("size")}",
            
            // Application log format
            $"{NUMS.As("timestamp")} {OneOf("DEBUG", "INFO", "WARN", "ERROR").As("level")} {ALPHNUMS.As("logger")} - {ANY_CHARS.As("message")}",
            
            // Error log format
            $"ERROR {NUMS.As("error_code")}: {ANY_CHARS.As("error_message")} at {ANY_CHARS.As("location")}"
        );
        
        _publisher = new DataPublisher<AnalyzedLog>();
    }
    
    public async Task AnalyzeLogsAsync(string logFilePath)
    {
        Guard.AgainstNullArgument(nameof(logFilePath), logFilePath);
        
        await Read.text(logFilePath)
            .SelectMany(line => _logPatterns.Map(line).Plus(line)) // Add original line as context
            .Cases("ip", "level", "error_code", Regxes.UNMATCHED.LINE)
            .SelectCase(
                ip => new AccessLogEntry { ClientIP = ip },
                level => new ApplicationLogEntry { Level = level },
                code => new ErrorLogEntry { ErrorCode = int.Parse(code) },
                line => new UnknownLogEntry { RawLine = line }
            )
            .ForEachCase(
                async entry => await _publisher.PublishDataAsync(new AnalyzedLog { Type = "Access", Data = entry }),
                async entry => await _publisher.PublishDataAsync(new AnalyzedLog { Type = "Application", Data = entry }),
                async entry => await _publisher.PublishDataAsync(new AnalyzedLog { Type = "Error", Data = entry }),
                async entry => await _publisher.PublishDataAsync(new AnalyzedLog { Type = "Unknown", Data = entry })
            )
            .AllCases()
            .Do(); // Execute the pipeline
    }
}
```

## Advanced Usage Patterns

### Multi-Stage Async Processing
```csharp
public class DataPipeline
{
    private readonly DataPublisher<RawData> _inputPublisher;
    private readonly DataPublisher<ProcessedData> _processedPublisher;
    private readonly DataPublisher<EnrichedData> _enrichedPublisher;
    
    public DataPipeline()
    {
        _inputPublisher = new DataPublisher<RawData>();
        _processedPublisher = new DataPublisher<ProcessedData>();
        _enrichedPublisher = new DataPublisher<EnrichedData>();
        
        SetupPipeline();
    }
    
    private void SetupPipeline()
    {
        // Stage 1: Raw data processing
        var rawDataStream = new DataFlow<RawData>(_inputPublisher);
        _ = Task.Run(async () =>
        {
            await foreach (var rawData in rawDataStream)
            {
                var processed = await ProcessRawData(rawData);
                await _processedPublisher.PublishDataAsync(processed);
            }
        });
        
        // Stage 2: Data enrichment
        var processedDataStream = new DataFlow<ProcessedData>(_processedPublisher);
        _ = Task.Run(async () =>
        {
            await foreach (var processedData in processedDataStream)
            {
                var enriched = await EnrichData(processedData);
                await _enrichedPublisher.PublishDataAsync(enriched);
            }
        });
        
        // Stage 3: Final output
        var enrichedDataStream = new DataFlow<EnrichedData>(_enrichedPublisher);
        _ = Task.Run(async () =>
        {
            await foreach (var enrichedData in enrichedDataStream)
            {
                await SaveToDatabase(enrichedData);
            }
        });
    }
    
    public async Task ProcessFileAsync(string filePath)
    {
        await Read.csv<RawData>(filePath)
            .ForEach(async data => await _inputPublisher.PublishDataAsync(data))
            .Do();
    }
}
```

### Context-Aware Processing with EnumerableWithNote
```csharp
public class BatchProcessor
{
    public async Task ProcessBatchAsync<T>(IEnumerable<T> items, ProcessingContext context)
    {
        Guard.AgainstNullArgument(nameof(items), items);
        Guard.AgainstNullArgument(nameof(context), context);
        
        var contextualItems = items.Plus(context);
        
        var results = contextualItems
            .Where(item => ShouldProcess(item, contextualItems._Plus))
            .Select(item => Transform(item, contextualItems._Plus))
            .Cases(
                item => IsHighPriority(item, contextualItems._Plus),
                item => IsNormalPriority(item, contextualItems._Plus)
            )
            .ForEachCase(
                item => ProcessHighPriority(item, contextualItems._Plus),
                item => ProcessNormalPriority(item, contextualItems._Plus),
                item => ProcessLowPriority(item, contextualItems._Plus)
            )
            .SelectCase(
                item => CreateHighPriorityResult(item, contextualItems._Plus),
                item => CreateNormalPriorityResult(item, contextualItems._Plus),
                item => CreateLowPriorityResult(item, contextualItems._Plus)
            )
            .AllCases()
            .Minus(ctx => ctx.LogBatchCompletion()); // Execute cleanup with context
        
        await SaveResults(results, context);
    }
    
    private bool ShouldProcess<T>(T item, ProcessingContext context)
    {
        return context.Filters.All(filter => filter(item));
    }
    
    private T Transform<T>(T item, ProcessingContext context)
    {
        return context.Transformations.Aggregate(item, (current, transform) => transform(current));
    }
}
```

### Real-time Data Monitoring
```csharp
public class SystemMonitor
{
    private readonly DataPublisher<SystemMetric> _metricsPublisher;
    private readonly DataPublisher<Alert> _alertPublisher;
    private readonly Regxes _metricPatterns;
    
    public SystemMonitor()
    {
        _metricsPublisher = new DataPublisher<SystemMetric>();
        _alertPublisher = new DataPublisher<Alert>();
        
        _metricPatterns = new Regxes(
            $"CPU: {NUMS.As("cpu_percent")}%",
            $"Memory: {NUMS.As("memory_mb")}MB \\({NUMS.As("memory_percent")}%\\)",
            $"Disk: {NUMS.As("disk_percent")}% full",
            $"Network: {NUMS.As("network_mbps")}Mbps"
        );
        
        SetupMonitoring();
    }
    
    private void SetupMonitoring()
    {
        // Process metrics and generate alerts
        var metricsStream = new DataFlow<SystemMetric>(_metricsPublisher);
        _ = Task.Run(async () =>
        {
            await foreach (var metric in metricsStream)
            {
                if (ShouldAlert(metric))
                {
                    var alert = CreateAlert(metric);
                    await _alertPublisher.PublishDataAsync(alert);
                }
            }
        });
        
        // Handle alerts
        var alertsStream = new DataFlow<Alert>(_alertPublisher);
        _ = Task.Run(async () =>
        {
            await foreach (var alert in alertsStream)
            {
                await HandleAlert(alert);
            }
        });
    }
    
    public async Task StartMonitoringAsync(string logPath)
    {
        await Read.text(logPath)
            .SelectMany(line => _metricPatterns.Map(line))
            .Cases("cpu_percent", "memory_percent", "disk_percent", "network_mbps")
            .SelectCase(
                cpu => new SystemMetric { Type = "CPU", Value = double.Parse(cpu), Unit = "%" },
                memory => new SystemMetric { Type = "Memory", Value = double.Parse(memory), Unit = "%" },
                disk => new SystemMetric { Type = "Disk", Value = double.Parse(disk), Unit = "%" },
                network => new SystemMetric { Type = "Network", Value = double.Parse(network), Unit = "Mbps" }
            )
            .AllCases()
            .ForEach(async metric => await _metricsPublisher.PublishDataAsync(metric))
            .Do();
    }
    
    private bool ShouldAlert(SystemMetric metric)
    {
        return metric.Type switch
        {
            "CPU" => metric.Value > 80,
            "Memory" => metric.Value > 90,
            "Disk" => metric.Value > 95,
            "Network" => metric.Value > 1000,
            _ => false
        };
    }
}
```

## Performance Considerations

### Memory Management
- **Channel Cleanup**: `DataPublisher<T>` automatically completes channels on disposal
- **Async Enumeration**: `DataFlow<T>` properly disposes resources
- **Guard Validation**: Minimal overhead with compile-time optimizations
- **Regex Compilation**: `Regxes` uses compiled regex for better performance

### Threading and Concurrency
- **Thread-Safe Publishers**: `DataPublisher<T>` handles concurrent access safely
- **Channel-Based Communication**: Uses .NET Channels for efficient async communication
- **Task Coordination**: `AsyncEnumerator<T>` properly coordinates multiple async sources
- **Cancellation Support**: Proper cancellation token handling throughout

### Best Practices
1. **Dispose Resources**: Always dispose `DataPublisher<T>` and `DataFlow<T>`
2. **Use Guards Early**: Apply validation at method entry points
3. **Compile Regex**: Use compiled regex for frequently used patterns
4. **Batch Operations**: Group related operations to reduce overhead
5. **Monitor Channels**: Watch for channel backpressure in high-throughput scenarios

### Common Pitfalls
- Not disposing publishers and async enumerables
- Creating too many small channels instead of batching
- Using complex regex patterns without compilation
- Forgetting to handle cancellation tokens
- Not validating inputs with Guard clauses

## Testing Strategies

### Unit Testing Guards
```csharp
[Test]
public void Guard_AgainstNullArgument_ThrowsWhenNull()
{
    Assert.Throws<ArgumentNullException>(() => 
        Guard.AgainstNullArgument("param", (string)null));
}

[Test]
public void Guard_AgainstOutOfRange_ThrowsWhenOutOfRange()
{
    Assert.Throws<ArgumentOutOfRangeException>(() => 
        Guard.AgainstOutOfRange("param", 15, 0, 10));
}
```

### Testing DataPublisher
```csharp
[Test]
public async Task DataPublisher_PublishesToAllSubscribers()
{
    var publisher = new DataPublisher<string>();
    var channel1 = Channel.CreateUnbounded<string>();
    var channel2 = Channel.CreateUnbounded<string>();
    
    publisher.AddWriter(channel1.Writer);
    publisher.AddWriter(channel2.Writer);
    
    await publisher.PublishDataAsync("test message");
    
    Assert.AreEqual("test message", await channel1.Reader.ReadAsync());
    Assert.AreEqual("test message", await channel2.Reader.ReadAsync());
}
```

### Testing Regex Patterns
```csharp
[Test]
public void Regxs_ParsesLogEntryCorrectly()
{
    var pattern = new Regxes($"{NUMS.As("timestamp")} {ALPHAS.As("level")} {ANY_CHARS.As("message")}");
    var result = pattern.Map("1234567890 ERROR Something went wrong").ToList();
    
    Assert.AreEqual("1234567890", result.First(x => x.groupName == "timestamp").subpart);
    Assert.AreEqual("ERROR", result.First(x => x.groupName == "level").subpart);
    Assert.AreEqual("Something went wrong", result.First(x => x.groupName == "message").subpart);
}
```

This completes the comprehensive documentation for the DataFlow.Framework layer, covering all major components, usage patterns, integration strategies, and best practices for building robust, high-performance data processing applications.
