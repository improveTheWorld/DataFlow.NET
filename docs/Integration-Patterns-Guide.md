# Integration Patterns Guide

> **How to connect DataFlow.NET to any external data source using existing primitives**

DataFlow.NET doesn't need plugins for every data source. Instead, it provides **powerful primitives** that adapt to any source. This guide shows how to integrate with common systems.

---

## Core Principle: Wrap and Flatten

Every external data source follows the same pattern:

```
┌─────────────────────────────────────────────────────────────────┐
│  EXTERNAL SOURCE                    DATAFLOW PIPELINE           │
│                                                                 │
│  ┌─────────┐      Wrap       ┌───────────────────┐              │
│  │ Kafka   │  ──────────────▶│ IAsyncEnumerable  │──▶ Process   │
│  │ HTTP    │   (5 lines)     │     <T>           │              │
│  │ EF Core │                 └───────────────────┘              │
│  └─────────┘                                                    │
└─────────────────────────────────────────────────────────────────┘
```

---

## 1. HTTP/REST APIs

### The Challenge: APIs Return Batches

Most REST APIs return arrays, not individual items:

```json
// GET /api/orders returns:
[
  { "id": 1, "amount": 100 },
  { "id": 2, "amount": 200 }
]
```

### Solution: Poll + SelectMany

```csharp
using System.Net.Http.Json;

// Create HTTP client
var http = new HttpClient { BaseAddress = new Uri("https://api.example.com") };

// Create streaming source from REST API
var ordersStream = (() => http.GetFromJsonAsync<Order[]>("/api/orders"))
    .Poll(TimeSpan.FromSeconds(5), cancellationToken)  // Poll every 5s
    .SelectMany(batch => batch.ToAsyncEnumerable());   // Flatten batches

// Now use standard DataFlow pipeline
await ordersStream
    .Where(o => o.Amount > 100)
    .Select(o => EnrichOrder(o))
    .WriteCsv("orders.csv");
```

### Visual: Why SelectMany?

```
Without SelectMany (batches):
┌─────────────────┐     ┌─────────────────┐     ┌─────────────────┐
│ [A, B, C]       │ ──▶ │ [D, E]          │ ──▶ │ [F, G, H]       │
└─────────────────┘     └─────────────────┘     └─────────────────┘
     Batch 1                 Batch 2                 Batch 3

With SelectMany (individual items):
┌─┐   ┌─┐   ┌─┐   ┌─┐   ┌─┐   ┌─┐   ┌─┐   ┌─┐
│A│ ─▶│B│ ─▶│C│ ─▶│D│ ─▶│E│ ─▶│F│ ─▶│G│ ─▶│H│
└─┘   └─┘   └─┘   └─┘   └─┘   └─┘   └─┘   └─┘
   All items flow individually through the pipeline
```

### Variations

```csharp
// With authentication
var ordersStream = (async () => {
    var request = new HttpRequestMessage(HttpMethod.Get, "/api/orders");
    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
    var response = await http.SendAsync(request);
    return await response.Content.ReadFromJsonAsync<Order[]>();
})
.Poll(TimeSpan.FromSeconds(10), cancellationToken)
.SelectMany(batch => batch ?? Array.Empty<Order>());

// Paginated API
async IAsyncEnumerable<Order> FetchAllPages([EnumeratorCancellation] CancellationToken ct)
{
    int page = 1;
    while (true)
    {
        var batch = await http.GetFromJsonAsync<Order[]>($"/api/orders?page={page}", ct);
        if (batch == null || batch.Length == 0) yield break;
        
        foreach (var order in batch)
            yield return order;
        
        page++;
    }
}

var allOrders = FetchAllPages(cancellationToken);
```

---

## 2. Entity Framework Core (Databases)

### The Power: EF Core is Already IAsyncEnumerable

EF Core queries are **natively compatible** with DataFlow.NET. No adapter needed!

```csharp
using Microsoft.EntityFrameworkCore;

// Your DbContext
public class AppDbContext : DbContext
{
    public DbSet<Order> Orders { get; set; }
    public DbSet<Customer> Customers { get; set; }
}

// Direct streaming from database
using var context = new AppDbContext();

await context.Orders
    .Where(o => o.Status == "Active")
    .AsAsyncEnumerable()                    // ← EF Core native method
    .Where(o => o.Amount > 100)             // DataFlow processing
    .Select(o => new OrderDto(o))
    .WriteCsv("active_orders.csv");
```

