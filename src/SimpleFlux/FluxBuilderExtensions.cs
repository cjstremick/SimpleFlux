using System.Reflection;

namespace SimpleFlux;

/// <summary>
/// Fluent event registration helpers for the SimpleFlux builder.
/// </summary>
/// <remarks>
/// <para>
/// Every event type used by the application must be registered before the store
/// is constructed. Three registration strategies are available (and combinable):
/// </para>
/// <list type="bullet">
///   <item><description><c>WithEvent&lt;T&gt;</c> / <c>WithEvents&lt;T1, T2, …&gt;</c> — register specific types.</description></item>
///   <item><description><c>ScanAssemblyOf&lt;TMarker&gt;</c> / <c>ScanAssembly(assembly)</c> — register every <see cref="FluxEvent"/> subclass found in an assembly.</description></item>
/// </list>
/// <para>
/// If no events are registered when the store is constructed, an
/// <see cref="InvalidOperationException"/> is thrown — there is no implicit
/// "scan all loaded assemblies" fallback.
/// </para>
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
            throw new ArgumentException($"'{eventType.FullName}' must derive from {nameof(FluxEvent)}.", nameof(eventType));

        builder.Options.EventTypes.Add(eventType);
        return builder;
    }

    /// <summary>
    /// Registers multiple event types explicitly in one call.
    /// </summary>
    /// <typeparam name="T1">First event type.</typeparam>
    /// <typeparam name="T2">Second event type.</typeparam>
    /// <param name="builder">The SimpleFlux builder from <c>AddSimpleFlux()</c>.</param>
    /// <returns>The builder, for chaining.</returns>
    public static IFluxBuilder WithEvents<T1, T2>(this IFluxBuilder builder)
        where T1 : FluxEvent
        where T2 : FluxEvent
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.WithEvent(typeof(T1)).WithEvent(typeof(T2));
    }

    /// <summary>
    /// Registers multiple event types explicitly in one call.
    /// </summary>
    public static IFluxBuilder WithEvents<T1, T2, T3>(this IFluxBuilder builder)
        where T1 : FluxEvent
        where T2 : FluxEvent
        where T3 : FluxEvent
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.WithEvent(typeof(T1)).WithEvent(typeof(T2)).WithEvent(typeof(T3));
    }

    /// <summary>
    /// Registers multiple event types explicitly in one call.
    /// </summary>
    public static IFluxBuilder WithEvents<T1, T2, T3, T4>(this IFluxBuilder builder)
        where T1 : FluxEvent
        where T2 : FluxEvent
        where T3 : FluxEvent
        where T4 : FluxEvent
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.WithEvent(typeof(T1)).WithEvent(typeof(T2)).WithEvent(typeof(T3)).WithEvent(typeof(T4));
    }

    /// <summary>
    /// Registers multiple event types explicitly in one call.
    /// </summary>
    public static IFluxBuilder WithEvents<T1, T2, T3, T4, T5>(this IFluxBuilder builder)
        where T1 : FluxEvent
        where T2 : FluxEvent
        where T3 : FluxEvent
        where T4 : FluxEvent
        where T5 : FluxEvent
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.WithEvent(typeof(T1)).WithEvent(typeof(T2)).WithEvent(typeof(T3)).WithEvent(typeof(T4)).WithEvent(typeof(T5));
    }

    /// <summary>
    /// Scans the assembly containing <typeparamref name="TMarker"/> for all
    /// <see cref="FluxEvent"/> subclasses and registers them.
    /// </summary>
    /// <typeparam name="TMarker">Any type from the target assembly (commonly an event or a marker type).</typeparam>
    /// <param name="builder">The SimpleFlux builder from <c>AddSimpleFlux()</c>.</param>
    /// <returns>The builder, for chaining.</returns>
    public static IFluxBuilder ScanAssemblyOf<TMarker>(this IFluxBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.ScanAssembly(typeof(TMarker).Assembly);
    }

    /// <summary>
    /// Scans the given assembly for all <see cref="FluxEvent"/> subclasses and registers them.
    /// </summary>
    /// <param name="builder">The SimpleFlux builder from <c>AddSimpleFlux()</c>.</param>
    /// <param name="assembly">The assembly to scan for event types.</param>
    /// <returns>The builder, for chaining.</returns>
    public static IFluxBuilder ScanAssembly(this IFluxBuilder builder, Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(assembly);

        builder.Options.EventAssemblies.Add(assembly);
        return builder;
    }
}
