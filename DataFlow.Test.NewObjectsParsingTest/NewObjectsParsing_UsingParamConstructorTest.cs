
using System.Collections.Generic;
using Xunit;
using Xunit.Asserts.Compare;
using DataFlow.Framework;
using System.Linq;

namespace DataFlow
{
    public class NewObjectsParsing_UsingParamConstructor
    {
        class Convert
        {
            public static Dictionary<string, int> ConvertToFeedDictionary(string[] feedingOrder)
                            => new(feedingOrder.Select((x, idx) => new KeyValuePair<string, int>(x.Trim(), idx)));
        }

        class All_Properties : IHasSchema
        {
            public int Property { get; set; }
            public int Property1 { get; set; }

            readonly static string[] _feedingOrder = { "Property", "Property1" };
            public Dictionary<string, int> GetSchema()
            {
                return Convert.ConvertToFeedDictionary(_feedingOrder);
            }
        }

        class All_Fields : IHasSchema
        {
            public int Field;
            public int Field1;

            readonly static string[] _feedingOrder = { "Field", "Field1" };
            public Dictionary<string, int> GetSchema()
            {
                return Convert.ConvertToFeedDictionary(_feedingOrder);
            }
        }


        class Mix_Field_Property : IHasSchema
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
            Mix_Field_PropertyWithConstructor? parsed = ObjectMaterializer.Create<Mix_Field_PropertyWithConstructor>("True ;2 ;yes".Split(';', 3));
            Mix_Field_PropertyWithConstructor expected = new Mix_Field_PropertyWithConstructor(true, 2, "yes");
            DeepAssert.Equal(expected, parsed);

        }
    }


}


