using System.Globalization;
using ClientManager.AdminUI.Localization;
using Microsoft.JSInterop;

namespace ClientManager.AdminUI.Services;

public class CultureService
{
    private readonly UserPreferencesService _preferences;
    private readonly IJSRuntime _jsRuntime;
    private IJSObjectReference? _module;
    private string? _appliedCulture;

    public CultureService(UserPreferencesService preferences, IJSRuntime jsRuntime)
    {
        _preferences = preferences;
        _jsRuntime = jsRuntime;
    }

    public string CurrentCulture { get; private set; } = SupportedCultures.Default;

    public async Task ApplyCultureFromPreferencesAsync()
    {
        var culture = await _preferences.GetCultureAsync();
        ApplyCultureToThread(culture);

        if (_appliedCulture == culture)
        {
            return;
        }

        var module = await GetModuleAsync();
        await module.InvokeVoidAsync(
            "applyCulture",
            culture,
            SupportedCultures.IsRtl(culture));
        _appliedCulture = culture;
    }

    public static void ApplyCultureToThread(string culture)
    {
        var normalized = SupportedCultures.Normalize(culture);
        var cultureInfo = CultureInfo.GetCultureInfo(normalized);
        CultureInfo.CurrentCulture = cultureInfo;
        CultureInfo.CurrentUICulture = cultureInfo;
    }

    private async Task<IJSObjectReference> GetModuleAsync()
    {
        _module ??= await _jsRuntime.InvokeAsync<IJSObjectReference>("import", "./js/preferences.js");
        return _module;
    }
}
