# DataFlow.NET Framework - Complete API Reference

This document provides a comprehensive reference for all public APIs in the DataFlow.NET framework, organized by namespace and class.

## Table of Contents

1. [DataFlow.Data Namespace](#DataFlow-data-namespace)
2. [DataFlow.Extensions Namespace](#DataFlow-extensions-namespace)
3. [DataFlow.Framework Namespace](#DataFlow-framework-namespace)
4. [Type Definitions](#type-definitions)
5. [Extension Method Quick Reference](#extension-method-quick-reference)

## DataFlow.Data Namespace

### Read Class

Static class providing data reading functionality with lazy evaluation.

#### Methods

```csharp
public static IEnumerable<string> text(StreamReader file, bool autoClose = true)
```
**Description:** Reads lines from a StreamReader  
**Parameters:**
- `file`: StreamReader instance to read from
- `autoClose`: Whether to automatically close the stream (default: true)  
**Returns:** `IEnumerable<string>` - Lazy enumerable of lines  
**Example:**
```csharp
using var reader = new StreamReader("data.txt");
var lines = Read.text(reader);
```

```csharp
public static IEnumerable<string> text(string path, bool autoClose = true)
```
**Description:** Reads lines from a text file  
**Parameters:**
- `path`: File path to read from
- `autoClose`: Whether to automatically close the file stream (default: true)  
**Returns:** `IEnumerable<string>` - Lazy enumerable of file lines  
**Example:**
```csharp
var lines = Read.text("data.txt");
```

```csharp
public static IEnumerable<T?> csv<T>(string path, string separator = ";", bool autoClose = true, params string[] schema)
```
**Description:** Reads and parses CSV files into strongly-typed objects  
**Type Constraints:** None  
**Parameters:**
- `path`: CSV file path
- `separator`: Field separator (default: ";")
- `autoClose`: Whether to automatically close the file (default: true)
- `schema`: Optional custom field schema (uses file header if not provided)  
**Returns:** `IEnumerable<T?>` - Lazy enumerable of parsed objects  
**Example:**
```csharp
var employees = Read.csv<Employee>("employees.csv", ",");
```

### Writers Class

Static class providing extension methods for writing data to various formats.

#### Extension Methods

```csharp
public static void WriteText(this IEnumerable<string> lines, StreamWriter file)
```
**Description:** Writes string enumerable to a StreamWriter  
**Parameters:**
- `lines`: Enumerable of strings to write
- `file`: StreamWriter instance  
**Example:**
```csharp
lines.WriteText(writer);
```

```csharp
public static void WriteText(this IEnumerable<string> lines, string path, bool autoFlash = true)
```
**Description:** Writes string enumerable to a text file  
**Parameters:**
- `lines`: Enumerable of strings to write
- `path`: Output file path
- `autoFlash`: Whether to auto-flush writes (default: true)  
**Example:**
```csharp
processedLines.WriteText("output.txt");
```

```csharp
public static void WriteCSV<T>(this IEnumerable<T> records, StreamWriter file, bool withTitle = true, string separator = ";") where T : struct
```
**Description:** Writes strongly-typed objects to CSV format using StreamWriter  
**Type Constraints:** `where T : struct`  
**Parameters:**
- `records`: Enumerable of objects to write
- `file`: StreamWriter instance
- `withTitle`: Whether to include header row (default: true)
- `separator`: Field separator (default: ";")  
**Example:**
```csharp
products.WriteCSV(writer, withTitle: true, separator: ",");
```

```csharp
public static void WriteCSV<T>(this IEnumerable<T> records, string path, bool withTitle = true, string separator = ";") where T : struct
```
**Description:** Writes strongly-typed objects to CSV file  
**Type Constraints:** `where T : struct`  
**Parameters:**
- `records`: Enumerable of objects to write
- `path`: Output CSV file path
- `withTitle`: Whether to include header row (default: true)
- `separator`: Field separator (default: ";")  
**Example:**
```csharp
products.WriteCSV("products.csv", withTitle: true, separator: ",");
```

## DataFlow.Extensions Namespace

### IEnumerableExtensions Class

Comprehensive extension methods for `IEnumerable<T>` manipulation.

#### Control Flow Extensions

```csharp
public static IEnumerable<T> Until<T>(this IEnumerable<T> items, Func<bool> stopCondition)
```
**Description:** Processes items until a global condition becomes true  
**Parameters:**
- `items`: Source enumerable
- `stopCondition`: Function that returns true when processing should stop  
**Returns:** `IEnumerable<T>` - Items processed before the condition was met  

```csharp
public static IEnumerable<T> Until<T>(this IEnumerable<T> items, Func<T, bool> stopCondition)
```
**Description:** Processes items until an item-specific condition is met  
**Parameters:**
- `items`: Source enumerable
- `stopCondition`: Function that evaluates each item  

```csharp
public static IEnumerable<T> Until<T>(this IEnumerable<T> items, Func<T, int, bool> stopCondition)
```
**Description:** Processes items until a condition involving the item and its index is met  
**Parameters:**
- `items`: Source enumerable
- `stopCondition`: Function that evaluates each item and its index  

```csharp
public static IEnumerable<T> Until<T>(this IEnumerable<T> items, int lastItemIdx)
```
**Description:** Processes items up to a specific index  
**Parameters:**
- `items`: Source enumerable
- `lastItemIdx`: Last index to process (inclusive)  

#### Action Extensions

```csharp
public static IEnumerable<T> ForEach<T>(this IEnumerable<T> items, Action<T> action)
```
**Description:** Executes an action for each item while maintaining the enumerable chain  
**Parameters:**
- `items`: Source enumerable
- `action`: Action to execute for each item  
**Returns:** `IEnumerable<T>` - Original enumerable for chaining  

```csharp
public static IEnumerable<T> ForEach<T>(this IEnumerable<T> items, Action<T, int> action)
```
**Description:** Executes an action with access to both item and index  
**Parameters:**
- `items`: Source enumerable
- `action`: Action to execute with item and index  

```csharp
public static void Do<T>(this IEnumerable<T> items)
```
**Description:** Forces enumeration of the sequence without returning values  
**Parameters:**
- `items`: Source enumerable  

```csharp
public static void Do<T>(this IEnumerable<T> items, Action action)
```
**Description:** Executes an action for each enumeration step  
**Parameters:**
- `items`: Source enumerable
- `action`: Action to execute during enumeration  

#### Cases Pattern Extensions

```csharp
public static IEnumerable<(int category, T item)> Cases<T>(this IEnumerable<T> items, params Func<T, bool>[] filters)
```
**Description:** Categorizes items based on predicates  
**Parameters:**
- `items`: Source enumerable
- `filters`: Array of predicate functions for categorization  
**Returns:** `IEnumerable<(int category, T item)>` - Items tagged with category indices  

```csharp
public static IEnumerable<(int category, T item, R newItem)> SelectCase<T, R>(this IEnumerable<(int category, T item)> items, params Func<T, R>[] selectors)
```
**Description:** Applies different transformations based on category  
**Parameters:**
- `items`: Categorized enumerable
- `selectors`: Array of transformation functions for each category  
**Returns:** `IEnumerable<(int category, T item, R newItem)>` - Items with transformations  

```csharp
public static IEnumerable<(int category, T item, R newItem)> ForEachCase<T, R>(this IEnumerable<(int category, T item, R newItem)> items, params Action<R>[] actions)
```
**Description:** Executes different actions based on category  
**Parameters:**
- `items`: Categorized enumerable with transformations
- `actions`: Array of actions for each category  
**Returns:** Same enumerable for chaining  

```csharp
public static IEnumerable<R> AllCases<T, R>(this IEnumerable<(int category, T item, R newItem)> items)
```
**Description:** Extracts transformed items from categorized enumerable  
**Parameters:**
- `items`: Categorized enumerable with transformations  
**Returns:** `IEnumerable<R>` - Transformed items only  

#### Aggregation Extensions

```csharp
public static T Cumul<T>(this IEnumerable<T> sequence, Func<T?, T, T> cumulate)
```
**Description:** Performs cumulative operations on a sequence  
**Parameters:**
- `sequence`: Source enumerable
- `cumulate`: Function to combine accumulator with current item  
**Returns:** `T` - Final accumulated result  

```csharp
public static TResult Cumul<T, TResult>(this IEnumerable<T> sequence, Func<TResult?, T, TResult> cumulate, TResult? initial)
```
**Description:** Performs cumulative operations with an initial value  
**Parameters:**
- `sequence`: Source enumerable
- `cumulate`: Function to combine accumulator with current item
- `initial`: Initial accumulator value  
**Returns:** `TResult` - Final accumulated result  

```csharp
public static dynamic Sum<T>(this IEnumerable<T> items)
```
**Description:** Generic sum operation using dynamic typing  
**Parameters:**
- `items`: Source enumerable of numeric values  
**Returns:** `dynamic` - Sum of all items  

#### Utility Extensions

```csharp
public static IEnumerable<T> MergeOrdered<T>(this IEnumerable<T> first, IEnumerable<T> second, Func<T, T, bool> isFirstLessThanOrEqualToSecond)
```
**Description:** Merges two ordered enumerables into a single ordered enumerable  
**Parameters:**
- `first`: First ordered enumerable
- `second`: Second ordered enumerable
- `isFirstLessThanOrEqualToSecond`: Comparison function  
**Returns:** `IEnumerable<T>` - Merged ordered enumerable  

```csharp
public static IEnumerable<T> Take<T>(this IEnumerable<T> sequence, int start, int count)
```
**Description:** Takes a specific range of items  
**Parameters:**
- `sequence`: Source enumerable
- `start`: Starting index
- `count`: Number of items to take  
**Returns:** `IEnumerable<T>` - Specified range of items  

```csharp
public static bool IsNullOrEmpty<T>(this IEnumerable<T> sequence)
```
**Description:** Checks if enumerable is null or empty  
**Parameters:**
- `sequence`: Enumerable to check  
**Returns:** `bool` - True if null or empty  

#### String Building Extensions

```csharp
public static string BuildString(this IEnumerable<string> items, StringBuilder str = null, string separator = ", ", string before = "{", string after = "}")
```
**Description:** Builds formatted strings from string enumerables  
**Parameters:**
- `items`: Source string enumerable
- `str`: Optional StringBuilder instance
- `separator`: Separator between items (default: ", ")
- `before`: Prefix string (default: "{")
- `after`: Suffix string (default: "}")  
**Returns:** `string` - Formatted string  

### StringExtensions Class

Extension methods for string manipulation and validation.

#### Validation Methods

```csharp
public static bool IsNullOrEmpty(this string text)
```
**Description:** Checks if string is null or empty  
**Parameters:**
- `text`: String to check  
**Returns:** `bool` - True if null or empty  

```csharp
public static bool IsNullOrWhiteSpace(this string text)
```
**Description:** Checks if string is null, empty, or contains only whitespace  
**Parameters:**
- `text`: String to check  
**Returns:** `bool` - True if null, empty, or whitespace only  

```csharp
public static bool IsBetween(this string text, string start, string end)
```
**Description:** Checks if string starts with one delimiter and ends with another  
**Parameters:**
- `text`: String to check
- `start`: Starting delimiter
- `end`: Ending delimiter  
**Returns:** `bool` - True if string is wrapped by delimiters  

#### Content Analysis Methods

```csharp
public static bool StartsWith(this string value, IEnumerable<string> acceptedStarts)
```
**Description:** Checks if string starts with any of the provided prefixes  
**Parameters:**
- `value`: String to check
- `acceptedStarts`: Collection of acceptable prefixes  
**Returns:** `bool` - True if starts with any prefix  

```csharp
public static bool ContainsAny(this string line, IEnumerable<string> tokens)
```
**Description:** Checks if string contains any of the specified tokens  
**Parameters:**
- `line`: String to search in
- `tokens`: Collection of tokens to search for  
**Returns:** `bool` - True if any token is found  

#### Manipulation Methods

```csharp
public static string ReplaceAt(this string value, int index, int length, string toInsert)
```
**Description:** Replaces substring at specific position  
**Parameters:**
- `value`: Original string
- `index`: Starting index for replacement
- `length`: Length of substring to replace
- `toInsert`: String to insert  
**Returns:** `string` - Modified string  

```csharp
public static int LastIdx(this string text)
```
**Description:** Gets the last valid index of the string  
**Parameters:**
- `text`: Source string  
**Returns:** `int` - Last valid index (Length - 1)  

### FileSystemExtensions Class

Extensions for file system operations with enhanced error handling.

```csharp
public static StreamWriter CreateFileWithoutFailure(this FilePath path, string renameSuffix = ".old")
```
**Description:** Creates a file safely, backing up existing files  
**Parameters:**
- `path`: FilePath instance
- `renameSuffix`: Suffix for backup files (default: ".old")  
**Returns:** `StreamWriter` - Writer for the new file  

```csharp
public static void WriteInFile(this IEnumerable<string> lines, string path, string renamesuffix = ".old", int flusheach = -1)
```
**Description:** Writes enumerable to file with automatic backup  
**Parameters:**
- `lines`: Lines to write
- `path`: Target file path
- `renamesuffix`: Backup suffix (default: ".old")
- `flusheach`: Flush frequency (-1 for no periodic flushing)  

```csharp
public static string DerivateFileName(this string name, Func<string, string> derivate, bool keepExtension = true, params Func<string, string>[] derivates)
```
**Description:** Creates derived filenames based on transformation functions  
**Parameters:**
- `name`: Original filename
- `derivate`: Primary transformation function
- `keepExtension`: Whether to preserve file extension (default: true)
- `derivates`: Additional transformations for directory parts  
**Returns:** `string` - Derived filename  

## DataFlow.Framework Namespace

### Guard Class

Static class providing defensive programming utilities.

#### Validation Methods

```csharp
public static void AgainstNullArgument<TArgument>(string parameterName, TArgument argument) where TArgument : class
```
**Description:** Validates that reference type arguments are not null  
**Type Constraints:** `where TArgument : class`  
**Parameters:**
- `parameterName`: Name of the parameter being validated
- `argument`: Argument value to validate  
**Throws:** `ArgumentNullException` if argument is null  

```csharp
public static void AgainstOutOfRange(string parameterName, int argument, int start, int end)
```
**Description:** Validates that integer arguments are within specified range  
**Parameters:**
- `parameterName`: Name of the parameter being validated
- `argument`: Integer value to validate
- `start`: Minimum allowed value (inclusive)
- `end`: Maximum allowed value (inclusive)  
**Throws:** `ArgumentOutOfRangeException` if argument is outside range  

```csharp
public static void AgainstNullArgumentIfNullable<TArgument>(string parameterName, TArgument argument)
```
**Description:** Validates nullable type arguments  
**Parameters:**
- `parameterName`: Name of the parameter being validated
- `argument`: Nullable argument to validate  
**Throws:** `ArgumentNullException` if nullable argument is null  

```csharp
public static void AgainstNullArgumentProperty<TProperty>(string parameterName, string propertyName, TProperty argumentProperty) where TProperty : class
```
**Description:** Validates that object properties are not null  
**Type Constraints:** `where TProperty : class`  
**Parameters:**
- `parameterName`: Name of the parameter containing the property
- `propertyName`: Name of the property being validated
- `argumentProperty`: Property value to validate  
**Throws:** `ArgumentException` if property is null  

```csharp
public static void AgainstContractNotRespected<TArgument>(string parameter1, string parameter2)
```
**Description:** Validates that arguments relation contract is respected  
**Parameters:**
- `parameter1`: Name of the first parameter
- `parameter2`: Name of the second parameter  
**Throws:** `ArgumentException` indicating contract violation  

### DataPublisher<T> Class

Implements the publisher-subscriber pattern for data distribution.

#### Constructors

```csharp
public DataPublisher()
```
**Description:** Creates a new DataPublisher instance  

#### Methods

```csharp
public void AddWriter(ChannelWriter<T> channelWriter, Func<T, bool>? condition = null)
```
**Description:** Adds a subscriber with optional filtering condition  
**Parameters:**
- `channelWriter`: Channel writer for the subscriber
- `condition`: Optional filter predicate (null means accept all data)  

```csharp
public void RemoveWriter(ChannelWriter<T> channelWriter)
```
**Description:** Removes a subscriber from the publisher  
**Parameters:**
- `channelWriter`: Channel writer to remove  

```csharp
public async Task PublishDataAsync(T newData)
```
**Description:** Publishes data to all matching subscribers asynchronously  
**Parameters:**
- `newData`: Data to publish  

```csharp
public int Count()
```
**Description:** Returns the number of active subscribers  
**Returns:** `int` - Number of active subscribers  

```csharp
public void Dispose()
```
**Description:** Properly disposes all channels and clears subscribers  

#### Interfaces Implemented
- `IDataSource<T>`
- `IDisposable`

### AsyncEnumerableMerger<T> Class

Provides asynchronous enumeration capabilities for data streams.

#### Constructors

```csharp
public AsyncEnumerableMerger(IDataSource<T> dataSource, Func<T, bool>? condition = null, ChannelOptions? options = null)
```
**Description:** Creates an async enumerable from a single data source  
**Parameters:**
- `dataSource`: Data source to subscribe to
- `condition`: Optional filter condition
- `options`: Channel options for buffering  

```csharp
public AsyncEnumerableMerger(Func<T, bool>? condition = null, ChannelOptions? options = null, params IDataSource<T>[] dataSource)
```
**Description:** Creates an async enumerable from multiple data sources  
**Parameters:**
- `condition`: Optional filter condition
- `options`: Channel options for buffering
- `dataSource`: Array of data sources to subscribe to  

```csharp
public AsyncEnumerableMerger(AsyncEnumerableMerger<T> Source, Func<T, bool>? condition = null, ChannelOptions? options = null)
```
**Description:** Creates an async enumerable by copying subscriptions from another instance  
**Parameters:**
- `Source`: Source AsyncEnumerableMerger to copy from
- `condition`: Optional filter condition
- `options`: Channel options for buffering  

#### Methods

```csharp
public AsyncEnumerableMerger<T> ListenTo(IDataSource<T> dataSource, Func<T, bool>? condition = null, ChannelOptions? options = null)
```
**Description:** Adds a subscription to a data source  
**Parameters:**
- `dataSource`: Data source to subscribe to
- `condition`: Optional filter condition
- `options`: Channel options  
**Returns:** `AsyncEnumerableMerger<T>` - Self for method chaining  

```csharp
public AsyncEnumerableMerger<T> Unlisten(IDataSource<T> dataSource)
```
**Description:** Removes subscription from a data source  
**Parameters:**
- `dataSource`: Data source to unsubscribe from  
**Returns:** `AsyncEnumerableMerger<T>` - Self for method chaining  

```csharp
public IAsyncEnumerator<T> GetAsyncEnumerator()
```
**Description:** Gets the async enumerator for iteration  
**Returns:** `IAsyncEnumerator<T>` - Async enumerator instance  

```csharp
public void Dispose()
```
**Description:** Disposes all subscriptions and resources  

#### Interfaces Implemented
- `IAsyncEnumerable<T>`
- `IDisposable`

### AsyncEnumerator<T> Class

Handles low-level async enumeration logic for multiple data sources.

#### Properties

```csharp
public T Current { get; private set; }
```
**Description:** Gets the current item in the enumeration  

#### Methods

```csharp
public async ValueTask<bool> MoveNextAsync()
```
**Description:** Advances to the next item asynchronously  
**Returns:** `ValueTask<bool>` - True if next item is available, false if enumeration is complete  

```csharp
public void Unlisten(ChannelReader<T> readers)
```
**Description:** Removes a channel reader from the enumeration  
**Parameters:**
- `readers`: Channel reader to remove  

```csharp
ValueTask IAsyncDisposable.DisposeAsync()
```
**Description:** Disposes async resources  
**Returns:** `ValueTask` - Disposal task  

#### Interfaces Implemented
- `IAsyncEnumerator<T>`

### EnumerableWithNote<T, P> Class

Extends `IEnumerable<T>` with additional metadata or context information.

#### Properties

```csharp
public P _Plus { get; set; }
```
**Description:** Additional context or metadata associated with the enumerable  

```csharp
public IEnumerable<T> Enumerable
```
**Description:** The underlying enumerable  

#### Constructors

```csharp
public EnumerableWithNote(IEnumerable<T> enumerable, P plus)
```
**Description:** Creates an EnumerableWithNote instance  
**Parameters:**
- `enumerable`: Source enumerable
- `plus`: Additional context/metadata  

#### Methods

```csharp
public IEnumerator<T> GetEnumerator()
```
**Description:** Gets the enumerator for the underlying enumerable  
**Returns:** `IEnumerator<T>` - Enumerator instance  

#### Interfaces Implemented
- `IEnumerable<T>`

### EnumerableWithNoteExtension Class

Extension methods for `EnumerableWithNote<T, P>`.

```csharp
public static EnumerableWithNote<T, P> Plus<T, P>(this IEnumerable<T> items, P plus)
```
**Description:** Adds context/metadata to an enumerable  
**Parameters:**
- `items`: Source enumerable
- `plus`: Context/metadata to add  
**Returns:** `EnumerableWithNote<T, P>` - Enumerable with context  

```csharp
public static IEnumerable<T> Minus<T, P>(this EnumerableWithNote<T, P> items)
```
**Description:** Extracts the enumerable without context  
**Parameters:**
- `items`: EnumerableWithNote instance  
**Returns:** `IEnumerable<T>` - Underlying enumerable  

```csharp
public static IEnumerable<T> Minus<T, P>(this EnumerableWithNote<T, P> items, Action close)
```
**Description:** Extracts the enumerable and executes cleanup action  
**Parameters:**
- `items`: EnumerableWithNote instance
- `close`: Cleanup action to execute  
**Returns:** `IEnumerable<T>` - Underlying enumerable  

### Regx Class

Provides simplified regular expression pattern building.

#### Constants

```csharp
public const string CHAR = ".";
public const string SPACE = @"\s";
public const string ALPHNUM = @"\w";
public const string NUM = @"\d";
public const string ALPHA = "[a-zA-Z]";
public const string ANY_CHARS = ".*";
public const string SPACES = @"\s+";
public const string MAYBE_SPACES = @"\s*";
public const string ALPHNUMS = @"\w+";
public const string NUMS = @"\d+";
public const string ALPHAS = "[a-zA-Z]+";
public const string WORD = MAYBE_SPACES + ALPHNUMS + MAYBE_SPACES;
public static string WORDS = MAYBE_SPACES + ALPHNUMS + Many(SPACE + ALPHNUMS) + MAYBE_SPACES;
```

#### Methods

```csharp
public static string Group(this string input)
```
**Description:** Wraps pattern in non-capturing group if needed  
**Parameters:**
- `input`: Pattern to group  
**Returns:** `string` - Grouped pattern  

```csharp
public static string Any(this string input)
```
**Description:** Applies zero-or-more quantifier (*)  
**Parameters:**
- `input`: Pattern to quantify  
**Returns:** `string` - Quantified pattern  

```csharp
public static string Many(this string input)
```
**Description:** Applies one-or-more quantifier (+)  
**Parameters:**
- `input`: Pattern to quantify  
**Returns:** `string` - Quantified pattern  

```csharp
public static string MayBe(this string input)
```
**Description:** Applies zero-or-one quantifier (?)  
**Parameters:**
- `input`: Pattern to quantify  
**Returns:** `string` - Quantified pattern  

```csharp
public static string As(this string input, string groupName = "")
```
**Description:** Creates named or unnamed capture groups  
**Parameters:**
- `input`: Pattern to capture
- `groupName`: Optional group name (empty for unnamed group)  
**Returns:** `string` - Capture group pattern  

```csharp
public static string OneOf(params string[] parameters)
```
**Description:** Creates alternation patterns  
**Parameters:**
- `parameters`: Array of alternative patterns  
**Returns:** `string` - Alternation pattern  

```csharp
public static string Many(this string input, int limitInf, int limitSup)
```
**Description:** Creates bounded quantifiers  
**Parameters:**
- `input`: Pattern to quantify
- `limitInf`: Minimum occurrences
- `limitSup`: Maximum occurrences  
**Returns:** `string` - Bounded quantifier pattern  

### Regxs Class

Provides advanced pattern matching with multiple regex support.

#### Constants

```csharp
public static class UNMATCHED
{
    public const string LINE = "UNMATCHED.LINE";
    public const string SLICE = "UNMATCHED.SLICE";
}
```

#### Constructors

```csharp
public Regxs(params Regex[] regs)
```
**Description:** Creates Regxs instance with compiled regex patterns  
**Parameters:**
- `regs`: Array of compiled Regex instances  

```csharp
public Regxs(params string[] patterns)
```
**Description:** Creates Regxs instance with string patterns  
**Parameters:**
- `patterns`: Array of regex pattern strings  

#### Methods

```csharp
public Regxs Add(Regex regex)
```
**Description:** Adds additional regex pattern  
**Parameters:**
- `regex`: Compiled Regex to add  
**Returns:** `Regxs` - Self for method chaining  

```csharp
public Regxs Add(string pattern)
```
**Description:** Adds additional string pattern  
**Parameters:**
- `pattern`: Regex pattern string to add  
**Returns:** `Regxs` - Self for method chaining  

```csharp
public IEnumerable<(string groupName, string subpart)> Map(string line)
```
**Description:** Maps a line to named groups and unmatched slices  
**Parameters:**
- `line`: Input line to process  
**Returns:** `IEnumerable<(string groupName, string subpart)>` - Mapped groups and content  

```csharp
public IEnumerable<(string groupName, (int startIndex, int Length) slice)> Slices(string line)
```
**Description:** Returns position information for matched groups  
**Parameters:**
- `line`: Input line to process  
**Returns:** `IEnumerable<(string groupName, (int startIndex, int Length) slice)>` - Position information  

### Syntax Parsing Classes

#### ITokenEater Interface

```csharp
public interface ITokenEater
{
    TokenDigestion AcceptToken(string token);
    void Activate();
}
```

#### TokenDigestion Enum

```csharp
[Flags]
public enum TokenDigestion
{
    None = 0,
    Digested = 2,
    Completed = 4,
    Propagate = 8
}
```

#### TerminalGrammElem Class

```csharp
public class TerminalGrammElem : ITokenEater
```

##### Constructors

```csharp
public TerminalGrammElem(string token)
```
**Description:** Creates a terminal grammar element  
**Parameters:**
- `token`: Token string to match  

##### Methods

```csharp
public TokenDigestion AcceptToken(string token)
```
**Description:** Processes a token  
**Parameters:**
- `token`: Token to process  
**Returns:** `TokenDigestion` - Processing result  

## Type Definitions

### Common Interfaces

#### IDataSource<T>
```csharp
public interface IDataSource<T>
{
    void AddWriter(ChannelWriter<T> writer, Func<T, bool>? condition = null);
    void RemoveWriter(ChannelWriter<T> writer);
    Task PublishDataAsync(T data);
}
```

#### IWithInternalSchema
```csharp
public interface IWithInternalSchema
{
    Dictionary<string, int> GetSchema();
}
```

### Attribute Classes

#### OrderAttribute
```csharp
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public class OrderAttribute : Attribute
{
    public int Order { get; }
    public OrderAttribute(int order);
}
```

### Utility Classes

#### FilePath
```csharp
public class FilePath
{
    public enum Status { File, Folder, MissedPath }
    
    public string FullName { get; }
    public Status status { get; }
    
    public FilePath(string path);
    public string Up();
    public static void Rename(string fullName, string suffix);
}
```

#### Subpart
```csharp
public class Subpart
{
    public string OriginalString { get; }
    public int StartIndex { get; }
    public int EndIndex { get; }
    
    public Subpart Trim(int start, int end);
    public Subpart TrimStart(int steps);
    public Subpart TrimEnd(int steps);
    public override string ToString();
}
```

## Extension Method Quick Reference

### IEnumerable<T> Extensions

| Method | Description | Returns |
|--------|-------------|---------|
| `Until(condition)` | Process until condition | `IEnumerable<T>` |
| `ForEach(action)` | Execute action for each | `IEnumerable<T>` |
| `Cases(filters)` | Categorize items | `IEnumerable<(int, T)>` |
| `SelectCase(selectors)` | Transform by category | `IEnumerable<(int, T, R)>` |
| `ForEachCase(actions)` | Execute by category | `IEnumerable<(int, T, R)>` |
| `AllCases()` | Extract transformations | `IEnumerable<R>` |
| `Cumul(function)` | Cumulative operation | `T` |
| `Sum()` | Generic sum | `dynamic` |
| `MergeOrdered(other, comparer)` | Merge ordered sequences | `IEnumerable<T>` |
| `IsNullOrEmpty()` | Check if null/empty | `bool` |
| `BuildString(options)` | Build formatted string | `string` |
| `Flat()` | Flatten nested enumerables | `IEnumerable<T>` |
| `Spy(tag, display)` | Debug enumerable | `IEnumerable<T>` |
| `Display(tag)` | Output to console | `IEnumerable<T>` |

### String Extensions

| Method | Description | Returns |
|--------|-------------|---------|
| `IsNullOrEmpty()` | Check if null/empty | `bool` |
| `IsNullOrWhiteSpace()` | Check if null/whitespace | `bool` |
| `IsBetween(start, end)` | Check delimiters | `bool` |
| `StartsWith(prefixes)` | Check multiple prefixes | `bool` |
| `ContainsAny(tokens)` | Check for any token | `bool` |
| `ReplaceAt(index, length, text)` | Replace at position | `string` |
| `LastIdx()` | Get last index | `int` |
| `SubPart(start, end)` | Create substring view | `Subpart` |

### File System Extensions

| Method | Description | Returns |
|--------|-------------|---------|
| `CreateFileWithoutFailure(suffix)` | Safe file creation | `StreamWriter` |
| `WriteInFile(path, suffix, flush)` | Write with backup | `void` |
| `DerivateFileName(transform, keep)` | Transform filename | `string` |

### Data Writing Extensions

| Method | Description | Returns |
|--------|-------------|---------|
| `WriteText(path, autoFlush)` | Write text lines | `void` |
| `WriteCSV(path, title, separator)` | Write CSV data | `void` |

This completes the comprehensive API reference for the DataFlow.NET framework, providing detailed information about all public classes, methods, properties, and their usage patterns.
