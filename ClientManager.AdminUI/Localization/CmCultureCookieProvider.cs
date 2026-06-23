using Microsoft.AspNetCore.Localization;

namespace ClientManager.AdminUI.Localization;

public class CmCultureCookieProvider : IRequestCultureProvider
{
    public const string CookieName = "cm-culture";

    public Task<ProviderCultureResult?> DetermineProviderCultureResult(HttpContext httpContext)
    {
        if (httpContext.Request.Cookies.TryGetValue(CookieName, out var culture)
            && SupportedCultures.IsSupported(culture))
        {
            var normalized = SupportedCultures.Normalize(culture);
            return Task.FromResult<ProviderCultureResult?>(
                new ProviderCultureResult(normalized, normalized));
        }

        return Task.FromResult<ProviderCultureResult?>(null);
    }
}
