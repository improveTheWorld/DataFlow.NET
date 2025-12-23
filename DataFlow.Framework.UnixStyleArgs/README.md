# UnixStyleArgs

`UnixStyleArgs` is a lightweight .NET library for parsing, validating, and integrating Unix-style command-line arguments (e.g., `-f`, `--file`) into your application's configuration.

## Features

- **Argument Definition**: Define short (`-s`) and long (`--long`) names, default values, and descriptions.
- **Validation**: Automatically check for mandatory arguments.
- **Help Generation**: Auto-generate help messages and error reports.
- **Configuration Integration**: Seamlessly build an `IConfiguration` and `ServiceProvider` from parsed arguments.

## Usage

### 1. Define Argument Requirements

Create a collection of `ArgRequirement` objects to define what arguments your application expects.

```csharp
using DataFlow.Framework;

var requirements = new List<ArgRequirement>
{
    // Optional argument with default value
    new ArgRequirement(
        argName: "Output", 
        shortName: "o", 
        longName: "output", 
        defaultValue: "./logs", 
        description: "Path to output directory"),

    // Mandatory argument
    new ArgRequirement(
        argName: "InputFile", 
        shortName: "i", 
        longName: "input", 
        defaultValue: "", 
        description: "Path to input file", 
        isMandatory: true),
        
    // Numeric argument (passed as string, parsed later)
    new ArgRequirement(
        argName: "RetryCount", 
        shortName: "r", 
        longName: "retry", 
        defaultValue: "3", 
        description: "Number of retries"),

    // Boolean Flag (Switch)
    new ArgRequirement(
        argName: "Verbose", 
        shortName: "v", 
        longName: "verbose", 
        defaultValue: "false", 
        description: "Enable verbose logging",
        isFlag: true)
}; 
```csharp
string[] args = Environment.GetCommandLineArgs(); // or args from Main

// CheckAgains returns null on SUCCESS, or a list of error/help messages on FAILURE.
var messages = requirements.CheckAgains(args, out List<string>? parsedArgs);

if (messages != null)
{
    // Validation failed or Help was requested
    foreach (var msg in messages)
    {
        Console.WriteLine(msg);
    }
    return; // Exit application
}

// Success! parsedArgs contains the normalized arguments (e.g., --Output ./logs --InputFile data.txt)
Console.WriteLine("Arguments parsed successfully!");
```

### 3. Integrate with Configuration (Dependency Injection)

Once parsed, you can use `ConfigureApp<TConfig>` to bind the arguments to a configuration class and create a `ServiceProvider`.

#### Define a Configuration Class
```csharp
public class AppConfig
{
    public string Output { get; set; }
    public string InputFile { get; set; }
    public int RetryCount { get; set; }
}
```

#### Configure the App
```csharp
// parsedArgs comes from the successful CheckAgains call
var serviceProvider = UnixStyleArgs.ConfigureApp<AppConfig>(parsedArgs.ToArray());

// Retrieve the configured object
var config = serviceProvider.GetService<AppConfig>();

Console.WriteLine($"Processing {config.InputFile}, Output: {config.Output}, Retries: {config.RetryCount}");
```

## Use Cases & Examples

### Case 1: Displaying Help
If the user passes `--help`, `CheckAgains` returns the generated help message.

**Input:**
```bash
myapp.exe --help
```

**Output (via `messages`):**
```text
myapp 
Copyright(C) 1.0.0

  -o, --output	(Default: ./logs) Path to output directory
  -i, --input	(Default: ) Path to input file
  -r, --retry	(Default: 3) Number of retries
  --help	Display this help screen
```

### Case 2: Missing Mandatory Argument
If a mandatory argument (e.g., `InputFile`) is missing, `CheckAgains` returns error messages.

**Input:**
```bash
myapp.exe -o ./custom-logs
```

**Output:**
```text
myapp 
Copyright(C) 1.0.0

ERROR(S):
  required option 'InputFile' is missing
  -o, --output	(Default: ./logs) Path to output directory
  ...
```

### Case 3: Using Defaults
If optional arguments are omitted, their default values are injected into the `parsedArgs` list.

**Input:**
```bash
myapp.exe -i data.csv
```

**Resulting `parsedArgs`:**
```text
--InputFile data.csv --Output ./logs --RetryCount 3
```

### Case 4: Overriding Defaults
Users can override defaults using short or long flags.

**Input:**
```bash
myapp.exe --input data.csv -r 5
```

**Resulting `parsedArgs`:**
--InputFile data.csv --Output ./logs --RetryCount 5
```

### Case 5: Boolean Flags
Flags (switches) do not require a value. Presence implies `true`, absence implies `false` (or default).

**Input:**
```bash
myapp.exe -v
```

**Resulting `parsedArgs`:**
```text
--Verbose=true ...
```

**Input (missing flag):**
```bash
myapp.exe
```

**Resulting `parsedArgs`:**
```text
--Verbose=false ...
```
