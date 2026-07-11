using ClientManager.Shared.Configuration.Storage;

namespace ClientManager.Api.Models.Configuration;

/// <summary>
/// Gates and tunables for capabilities that can wipe data, override production state,
/// or widen cross-pod cache staleness windows.
/// </summary>
/// <remarks>
/// <para>
/// Bind from the <c>DangerZone</c> appsettings section. Normal operational settings
/// (persistence, usage retention windows, Swagger, metrics) stay outside this section.
/// </para>
/// <para>
/// Boolean gates use nullable binding so the host can distinguish "not configured" from
/// "explicitly false". <see cref="DangerZoneOptionsPostConfigure"/> applies environment
/// defaults before any gate is enforced.
/// </para>
/// <para><strong>Production defaults (when a gate is omitted)</strong></para>
/// <list type="bullet">
///   <item><description><see cref="EnableStartupSeeding"/> — <c>false</c></description></item>
///   <item><description><see cref="EnableSeedExport"/> — <c>false</c></description></item>
///   <item><description><see cref="EnableSeedImport"/> — <c>false</c></description></item>
///   <item><description><see cref="EnableUsagePruning"/> — <c>true</c></description></item>
/// </list>
/// <para><strong>Development defaults (when a gate is omitted)</strong></para>
/// <list type="bullet">
///   <item><description>All seed gates — <c>true</c></description></item>
///   <item><description><see cref="EnableUsagePruning"/> — <c>true</c></description></item>
/// </list>
/// <para>
/// See <c>docs/danger-zone.md</c> for workflows, examples, and cache TTL tuning guidance.
/// </para>
/// </remarks>
public sealed class DangerZoneOptions
{
    /// <summary>The configuration section name.</summary>
    public const string SectionName = "DangerZone";

    /// <summary>Nested subsection name for <see cref="StorageReadCache"/> TTL bindings.</summary>
    public const string StorageReadCacheSubsection = "StorageReadCache";

    /// <summary>
    /// When <c>true</c>, <c>DataSeedService</c> runs at startup if the root <c>Seed</c> section exists.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The root <c>Seed</c> section only defines catalog entities to ensure; this gate controls
    /// whether startup import actually runs. Startup seeding uses skip semantics (missing IDs only).
    /// </para>
    /// <para>
    /// In Production, leave this <c>false</c> after first bootstrap so redeploys cannot mutate catalog
    /// state from baked-in config. Set <c>true</c> only for deliberate bootstrap windows.
    /// </para>
    /// </remarks>
    public bool? EnableStartupSeeding { get; set; }

    /// <summary>
    /// When <c>true</c>, <c>GET /api/v1/seed</c> can export catalog and statistics.
    /// </summary>
    /// <remarks>
    /// <para>Export leaks the full catalog and optional usage history. Disabled requests receive HTTP 404.</para>
    /// <para>Example: <c>curl "http://localhost:5062/api/v1/seed"</c> requires this gate in Production.</para>
    /// </remarks>
    public bool? EnableSeedExport { get; set; }

    /// <summary>
    /// When <c>true</c>, <c>POST</c>, <c>PUT</c>, and <c>DELETE /api/v1/seed</c> are available.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Import and delete can wipe or overwrite clients, services, limits, pools, and statistics.
    /// Disabled requests receive HTTP 404. Split from export so Production can allow read-only migration
    /// exports while blocking destructive verbs.
    /// </para>
    /// </remarks>
    public bool? EnableSeedImport { get; set; }

    /// <summary>
    /// When <c>true</c>, the usage persistence slow loop deletes expired snapshot buckets
    /// per <c>UsageTracking</c> retention windows.
    /// </summary>
    /// <remarks>
    /// <para>
    /// When <c>false</c>, rollups continue but prune is skipped — historical buckets accumulate
    /// until pruning is re-enabled. Defaults to <c>true</c> in all environments.
    /// </para>
    /// </remarks>
    public bool? EnableUsagePruning { get; set; }

    /// <summary>
    /// In-memory read-cache TTL overrides for catalog and statistics queries.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Omitted properties use code defaults (<c>30s</c> / <c>1s</c> / <c>5s</c>). Writes on this pod
    /// still invalidate local cache immediately; TTLs bound cross-pod staleness and reload cost.
    /// </para>
    /// </remarks>
    public StorageReadCacheOptions StorageReadCache { get; init; } = new();

    /// <summary>Resolved startup seeding gate after environment defaults are applied.</summary>
    public bool IsStartupSeedingEnabled => EnableStartupSeeding ?? false;

    /// <summary>Resolved seed export gate after environment defaults are applied.</summary>
    public bool IsSeedExportEnabled => EnableSeedExport ?? false;

    /// <summary>Resolved seed import gate after environment defaults are applied.</summary>
    public bool IsSeedImportEnabled => EnableSeedImport ?? false;

    /// <summary>Resolved usage pruning gate after environment defaults are applied.</summary>
    public bool IsUsagePruningEnabled => EnableUsagePruning ?? true;
}
