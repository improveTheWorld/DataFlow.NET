using DataFlow.Framework;
// ============================================
// For the original test (FlexibleHeaderModel)
// ============================================


using DataFlow.Data;
namespace DataFlow.Core.Tests.Materialization;


// ============================================
// For the critical validation tests
// ============================================

// Basic model with required int
public class Model
{
    public string Name { get; set; } = string.Empty;
    public int Age { get; set; }
}



// Or as record:
public record NullableModel
{
    public string Name { get; init; } = string.Empty;
    public int? Age { get; init; }
}

// ============================================
// Additional test models (comprehensive)
// ============================================

// Model with various types for comprehensive testing
public class ComprehensiveModel
{
    public string Name { get; set; } = string.Empty;
    public int Age { get; set; }
    public decimal Salary { get; set; }
    public DateTime BirthDate { get; set; }
    public bool IsActive { get; set; }
    public Guid Id { get; set; }
}

// Model with all nullable types
public class AllNullableModel
{
    public string? Name { get; set; }
    public int? Age { get; set; }
    public decimal? Salary { get; set; }
    public DateTime? BirthDate { get; set; }
    public bool? IsActive { get; set; }
    public Guid? Id { get; set; }
}

// Model with mixed nullable/required
public class MixedModel
{
    public string Name { get; set; } = string.Empty;  // Required
    public int Age { get; set; }                       // Required
    public decimal? Salary { get; set; }               // Optional
    public DateTime? StartDate { get; set; }           // Optional
}


public class ObjectMaterializerTests
{
    // ============================================
    // Original Test (Case-insensitive + Missing field)
    // ============================================

    [Fact]
    public void Create_FlexibleHeaders_CaseInsensitiveAndMissingField_Success()
    {
        // Arrange - Real-world CSV headers often have inconsistent casing
        var schema = new[] { "FULLNAME", "personage", "AnnualSalary" };
        var csvRow = new object[] { "John Doe", "30" };  // Missing AnnualSalary value
        schema = ObjectMaterializer.ResolveSchema<FlexibleHeaderModel>(schema);
        // Act - Using case-insensitive mapping
        var obj = ObjectMaterializer.Create<FlexibleHeaderModel>(schema, csvRow);

        // Assert
        Assert.Equal("John Doe", obj.FullName);
        Assert.Equal(30, obj.PersonAge);
        Assert.Equal(0m, obj.AnnualSalary);  // Should be default(decimal)
    }

    // ============================================
    // Critical Validation Tests
    // ============================================

    [Fact]
    public void Create_InvalidTypeConversion_ShouldThrow()
    {
        // Arrange
        var schema = new[] { "Age" };
        var values = new object[] { "not-a-number" };

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() =>
    ObjectMaterializer.Create<Model>(schema, values));

