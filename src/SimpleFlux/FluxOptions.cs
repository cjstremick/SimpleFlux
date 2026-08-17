using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace SimpleFlux;

/// <summary>
/// Configuration for a SimpleFlux registration.
/// </summary>
public sealed class FluxOptions
{
    /// <summary>
    /// The assemblies to scan for <see cref="FluxEvent"/> types.
    /// </summary>
    /// <remarks>
    /// When empty, all loaded assemblies are scanned (previous behavior). Set this to
    /// make discovery fast and explicit in larger applications.
    /// </remarks>
    public IList<Assembly> EventAssemblies { get; } = new List<Assembly>();

    /// <summary>
    /// The DI lifetime used for the <see cref="IStreamStore"/> and <see cref="FluxStore"/>
    /// registrations. Both are stateless after construction, so
    /// <see cref="ServiceLifetime.Singleton"/> is the default.
    /// </summary>
    public ServiceLifetime StoreLifetime { get; set; } = ServiceLifetime.Singleton;
}