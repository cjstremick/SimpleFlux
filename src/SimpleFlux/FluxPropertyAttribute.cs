namespace SimpleFlux;

/// <summary>
/// Marks a property to be persisted with the event, under the given column name.
/// </summary>
/// <remarks>
/// Only properties decorated with this attribute are stored and restored. The
/// <see cref="Name"/> is used as the column name in Azure Table Storage, so it must
/// be unique within the event type. The column value must be a type supported by
/// Azure Table Storage (strings, numbers, booleans, dates, GUIDs, byte arrays).
/// </remarks>
[AttributeUsage(AttributeTargets.Property)]
public class FluxPropertyAttribute : Attribute
{
    /// <summary>
    /// Creates the attribute with the given column name.
    /// </summary>
    /// <param name="name">The column name used in Azure Table Storage.</param>
    public FluxPropertyAttribute(string name)
    {
        Name = name;
    }

    /// <summary>
    /// The column name used in Azure Table Storage.
    /// </summary>
    public string Name { get; }
}