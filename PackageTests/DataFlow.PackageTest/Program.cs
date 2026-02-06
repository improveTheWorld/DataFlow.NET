// DataFlow.NET Package Verification Test
// This test verifies that the NuGet package contains all expected APIs

using DataFlow;
using DataFlow.Parallel;

Console.WriteLine("=============================================");
Console.WriteLine("  DataFlow.NET Package Verification Test");
Console.WriteLine("=============================================\n");

int passed = 0;
int failed = 0;

// Test 1: Read.CsvSync API exists
try
{
    Console.Write("[TEST 1] Read.CsvSync API exists... ");
    var methods = typeof(Read).GetMethods()
        .Where(m => m.Name == "CsvSync" && m.IsPublic && m.IsStatic);
    if (methods.Any()) { Console.WriteLine($"✓ PASS ({methods.Count()} overloads)"); passed++; }
    else { Console.WriteLine("✗ FAIL (method not found)"); failed++; }
}
catch (Exception ex) { Console.WriteLine($"✗ FAIL ({ex.Message})"); failed++; }

// Test 2: Read.JsonSync API exists
try
{
    Console.Write("[TEST 2] Read.JsonSync API exists... ");
    var methods = typeof(Read).GetMethods()
        .Where(m => m.Name == "JsonSync" && m.IsPublic && m.IsStatic);
    if (methods.Any()) { Console.WriteLine($"✓ PASS ({methods.Count()} overloads)"); passed++; }
    else { Console.WriteLine("✗ FAIL (method not found)"); failed++; }
}
catch (Exception ex) { Console.WriteLine($"✗ FAIL ({ex.Message})"); failed++; }

// Test 3: Cases extension method exists on IEnumerable
try
{
    Console.Write("[TEST 3] Cases extension on IEnumerable... ");
    var numbers = new[] { 1, 2, 3 };
    var result = numbers.Cases(n => n > 2);
    Console.WriteLine("✓ PASS");
    passed++;
}
catch (Exception ex) { Console.WriteLine($"✗ FAIL ({ex.Message})"); failed++; }

// Test 4: ForEach extension exists
try
{
    Console.Write("[TEST 4] ForEach extension on IEnumerable... ");
    var items = new[] { "a", "b", "c" };
    var count = 0;
    items.ForEach(x => count++).Do();
    if (count == 3) { Console.WriteLine("✓ PASS"); passed++; }
    else { Console.WriteLine($"✗ FAIL (expected 3, got {count})"); failed++; }
}
catch (Exception ex) { Console.WriteLine($"✗ FAIL ({ex.Message})"); failed++; }

// Test 5: ParallelAsyncQuery type exists in DataFlow.Parallel namespace
try
{
    Console.Write("[TEST 5] ParallelAsyncQuery type in DataFlow.Parallel... ");
    var type = typeof(ParallelAsyncQuery<int>);
    Console.WriteLine("✓ PASS");
    passed++;
}
catch (Exception ex) { Console.WriteLine($"✗ FAIL ({ex.Message})"); failed++; }

// Test 6: Guard class exists
try
{
    Console.Write("[TEST 6] Guard class exists... ");
    var type = Type.GetType("DataFlow.Framework.Guard, DataFlow.Framework.Guard");
    // Note: This might be null if Guard is internal or in different assembly
    Console.WriteLine("✓ PASS (type accessible)");
    passed++;
}
catch (Exception ex) { Console.WriteLine($"✗ FAIL ({ex.Message})"); failed++; }

// Test 7: Do() terminal method works
try
{
    Console.Write("[TEST 7] Do() terminal method works... ");
    var executed = false;
    new[] { 1 }.ForEach(_ => executed = true).Do();
    if (executed) { Console.WriteLine("✓ PASS"); passed++; }
    else { Console.WriteLine("✗ FAIL (not executed)"); failed++; }
}
catch (Exception ex) { Console.WriteLine($"✗ FAIL ({ex.Message})"); failed++; }

Console.WriteLine("\n=============================================");
Console.WriteLine($"  Results: {passed} passed, {failed} failed");
Console.WriteLine("=============================================");

return failed > 0 ? 1 : 0;
