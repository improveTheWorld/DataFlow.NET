using System;
using System.Collections.Generic;
using Xunit;
using Xunit.Asserts.Compare;
using Moq;
using iCode.Extensions;
using iCode.Framework;
using System.Linq;

namespace iCode.Tests
{
    public class NewObjectsParsing_UsingZeroParamConstructor
    {
        class Convert
        {
            public static Dictionary<string, int> ConvertToFeedDictionary(string[] feedingOrder)
                => new(feedingOrder.Select((x, idx) => new KeyValuePair<string, int>(x.Trim(), idx)));
           
        }
        class All_Properties : IWithIntenalSchema
        {
            public int Property { get; set; }
            public int Property1 { get; set; }

            readonly static string[] _feedingOrder = { "Property", "Property1" };
            public Dictionary<string, int> GetSchema()
            {
                return Convert.ConvertToFeedDictionary(_feedingOrder);
            }
        }

        class All_Fields : IWithIntenalSchema
        {
            public int Field;
            public int Field1;

            readonly static string[] _feedingOrder = { "Field", "Field1" };
            public Dictionary<string, int> GetSchema()
            {
                return Convert.ConvertToFeedDictionary(_feedingOrder);
            }
        }


        class Mix_Field_Property : IWithIntenalSchema
        {
            public int intField;
            public string StringProperty { get; set; }
            public bool FieldBool;
            
            readonly static string[] _feedingOrder = { "intField", "StringProperty", "FieldBool" };
            public Dictionary<string, int> GetSchema()
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




        class Mix_Field_Oredered
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
        void Parse_IFeedable()
        {

            Mix_Field_Property parsed = NEW.GetNew< Mix_Field_Property>("2;yes;True".Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries));
            Mix_Field_Property expected = new Mix_Field_Property { FieldBool = true, intField = 2, StringProperty = "yes" };
            DeepAssert.Equal(expected, parsed);



        }

        [Fact]
        void Parse_WithFeedingOrder()
        {

            Mix_Field_Property expected = new Mix_Field_Property { FieldBool = true, intField = 2, StringProperty = "yes" };            
            var parsed = NEW.GetNew<Mix_Field_Property>(" yes;2 ;True  ".Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries),  "StringProperty", "intField", "FieldBool");
            DeepAssert.Equal(expected, parsed);

        }

        [Fact]
        void Parse_OrderObject()
        {
           
            Mix_Field_Oredered expected = new Mix_Field_Oredered { FieldBool = true, intField = 2, StringProperty = "yes" };
            var parsed = NEW.GetNew<Mix_Field_Oredered>(" 2 ;yes; True  ".Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries));

            DeepAssert.Equal(expected, parsed);

        }



    }
}
