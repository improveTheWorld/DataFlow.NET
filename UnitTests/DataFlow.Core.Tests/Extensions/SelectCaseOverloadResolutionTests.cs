using DataFlow;

namespace DataFlow.Core.Tests.Extensions;

/// <summary>
/// Tests to explore when SelectCase/AllCases require explicit type parameters.
/// Related to overload resolution between single-type and multi-type variants.
/// </summary>
public class SelectCaseOverloadResolutionTests
{
    #region Cases that work WITHOUT explicit types

    [Fact]
    public void SelectCase_OneSelector_NoExplicitTypesNeeded()
    {
        // Arrange
        var items = new[] { "a", "b", "c" };

        // Act - With 1 selector, no ambiguity with SelectCase<T, R1, R2>
        var result = items
            .Cases(s => s == "a")
            .SelectCase(s => s.ToUpper())  // Only 1 selector - uses params version
            .AllCases()
            .ToList();

        // Assert
        Assert.Single(result);
        Assert.Equal("A", result[0]);
    }

    [Fact]
    public void SelectCase_ThreeSelectors_NoExplicitTypesNeeded()
    {
        // Arrange
        var items = new[] { "a", "b", "c" };

        // Act - With 3 selectors, no match with SelectCase<T, R1, R2> (needs exactly 2)
        var result = items
            .Cases(s => s == "a", s => s == "b")
            .SelectCase(
                s => s.ToUpper(),
                s => s.ToLower(),
                s => s  // 3rd selector for supra
            )
            .AllCases()
            .ToList();

        // Assert
        Assert.Equal(3, result.Count);
    }

    [Fact]
    public void SelectCase_TwoSelectors_NonTupleReturn_NoExplicitTypesNeeded()
    {
        // Arrange
        var items = new[] { "a", "b" };

        // Act - Returns string, not a tuple, so no ambiguity
        var result = items
            .Cases(s => s == "a")
            .SelectCase(
                s => s.ToUpper(),
                s => s.ToLower()
            )
            .AllCases()
            .ToList();

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Equal("A", result[0]);
        Assert.Equal("b", result[1]);
    }

    #endregion

    #region Cases that REQUIRE explicit types (the problematic ones)

    [Fact]
    public void SelectCase_TwoSelectors_TupleReturn_RequiresExplicitTypes()
    {
        // Arrange
        var items = new[] { ("key", 1), ("key", 2) };

        // Act - Returns a tuple (string, int), and we have 2 selectors
        // This could be confused with SelectCase<T, R1, R2> where result is (R1?, R2?)
        var result = items
            .Cases(x => x.Item2 > 1)
            // Without explicit types, this WOULD fail to compile because:
            // - SelectCase<T, R1, R2> is preferred (2 explicit params vs params array)
            // - AllCases<T, R1, R2> returns IEnumerable<object?>
            .SelectCase(new Func<(string, int), (string, string)>[] {
                x => ("high", x.Item1),
                x => ("low", x.Item1)
            })
            .AllCases<(string, int), (string, string)>()
            .ToList();

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Equal(("low", "key"), result[0]);
        Assert.Equal(("high", "key"), result[1]);
    }

    [Fact]
    public void SelectCase_TwoSelectors_ValueTuple2Elements_RequiresExplicitTypes()
    {
        // Arrange - Similar to RegexTokenizer scenario
        var slices = new[] {
            ("groupA", (0, 5)),
            ("groupB", (5, 10))
        };

        // Act - This is the exact pattern that breaks in RegexTokenizer
        var result = slices
            .Cases(x => x.Item1 == "groupA")
            .SelectCase(new Func<(string, (int, int)), (string, string)>[] {
                x => ("MATCHED", x.Item1),
                x => ("unmatched", x.Item1)
            })
            .AllCases<(string, (int, int)), (string, string)>()
            .ToList();

        // Assert
        Assert.Equal(2, result.Count);
    }

    #endregion

    #region What multi-type SelectCase is ACTUALLY for

    [Fact]
    public void SelectCase_MultiType_DifferentReturnTypes_IsTheIntendedUseCase()
    {
        // Arrange
        var items = new[] { "error", "warning", "info" };

        // Act - This is what multi-type SelectCase is designed for:
        // Different branches return DIFFERENT types
        var result = items
            .Cases(
                s => s == "error",
                s => s == "warning"
            )
            .SelectCases<string, int, string>(  // Explicit: R1=int, R2=string
                error => 500,           // Returns int
                warning => "WARNING!"   // Returns string (different type!)
            )
            .AllCases()  // Returns IEnumerable<object?> because types differ
            .ToList();

        // Assert
        Assert.Equal(2, result.Count);
        // Results are boxed to object
        Assert.Equal(500, result[0]);
        Assert.Equal("WARNING!", result[1]);
    }

    #endregion

    #region Proposed fix: Flattened tuples would solve this

    // The issue is that:
    // - SelectCase<T, R> returns: (int, T, R?)
    // - SelectCase<T, R1, R2> returns: (int, T, (R1?, R2?))
    //
    // When R is a tuple like (string, string), it matches (R1?, R2?) pattern!
    //
    // PROPOSED FIX: Flatten multi-type returns:
    // - SelectCase<T, R1, R2> returns: (int, T, R1?, R2?) - 4 elements
    //
    // Then:
    // - Single-type: (int, T, R?) - 3 elements
    // - Multi-type 2: (int, T, R1?, R2?) - 4 elements
    // - Multi-type 3: (int, T, R1?, R2?, R3?) - 5 elements
    //
    // These are DISTINCT tuple arities - no ambiguity possible!

    #endregion
}
