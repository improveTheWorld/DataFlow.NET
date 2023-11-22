namespace iCode.Extensions.ObjecExtensions.InvokePrivateMethod
{
    public static class InvokePrivateMethod
    {
        public static object? iInvoke(this object instance, string method, params object[] parameteres)
        {
            return instance.GetType().GetMethod(method).Invoke(instance,parameteres);
        }
    }
}