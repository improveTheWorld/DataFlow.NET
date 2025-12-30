namespace DataFlow.Framework;

internal static class MemberMaterializer
{


    // In MemberMaterializer.cs - make this internal and prefer ObjectMaterializer's cached version







    // In MemberMaterializer.cs - simplify FeedUsingSchema
    public static void FeedUsingSchema<T>(
     ref T obj,
     string[] schema,
     object?[] values)
    {
        if (values == null) throw new ArgumentNullException(nameof(values));

        // Use cached plan (O(1) access) instead of O(n*m) loop
        var plan = MemberMaterializationPlanner.Get<T>();
        var action = plan.GetSchemaAction(schema);
        action(obj, values);
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

