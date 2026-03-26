using ClientManager.Shared.Models.Enums;

namespace ClientManager.Api.Models.Exceptions;

/// <summary>
/// Thrown when a global rate limit already exists for a given target.
/// </summary>
public class GlobalRateLimitAlreadyExistsException(string targetId, TargetType targetType) : ConflictException($"A global rate limit already exists for {targetType} '{targetId}'")
{
    public string TargetId { get; } = targetId;
    public TargetType TargetType { get; } = targetType;
}