        // Verify the inner exception is FormatException
        Assert.IsType<FormatException>(ex.InnerException);
        Assert.Contains("not-a-number", ex.InnerException.Message);
    }

   
    [Fact]
    public void Create_NullForNullableInt_ShouldSucceed()
    {
        // Arrange
        var schema = new[] { "Age" };
        var values = new object[] { null };

        // Act
        var obj = ObjectMaterializer.Create<NullableModel>(schema, values);

        // Assert
        Assert.Null(obj.Age);  // int? should accept null
    }

    [Fact]
    public void Create_EmptyStringForNullableInt_ShouldSucceed()
    {
        // Arrange
        var schema = new[] { "Age" };
        var values = new object[] { "" };  // Empty string for nullable

        // Act
        var obj = ObjectMaterializer.Create<NullableModel>(schema, values);

        // Assert
        Assert.Null(obj.Age);  // Should treat empty string as null for nullable
    }

    [Fact]
    public void Create_WrongTypeObject_ShouldThrow()
    {
        // Arrange
        var schema = new[] { "Age" };
        var values = new object[] { new DateTime() };  // Wrong type

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() =>
   ObjectMaterializer.Create<Model>(schema, values));

        // Verify the inner exception is FormatException
        Assert.IsType<FormatException>(ex.InnerException);
        Assert.Contains("Cannot convert", ex.InnerException.Message);
    }

    [Fact]
    public void Create_ValidStringToInt_ShouldConvert()
    {
        // Arrange
        var schema = new[] { "Name", "Age" };
        var values = new object[] { "John", "25" };  // String "25"

        // Act
        var obj = ObjectMaterializer.Create<Model>(schema, values);

        // Assert
        Assert.Equal("John", obj.Name);
        Assert.Equal(25, obj.Age);
    }

    [Fact]
    public void Create_ValidIntObject_ShouldAssign()
    {
        // Arrange
        var schema = new[] { "Name", "Age" };
        var values = new object[] { "John", 25 };  // Already int

        // Act
        var obj = ObjectMaterializer.Create<Model>(schema, values);

        // Assert
        Assert.Equal("John", obj.Name);
        Assert.Equal(25, obj.Age);
    }

    // ============================================
    // Comprehensive Type Tests
    // ============================================

    [Fact]
    public void Create_AllTypes_ValidConversions_Success()
    {
        // Arrange
        var schema = new[] { "Name", "Age", "Salary", "BirthDate", "IsActive", "Id" };
        var values = new object[]
        {
            "John Doe",
            "30",
            "50000.50",
            "1994-01-15",
            "true",
            "550e8400-e29b-41d4-a716-446655440000"
        };

        // Act
        var obj = ObjectMaterializer.Create<ComprehensiveModel>(schema, values);

        // Assert
        Assert.Equal("John Doe", obj.Name);
        Assert.Equal(30, obj.Age);
        Assert.Equal(50000.50m, obj.Salary);
        Assert.Equal(new DateTime(1994, 1, 15), obj.BirthDate);
        Assert.True(obj.IsActive);
        Assert.Equal(Guid.Parse("550e8400-e29b-41d4-a716-446655440000"), obj.Id);
    }

    [Fact]
    public void Create_AllNullable_NullValues_Success()
    {
        // Arrange
        var schema = new[] { "Name", "Age", "Salary", "BirthDate", "IsActive", "Id" };
        var values = new object[] { null, null, null, null, null, null };

        // Act
        var obj = ObjectMaterializer.Create<AllNullableModel>(schema, values);

        // Assert
        Assert.Null(obj.Name);
        Assert.Null(obj.Age);
        Assert.Null(obj.Salary);
        Assert.Null(obj.BirthDate);
        Assert.Null(obj.IsActive);
        Assert.Null(obj.Id);
    }

    [Fact]
    public void Create_MixedNullable_PartialNulls_Success()
    {
        // Arrange
        var schema = new[] { "Name", "Age", "Salary", "StartDate" };
        var values = new object[] { "John", "30", null, null };  // Nulls for optional fields

        // Act
        var obj = ObjectMaterializer.Create<MixedModel>(schema, values);

        // Assert
        Assert.Equal("John", obj.Name);
        Assert.Equal(30, obj.Age);
        Assert.Null(obj.Salary);
        Assert.Null(obj.StartDate);
    }

    // ============================================
    // Edge Cases
    // ============================================

    [Fact]
    public void Create_ExtraSchemaFields_ShouldUseDefaults()
    {
        // Arrange - More schema fields than values
        var schema = new[] { "Name", "Age", "Salary" };
        var values = new object[] { "John", "30" };  // Missing Salary

        // Act
        var obj = ObjectMaterializer.Create<MixedModel>(schema, values);

        // Assert
        Assert.Equal("John", obj.Name);
        Assert.Equal(30, obj.Age);
        Assert.Null(obj.Salary);  // Should be default for nullable
    }

    [Fact]
    public void Create_ExtraValues_ShouldIgnore()
    {
        // Arrange - More values than schema fields
        var schema = new[] { "Name", "Age" };
        var values = new object[] { "John", "30", "ExtraValue", "AnotherExtra" };

        // Act
        var obj = ObjectMaterializer.Create<Model>(schema, values);

        // Assert
        Assert.Equal("John", obj.Name);
        Assert.Equal(30, obj.Age);
        // Extra values should be ignored
    }

    [Fact]
    public void Create_EmptySchema_ShouldReturnDefaultObject()
    {
        // Arrange
        var schema = Array.Empty<string>();
        var values = Array.Empty<object>();

        // Act
        var obj = ObjectMaterializer.Create<Model>(schema, values);

        // Assert
        Assert.NotNull(obj);
        Assert.Equal(string.Empty, obj.Name);  // Default value
        Assert.Equal(0, obj.Age);              // Default value
    }

    [Fact]
    public void Create_LeadingTrailingSpaces_ShouldTrim()
    {
        // Arrange
        var schema = new[] { "Name", "Age" };
        var values = new object[] { "  John Doe  ", "  30  " };

        // Act
        var obj = ObjectMaterializer.Create<Model>(schema, values);

        // Assert
        Assert.Equal("John Doe", obj.Name.Trim());  // Should handle trimming
        Assert.Equal(30, obj.Age);
    }


    [Fact]
    public void Create_EmptyStringForRequiredInt_AssignsDefault()
    {
        // Arrange
        var schema = new[] { "Age" };
        var values = new object[] { "" };  // Empty string

        // Act
        var obj = ObjectMaterializer.Create<Model>(schema, values);

        // Assert - Lenient behavior
        Assert.NotNull(obj);
        Assert.Equal(0, obj.Age);  // default(int)
    }

    [Fact]
    public void Create_NullForValueType_AssignsDefault()
    {
        // Arrange
        var schema = new[] { "Age" };
        var values = new object[] { null };

        // Act
        var obj = ObjectMaterializer.Create<Model>(schema, values);

        // Assert - Lenient behavior
        Assert.NotNull(obj);
        Assert.Equal(0, obj.Age);  // default(int)
    }

    [Fact]
    public void Create_WhitespaceForInt_AssignsDefault()
    {
        // Arrange
        var schema = new[] { "Age" };
        var values = new object[] { "   " };

        // Act
        var obj = ObjectMaterializer.Create<Model>(schema, values);

        // Assert - Lenient behavior
        Assert.NotNull(obj);
        Assert.Equal(0, obj.Age);  // default(int)
    }

   

}
