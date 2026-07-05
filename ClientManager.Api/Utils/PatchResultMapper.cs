using ClientManager.Api.Models.Exceptions;
using ClientManager.Shared.Models.Problems;
using Microsoft.AspNetCore.Http;

namespace ClientManager.Api.Utils;

/// <summary>
/// Maps patch failures to per-item problem payloads.
/// </summary>
internal static class PatchResultMapper
{
    public static ProblemResponse ToProblem(Exception exception) =>
        exception switch
        {
            HttpProblemException problem => new ProblemResponse
            {
                Title = problem.Title,
                Status = problem.StatusCode,
                Detail = problem.Message,
                ErrorCode = problem.ErrorCode
            },
            _ => new ProblemResponse
            {
                Title = "Internal Server Error",
                Status = StatusCodes.Status500InternalServerError,
                Detail = exception.Message
            }
        };
}
