namespace DataFlow.Extensions;

public static class InvokePrivateMethod
{
    public static object? Invoke(this object instance, string method, params object[] parameteres)
    {
        return instance.GetType().GetMethod(method).Invoke(instance,parameteres);
    }
}