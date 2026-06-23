using System.Globalization;
using ClientManager.AdminUI.Resources;
using Microsoft.Extensions.Localization;

namespace ClientManager.AdminUI.Localization;

public static class LocalizationValidator
{
    public static void ValidateDevelopment(
        IStringLocalizer<SharedResources> localizer,
        IWebHostEnvironment environment)
    {
        if (!environment.IsDevelopment())
        {
            return;
        }

        foreach (var culture in SupportedCultures.Codes)
        {
            var previous = CultureInfo.CurrentUICulture;
            try
            {
                var cultureInfo = CultureInfo.GetCultureInfo(culture);
                CultureInfo.CurrentUICulture = cultureInfo;
                CultureInfo.CurrentCulture = cultureInfo;

                var probe = localizer[SupportedCultures.ProbeKey];
                if (probe.ResourceNotFound || string.IsNullOrWhiteSpace(probe.Value))
                {
                    throw new InvalidOperationException(
                        $"Missing localization probe '{SupportedCultures.ProbeKey}' for culture '{culture}'. "
                        + (culture == SupportedCultures.Default
                            ? "Add it to Resources/SharedResources.resx."
                            : $"Add Resources/SharedResources.{culture}.resx."));
                }

                foreach (var optionCode in SupportedCultures.Codes)
                {
                    var key = $"LanguageOption.{optionCode}";
                    var label = localizer[key];
                    if (label.ResourceNotFound || string.IsNullOrWhiteSpace(label.Value))
                    {
                        throw new InvalidOperationException(
                            $"Missing '{key}' in culture '{culture}'. Add LanguageOption.* entries for every supported culture.");
                    }
                }

                if (culture == "he-IL")
                {
                    var settingsTitle = localizer["Settings.Title"];
                    if (settingsTitle.ResourceNotFound
                        || settingsTitle.Value is "Settings" or "Settings.Title")
                    {
                        throw new InvalidOperationException(
                            "Hebrew satellite resources are not loading (Settings.Title still English). "
                            + "Rebuild the project and ensure SharedResources.he-IL.resx is included.");
                    }
                }
            }
            finally
            {
                CultureInfo.CurrentUICulture = previous;
                CultureInfo.CurrentCulture = previous;
            }
        }
    }
}
