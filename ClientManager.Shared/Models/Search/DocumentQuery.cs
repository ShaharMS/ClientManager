using System.Text.Json.Serialization;

namespace ClientManager.Shared.Models.Search;

/// <summary>
/// Composable query model for searching documents in a collection.
/// Supports field-level filters, sorting, and pagination. Stores translate this to their
/// native query language, or fall back to in-memory evaluation.
///
/// <para>
/// Properties use public setters for JSON deserialization from HTTP request bodies.
/// Fluent methods (<see cref="Where"/>, <see cref="OrderBy"/>, <see cref="WithPagination"/>)
/// are convenience shortcuts for building queries in code.
/// </para>
/// </summary>
public class DocumentQuery
{
    /// <summary>
    /// Field-level filter clauses. Multiple filters are AND'd together.
    /// </summary>
    public List<FilterClause> Filters { get; set; } = [];

    /// <summary>
    /// Optional sort clause for ordering results.
    /// </summary>
    public SortClause? Sort { get; set; }

    /// <summary>
    /// Number of documents to skip (for pagination).
    /// </summary>
    public int? Skip { get; set; }

    /// <summary>
    /// Maximum number of documents to return (for pagination).
    /// </summary>
    public int? Take { get; set; }

    /// <summary>
    /// Free-text search term for matching across all string fields.
    /// Not exposed to API consumers — used internally by store implementations.
    /// </summary>
    [JsonIgnore]
    public string? TextSearch { get; set; }

    /// <summary>
    /// Returns an empty query that matches all documents with no filters or pagination.
    /// </summary>
    public static DocumentQuery All => new();

    /// <summary>
    /// Adds a field-level filter clause. Multiple calls are AND'd together.
    /// </summary>
    /// <param name="field">The JSON property name to filter on (case-insensitive).</param>
    /// <param name="op">The comparison operator.</param>
    /// <param name="value">The value to compare against (string, int, bool, DateTime, enum).</param>
    public DocumentQuery Where(string field, FilterOperator op, object value)
    {
        Filters.Add(new FilterClause(field, op, value));
        return this;
    }

    /// <summary>
    /// Sets a free-text search term that matches against all string fields.
    /// </summary>
    /// <param name="text">The text to search for (case-insensitive).</param>
    public DocumentQuery WithTextSearch(string text)
    {
        TextSearch = text;
        return this;
    }

    /// <summary>
    /// Sets the sort order for the results.
    /// </summary>
    /// <param name="field">The JSON property name to sort by.</param>
    /// <param name="direction">The sort direction.</param>
    public DocumentQuery OrderBy(string field, SortDirection direction)
    {
        Sort = new SortClause(field, direction);
        return this;
    }

    /// <summary>
    /// Sets pagination parameters for the results.
    /// </summary>
    /// <param name="skip">Number of documents to skip.</param>
    /// <param name="take">Maximum number of documents to return.</param>
    public DocumentQuery WithPagination(int skip, int take)
    {
        Skip = skip;
        Take = take;
        return this;
    }
}
