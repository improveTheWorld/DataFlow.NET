# DataFlow.Logger

## Core Philosophy
DataFlow.Logger is a **diagnostic-centric logging library** designed for high-granularity tracing during development and debugging. Unlike traditional application loggers that primarily rely on static log levels, DataFlow.Logger focuses on **Context and State**.

It empowers developers to:
- **Watch specific object instances** (`WatchByLogger`) without modifying the rest of the application.
- **Watch entire namespaces** dynamically to isolate component behavior.
- **Reactively log state changes** using `Loggable<T>`, eliminating repetitive log statements.
- **Filter noise** using powerful predicates (e.g., "Log only if `value > 100`").

## Key Features

### 1. Zero-Setup Static Facade
Access the logger from anywhere without complex dependency injection setup:
```csharp
iLogger.Info("System started");
iLogger.Error("Something went wrong", exception);
```
Instead of enabling "Debug" level for the entire application, you can surgically target specific areas:

#### By Instance
Trace only specific objects of interest:
```csharp
var myService = new Service();
myService.WatchByLogger("CriticalService"); // Only this instance is logged
```

#### By Namespace
Monitor all objects within a specific namespace:
```csharp
iLogger.Filters.WatchedNameSpaces.Watch(new NameSpaceComparer("My.Critical.Namespace"));
```

#### By Custom Criteria
Define complex logic for what should be logged. The logger accepts all logs by default if no criteria are defined.
```csharp
// Example: Log only odd numbers from NumericLoggableObject
iLogger.Filters.RequesterAcceptanceCriterias.SetCriteria(obj => 
    obj is NumericLoggableObject num && num.IsOdd());
```

### 3. Reactive Logging (`Loggable<T>`)
Automatically log variable changes without cluttering your business logic with `Log()` calls. This is ideal for tracking state transitions.
`Loggable<T>` is a wrapper that triggers a log entry whenever the value is assigned or modified.

```csharp
Loggable<int> counter = 0; // Implicit conversion
counter = 1;  // Logs: "Value Changes due to assignment: 1"
counter += 5; // Logs: "Value Changes due to '+' operator: 6"
```

### 4. Flexible Architecture
- **Synchronous by Default**: Logs are written directly to targets for immediate feedback during debugging.
- **Optional Async Buffering**: Enable `BufferEnabled` to push logs to a background channel, ensuring the main application thread is not blocked by I/O.
- **Targets**:
  - **Console**: Colored output for local debugging.
  - **File**: Simple file logging.
  - **Kafka**: Push logs to Event Hubs/Kafka for centralized analysis.

## Configuration

Configure the logger output format and behavior globally via `iLogger.Filters` and `iLogger` properties:

### Output Formatting
```csharp
iLogger.Filters.IncludeTimestamp = true;
iLogger.Filters.IncludeThreadId = true;
iLogger.Filters.IncludeInstanceName = true;
iLogger.Filters.IncludeTaskId = true;
```

### Log Level Threshold
Set the **minimum severity** level required for a log to be processed. Logs below this level will be ignored.
```csharp
// Only allow Info, Warn, Error, Fatal. (Debug and Trace are ignored)
iLogger.MaxAuthorizedLogLevel = LogLevel.Info; 
```

### Async Mode
Enable background buffering for better performance in high-volume scenarios:
```csharp
iLogger.BufferEnabled = true; // Starts the background processing loop
```

## Architecture Overview
- **`iLogger`**: The static entry point and facade.
- **`LogFilter`**: The central engine for evaluating filtering rules.
- **`ILoggerTarget`**: Interface for output sinks (Console, File, Kafka).
- **`Loggable<T>`**: Wrapper for reactive state logging.

