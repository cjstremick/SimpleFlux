using Microsoft.Extensions.DependencyInjection;

namespace SimpleFlux;

/// <summary>
/// The fluent registration surface returned by <c>AddSimpleFlux()</c>.
/// </summary>
/// <remarks>
/// Backend packages extend this with <c>Use&lt;Backend&gt;()</c> extension methods
/// (e.g. <c>UseAzureTables(...)</c>, <c>UseInMemory()</c>) that register their
/// <see cref="IStreamStore"/> implementation. Third-party backends use the same hook.
/// </remarks>
public interface IFluxBuilder
{
    /// <summary>
    /// The service collection being built.
    /// </summary>
    IServiceCollection Services { get; }

    /// <summary>
    /// The options for this registration.
    /// </summary>
    FluxOptions Options { get; }
}

/// <summary>
/// Default <see cref="IFluxBuilder"/> implementation.
/// </summary>
public sealed class FluxBuilder : IFluxBuilder
{
    /// <summary>
    /// Creates a builder over the given service collection and options.
    /// </summary>
    /// <param name="services">The service collection being built.</param>
    /// <param name="options">The options for this registration.</param>
    public FluxBuilder(IServiceCollection services, FluxOptions options)
    {
        Services = services;
        Options = options;
    }

    /// <inheritdoc />
    public IServiceCollection Services { get; }

    /// <inheritdoc />
    public FluxOptions Options { get; }
}