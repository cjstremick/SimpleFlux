using Azure.Data.Tables;
using Microsoft.Extensions.DependencyInjection;

namespace SimpleFlux.AzureTables;

/// <summary>
/// Fluent registration extensions for the Azure Tables backend.
/// </summary>
public static class FluxBuilderExtensions
{
    /// <summary>
    /// Registers <see cref="AzureTableStreamStore"/> as the storage backend.
    /// </summary>
    /// <param name="builder">The SimpleFlux builder from <c>AddSimpleFlux()</c>.</param>
    /// <param name="tableClient">The Azure Table client to persist streams to.</param>
    /// <returns>The builder, for chaining.</returns>
    public static IFluxBuilder UseAzureTables(this IFluxBuilder builder, TableClient tableClient)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(tableClient);

        builder.Services.Add(new ServiceDescriptor(
            typeof(IStreamStore),
            _ => new AzureTableStreamStore(tableClient),
            builder.Options.StoreLifetime));

        return builder;
    }
}