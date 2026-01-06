using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace DataFlow.Framework;

/// <summary>
/// Internal helper to centralize constructor resolution logic and caching.
/// </summary>
internal static class ConstructorHelper<T>
{
    // Lazy singleton pattern for the primary constructor (thread-safe by default)
    // IMPORTANT: Skip copy constructors (ctor with single parameter of same type) - common in records
    private static readonly Lazy<ConstructorInfo?> _primaryCtor = new(() =>
        typeof(T).GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                 // Filter out copy constructors (records have these synthesized)
                 .Where(c => !IsCopyConstructor(c))
                 .OrderByDescending(c => c.GetParameters().Length)
                 .FirstOrDefault());

    /// <summary>
    /// Checks if a constructor is a copy constructor (single parameter of same type).
    /// </summary>
    private static bool IsCopyConstructor(ConstructorInfo ctor)
    {
        var parms = ctor.GetParameters();
        return parms.Length == 1 && parms[0].ParameterType == typeof(T);
    }


    /// <summary>
    /// Gets the "primary" constructor (the one with the most parameters), or null if none exist.
    /// </summary>
    public static ConstructorInfo? PrimaryCtor => _primaryCtor.Value;

    /// <summary>
    /// Builds a parameter map for a given constructor and schema dictionary.
    /// Maps each constructor parameter to a source index in the value array.
    /// </summary>
    public static (int valueIndex, Type paramType)[] BuildParamMap(
        ConstructorInfo ctor,
        Dictionary<string, int> schemaDict)
    {
        var parms = ctor.GetParameters();
        var map = new (int, Type)[parms.Length];

        for (int i = 0; i < parms.Length; i++)
        {
            var p = parms[i];
            var name = p.Name ?? string.Empty;

            // Map parameter name to input value index using the schema dictionary
            int idx = schemaDict.TryGetValue(name, out var colIndex) ? colIndex : -1;

            map[i] = (idx, p.ParameterType);
        }

        return map;
    }
}