### EF Core + DataFlow Patterns

```csharp
// Combine database with file source
var dbOrders = context.Orders
    .Where(o => o.CreatedDate > DateTime.Today.AddDays(-7))
    .AsAsyncEnumerable();

var fileOrders = Read.Csv<Order>("historical_orders.csv");

// Merge both sources
var allOrders = new UnifiedStream<Order>()
    .Unify(dbOrders, "database")
    .Unify(fileOrders, "file");

await allOrders
    .Cases(o => o.Priority == "High")
    .SelectCase(
        high => ProcessHighPriority(high),
        normal => ProcessNormal(normal)
    )
    .AllCases()
    .Do(o => SaveResult(o));
```

### Performance Tip: Use AsNoTracking

```csharp
// For read-only streaming, skip change tracking
var stream = context.Orders
    .AsNoTracking()                         // ← Better performance
    .AsAsyncEnumerable();
```

---

## 3. Kafka (Using Confluent.Kafka)

### Pattern: Wrap Consumer + Buffer for Backpressure

```csharp
using Confluent.Kafka;

// Kafka consumer configuration
var config = new ConsumerConfig
{
    BootstrapServers = "localhost:9092",
    GroupId = "dataflow-consumer",
    AutoOffsetReset = AutoOffsetReset.Earliest
};

// Create async enumerable from Kafka consumer
async IAsyncEnumerable<Order> ConsumeKafka([EnumeratorCancellation] CancellationToken ct)
{
    using var consumer = new ConsumerBuilder<string, string>(config).Build();
    consumer.Subscribe("orders-topic");
    
    while (!ct.IsCancellationRequested)
    {
        var result = consumer.Consume(ct);
        if (result?.Message?.Value != null)
        {
            var order = JsonSerializer.Deserialize<Order>(result.Message.Value);
            if (order != null)
                yield return order;
        }
    }
}

// Use with DataFlow
var kafkaStream = ConsumeKafka(cancellationToken)
    .WithBoundedBuffer(1024);               // Add backpressure buffer

await kafkaStream
    .Where(o => o.IsValid)
    .AsParallel()
    .WithMaxConcurrency(4)
    .Select(async o => await EnrichAsync(o))
    .WriteCsv("kafka_orders.csv");
```

### Visual: Kafka Integration

```
┌────────────────────────────────────────────────────────────────────┐
│                         KAFKA INTEGRATION                          │
│                                                                    │
│  ┌─────────────┐    ┌───────────────┐    ┌────────────────────┐   │
│  │   Kafka     │    │   Bounded     │    │    DataFlow        │   │
│  │   Topic     │───▶│   Buffer      │───▶│    Pipeline        │   │
│  │             │    │   (1024)      │    │                    │   │
│  └─────────────┘    └───────────────┘    └────────────────────┘   │
│                                                                    │
│  Push from Kafka     Backpressure        Where/Select/Write       │
│  (fast producer)     (absorb bursts)     (controlled pace)        │
└────────────────────────────────────────────────────────────────────┘
```

---

## 4. RabbitMQ

### Pattern: Similar to Kafka

```csharp
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

async IAsyncEnumerable<Order> ConsumeRabbitMQ([EnumeratorCancellation] CancellationToken ct)
{
    var factory = new ConnectionFactory { HostName = "localhost" };
    using var connection = factory.CreateConnection();
    using var channel = connection.CreateModel();
    
    var consumer = new EventingBasicConsumer(channel);
    var messageQueue = Channel.CreateBounded<Order>(1024);
    
    consumer.Received += (model, ea) =>
    {
        var body = ea.Body.ToArray();
        var order = JsonSerializer.Deserialize<Order>(body);
        if (order != null)
            messageQueue.Writer.TryWrite(order);
    };
    
    channel.BasicConsume("orders-queue", autoAck: true, consumer);
    
    await foreach (var order in messageQueue.Reader.ReadAllAsync(ct))
    {
        yield return order;
    }
}

// Use with DataFlow
await ConsumeRabbitMQ(cancellationToken)
    .Where(o => o.Amount > 0)
    .Select(o => ProcessOrder(o))
    .Do(o => Console.WriteLine(o));
```

