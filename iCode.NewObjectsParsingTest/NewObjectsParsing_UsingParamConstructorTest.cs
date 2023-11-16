using System;
using System.Collections.Generic;
using Xunit;
using Xunit.Asserts.Compare;
using Moq;
using iCode.Extentions.NewObjectsParsing;
using iCode.Framework.AutomizedFeeding;

namespace iCode
{
    public class NewObjectsParsing_UsingParamConstructor
    {


        //internal class AllFieldsAreString
        //{
        //    public string Param0;
        //    public string Param1;
        //    public string Param2;
        //    public string Param3;
        //    public string Param4;

        //    public AllFieldsAreString(string param0, string param1, string param2, string param3, string param4)
        //    {
        //        Param0 = param0;
        //        Param1 = param1;
        //        Param2 = param2;
        //        Param3 = param3;
        //        Param4 = param4;
        //    }
        //}

        //internal class MixedTypesField 
        //{
        //    public string Param0;
        //    public string Param1;
        //    public int ParamInt;
        //    public string Param3;
        //    public bool ParamBool;

        //    public MixedTypesField(string param0, string param1, int param2, string param3, bool param4)
        //    {
        //        Param0 = param0;
        //        Param1 = param1;
        //        ParamInt = param2;
        //        Param3 = param3;
        //        ParamBool = param4;
        //    }

        //}


        //MixedTypesField MixedTypesFieldSetUp(string param0, string param1, int param2, string param3, bool param4)
        //{

        //    return new MixedTypesField(param0, param1, param2, param3, param4);
        //    //return new MixedTypesField
        //    //{
        //    //    Param0 = param0,
        //    //    Param1 = param1,
        //    //    ParamInt = param2,
        //    //    Param3 = param3,
        //    //    ParamBool = param4
        //    //};
        //}

        //AllFieldsAreString AllFieldsAreStringSetUp(string param0, string param1, string param2, string param3, string param4)
        //{
        //    return new AllFieldsAreString(param0, param1, param2, param3, param4);
        //    //return new AllFieldsAreString
        //    //{
        //    //    Param0 = param0,
        //    //    Param1 = param1,
        //    //    Param2 = param2,
        //    //    Param3 = param3,
        //    //    Param4 = param4
        //    //};


        //}

        class Convert
        {
            public static Dictionary<string, int> ConvertToFeedDictionary(string[] feedingOrder)
            {

                Dictionary<string, int> retValue = new Dictionary<string, int>();
                int index = 0;
                foreach (string fieldName in feedingOrder)
                {
                    retValue.Add(fieldName.Trim(), index);
                    index++;
                }
                return retValue;
            }
        }

        class All_Properties : IFeedingInternalOrder
        {
            public int Property { get; set; }
            public int Property1 { get; set; }

            readonly static string[] _feedingOrder = { "Property", "Property1" };
            public Dictionary<string, int> GetFeedingDictionary()
            {
                return Convert.ConvertToFeedDictionary(_feedingOrder);
            }
        }

        class All_Fields : IFeedingInternalOrder
        {
            public int Field;
            public int Field1;

            readonly static string[] _feedingOrder = { "Field", "Field1" };
            public Dictionary<string, int> GetFeedingDictionary()
            {
                return Convert.ConvertToFeedDictionary(_feedingOrder);
            }
        }


        class Mix_Field_Property : IFeedingInternalOrder
        {
            public int intField;
            public string StringProperty { get; set; }
            public bool FieldBool;

            readonly static string[] _feedingOrder = { "intField", "StringProperty", "FieldBool" };
            public Dictionary<string, int> GetFeedingDictionary()
            {
                return Convert.ConvertToFeedDictionary(_feedingOrder);
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

        [Fact]
        void Parse_StringFields_returnsAllFieldsAreFilled()
        {
            Mix_Field_PropertyWithConstructor parsed = ("True ;2 ;yes").AsObject<Mix_Field_PropertyWithConstructor>(";");
            Mix_Field_PropertyWithConstructor expected = new Mix_Field_PropertyWithConstructor(true, 2, "yes");
            DeepAssert.Equal(expected, parsed);

        }

       
       

        //[Fact]
        //void Parse_StringFields_returnsLastFieldNull()
        //{
        //    AllFieldsAreString expected = AllFieldsAreStringSetUp("s0", "s1", "s2", "s3", null);

        //    ParserToTest = new CSVLineParser(',', "Param0, Param1, Param2 , Param3, Param4");
        //    DeepAssert.Equal(expected,ParserToTest.Parse<AllFieldsAreString>("s0, s1,s2,s3"));

        //}

        //[Fact]
        //void Parse_StringFields_returnsFirstFielddNull()
        //{
        //    AllFieldsAreString expected =  AllFieldsAreStringSetUp(null, "s1", "s2", "s3", "s4");

        //    ParserToTest = new CSVLineParser(',', "Param0, Param1, Param2 , Param3, Param4");
        //    DeepAssert.Equal(expected,ParserToTest.Parse<AllFieldsAreString>(", s1,s2,s3, s4 "));

        //}

        //[Fact]
        //void Parse_StringFields_returnsSecondFielddNull()
        //{
        //    AllFieldsAreString expected =  AllFieldsAreStringSetUp("s0", null, "s2", "s3", "s4");

        //    ParserToTest = new CSVLineParser(',', "Param0, Param1, Param2 , Param3, Param4");
        //    DeepAssert.Equal(expected , ParserToTest.Parse<AllFieldsAreString>("s0, ,s2,s3, s4 "));
        //}

        //[Fact]
        //void Parse_MixedTypesFields_returnsAllFieldsValues()
        //{
        //    ParserToTest = new CSVLineParser(',', "Param0, Param1, ParamInt , Param3, ParamBool");

        //    MixedTypesField expected = MixedTypesFieldSetUp("0", "1", 2, "3", true);
        //    MixedTypesField computed = ParserToTest.Parse<MixedTypesField>("0,1,2,3,True");
        //    DeepAssert.Equal(expected,computed);

        //}

        //[Fact]
        //void Parse_MixedTypesFields_ThrowsError()
        //{
        //    ParserToTest = new CSVLineParser(',', "Param0, Param1, ParamInt , Param3, ParamBool");

        //    try
        //    {
        //        ParserToTest.Parse<MixedTypesField>("s0,s1,s2,s3,s4");
        //    }
        //    catch (System.FormatException)
        //    {
        //        Assert.True(true);
        //        return;
        //    }

        //    Assert.True(false);

        //}
    }


}


