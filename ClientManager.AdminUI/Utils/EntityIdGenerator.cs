namespace ClientManager.AdminUI.Utils;

/// <summary>
/// Generates stable, human-readable IDs for catalog entities tied to a target.
/// </summary>
internal static class EntityIdGenerator
{
    /// <summary>
    /// Builds an ID from the target entity id and a short random suffix, e.g. <c>pdf-render-a1b2c3d4</c>.
    /// </summary>
    public static string FromTarget(string targetId) =>
        $"{targetId}-{Guid.NewGuid().ToString("N")[..8]}";
}
