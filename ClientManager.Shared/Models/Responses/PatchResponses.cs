using ClientManager.Shared.Models.Problems;

namespace ClientManager.Shared.Models.Responses;

/// <summary>
/// Outcome of a single item in a bulk PATCH request.
/// </summary>
public enum PatchItemStatus
{
    /// <summary>The patch was applied successfully.</summary>
    Updated,

    /// <summary>The patch failed for this item.</summary>
    Failed
}

/// <summary>
/// Result for one entity in a bulk PATCH operation.
/// </summary>
/// <typeparam name="T">The catalog entity type.</typeparam>
public record PatchItemResult<T>
{
    /// <summary>Entity identifier from the patch item.</summary>
    public required string Id { get; init; }

    /// <summary>Whether the patch succeeded or failed.</summary>
    public PatchItemStatus Status { get; init; }

    /// <summary>Updated entity when <see cref="Status"/> is <see cref="PatchItemStatus.Updated"/>.</summary>
    public T? Entity { get; init; }

    /// <summary>Problem details when <see cref="Status"/> is <see cref="PatchItemStatus.Failed"/>.</summary>
    public ProblemResponse? Error { get; init; }
}

/// <summary>
/// Response body for bulk PATCH on a catalog resource.
/// </summary>
/// <typeparam name="T">The catalog entity type.</typeparam>
public record BulkPatchResponse<T>
{
    /// <summary>Per-item patch outcomes.</summary>
    public required IReadOnlyList<PatchItemResult<T>> Results { get; init; }
}
