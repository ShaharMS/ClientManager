namespace ClientManager.Shared.Models.Enums;

/// <summary>
/// Defines the type of usage event being tracked.
/// </summary>
public enum UsageEventType
{
    /// <summary>
    /// Request granted or resource acquired.
    /// </summary>
    Granted,
    /// <summary>
    /// Request denied or resource acquisition denied.
    /// </summary>
    Denied
}
