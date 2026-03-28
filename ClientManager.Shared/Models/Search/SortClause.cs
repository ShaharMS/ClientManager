namespace ClientManager.Shared.Models.Search;

/// <summary>
/// Defines the sort order for search results.
/// At most one sort clause is applied per query.
/// </summary>
/// <param name="FieldName">The JSON property name to sort by (case-insensitive).</param>
/// <param name="Direction">Whether to sort ascending or descending.</param>
public record SortClause(string FieldName, SortDirection Direction);
