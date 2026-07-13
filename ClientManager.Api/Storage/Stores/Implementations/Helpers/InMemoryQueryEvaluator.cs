using System.Reflection;
using ClientManager.Shared.Models.Search;

namespace ClientManager.Api.Storage.Stores.Implementations.Helpers;

/// <summary>
/// Applies a <see cref="DocumentQuery"/> to an in-memory list using reflection.
/// Used as the fallback engine for plain Redis and as a safety net for any store.
/// </summary>
public static class InMemoryQueryEvaluator
{
    /// <summary>
    /// Evaluates a <see cref="DocumentQuery"/> against an in-memory list by applying
    /// each <see cref="FilterClause"/> via reflection, then optional text search, sort,
    /// and pagination. Returns a <see cref="SearchResult{T}"/> whose <c>TotalCount</c>
    /// reflects the total matches before pagination is applied.
    ///
    /// <para>
    /// This is the universal fallback used by every <see cref="Stores.Interfaces.IDocumentStore"/> that does
    /// not (yet) implement native query translation.
    /// </para>
    /// </summary>
    public static SearchResult<T> Apply<T>(IReadOnlyList<T> items, DocumentQuery query)
    {
        IEnumerable<T> result = items;

        foreach (var filter in query.Filters)
        {
            result = result.Where(item => MatchesFilter(item, filter));
        }

        if (!string.IsNullOrEmpty(query.TextSearch))
        {
            var search = query.TextSearch;
            result = result.Where(item => MatchesTextSearch(item, search));
        }

        var materialized = result.ToList();
        long totalCount = materialized.Count;

        if (query.Sort is not null)
        {
            materialized = ApplySort(materialized, query.Sort);
        }

        IEnumerable<T> paged = materialized;

        if (query.Skip.HasValue)
            paged = paged.Skip(query.Skip.Value);

        if (query.Take.HasValue)
            paged = paged.Take(query.Take.Value);

        return new SearchResult<T>(paged.ToList(), totalCount);
    }

    private static bool MatchesFilter<T>(T item, FilterClause filter)
    {
        var property = GetProperty<T>(filter.FieldName);
        if (property is null)
            return false;

        var propertyValue = property.GetValue(item);
        return EvaluateOperator(propertyValue, filter.Operator, filter.Value);
    }

    private static bool MatchesTextSearch<T>(T item, string searchText)
    {
        var properties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);

        foreach (var property in properties)
        {
            if (property.PropertyType != typeof(string))
                continue;

            var value = property.GetValue(item) as string;
            if (value is not null && value.Contains(searchText, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static List<T> ApplySort<T>(List<T> items, SortClause sort)
    {
        var property = GetProperty<T>(sort.FieldName);
        if (property is null)
            return items;

        return sort.Direction == SortDirection.Ascending
            ? [.. items.OrderBy(item => property.GetValue(item))]
            : [.. items.OrderByDescending(item => property.GetValue(item))];
    }

    private static PropertyInfo? GetProperty<T>(string fieldName)
    {
        return typeof(T).GetProperty(
            fieldName,
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
    }

    private static bool EvaluateOperator(object? propertyValue, FilterOperator op, object filterValue)
    {
        if (propertyValue is null)
            return op == FilterOperator.NotEquals;

        return op switch
        {
            FilterOperator.Equals => Equals(Normalize(propertyValue), Normalize(filterValue)),
            FilterOperator.NotEquals => !Equals(Normalize(propertyValue), Normalize(filterValue)),
            FilterOperator.Contains => propertyValue is string s
                && s.Contains(filterValue.ToString()!, StringComparison.OrdinalIgnoreCase),
            FilterOperator.StartsWith => propertyValue is string sw
                && sw.StartsWith(filterValue.ToString()!, StringComparison.OrdinalIgnoreCase),
            FilterOperator.GreaterThan => Compare(propertyValue, filterValue) > 0,
            FilterOperator.GreaterThanOrEqual => Compare(propertyValue, filterValue) >= 0,
            FilterOperator.LessThan => Compare(propertyValue, filterValue) < 0,
            FilterOperator.LessThanOrEqual => Compare(propertyValue, filterValue) <= 0,
            _ => false
        };
    }

    private static object Normalize(object value)
    {
        if (value is Enum e)
            return e.ToString();

        return value;
    }

    private static int Compare(object left, object right)
    {
        if (left is IComparable comparable)
        {
            if (left.GetType() == right.GetType())
                return comparable.CompareTo(right);

            var converted = Convert.ChangeType(right, left.GetType());
            return comparable.CompareTo(converted);
        }

        return 0;
    }
}
