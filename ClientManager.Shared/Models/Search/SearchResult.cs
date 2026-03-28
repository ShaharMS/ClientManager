namespace ClientManager.Shared.Models.Search;

/// <summary>
/// The result of a search operation, containing the matching items and the total count
/// of documents matching the query (before pagination).
/// </summary>
/// <typeparam name="T">The document type.</typeparam>
/// <param name="Items">The page of matching documents.</param>
/// <param name="TotalCount">Total number of documents matching the query, ignoring pagination.</param>
public record SearchResult<T>(
    IEnumerable<T> Items,
    long TotalCount);
