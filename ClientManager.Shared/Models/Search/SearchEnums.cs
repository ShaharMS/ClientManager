namespace ClientManager.Shared.Models.Search;

/// <summary>
/// Comparison operators used in <see cref="FilterClause"/> to define how a document
/// field is matched against a target value. Each storage provider translates these to its
/// native query language, or the in-memory evaluator applies them via reflection.
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

/// <summary>
/// Sort direction for a <see cref="SortClause"/> within a <see cref="DocumentQuery"/>.
/// </summary>
public enum SortDirection
{
    Ascending,
    Descending
}
