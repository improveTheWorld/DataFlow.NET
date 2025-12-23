namespace DataFlow.Framework;

public class ArgRequirement
{
    [Order] public string ArgName;
    [Order] public string ShortName;
    [Order] public string LongName;
    [Order] public bool IsMandatory;
    [Order] public bool IsFlag;
    [Order] public string DefaultValue;
    [Order] public string Description;

    public ArgRequirement(string argName, string shortName, string longName, string defaultValue, string description = "", bool isMandatory = false, bool isFlag = false)
    {
        ArgName = argName;
        ShortName = shortName;
        LongName = longName;
        IsMandatory = isMandatory;
        DefaultValue = defaultValue;
        Description = description;
        IsFlag = isFlag;
    }


    public override string ToString()
    {
        return $" Parameter :{ArgName}, ShortName :{ShortName}, LongName :{LongName}, required :{IsMandatory}, DefaultValue :{DefaultValue}, HelpText :{Description}, ";
    }
}
public static class InvokePrivateMethod
{
    public static object? Invoke(this IEnumerable<ArgRequirement> instance, string method, params object[] parameteres)
    {
        return instance.GetType().GetMethod(method)!.Invoke(instance, parameteres);
    }

}
