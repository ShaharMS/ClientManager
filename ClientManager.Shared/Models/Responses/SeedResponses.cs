namespace ClientManager.Shared.Models.Responses;

/// <summary>
/// Counts returned after a seed import operation.
/// </summary>
public record SeedImportSummary
{
    /// <summary>Entities created during import.</summary>
    public int Created { get; init; }

    /// <summary>Entities updated during import (replace strategy only).</summary>
    public int Updated { get; init; }

    /// <summary>Entities skipped because they already existed (skip strategy only).</summary>
    public int Skipped { get; init; }

    /// <summary>Entities deleted before insert (POST wholesale replace only).</summary>
    public int Deleted { get; init; }

    /// <summary>Documents processed during the operation.</summary>
    public int Processed { get; init; }

    /// <summary>Wall-clock duration of the operation.</summary>
    public long ElapsedMs { get; init; }
}
