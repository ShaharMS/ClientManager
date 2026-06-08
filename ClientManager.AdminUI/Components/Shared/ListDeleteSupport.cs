using Radzen;

namespace ClientManager.AdminUI.Components.Shared;

/// <summary>
/// Shared confirm-and-delete flow for catalog list pages.
/// </summary>
internal static class ListDeleteSupport
{
    public static async Task ExecuteAsync(
        DialogService dialog,
        string entityLabel,
        string id,
        Func<Task> deleteAsync,
        Func<Task> reloadAsync,
        Action<string?> setDeleteError,
        Action refreshUi)
    {
        var confirmed = await dialog.Confirm(
            $"Are you sure you want to delete {entityLabel} \"{id}\"? This cannot be undone.",
            "Confirm delete",
            new ConfirmOptions
            {
                OkButtonText = "Delete",
                CancelButtonText = "Cancel"
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
            setDeleteError($"Delete failed: {ex.Message}");
            refreshUi();
        }
    }
}
