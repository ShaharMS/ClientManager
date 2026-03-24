namespace ClientManager.Api.Models.Responses;

/// <summary>
/// Paginated response envelope for list endpoints.
/// </summary>
/// <typeparam name="T">The type of items in the page.</typeparam>
/// <param name="Items">The items in the current page.</param>
/// <param name="Page">The 1-based page number.</param>
/// <param name="PageSize">The number of items per page.</param>
/// <param name="TotalCount">The total number of items across all pages.</param>
/// <param name="TotalPages">The total number of pages.</param>
public record PagedResponse<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages);
