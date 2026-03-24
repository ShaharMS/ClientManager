namespace ClientManager.Api.Models.Requests;

/// <summary>
/// Pagination parameters for list endpoints.
/// </summary>
/// <param name="Page">The 1-based page number to retrieve.</param>
/// <param name="PageSize">The number of items per page.</param>
public record PagedRequest(int Page = 1, int PageSize = 20)
{
    /// <summary>
    /// Returns a validated copy with <see cref="Page"/> clamped to &gt;= 1
    /// and <see cref="PageSize"/> clamped to [1, 100].
    /// </summary>
    public PagedRequest Clamp() => this with
    {
        Page = Math.Max(1, Page),
        PageSize = Math.Clamp(PageSize, 1, 100)
    };
}
