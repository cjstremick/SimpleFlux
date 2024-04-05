using SimpleFlux.Sample.Events;

namespace SimpleFlux.Sample;

public class ItemInventoryProjection : FluxProjection
{
    public ItemInventoryProjection(string id)
        : base(id)
    {
    }

    public int Quantity { get; private set; }

    public void Apply(ItemAdded e)
    {
        Quantity += e.Quantity;
    }

    public void Apply(ItemRemoved e)
    {
        Quantity -= e.Quantity;
        Quantity = Math.Max(0, Quantity);
    }
}