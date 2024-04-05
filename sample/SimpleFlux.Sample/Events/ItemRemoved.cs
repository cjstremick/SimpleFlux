namespace SimpleFlux.Sample.Events;

[FluxEvent("ItemRemoved")]
public class ItemRemoved : FluxEvent
{
    public ItemRemoved(string sku)
        : base(sku)
    {
    }

    public ItemRemoved(string sku, int quantity) : base(sku)
    {
        Quantity = quantity;
    }

    [FluxProperty("Quantity")] public int Quantity { get; set; }
    
    public override string ToString()
    {
        return $"ItemRemoved: {Id} ({Quantity})";
    }
}