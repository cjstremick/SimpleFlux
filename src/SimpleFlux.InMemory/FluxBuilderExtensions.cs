using Microsoft.Extensions.DependencyInjection;

namespace SimpleFlux.InMemory;

/// <summary>
/// Fluent registration extensions for the in-memory backend.
/// </summary>
public static class FluxBuilderExtensions
{
    /// <summary>
    /// Registers <see cref="InMemoryStreamStore"/> as the storage backend.
    /// </summary>
    /// <param name="builder">The SimpleFlux builder from <c>AddSimpleFlux()</c>.</param>
    /// <returns>The builder, for chaining.</returns>
    public static IFluxBuilder UseInMemory(this IFluxBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.Add(new ServiceDescriptor(
            typeof(IStreamStore),
            sp => new InMemoryStreamStore(),
            builder.Options.StoreLifetime));

        return builder;
    }
}