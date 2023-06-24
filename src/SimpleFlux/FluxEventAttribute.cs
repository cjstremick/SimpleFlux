namespace SimpleFlux;

[AttributeUsage(AttributeTargets.Class)]
public class FluxEventAttribute : Attribute
{
    public FluxEventAttribute(string name)
    {
        Name = name;
    }

    public string Name { get; }
}