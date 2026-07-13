using System.Text.Json;

namespace ClientManager.Api.Storage.Stores.Implementations.Helpers;

/// <summary>
/// Shared serialization settings used by every <see cref="Interfaces.IDocumentStore"/> implementation
/// so each provider does not redefine an identical <see cref="JsonSerializerOptions"/> instance.
/// </summary>
public static class StoreSerialization
{
    /// <summary>
    /// Case-insensitive JSON options shared across all document-store providers.
    /// </summary>
    public static JsonSerializerOptions JsonOptions { get; } = new()
    {
        PropertyNameCaseInsensitive = true
    };
}
