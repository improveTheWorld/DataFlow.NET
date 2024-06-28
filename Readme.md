# The *iCode* Framework

iCode is a powerful and flexible open-source framework for data processing, transformation, and manipulation in C#. It provides a set of classes and extensions that simplify common data handling tasks and enhance code reusability and extensibility.

**Please note that iCode still currently under development and the current version is a prototype.**

iCode framework is designed to streamline data processing and manipulation tasks, making it easier for developers to work with data efficiently. Whether you're dealing with text files, CSV files, regular expressions, or complex data transformations, iCode provides a comprehensive set of tools to simplify your code and boost productivity.

## Key Features

1. **Simplified Data Reading**: The `Read` class allows you to effortlessly read data from text files, CSV, YAML and JSON files, or even a custom-defined CFG Grammar formatted file, with just a few lines of code. The complexities of file handling and parsing are abstracted away, enabling you to focus on working with the data itself.
2. **Powerful Data Transformation**: Easily perform complex data transformations and manipulation by cascading multiple simple transformations in a fluent code style. A wide range of data transformation capabilities are already provided through the `EnumerablePlus` class and the extensive collection of `IEnumerable` extensions, from selecting, filtering, and classifying data to applying custom transformations. iCode empowers you to manipulate data with ease and flexibility.
3. **Lazy Evaluation**: Read and transformations are only applied as needed, allowing efficient processing and drastically optimizing memory usage. It makes it possible to easily process huge files without dumping hardware resources.
4. **Regular Expression Matching**: The `Rgxs` class simplifies the process of defining regular expression patterns and associating them with specific transformations. It allows you to extract, validate, and transform data based on regular expressions, making it ideal for tasks such as parsing log files, extracting information from text, and data cleaning.
5. **Simplified Regular Expression Syntax**: iCode adds a cosmetic lightweight layer over defining regular expressions, making it much more intuitive and human-friendly.
6. **Integration**: iCode seamlessly integrates with other popular libraries and frameworks in the .NET ecosystem. It leverages the power of LINQ and follows standard C# conventions, making it easy to incorporate into your existing codebase. Whether you're using Entity Framework for data access or working with third-party libraries, iCode fits right in.
7. **Performance Optimization**: Performance is a key consideration in the design of the iCode framework. It utilizes efficient data structures and algorithms to ensure optimal performance when processing large datasets. Whether you're dealing with millions of records or complex data transformations, iCode is built to handle the load efficiently.
8. **Fluent API**: Chain together regex patterns and transformations using a fluent, readable syntax. By leveraging the capabilities of the iCode framework, you can significantly reduce the amount of boilerplate code you write, improve code readability, and enhance the overall maintainability of your data processing pipelines. It provides a solid foundation for building data-driven applications, whether you're working on data analysis, ETL processes, or any other data-related tasks.

### Usage

##### Read, transform and write CSV

Here is a simple usage example of the `Read` class and `IEnumerable` extensions:

```csharp
//Person struct declaration 
public struct Person
{
    public string FirstName;
    public string Name;
    public int Age;
}

// Read lines from a CSV file, extract data, and fill into an Enumerable of Person
Read.csv<Person>("People.csv", ",")
// Convert names to uppercase
    .Select(p =>
    {
        p.Name = p.Name.ToUpper();
        return p;
    })
    // Rewrite into a new CSV file
    .WriteCSV("People_UpperCase.csv", true);

// Note: The file People.csv is processed without being fully loaded into memory.

```

#### Advanced Example with Multiprocessing

In the next code example we use iCode to process a log file and categorize log entries based on their log levels (error, warning, or info). Here's a simple explanation of what the algorithm does:

- It reads the log file line by line until it encounters a line Starting with "STOP:".
- For each line, it categorizes the log entry based on whether it contains "ERROR", "WARNING", or neither (assumed to be an info log).
- Depending on the log level, it applies a suitable transformation to each line by adding the log level information at the beginning of the line.
- It writes each log level to a separate file (errors.txt, warnings.txt, others.txt) based on the categorization.
- It also re-writes the initial log file with the log level information added for all processed log lines (log_WithLevel.txt).
The algorithm efficiently processes the log file in a single pass, applying transformations and writing operations on the fly, line by line. This approach enhances performance and optimizes resource usage.


```csharp
     
        StreamWriter errorLogs = new("errors.txt");
        StreamWriter warningLogs = new("warnings.txt");
        StreamWriter InfoLogs = new("others.txt");

        // Read lines and categorize by log level
        Read.text("log.txt")
            .Until(line=> line.StartsWith("STOP:"))
            .Cases(
                line => line.ToUpper().Contains("ERROR"),
                line => line.ToUpper().Contains("WARNING"),
                line => true
            )
            // Apply suitable transformations for each category,
            // => Add Log level information at the begining of each line
            .SelectCase(
                line => line = $"ERROR : {line}",   // for lines containing "error" 
                line => line = $"WARNING : {line}", // for lines containing  "warning"
                line => line = $"INFO : {line}"     // for other lines
            )
            // Write each log level in a different file
            .ForEachCase(
                line => errorLogs.WriteLine(line),
                line => warningLogs.WriteLine(line),
                line => InfoLogs.WriteLine(line)
            )
            // Re-Write a new log file with the level information added for all processed log lines
            .AllCases()
            .WriteText("log_WithLevel.txt");

        errorLogs.Close();
        warningLogs.Close();
        InfoLogs.Close();

        // Note: The log file is processed in a single pass,
        // Transformations, chained actions and the different write operations,
        // are applied on the fly, line by line,
        // enhancing performance, with optimized ressource usage.
```
#### Using Map method with Regex
```csharp

public static void LogAnalysisExample()
{
    /* The log file conaint the following lines:
   
        "Low memory condition detected while running application, this is a warning"
        "Server Status OK,  Received Response : 500 serevr error"
        "Received Response : 200 Status ok" 
        "Resource allocation exceeded for process ID 453." 
        "Received Response : 404 not Found" */

    int errorsCount = 0;

    // Read the log stream and analyze each line
    Read.text("log.txt")
        // Define regex patterns for extracting error codes and messages
        .Map($"Received Response : {NUMS.As("ErrorCode")} {WORDS.As("errorMessage")}")
        // Categorize log entries based on the extracted error code and message
        .Cases(
            "ErrorCode",
            "errorMessage"
        )
        // Apply transformations based on the category
        .SelectCase(
            code =>
            {
                // Increment the error count if the status code is not 200
                if (code != "200") errorsCount++;
                return code;
            },
            message => "--"+message.ToUpper()+"--",
            x => x
        )
        // Combine all processed log entries
        .AllCases()
        // Convert the processed entries to lines with a specified separator
        .ToLines(Environment.NewLine)
        // Display the processed log entries
        .Display();

    // Print the total count of errors
    Console.WriteLine($"Number of errors : {errorsCount}");
   /*
        New Log :---------{
        Low memory condition detected while running application, this is a warning
        Server Status OK,  Received Response : 500--SEREVR ERROR--
        Received Response: 200--STATUS OK--
        Resource allocation exceeded for process ID 453.
        Received Response : 404--NOT FOUND--
        ------ -}
        Number of errors: 2
            */

```
## Licensing

* This project is licensed under the Apache V2.0 for free software use - see the [LICENSE](./LICENSE-APACHE.txt) file for details.
* For commercial software use, see the [LICENSE\_NOTICE](./LICENSE_NOTICE.md) file.

## Contact

For licensing inquiries or more information, please contact:
Email: [tecnet.paris@gmail.com](mailto:tecnet.paris@gmail.com)
