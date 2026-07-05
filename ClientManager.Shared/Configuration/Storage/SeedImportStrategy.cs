namespace ClientManager.Shared.Configuration.Storage;

/// <summary>
/// Controls how seed import handles entities whose IDs already exist in storage.
/// </summary>
public enum SeedImportStrategy
{
    /// <summary>Leave existing entities unchanged; create only missing IDs.</summary>
    Skip,

    /// <summary>Overwrite existing entities by ID or create when missing.</summary>
    Replace
}
