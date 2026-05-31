namespace ClientManager.Api.Models.Configuration;

/// <summary>
/// Settings that control how the API behaves when a hot-path request fails with an
/// unexpected server error (an error that would otherwise be returned as HTTP 500).
/// <para>
/// <b>Why this exists.</b> The operational hot paths — access checks and resource
/// acquire/release — sit in front of every customer request. When a defect or a
/// downstream fault causes one of these endpoints to throw, the default behavior is a
/// 500, which blocks the caller. For an operator who would rather keep customers
/// flowing than have a single internal fault lock the entire service collection out for
/// every customer at once, that trade-off is undesirable. When this option is enabled,
/// an unexpected server error on a designated hot path is converted into a successful
/// (HTTP 200) "fail-open" response so the client can continue to use its resources.
/// </para>
/// <para>
/// <b>⚠ Security and correctness warning.</b> This is a deliberate <i>fail-open</i>
/// switch. While it is enabled, an internal fault on the access-check path causes the
/// API to return an <i>access-granted</i> response, and a fault on the acquire path
/// returns an <i>acquired</i> response, even though the access/rate-limit/allocation
/// decision could not actually be evaluated. In effect, authentication and rate limiting
/// are bypassed for the affected requests for the duration of the fault. The failure is
/// still recorded internally (logged at error level with the original exception) so the
/// fault is never hidden from operators — only from the calling customer, who by design
/// sees an ordinary success with no degradation marker. Only enable this where that
/// trade-off (availability over strict enforcement) is acceptable and the deployment is
/// otherwise trusted. It is <b>disabled by default</b>.
/// </para>
/// <para>
/// Only true unexpected errors are converted. Deliberate outcomes that are already
/// modeled as problem responses (for example 403 Forbidden, 404 Not Found,
/// 429 Too Many Requests, and 503 Service Unavailable) are <i>not</i> masked and continue
/// to surface to the caller as before.
/// </para>
/// </summary>
public sealed class HotPathResilienceOptions
{
    /// <summary>
    /// The configuration section name.
    /// </summary>
    public const string SectionName = "HotPathResilience";

    /// <summary>
    /// When <see langword="true"/>, an unexpected server error (HTTP 500) on a designated
    /// hot path is converted into a successful fail-open response so the client is not
    /// disrupted. The underlying failure is still logged at error level. Defaults to
    /// <see langword="false"/> (the standard 500 behavior is preserved).
    /// <para>
    /// See the type-level remarks for the full security trade-off before enabling this.
    /// </para>
    /// </summary>
    public bool FailOpenOnServerError { get; init; }

    /// <summary>
    /// The lifetime assigned to a resource allocation that is granted by a fail-open
    /// response on the acquire hot path. Because the real allocation could not be created,
    /// the fabricated allocation is given a bounded expiry so it cannot live indefinitely.
    /// Defaults to five minutes. Ignored when <see cref="FailOpenOnServerError"/> is
    /// <see langword="false"/>.
    /// </summary>
    public TimeSpan FailOpenAllocationLifetime { get; init; } = TimeSpan.FromMinutes(5);
}
