namespace ClientManager.Shared.Models.Enums;

/// <summary>
/// Defines the type of target usage events being tracked.
/// <para>
///     Different event types are relevant for different
///     <see cref="TargetType"/>s, with some overlap. See the documentation for each <see cref="UsageEventType"/> for more details on their applicability and semantics.
/// </para>
/// </summary>
public enum UsageEventType
{
    /// <summary>
    /// <list type="table">
    ///     <listheader>
    ///         <term>Target Type</term>
    ///         <description>Semantics</description>
    ///     </listheader>
    ///     <item>
    ///         <term><see cref="TargetType.Service"/></term>
    ///         <description>Request was passed on to handling by the specific service</description>
    ///     </item>
    ///     <item>
    ///         <term><see cref="TargetType.ResourcePool"/></term>
    ///         <description>The client is allowed to open a connection against this resource pool</description>
    ///     </item>
    /// </list>
    /// </summary>
    Granted,
    /// <summary>
    /// <list type="table">
    ///     <listheader>
    ///         <term>Target Type</term>
    ///         <description>Semantics</description>
    ///     </listheader>
    ///     <item>
    ///         <term><see cref="TargetType.Service"/></term>
    ///         <description>Request was denied by the specific service</description>
    ///     </item>
    ///     <item>
    ///         <term><see cref="TargetType.ResourcePool"/></term>
    ///         <description>The client is not allowed to open a connection against this resource pool</description>
    ///     </item>
    /// </list>

    /// </summary>
    Denied,
    /// <summary>
    /// <list type="table">
    ///     <listheader>
    ///         <term>Target Type</term>
    ///         <description>Semantics</description>
    ///     </listheader>
    ///     <item>
    ///         <term><see cref="TargetType.ResourcePool"/></term>
    ///         <description>Resource allocation explicitly released from this resource pool</description>
    ///     </item>
    /// </list>
    /// </summary>
    Released
}
