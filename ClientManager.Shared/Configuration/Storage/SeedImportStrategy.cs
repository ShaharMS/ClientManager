namespace ClientManager.Shared.Configuration.Storage;

/// <summary>
/// Controls how seed import handles entities whose IDs already exist in storage.
/// </summary>
/// <remarks>
/// Passed as a query parameter on seed import endpoints. <see cref="Skip"/> is safe for
/// additive restores; <see cref="Replace"/> overwrites matching IDs in place.
/// </remarks>
public enum SeedImportStrategy
{
    /// <summary>Leave existing entities unchanged; create only missing IDs.</summary>
    Skip,

    /// <summary>Overwrite existing entities by ID or create when missing.</summary>
    Replace
}
