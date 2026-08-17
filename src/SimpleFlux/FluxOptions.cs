using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace SimpleFlux;

/// <summary>
/// Configuration for a SimpleFlux registration.
/// </summary>
public sealed class FluxOptions
{
    /// <summary>
    /// The event types registered explicitly (via <c>WithEvent&lt;T&gt;()</c> or direct mutation).
    /// </summary>
    /// <remarks>
    /// Registered types are known to <see cref="FluxStore"/> without any assembly scanning.
    /// </remarks>
    public IList<Type> EventTypes { get; } = new List<Type>();

    /// <summary>
    /// The assemblies to scan for <see cref="FluxEvent"/> types.
    /// </summary>
    /// <remarks>
    /// When both <see cref="EventTypes"/> and <see cref="EventAssemblies"/> are empty,
    /// all loaded assemblies are scanned (previous behavior). As soon as anything is
    /// registered explicitly, only the registered types/assemblies are used.
    /// </remarks>
    public IList<Assembly> EventAssemblies { get; } = new List<Assembly>();

    /// <summary>
    /// The DI lifetime used for the <see cref="IStreamStore"/> and <see cref="FluxStore"/>
    /// registrations. Both are stateless after construction, so
    /// <see cref="ServiceLifetime.Singleton"/> is the default.
    /// </summary>
    public ServiceLifetime StoreLifetime { get; set; } = ServiceLifetime.Singleton;
}