namespace ClientManager.Api.Models.Responses;

public record ResourceAcquireResponse
{
    public required string AllocationId { get; init; }
    public DateTime ExpiresAt { get; init; }
}
