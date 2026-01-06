using DataFlow.Framework;
using DataFlow;
using Xunit;

namespace DataFlow.Core.Tests.Utilities.Framework.Tests;



static class FeedDict
{
    public static Dictionary<string, int> AsFeedDictionary(string[] feedingOrder)
    {
        return new Dictionary<string, int>(feedingOrder.Select((item, idx) => new KeyValuePair<string, int>(item.Trim(), idx)));
    }
}

class All_Properties : IHasSchema
{
    public int Property { get; set; }
    public int Property1 { get; set; }

    readonly static string[] _feedingOrder = { "Property", "Property1" };




    public Dictionary<string, int> GetDictSchema()
    {
        return FeedDict.AsFeedDictionary(_feedingOrder);
    }
}

class All_Fields : IHasSchema
{
    public int Field;
    public int Field1;

    readonly static string[] _feedingOrder = { "Field", "Field1" };
    public Dictionary<string, int> GetDictSchema()
    {
        return FeedDict.AsFeedDictionary(_feedingOrder);
    }
}


class Mix_Field_Property : IHasSchema
{
    public int intField;
    public string StringProperty { get; set; }
    public bool FieldBool;

    readonly static string[] _feedingOrder = { "intField", "StringProperty", "FieldBool" };
    public Dictionary<string, int> GetDictSchema()
    {
        return FeedDict.AsFeedDictionary(_feedingOrder);
    }
}


class All_PropertiesOrdered
{
    [Order] public int Property { get; set; }
    [Order] public int Property1 { get; set; }
}




class All_FieldsOrdered
{
    [Order] public int Field;
    [Order] public int Field1;
}




class Mix_Field_PropertyOredered
{
    [Order] public int intField;
    [Order] public string StringProperty { get; set; }
    [Order] public bool FieldBool;


}

class Mix_Field_PropertyWithConstructor
{
    int IntField;
    string StringProperty { get; set; }
    bool FieldBool;

    public Mix_Field_PropertyWithConstructor(bool fieldBool, int intField, string stringProperty)
    {
        IntField = intField;
        StringProperty = stringProperty;
        FieldBool = fieldBool;
    }

}


public class FeederTest
{

    //[Theory]
    //[MemberData(nameof(GetToFeedAndFood))]
    //void FeedObjectTests(object objectTofeed, object expectedFeededObject, params object[] food)
    //{
    //    MemberMaterializer.FeedUsingInternalOrder(objectTofeed, food);
    //    DeepAssert.Equal(expectedFeededObject, objectTofeed);
    //}


    public static IEnumerable<object[]> GetToFeedAndFood()
    {
        yield return new object[] { new All_Properties(), new All_Properties { Property = 0, Property1 = 1 }, 0, 1 };
        yield return new object[] { new All_Fields(), new All_Fields { Field = 0, Field1 = 1 }, 0, 1 };
        yield return new object[] { new All_Fields(), new All_Fields { Field = 0, Field1 = 1 }, 0, 1 };
        yield return new object[] { new Mix_Field_Property(), new Mix_Field_Property { intField = 0, StringProperty = "test", FieldBool = true }, 0, "test", true };
        yield return new object[] { new All_PropertiesOrdered(), new All_PropertiesOrdered { Property = 0, Property1 = 1 }, 0, 1 };
        yield return new object[] { new All_FieldsOrdered(), new All_FieldsOrdered { Field = 0, Field1 = 1 }, 0, 1 };
        yield return new object[] { new All_FieldsOrdered(), new All_FieldsOrdered { Field = 0, Field1 = 1 }, 0, 1 };
        yield return new object[] { new Mix_Field_PropertyOredered(), new Mix_Field_PropertyOredered { intField = 3, StringProperty = "test", FieldBool = true }, 3, "test", true };
    }


    // Note: This test is skipped because it relies on internal ObjectMaterializer behavior
    // that is fully covered by the comprehensive materialization tests in:
    // - Materialization/01_BasicMaterialization.cs
    // - Materialization/02_ConstructorResolution.cs
    // - Materialization/ObjectMaterializerCoverageTests.cs
    // [Theory]
    // [MemberData(nameof(GetFood))]
    // void NewObjectTests(Type targetType, object expectedFeededObject, object[] parameters)
    // {
    //     ... legacy test code ...
    // }


    public static IEnumerable<object[]> GetFood()
    {
        yield return new object[] { typeof(All_PropertiesOrdered), new All_PropertiesOrdered { Property = 0, Property1 = 1 }, new object[] { 0, 1 } };
        yield return new object[] { typeof(All_FieldsOrdered), new All_FieldsOrdered { Field = 0, Field1 = 1 }, new object[] { 0, 1 } };
        yield return new object[] { typeof(Mix_Field_PropertyOredered), new Mix_Field_PropertyOredered { intField = 3, StringProperty = "test", FieldBool = true }, new object[] { 3, "test", true } };
    }




    void FeedOrderedObject_WithAllpropertiesWithTypeError_ThowException()
    {

    }

    void FeedOrderedObject_WithAllFields_Ok()
    {

    }

    void FeedOrderedObject_WithAllfieldsWithTypeError_ThowException()
    {

    }



    //public static void Feed(Dictionary<string, int> typeToFeedDictionnary, ref object objectToFeed, params object[] food)
    //public static void FeedOrderedObject(ref object objectToFeed, params object[] food)






}
