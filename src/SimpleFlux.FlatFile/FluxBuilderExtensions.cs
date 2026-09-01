using Microsoft.Extensions.DependencyInjection;

namespace SimpleFlux.FlatFile;

/// <summary>
/// Fluent registration extensions for the flat-file backend.
/// </summary>
public static class FluxBuilderExtensions
{
    /// <summary>
    /// Registers <see cref="FlatFileStreamStore"/> as the storage backend.
    /// </summary>
    /// <param name="builder">The SimpleFlux builder from <c>AddSimpleFlux()</c>.</param>
    /// <param name="rootDirectory">
    /// The root directory for stream storage. Each stream gets its own subdirectory
    /// containing an append-only <c>events.jsonl</c> file and a <c>meta.json</c> metadata file.
    /// The directory is created if it does not exist.
    /// </param>
    /// <returns>The builder, for chaining.</returns>
    public static IFluxBuilder UseFlatFile(this IFluxBuilder builder, string rootDirectory)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(rootDirectory);

        builder.Services.Add(new ServiceDescriptor(
            typeof(IStreamStore),
            _ => new FlatFileStreamStore(rootDirectory),
            builder.Options.StoreLifetime));

        return builder;
    }
}
