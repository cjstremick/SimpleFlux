namespace SimpleFlux.Sample.Events;

[FluxEvent("ItemAdded")]
public class ItemAdded : FluxEvent
{
    public ItemAdded(string sku)
        : base(sku)
    {
    }

    public ItemAdded(string sku, int quatity) 
        : base(sku)
    {
        Quantity = quatity;
    }

    [FluxProperty("Quantity")] public int Quantity { get; set; }
    
    public override string ToString()
    {
        return $"ItemAdded: {Id} ({Quantity})";
    }
}