---

## 5. WebSocket Streams

### Pattern: Buffer Push Events

```csharp
using System.Net.WebSockets;

async IAsyncEnumerable<StockPrice> StreamWebSocket(
    string url, 
    [EnumeratorCancellation] CancellationToken ct)
{
    using var ws = new ClientWebSocket();
    await ws.ConnectAsync(new Uri(url), ct);
    
    var buffer = new byte[4096];
    while (ws.State == WebSocketState.Open && !ct.IsCancellationRequested)
    {
        var result = await ws.ReceiveAsync(buffer, ct);
        if (result.MessageType == WebSocketMessageType.Text)
        {
            var json = Encoding.UTF8.GetString(buffer, 0, result.Count);
            var price = JsonSerializer.Deserialize<StockPrice>(json);
            if (price != null)
                yield return price;
        }
    }
}

// Real-time stock processing
await StreamWebSocket("wss://market.example.com/prices", cancellationToken)
    .WithBoundedBuffer(256)                 // Handle bursts
    .Throttle(TimeSpan.FromMilliseconds(100)) // Rate limit
    .Where(p => p.Change > 0.05m)           // Significant moves only
    .Do(p => AlertUser(p));
```

---

## 6. Azure Service Bus

### Pattern: SDK + Async Wrapper

```csharp
using Azure.Messaging.ServiceBus;

async IAsyncEnumerable<Order> ReceiveFromServiceBus(
    string connectionString,
    string queueName,
    [EnumeratorCancellation] CancellationToken ct)
{
    await using var client = new ServiceBusClient(connectionString);
    await using var receiver = client.CreateReceiver(queueName);
    
    while (!ct.IsCancellationRequested)
    {
        var message = await receiver.ReceiveMessageAsync(
            TimeSpan.FromSeconds(30), ct);
        
        if (message != null)
        {
            var order = message.Body.ToObjectFromJson<Order>();
            await receiver.CompleteMessageAsync(message, ct);
            yield return order;
        }
    }
}

// Use with DataFlow
await ReceiveFromServiceBus(connectionString, "orders", cancellationToken)
    .AsParallel()
    .Select(async o => await ProcessAsync(o))
    .Do(o => LogResult(o));
```

---

## Summary: Integration Patterns

| Source Type | Pattern | Key Methods |
|------------|---------|-------------|
| **REST API (polling)** | Poll + SelectMany | `.Poll()`, `.SelectMany()` |
| **REST API (paginated)** | Custom async iterator | `async IAsyncEnumerable` |
| **EF Core** | Native | `.AsAsyncEnumerable()` |
| **Kafka** | Wrap + Buffer | `.WithBoundedBuffer()` |
| **RabbitMQ** | Channel-based | `Channel<T>` |
| **WebSocket** | Wrap + Throttle | `.Throttle()` |
| **Service Bus** | Async wrapper | Custom iterator |

---

## Best Practices

> [!TIP]
> ### Use ForEach Instead of External Loops
> To preserve composability, prefer `.Do(action)` over `await foreach`:
> ```csharp
> // ✅ Composable - can extend the pipeline
> await stream.Select(x => Process(x)).Do(x => Save(x));
> 
> // ❌ Less composable - loop breaks the chain
> await foreach (var item in stream.Select(x => Process(x)))
> {
>     Save(item);
> }
> ```

> [!IMPORTANT]
> ### Always Add Backpressure for Push Sources
> Push-based sources (Kafka, WebSocket, events) can overwhelm your pipeline.
> Always use `.WithBoundedBuffer()` or `.Throttle()` at the entry point.

> [!NOTE]
> ### No Plugins Required
> DataFlow.NET's primitives are powerful enough to integrate with any source.
> The patterns shown here work without additional NuGet packages (beyond the source's SDK).

---

## See Also

- [SUPRA Pattern](DataFlow-SUPRA-Pattern.md) - Core architecture philosophy
- [ParallelAsyncQuery API](ParallelAsyncQuery-API-Reference.md) - Parallel processing
- [Extension Methods API](Extension-Methods-API-Reference.md) - All available methods
