namespace SimpleFlux;

[AttributeUsage(AttributeTargets.Property)]
public class FluxPropertyAttribute : Attribute
{
    public FluxPropertyAttribute(string name)
    {
        Name = name;
    }

    public string Name { get; }
}