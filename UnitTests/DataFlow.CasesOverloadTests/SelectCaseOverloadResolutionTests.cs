using DataFlow;

namespace DataFlow.CasesOverloadTests;

/// <summary>
/// Tests to verify that the SelectCase/SelectCases API works correctly.
/// After the fix (renaming + flattening), there should be NO overload ambiguity.
/// </summary>
public class SelectCaseOverloadResolutionTests
{
    #region Single-Type SelectCase (SelectCase with params)

    [Fact]
    public void SelectCase_OneSelector_Works()
    {
        // Arrange
        var items = new[] { "a", "b", "c" };

        // Act
        var result = items
            .Cases(s => s == "a")
            .SelectCase(s => s.ToUpper())
            .AllCases()
            .ToList();

        // Assert
        Assert.Single(result);
        Assert.Equal("A", result[0]);
    }

    [Fact]
    public void SelectCase_TwoSelectors_Works()
    {
        // Arrange
        var items = new[] { "a", "b" };

        // Act - With fix, 2 selectors work fine with single-type SelectCase
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

    [Fact]
    public void SelectCase_ThreeSelectors_Works()
    {
        // Arrange
        var items = new[] { "a", "b", "c" };

        // Act
        var result = items
            .Cases(s => s == "a", s => s == "b")
            .SelectCase(
                s => s.ToUpper(),
                s => s.ToLower(),
                s => s  // supra
            )
            .AllCases()
            .ToList();

        // Assert
        Assert.Equal(3, result.Count);
    }

    [Fact]
    public void SelectCase_TwoSelectors_TupleReturn_WorksWithFix()
    {
        // Arrange - Similar to RegexTokenizer scenario
        var slices = new[] {
            ("groupA", (0, 5)),
            ("groupB", (5, 10))
        };

        // Act - With the fix (renaming + flattening), this works without explicit types!
        var result = slices
            .Cases(x => x.Item1 == "groupA")
            .SelectCase(
                x => ("MATCHED", x.Item1),
                x => ("unmatched", x.Item1)
            )
            .AllCases()
            .ToList();

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Equal(("MATCHED", "groupA"), result[0]);
        Assert.Equal(("unmatched", "groupB"), result[1]);
    }

    #endregion

    #region Multi-Type SelectCases (returns different types per branch)

    [Fact]
    public void SelectCases_MultiType_DifferentReturnTypes()
    {
        // Arrange
        var items = new[] { "error", "warning", "info" };

        // Act - SelectCases (with 's') for multi-type branches
        var result = items
            .Cases(
                s => s == "error",
                s => s == "warning"
            )
            .SelectCases(           // Note: SelectCases not SelectCase
                error => 500,       // Returns int
                warning => "WARN!"  // Returns string
            )
            .AllCases()             // Returns IEnumerable<object?>
            .ToList();

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Equal(500, result[0]);
        Assert.Equal("WARN!", result[1]);
    }

    #endregion
}
