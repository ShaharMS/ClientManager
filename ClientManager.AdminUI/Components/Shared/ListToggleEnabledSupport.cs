using System.Globalization;
using ClientManager.AdminUI.Resources;
using ClientManager.AdminUI.Services;
using Microsoft.Extensions.Localization;
using Radzen;

namespace ClientManager.AdminUI.Components.Shared;

/// <summary>
/// Shared confirm-and-toggle flow for enable/disable actions on catalog list pages.
/// </summary>
internal static class ListToggleEnabledSupport
{
    public static async Task ExecuteAsync(
        DialogService dialog,
        IStringLocalizer<SharedResources> localizer,
        ApiErrorLocalizer errors,
        string entityLabel,
        string id,
        bool currentlyEnabled,
        Func<Task> toggleAsync,
        Func<Task> reloadAsync,
        Action<string?> setActionError,
        Action refreshUi,
        string? disableMessage = null,
        string? enableMessage = null)
    {
        var message = currentlyEnabled
            ? (disableMessage ?? string.Format(CultureInfo.CurrentCulture, localizer["Dialog.ConfirmDisable.DefaultMessage"], entityLabel, id))
            : (enableMessage ?? string.Format(CultureInfo.CurrentCulture, localizer["Dialog.ConfirmEnable.DefaultMessage"], entityLabel, id));

        var confirmed = await dialog.Confirm(
            message,
            currentlyEnabled ? localizer["Dialog.ConfirmDisable.Title"] : localizer["Dialog.ConfirmEnable.Title"],
            new ConfirmOptions
            {
                OkButtonText = currentlyEnabled ? localizer["Dialog.ConfirmDisable.Ok"] : localizer["Dialog.ConfirmEnable.Ok"],
                CancelButtonText = localizer["Dialog.ConfirmToggle.Cancel"]
            });

        if (confirmed != true)
        {
            return;
        }

        setActionError(null);
        refreshUi();

        try
        {
            await toggleAsync();
            await reloadAsync();
            refreshUi();
        }
        catch (HttpRequestException ex)
        {
            var verb = currentlyEnabled ? localizer["Dialog.ConfirmDisable.Ok"] : localizer["Dialog.ConfirmEnable.Ok"];
            setActionError(string.Format(CultureInfo.CurrentCulture, localizer["Dialog.ToggleFailed"], verb, errors.Localize(ex)));
            refreshUi();
        }
    }
}
