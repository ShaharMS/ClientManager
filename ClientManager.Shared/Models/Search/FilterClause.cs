using System.Text.Json.Serialization;

namespace ClientManager.Shared.Models.Search;

/// <summary>
/// A single field-level filter condition within a <see cref="DocumentQuery"/>.
/// Multiple clauses added to a query are AND'd together. Stores translate each clause
/// to their native query syntax; the in-memory fallback evaluates them via reflection.
/// </summary>
/// <param name="FieldName">The JSON property name to filter on (case-insensitive).</param>
/// <param name="Operator">The comparison operator to apply.</param>
/// <param name="Value">The target value to compare against (string, int, bool, DateTime, or enum).</param>
public record FilterClause(
    string FieldName,
    FilterOperator Operator,
    [property: JsonConverter(typeof(FilterClauseValueConverter))] object Value);
