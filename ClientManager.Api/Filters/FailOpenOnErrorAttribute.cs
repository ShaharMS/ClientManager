namespace ClientManager.Api.Filters;

/// <summary>
/// Identifies which fail-open response a hot-path action should return when an unexpected
/// server error is converted into a successful response by <see cref="HotPathFailOpenFilter"/>.
/// </summary>
public enum HotPathFailOpenKind
{
    /// <summary>
    /// Returns an access-check response that grants access (rate-limit information unknown).
    /// </summary>
    GrantAccess,

    /// <summary>
    /// Returns a resource-acquire response that grants a time-bounded allocation.
    /// </summary>
    GrantAcquire,

    /// <summary>
    /// Returns a resource-release response that reports the allocation as released.
    /// </summary>
    ConfirmRelease
}

/// <summary>
/// Marks a controller action as eligible for hot-path fail-open handling. When the
/// <c>HotPathResilience:FailOpenOnServerError</c> option is enabled and the action throws an
/// unexpected server error, <see cref="HotPathFailOpenFilter"/> converts the 500 into the
/// successful response described by <see cref="Kind"/> instead of failing the request.
/// <para>
/// Apply this only to operational hot paths where availability is preferred over strict
/// enforcement during an internal fault. See <see cref="Models.Configuration.HotPathResilienceOptions"/>
/// for the security trade-off.
/// </para>
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class FailOpenOnErrorAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of <see cref="FailOpenOnErrorAttribute"/>.
    /// </summary>
    /// <param name="kind">The fail-open response this action should return on an unexpected error.</param>
    public FailOpenOnErrorAttribute(HotPathFailOpenKind kind)
    {
        Kind = kind;
    }

    /// <summary>
    /// The fail-open response this action returns when an unexpected server error is suppressed.
    /// </summary>
    public HotPathFailOpenKind Kind { get; }
}
