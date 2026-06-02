namespace ClientManager.Shared.Models.Problems;

public record StorageProblemResponse : ProblemResponse
{
    public string? ErrorCode { get; init; }
}