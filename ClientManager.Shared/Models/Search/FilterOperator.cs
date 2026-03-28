namespace ClientManager.Shared.Models.Search;

/// <summary>
/// Comparison operators used in <see cref="FilterClause"/> to define how a document
/// field is matched against a target value. Each store translates these to its native
/// query language (e.g. MongoDB filter, Lucene query, RediSearch predicate) or the
/// in-memory evaluator applies them via reflection.
/// </summary>
public enum FilterOperator
{
    Equals,
    NotEquals,
    Contains,
    StartsWith,
    GreaterThan,
    GreaterThanOrEqual,
    LessThan,
    LessThanOrEqual
}
