using DataFlow.Framework;
using System.Diagnostics;
using System.Reflection;
using Xunit.Abstractions;

namespace DataFlow.Core.Tests.Materialization;

public class PerformanceTests
{
    private readonly ITestOutputHelper _output;

    // =====================================================
    // PERFORMANCE TEST CONFIGURATION
    // =====================================================
    // Set to TRUE for fast development testing (~10K iterations)
    // Set to FALSE for accurate benchmarking (~1M+ iterations)
    private const bool DEV_MODE = false;  // BENCHMARK mode for accurate baseline

    // Iteration counts per mode
    private static readonly int ITERATIONS_SMALL = DEV_MODE ? 10_000 : 100_000;
    private static readonly int ITERATIONS_MEDIUM = DEV_MODE ? 10_000 : 1_000_000;
    private static readonly int ITERATIONS_LARGE = DEV_MODE ? 10_000 : 10_000_000;
    private static readonly int ITERATIONS_MEMORY = DEV_MODE ? 10_000 : 100_000;
    // =====================================================

    public PerformanceTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void Create_FirstCall_ShouldCacheCompiledPlan()
    {
        // Arrange
        var schema = new[] { "Name", "Age", "Salary" };
        var values = new object[] { "John", 30, 75000m };

        // Act - First call (includes compilation)
        var sw1 = Stopwatch.StartNew();
        var person1 = ObjectMaterializer.Create<PersonMutable>(schema, values);
        sw1.Stop();

        // Act - Second call (uses cached plan)
        var sw2 = Stopwatch.StartNew();
        var person2 = ObjectMaterializer.Create<PersonMutable>(schema, values);
        sw2.Stop();

        // Assert
        _output.WriteLine($"First call: {sw1.ElapsedTicks} ticks");
        _output.WriteLine($"Second call: {sw2.ElapsedTicks} ticks");
        _output.WriteLine($"Speedup: {(double)sw1.ElapsedTicks / sw2.ElapsedTicks:F2}x");

        // Second call should be faster (at least 3x) - relaxed for CI/test environments
        Assert.True(sw2.ElapsedTicks * 3 < sw1.ElapsedTicks,
            $"Second call ({sw2.ElapsedTicks}) should be faster than first ({sw1.ElapsedTicks})");
    }

    [Fact]
    public void Create_BulkMaterialization_ShouldBeEfficient()
    {
        // Arrange
        var schema = new[] { "Name", "Age", "Salary" };
        int iterations = ITERATIONS_SMALL;

        // Warmup
        ObjectMaterializer.Create<PersonMutable>(schema, new object[] { "Warmup", 1, 1m });

        // Act
        var sw = Stopwatch.StartNew();
        for (int i = 0; i < iterations; i++)
        {
            var values = new object[] { $"Person{i}", 20 + (i % 50), 50000m + i };
            var person = ObjectMaterializer.Create<PersonMutable>(schema, values);
        }
        sw.Stop();

        // Assert
        var avgMicroseconds = (sw.Elapsed.TotalMilliseconds * 1000) / iterations;
        _output.WriteLine($"Mode: {(DEV_MODE ? "DEV" : "BENCHMARK")}");
        _output.WriteLine($"Total: {sw.ElapsedMilliseconds}ms for {iterations} iterations");
        _output.WriteLine($"Average: {avgMicroseconds:F2} microseconds per materialization");

        // Should be under 50 microseconds per materialization on modern hardware
        Assert.True(avgMicroseconds < 50,
            $"Average materialization time ({avgMicroseconds:F2}µs) should be under 50µs");
    }

    [Fact]
    public void Create_WithReflectionComparison_ShouldBeFaster_Session()
    {
        var schema = new[] { "Name", "Age", "Salary" };
        var values = new object[] { "John", 30, 75000m };
        int iterations = ITERATIONS_LARGE;

        var session = ObjectMaterializer.CreateFeedSession<PersonMutable>(schema);

        // Warmup
        session.Create(values);
        CreateViaReflection(schema, values);

        var sw1 = Stopwatch.StartNew();
        for (int i = 0; i < iterations; i++)
            session.Create(values);
        sw1.Stop();

        var sw2 = Stopwatch.StartNew();
        for (int i = 0; i < iterations; i++)
            CreateViaReflection(schema, values);
        sw2.Stop();

        _output.WriteLine($"Mode: {(DEV_MODE ? "DEV" : "BENCHMARK")}");
        _output.WriteLine($"Session/ObjectMaterializer: {sw1.ElapsedMilliseconds}ms");
        _output.WriteLine($"Reflection: {sw2.ElapsedMilliseconds}ms");
        _output.WriteLine($"Speedup: {(double)sw2.ElapsedMilliseconds / sw1.ElapsedMilliseconds:F2}x");

        Assert.True(sw1.ElapsedMilliseconds * 1.5 < sw2.ElapsedMilliseconds,
           $"ObjectMaterializer ({sw1.ElapsedMilliseconds}ms) should be faster than reflection ({sw2.ElapsedMilliseconds}ms), {(decimal)sw2.ElapsedMilliseconds / (decimal)sw1.ElapsedMilliseconds} times faster");
    }

