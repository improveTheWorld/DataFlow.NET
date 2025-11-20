namespace DataFlow.Framework;

internal static class MemberMaterializer
{


    // In MemberMaterializer.cs - make this internal and prefer ObjectMaterializer's cached version
    



    public static void FeedUsingInternalOrder<T>(ref T objectToFill,  params object[] parameters)
    {
        if (objectToFill is IHasSchema withSchema)
        {
            FeedUsingSchema(ref objectToFill, withSchema.GetDictSchema(), parameters);
        }
        else //suppose FeedOredered ( definition of attribute with [order] tag
        {
            FeedOrdered(ref objectToFill, parameters);
        }

    }


    // In MemberMaterializer.cs - simplify FeedUsingSchema
   public static void FeedUsingSchema<T>(
    ref T obj,
    string[] schema,
    object?[] values)
{
    if (values == null) throw new ArgumentNullException(nameof(values));
    
    var plan = MemberMaterializationPlanner.Get<T>();
    
    // Direct inline mapping - no cache, no allocations
    foreach (ref readonly var member in plan.Members.AsSpan())
    {
        for (int i = 0; i < schema.Length; i++)
        {
            if (member.Name.Equals(schema[i]))
            {
                if ((uint)i < (uint)values.Length)
                    member.Set(obj, values[i]);
                break;
            }
        }
    }
}
    public static void FeedUsingSchema<T>(
          ref T obj,
          Dictionary<string, int> schemaDict,
          object?[] values)
    {
        if (values == null) throw new ArgumentNullException(nameof(values));



        var plan = MemberMaterializationPlanner.Get<T>();

        foreach (ref readonly var member in plan.Members.AsSpan())
        {
            if (schemaDict.TryGetValue(member.Name, out var idx))
            {
                if ((uint)idx < (uint)values.Length)
                    member.Set(obj, values[idx]);
            }
        }
    }
    // Feed by [Order] attributes only
    public static void FeedOrdered<T>(ref T obj, object?[] values)
    {
        if (values == null) throw new ArgumentNullException(nameof(values));

        var plan = MemberMaterializationPlanner.Get<T>();
        // Gather ordered members into a temporary index array once per call (cheap)
        // Members array is small; iterate and assign
        int vIndex = 0;
        for (int i = 0; i < plan.Members.Length && vIndex < values.Length; i++)
        {
            var m = plan.Members[i];
            if (m.OrderIndex >= 0)
            {
                m.Set(obj, values[vIndex++]);
            }
        }
        // No return needed when using ref
    }

}
  
