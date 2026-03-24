using ClientManager.Api.Models.Requests;
using ClientManager.Api.Models.Responses;

namespace ClientManager.Api.Extensions;

/// <summary>
/// LINQ extension methods for in-memory pagination.
/// </summary>
public static class PaginationExtensions
{
    /// <summary>
    /// Paginates a collection using the given <see cref="PagedRequest"/> parameters.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="source">The full collection to paginate.</param>
    /// <param name="request">Pagination parameters (page number and page size).</param>
    /// <returns>A <see cref="PagedResponse{T}"/> containing the requested page of items.</returns>
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
