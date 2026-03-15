namespace ClientManager.Shared.Models.Entities;

/// <summary>
/// Per-client quota for a resource pool. Limits how many slots this client can hold concurrently.
/// </summary>
/// <param name="MaxSlots">Maximum number of concurrent slots this client may hold in the pool.</param>
public readonly record struct ResourcePoolSettings(uint MaxSlots);
