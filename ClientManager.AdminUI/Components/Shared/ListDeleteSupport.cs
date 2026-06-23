using System.Globalization;
using ClientManager.AdminUI.Resources;
using ClientManager.AdminUI.Services;
using Microsoft.Extensions.Localization;
using Radzen;

namespace ClientManager.AdminUI.Components.Shared;

/// <summary>
/// Shared confirm-and-delete flow for catalog list pages.
/// </summary>
internal static class ListDeleteSupport
{
    public static async Task ExecuteAsync(
        DialogService dialog,
        IStringLocalizer<SharedResources> localizer,
        ApiErrorLocalizer errors,
        string entityLabel,
        string id,
        Func<Task> deleteAsync,
        Func<Task> reloadAsync,
        Action<string?> setDeleteError,
        Action refreshUi)
    {
        var confirmed = await dialog.Confirm(
            string.Format(CultureInfo.CurrentCulture, localizer["Dialog.ConfirmDelete.Message"], entityLabel, id),
            localizer["Dialog.ConfirmDelete.Title"],
            new ConfirmOptions
            {
                OkButtonText = localizer["Dialog.ConfirmDelete.Ok"],
                CancelButtonText = localizer["Dialog.ConfirmDelete.Cancel"]
            });

        if (confirmed != true)
        {
            return;
        }

        setDeleteError(null);
        refreshUi();

        try
        {
            await deleteAsync();
            await reloadAsync();
            refreshUi();
        }
        catch (HttpRequestException ex)
        {
            setDeleteError(errors.Format("Dialog.ConfirmDelete.Failed", ex));
            refreshUi();
        }
    }
}
