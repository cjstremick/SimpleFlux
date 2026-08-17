using System.Reflection;

namespace SimpleFlux;

/// <summary>
/// Fluent event registration helpers for the SimpleFlux builder.
/// </summary>
/// <remarks>
/// Calling any of these opts into explicit event discovery — once anything is
/// registered, <see cref="FluxStore"/> knows only the registered types/assemblies
/// (no hidden full-assembly scan). Registering nothing keeps the previous behavior:
/// all loaded assemblies are scanned.
/// </remarks>
public static class FluxBuilderExtensions
{
    /// <summary>
    /// Registers a single event type explicitly (no scanning).
    /// </summary>
    /// <typeparam name="T">The event type to register.</typeparam>
    /// <param name="builder">The SimpleFlux builder from <c>AddSimpleFlux()</c>.</param>
    /// <returns>The builder, for chaining.</returns>
    public static IFluxBuilder WithEvent<T>(this IFluxBuilder builder)
        where T : FluxEvent
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.WithEvent(typeof(T));
    }

    /// <summary>
    /// Registers a single event type explicitly (no scanning).
    /// </summary>
    /// <param name="builder">The SimpleFlux builder from <c>AddSimpleFlux()</c>.</param>
    /// <param name="eventType">The event type; must derive from <see cref="FluxEvent"/>.</param>
    /// <returns>The builder, for chaining.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="eventType"/> is not a <see cref="FluxEvent"/> subclass.</exception>
    public static IFluxBuilder WithEvent(this IFluxBuilder builder, Type eventType)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(eventType);
        if (!typeof(FluxEvent).IsAssignableFrom(eventType))
            throw new ArgumentException($"Type '{eventType.FullName}' must derive from {nameof(FluxEvent)}.", nameof(eventType));

        builder.Options.EventTypes.Add(eventType);
        return builder;
    }

    /// <summary>
    /// Registers all <see cref="FluxEvent"/> types found in the assembly containing the marker type.
    /// </summary>
    /// <typeparam name="TMarker">Any type from the assembly to scan (commonly an event or a marker interface).</typeparam>
    /// <param name="builder">The SimpleFlux builder from <c>AddSimpleFlux()</c>.</param>
    /// <returns>The builder, for chaining.</returns>
    public static IFluxBuilder WithAssemblyEvents<TMarker>(this IFluxBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.WithAssemblyEvents(typeof(TMarker).Assembly);
    }

    /// <summary>
    /// Registers all <see cref="FluxEvent"/> types found in the given assembly.
    /// </summary>
    /// <param name="builder">The SimpleFlux builder from <c>AddSimpleFlux()</c>.</param>
    /// <param name="assembly">The assembly to scan for event types.</param>
    /// <returns>The builder, for chaining.</returns>
    public static IFluxBuilder WithAssemblyEvents(this IFluxBuilder builder, Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(assembly);

        builder.Options.EventAssemblies.Add(assembly);
        return builder;
    }
}