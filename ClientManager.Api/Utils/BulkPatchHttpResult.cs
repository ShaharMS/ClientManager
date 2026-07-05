using ClientManager.Shared.Models.Responses;
using Microsoft.AspNetCore.Mvc;

namespace ClientManager.Api.Utils;

/// <summary>
/// Maps bulk PATCH per-item outcomes to HTTP status codes.
/// </summary>
public static class BulkPatchHttpResult
{
    /// <summary>
    /// Returns 200 when all items updated, 207 when mixed, 422 when every item failed.
    /// </summary>
    public static IActionResult FromResults<T>(IReadOnlyList<PatchItemResult<T>> results)
    {
        var response = new BulkPatchResponse<T> { Results = results };
        var updated = 0;
        var failed = 0;

        foreach (var result in results)
        {
            if (result.Status == PatchItemStatus.Updated)
            {
                updated++;
            }
            else
            {
                failed++;
            }
        }

        if (failed == 0)
        {
            return new ObjectResult(response) { StatusCode = StatusCodes.Status200OK };
        }

        if (updated == 0)
        {
            return new ObjectResult(response) { StatusCode = StatusCodes.Status422UnprocessableEntity };
        }

        return new ObjectResult(response) { StatusCode = StatusCodes.Status207MultiStatus };
    }
}
