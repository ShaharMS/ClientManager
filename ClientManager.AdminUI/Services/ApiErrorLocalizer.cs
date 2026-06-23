using System.Globalization;
using ClientManager.AdminUI.Localization;
using ClientManager.AdminUI.Resources;
using Microsoft.Extensions.Localization;

namespace ClientManager.AdminUI.Services;

public class ApiErrorLocalizer
{
    private readonly IStringLocalizer<SharedResources> _localizer;

    public ApiErrorLocalizer(IStringLocalizer<SharedResources> localizer)
    {
        _localizer = localizer;
    }

    public string Localize(Exception exception)
    {
        if (exception is ApiProblemException problem && !string.IsNullOrEmpty(problem.ErrorCode))
        {
            var key = $"Errors.{problem.ErrorCode}";
            var localized = _localizer[key];
            if (!localized.ResourceNotFound && !string.IsNullOrWhiteSpace(localized.Value))
            {
                return localized.Value;
            }
        }

        return exception.Message;
    }

    public string Format(string resourceKey, Exception exception) =>
        string.Format(CultureInfo.CurrentCulture, _localizer[resourceKey].Value, Localize(exception));
}
