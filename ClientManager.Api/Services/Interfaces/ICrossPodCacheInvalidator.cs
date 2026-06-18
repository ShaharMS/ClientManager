namespace ClientManager.Api.Services.Interfaces;

/// <summary>
/// Broadcasts cache invalidation to other API instances.
/// </summary>
public interface ICrossPodCacheInvalidator
{
    /// <summary>
    /// Notifies other pods that catalog caches should be invalidated.
    /// </summary>
    void PublishCatalogInvalidation();
}
