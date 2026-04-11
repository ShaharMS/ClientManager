namespace ClientManager.Shared.Models.Problems;

public class ProblemResponse
{
    public string? Title { get; init; }

    public int? Status { get; init; }

    public string? Detail { get; init; }

    public string? TraceId { get; init; }
}