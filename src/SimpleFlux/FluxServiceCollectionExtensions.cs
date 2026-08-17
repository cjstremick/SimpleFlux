using Microsoft.Extensions.DependencyInjection;

namespace SimpleFlux;

/// <summary>
/// DI registration entry points for SimpleFlux.
/// </summary>
public static class FluxServiceCollectionExtensions
{
    /// <summary>
    /// Registers SimpleFlux services (event discovery and <see cref="FluxStore"/>).
    /// </summary>
    /// <remarks>
    /// An <see cref="IStreamStore"/> must also be registered — usually via a backend
    /// extension on the returned builder, e.g. <c>services.AddSimpleFlux().UseInMemory()</c>.
    /// </remarks>
    /// <param name="services">The service collection to register into.</param>
    /// <param name="configure">Optional configuration (event assemblies, lifetimes).</param>
    /// <returns>The builder used to select a storage backend.</returns>
    public static IFluxBuilder AddSimpleFlux(this IServiceCollection services, Action<FluxOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var options = new FluxOptions();
        configure?.Invoke(options);

        services.AddSingleton(options);
        services.Add(new ServiceDescriptor(
            typeof(FluxStore),
            sp => new FluxStore(sp.GetRequiredService<IStreamStore>(), sp.GetRequiredService<FluxOptions>()),
            options.StoreLifetime));

        return new FluxBuilder(services, options);
    }
}