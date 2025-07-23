using System.Runtime.CompilerServices;


namespace DataFlow.Framework;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, Inherited = false, AllowMultiple = false)]
public sealed class OrderAttribute : Attribute
{
    readonly int order_;
    public OrderAttribute([CallerLineNumber]int order = 0)
    {
        order_ = order;
    }

    public int Order { get { return order_; } }
}
  
