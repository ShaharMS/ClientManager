using ClientManager.AdminUI.Services;
using Radzen;

namespace ClientManager.AdminUI.Utils;

public static class ListPageUrlSupport
{
    public sealed record GridQueryState(
        string? Search,
        bool? Enabled,
        int Page,
        string? SortProperty,
        SortOrder? SortOrder);

    public static GridQueryState Parse(IReadOnlyDictionary<string, string> query, bool hasEnabledFilter)
    {
        var (sortProperty, sortOrder) = QueryParamParsers.TryParseSort(
            query.GetValueOrDefault(QueryParamParsers.Sort),
            query.GetValueOrDefault(QueryParamParsers.Dir));

        return new GridQueryState(
            query.GetValueOrDefault(QueryParamParsers.Search),
            hasEnabledFilter
                ? QueryParamParsers.TryParseEnabled(query.GetValueOrDefault(QueryParamParsers.Enabled))
                : null,
            QueryParamParsers.TryParsePage(query.GetValueOrDefault(QueryParamParsers.Page)),
            sortProperty,
            sortOrder);
    }

    public static Dictionary<string, string?> Build(GridQueryState state, bool hasEnabledFilter)
    {
        var query = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        QueryParamParsers.WriteIfPresent(
            query, QueryParamParsers.Search, string.IsNullOrWhiteSpace(state.Search) ? null : state.Search);

        if (hasEnabledFilter)
        {
            QueryParamParsers.WriteIfPresent(
                query, QueryParamParsers.Enabled, QueryParamParsers.FormatEnabled(state.Enabled));
        }

        QueryParamParsers.WriteIfPresent(query, QueryParamParsers.Page, QueryParamParsers.FormatPage(state.Page));
        QueryParamParsers.WriteSort(query, state.SortProperty, state.SortOrder);
        return query;
    }

    public static void Sync(UrlQuerySync urlQuery, GridQueryState state, bool hasEnabledFilter) =>
        urlQuery.Replace(Build(state, hasEnabledFilter));

    public static void SyncDebounced(
        UrlQuerySync urlQuery,
        GridQueryState state,
        bool hasEnabledFilter,
        Func<Func<Task>, Task> dispatchAsync) =>
        urlQuery.ReplaceDebounced(Build(state, hasEnabledFilter), dispatchAsync);
}
