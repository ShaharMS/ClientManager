using ClientManager.AdminUI.Models;
using ClientManager.AdminUI.Models.Dashboard;
using ClientManager.AdminUI.Utils;

namespace ClientManager.AdminUI.Components.Pages.Dashboard;

public partial class Dashboard
{
    private ChartTimeRange _defaultTimeRange = ChartTimeRange.FromPreset(TimeRangePreset.Default);
    private AxisScaleType _defaultAxisScale = AxisScaleType.Linear;
    private string _defaultPollKey = PollingIntervalPreset.Default.Key;
    private string _pollingKey = PollingIntervalPreset.Default.Key;
    private string? _initialTimeRangeKey;
    private string? _initialPollingKey;
    private AxisScaleType? _initialAxisScale;
    private DateTime? _initialCustomFromUtc;
    private DateTime? _initialCustomToUtc;

    private void HydrateFromUrl()
    {
        var query = UrlQuery.Parse();
        UrlQuery.SuppressWrite = true;
        try
        {
            if (query.TryGetValue(QueryParamParsers.Type, out var type)
                && (type == "Service" || type == "ResourcePool"))
            {
                _selectedFilterType = type;
            }

            _filterTargets = _selectedFilterType == "Service" ? _allServices : _allPools;

            if (query.TryGetValue(QueryParamParsers.Target, out var target)
                && _filterTargets.Any(t => t.Id == target))
            {
                _selectedTargetId = target;
            }
            else if (_filterTargets.Count > 0)
            {
                _selectedTargetId = _filterTargets[0].Id;
            }

            var validClientIds = _clients.Select(c => c.Id).ToHashSet(StringComparer.Ordinal);
            _selectedClientIds = QueryParamParsers.ParseClientIds(
                query.GetValueOrDefault(QueryParamParsers.Clients), validClientIds);

            var parsedRange = QueryParamParsers.TryParseTimeRange(
                query.GetValueOrDefault(QueryParamParsers.Range),
                query.GetValueOrDefault(QueryParamParsers.From),
                query.GetValueOrDefault(QueryParamParsers.To));
            if (parsedRange is not null)
            {
                _timeRange = parsedRange;
            }

            if (QueryParamParsers.TryParseScale(query.GetValueOrDefault(QueryParamParsers.Scale)) is { } parsedScale)
            {
                _axisScaleType = parsedScale;
            }

            if (QueryParamParsers.TryParsePoll(query.GetValueOrDefault(QueryParamParsers.Poll)) is { } parsedPoll)
            {
                _pollingKey = parsedPoll.Key;
            }

            if (query.TryGetValue(QueryParamParsers.Search, out var search))
            {
                _tableSearch = search;
            }

            SetInitialChartDropdownState(query);
            OnTableSearchChanged();
        }
        finally
        {
            UrlQuery.SuppressWrite = false;
        }
    }

    private void SetInitialChartDropdownState(IReadOnlyDictionary<string, string> query)
    {
        if (query.ContainsKey(QueryParamParsers.Range)
            || query.ContainsKey(QueryParamParsers.From)
            || query.ContainsKey(QueryParamParsers.To))
        {
            _initialTimeRangeKey = QueryParamParsers.TryGetRangeKey(_timeRange);
            if (_timeRange.Mode == ChartTimeRangeMode.Custom)
            {
                _initialTimeRangeKey = "custom";
                _initialCustomFromUtc = _timeRange.CustomFromUtc;
                _initialCustomToUtc = _timeRange.CustomToUtc;
            }
        }
        else
        {
            _initialTimeRangeKey = null;
            _initialCustomFromUtc = null;
            _initialCustomToUtc = null;
        }

        _initialPollingKey = query.ContainsKey(QueryParamParsers.Poll) ? _pollingKey : null;
        _initialAxisScale = query.ContainsKey(QueryParamParsers.Scale) ? _axisScaleType : null;
    }

    private Dictionary<string, string?> BuildQueryParams()
    {
        var query = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        if (_selectedFilterType != "Service")
        {
            query[QueryParamParsers.Type] = _selectedFilterType;
        }

        if (!string.IsNullOrEmpty(_selectedTargetId)
            && _selectedTargetId != DashboardChartLoadContext.AllTargetsId)
        {
            query[QueryParamParsers.Target] = _selectedTargetId;
        }

        QueryParamParsers.WriteTimeRange(query, _timeRange, _defaultTimeRange);
        QueryParamParsers.WriteIfPresent(
            query, QueryParamParsers.Scale, QueryParamParsers.FormatScale(_axisScaleType, _defaultAxisScale));
        QueryParamParsers.WriteIfPresent(
            query, QueryParamParsers.Poll, QueryParamParsers.FormatPoll(_pollingKey, _defaultPollKey));
        QueryParamParsers.WriteIfPresent(
            query, QueryParamParsers.Clients, QueryParamParsers.FormatClientIds(_selectedClientIds));
        QueryParamParsers.WriteIfPresent(
            query, QueryParamParsers.Search, string.IsNullOrWhiteSpace(_tableSearch) ? null : _tableSearch);

        return query;
    }

    private void SyncUrl() => UrlQuery.Replace(BuildQueryParams());

    private void SyncUrlDebounced() =>
        UrlQuery.ReplaceDebounced(BuildQueryParams(), InvokeAsync);
}
