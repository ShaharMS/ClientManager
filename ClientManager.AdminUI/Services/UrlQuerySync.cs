using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.WebUtilities;

namespace ClientManager.AdminUI.Services;

public sealed class UrlQuerySync : IDisposable
{
    private readonly NavigationManager _navigation;
    private CancellationTokenSource? _debounceCts;
    private bool _suppressWrite;

    public UrlQuerySync(NavigationManager navigation)
    {
        _navigation = navigation;
    }

    public bool SuppressWrite
    {
        get => _suppressWrite;
        set => _suppressWrite = value;
    }

    public IReadOnlyDictionary<string, string> Parse()
    {
        var uri = _navigation.ToAbsoluteUri(_navigation.Uri);
        return QueryHelpers.ParseQuery(uri.Query)
            .ToDictionary(
                pair => pair.Key,
                pair => pair.Value.ToString(),
                StringComparer.OrdinalIgnoreCase);
    }

    public string? Get(string key) =>
        Parse().TryGetValue(key, out var value) ? value : null;

    public void Replace(IReadOnlyDictionary<string, string?> parameters)
    {
        if (_suppressWrite)
        {
            return;
        }

        var uri = _navigation.ToAbsoluteUri(_navigation.Uri);
        var path = uri.GetLeftPart(UriPartial.Path);
        var query = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        foreach (var (key, value) in parameters)
        {
            if (!string.IsNullOrEmpty(value))
            {
                query[key] = value;
            }
        }

        var newUri = query.Count == 0 ? path : QueryHelpers.AddQueryString(path, query);
        if (string.Equals(newUri, _navigation.Uri, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _navigation.NavigateTo(newUri, replace: true);
    }

    public void ReplaceDebounced(
        IReadOnlyDictionary<string, string?> parameters,
        Func<Func<Task>, Task> dispatchAsync,
        int delayMs = 300)
    {
        if (_suppressWrite)
        {
            return;
        }

        _debounceCts?.Cancel();
        _debounceCts?.Dispose();
        _debounceCts = new CancellationTokenSource();
        var token = _debounceCts.Token;

        _ = dispatchAsync(async () =>
        {
            try
            {
                await Task.Delay(delayMs, token);
                Replace(parameters);
            }
            catch (TaskCanceledException)
            {
            }
        });
    }

    public void Dispose()
    {
        _debounceCts?.Cancel();
        _debounceCts?.Dispose();
    }
}
