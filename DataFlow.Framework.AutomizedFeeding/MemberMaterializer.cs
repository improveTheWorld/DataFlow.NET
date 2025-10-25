namespace DataFlow.Framework;

internal static class MemberMaterializer
{
    public static Dictionary<string, int> GetSchemaDictionary(string[] schema, StringComparer? comparer = null)
    {
        comparer ??= StringComparer.OrdinalIgnoreCase;
        var dict = new Dictionary<string, int>(schema.Length, comparer);
        for (int i = 0; i < schema.Length; i++)
            dict[schema[i]] = i;
        return dict;
    }


    public static T FeedUsingInternalOrder<T>( T objectToFill,  params object[] parameters)
    {
        if (objectToFill is IHasSchema withSchema)
        {
            return FeedUsingSchema(objectToFill, withSchema.GetSchema(), parameters);
        }
        else //suppose FeedOredered ( definition of attribute with [order] tag
        {
            return FeedOrdered(objectToFill, parameters);
        }

    }


    public static T FeedUsingSchema<T>(
        T obj,
        Dictionary<string, int> schemaDict,
        object?[] values,
        bool caseInsensitiveHeaders = true)
    {
        if (obj == null) throw new ArgumentNullException(nameof(obj));
        if (values == null) throw new ArgumentNullException(nameof(values));

        var plan = MemberMaterializationPlanner.Get<T>(caseInsensitiveHeaders);

        // Normalize schema dictionary to match plan's comparer
        Dictionary<string, int> normalizedSchema = schemaDict;
        if (schemaDict.Comparer != plan.NameComparer)
        {
            normalizedSchema = new Dictionary<string, int>(schemaDict, plan.NameComparer);
        }

        foreach (ref readonly var member in plan.Members.AsSpan())
        {
            if (normalizedSchema.TryGetValue(member.Name, out var idx))
            {
                if ((uint)idx < (uint)values.Length)
                    member.Set(obj, values[idx]);
            }
        }
        return obj;
    }
    


    // Feed by [Order] attributes only
    public static T FeedOrdered<T>(T obj, object?[] values)
    {
        if (obj == null) throw new ArgumentNullException(nameof(obj));
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
        return obj;
    }

}
  
