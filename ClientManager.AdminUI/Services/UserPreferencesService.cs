using ClientManager.AdminUI.Localization;
using ClientManager.AdminUI.Models;
using Microsoft.JSInterop;


namespace ClientManager.AdminUI.Services;

public class UserPreferencesService : IAsyncDisposable
{
    private readonly IJSRuntime _jsRuntime;
    private IJSObjectReference? _module;
    private UserPreferences? _cached;

    public UserPreferencesService(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    public event Action? OnPreferencesChanged;

    private async Task<IJSObjectReference> GetModuleAsync()
    {
        _module ??= await _jsRuntime.InvokeAsync<IJSObjectReference>(
            "import", "./js/preferences.js");
        return _module;
    }

    public async Task<UserPreferences> GetPreferencesAsync()
    {
        if (_cached is not null) return _cached;

        var module = await GetModuleAsync();
        _cached = await module.InvokeAsync<UserPreferences?>("getPreferences")
                  ?? new UserPreferences();
        return _cached;
    }

    public async Task SavePreferencesAsync(UserPreferences preferences)
    {
        _cached = preferences;
        var module = await GetModuleAsync();
        await module.InvokeVoidAsync("savePreferences", preferences);
        await module.InvokeVoidAsync("applyTheme", preferences.Theme);
        if (!string.IsNullOrWhiteSpace(preferences.Culture))
        {
            await module.InvokeVoidAsync(
                "applyCulture",
                SupportedCultures.Normalize(preferences.Culture),
                SupportedCultures.IsRtl(preferences.Culture));
        }
        OnPreferencesChanged?.Invoke();
    }

    public async Task<string> GetCultureAsync()
    {
        var prefs = await GetPreferencesAsync();
        if (!string.IsNullOrWhiteSpace(prefs.Culture))
        {
            return SupportedCultures.Normalize(prefs.Culture);
        }

        var module = await GetModuleAsync();
        var resolved = await module.InvokeAsync<string>("getResolvedCulture");
        return SupportedCultures.Normalize(resolved);
    }

    public async Task ApplyCurrentThemeAsync()
    {
        var prefs = await GetPreferencesAsync();
        var module = await GetModuleAsync();
        await module.InvokeVoidAsync("applyTheme", prefs.Theme);
    }

    public async Task<TimeRangePreset> GetDefaultTimeRangeAsync()
    {
        var prefs = await GetPreferencesAsync();
        return TimeRangePreset.FindByKey(prefs.DefaultTimeRange)
               ?? TimeRangePreset.Default;
    }

    public async Task<PollingIntervalPreset> GetDefaultPollingIntervalAsync()
    {
        var prefs = await GetPreferencesAsync();
        return PollingIntervalPreset.FindByKey(prefs.DefaultPollingInterval)
               ?? PollingIntervalPreset.Default;
    }

    public async Task<AxisScaleType> GetDefaultAxisScaleAsync()
    {
        var prefs = await GetPreferencesAsync();
        return Enum.TryParse<AxisScaleType>(prefs.DefaultAxisScale, out var scale)
            ? scale
            : AxisScaleType.Linear;
    }

    public async ValueTask DisposeAsync()
    {
        if (_module is not null)
        {
            await _module.DisposeAsync();
        }
        GC.SuppressFinalize(this);
    }
}
