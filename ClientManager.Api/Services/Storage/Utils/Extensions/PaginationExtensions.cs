using ClientManager.Shared.Models.Requests;
using ClientManager.Shared.Models.Responses;

namespace ClientManager.Api.Services.Storage.Utils.Extensions;

/// <summary>
/// Provides in-memory pagination helpers for internal configuration list endpoints.
/// </summary>
public static class PaginationExtensions
{
    /// <summary>
    /// Converts an in-memory list to a paged response using the shared paging contract.
    /// </summary>
    public static PagedResponse<T> ToPagedResponse<T>(this IReadOnlyList<T> source, PagedRequest request)
    {
        var clamped = request.Clamp();
        var totalCount = source.Count;
        var totalPages = (int)Math.Ceiling((double)totalCount / clamped.PageSize);

        var items = source
            .Skip((clamped.Page - 1) * clamped.PageSize)
            .Take(clamped.PageSize)
            .ToList();

        return new PagedResponse<T>(items, clamped.Page, clamped.PageSize, totalCount, totalPages);
    }
}