    [Fact]
    public void Create_MemoryAllocation_ShouldBeMinimal()
    {
        // Arrange
        var schema = new[] { "Name", "Age", "Salary" };
        var values = new object[] { "John", 30, 75000m };
        int iterations = ITERATIONS_MEMORY;

        // Warmup and force GC
        ObjectMaterializer.Create<PersonMutable>(schema, values);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        // Act
        var beforeMemory = GC.GetTotalMemory(false);

        for (int i = 0; i < iterations; i++)
        {
            ObjectMaterializer.Create<PersonMutable>(schema, values);
        }

        var afterMemory = GC.GetTotalMemory(false);
        var allocatedBytes = afterMemory - beforeMemory;

        // Assert
        _output.WriteLine($"Mode: {(DEV_MODE ? "DEV" : "BENCHMARK")}");
        _output.WriteLine($"Memory allocated: {allocatedBytes:N0} bytes for {iterations} materializations");
        _output.WriteLine($"Average per object: {allocatedBytes / iterations:N0} bytes");

        // Should be reasonable (mostly just the Person objects themselves)
        // In DEV_MODE, allow proportionally more overhead
        var maxAllowed = DEV_MODE ? 100_000_000 : 20_000_000;  // Temporary: raised for baseline
        Assert.True(allocatedBytes < maxAllowed,
            $"Memory allocation ({allocatedBytes:N0} bytes) seems excessive");
    }

    // Helper method for reflection comparison
    private PersonMutable CreateViaReflection(string[] schema, object[] values)
    {
        var person = new PersonMutable();
        var type = typeof(PersonMutable);

        for (int i = 0; i < schema.Length && i < values.Length; i++)
        {
            var prop = type.GetProperty(schema[i]);
            if (prop != null && prop.CanWrite)
            {
                var convertedValue = Convert.ChangeType(values[i], prop.PropertyType);
                prop.SetValue(person, convertedValue);
            }
        }

        return person;
    }

    public sealed record PersonCtor(string Name, int Age, decimal Salary);

    [Fact]
    public void CreateCtorSession_WithReflectionComparison_ShouldBeFaster()
    {
        var schema = new[] { "Name", "Age", "Salary" };
        var values = new object?[] { "John", 30, 75000m };
        int iterations = ITERATIONS_MEDIUM;

        // Session under test
        var session = ObjectMaterializer.CreateCtorSession<PersonCtor>(schema);

        // Warmup (JIT, caches)
        session.Create(values);
        CreateViaReflection(schema, values);

        PersonCtor x;
        var sw1 = Stopwatch.StartNew();
        for (int i = 0; i < iterations; i++)
            x = session.Create(values);
        sw1.Stop();

        var sw2 = Stopwatch.StartNew();
        for (int i = 0; i < iterations; i++)
            CreateViaReflection(schema, values);
        sw2.Stop();

        _output.WriteLine($"Mode: {(DEV_MODE ? "DEV" : "BENCHMARK")}");
        _output.WriteLine($"CtorSession/ObjectMaterializer: {sw1.ElapsedMilliseconds}ms");
        _output.WriteLine($"Reflection: {sw2.ElapsedMilliseconds}ms");
        _output.WriteLine($"Speedup: {(double)sw2.ElapsedMilliseconds / sw1.ElapsedMilliseconds:F2}x");

        Assert.True(sw1.ElapsedMilliseconds * 2.5 < sw2.ElapsedMilliseconds,
            $"CtorSession ({sw1.ElapsedMilliseconds}ms) should be much faster than reflection ({sw2.ElapsedMilliseconds}ms), {(decimal)sw2.ElapsedMilliseconds / (decimal)sw1.ElapsedMilliseconds}x faster");
    }

    [Fact]
    public void CreateGeneralSession_WithReflectionComparison_ShouldBeFaster()
    {
        var schema = new[] { "Name", "Age", "Salary" };
        var values = new object?[] { "John", 30, 75000m };
        int iterations = ITERATIONS_MEDIUM;

        // Session under test
        var session = ObjectMaterializer.CreateGeneralSession<PersonCtor>(schema);

        // Warmup (JIT, caches) - run both methods multiple times
        session.Create(values);
        CreateViaReflection(schema, values);
        for (int i = 0; i < 1000; i++)
        {
            session.Create(values);
            CreateViaReflection(schema, values);
        }

        PersonCtor x;
        var sw1 = Stopwatch.StartNew();
        for (int i = 0; i < iterations; i++)
            x = session.Create(values);
        sw1.Stop();

        var sw2 = Stopwatch.StartNew();
        for (int i = 0; i < iterations; i++)
            CreateViaReflection(schema, values);
        sw2.Stop();

        _output.WriteLine($"Mode: {(DEV_MODE ? "DEV" : "BENCHMARK")}");
        _output.WriteLine($"GeneralSession/ObjectMaterializer: {sw1.ElapsedMilliseconds}ms");
        _output.WriteLine($"Reflection: {sw2.ElapsedMilliseconds}ms");
        _output.WriteLine($"Speedup: {(double)sw2.ElapsedMilliseconds / sw1.ElapsedMilliseconds:F2}x");

        var speedup = sw1.ElapsedMilliseconds > 0 ? (decimal)sw2.ElapsedMilliseconds / sw1.ElapsedMilliseconds : 999m;
        Assert.True(sw1.ElapsedMilliseconds * 4 < sw2.ElapsedMilliseconds || sw1.ElapsedMilliseconds == 0,
            $"GeneralSession ({sw1.ElapsedMilliseconds}ms) should be faster than reflection ({sw2.ElapsedMilliseconds}ms), {speedup}x faster");
    }
}
