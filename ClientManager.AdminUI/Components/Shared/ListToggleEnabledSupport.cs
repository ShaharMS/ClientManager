using Radzen;

namespace ClientManager.AdminUI.Components.Shared;

/// <summary>
/// Shared confirm-and-toggle flow for enable/disable actions on catalog list pages.
/// </summary>
internal static class ListToggleEnabledSupport
{
    public static async Task ExecuteAsync(
        DialogService dialog,
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
            ? (disableMessage ?? $"Disable {entityLabel} \"{id}\"? Requests from this client will be rejected until it is re-enabled.")
            : (enableMessage ?? $"Enable {entityLabel} \"{id}\"? Requests will be allowed again, subject to access rules.");

        var confirmed = await dialog.Confirm(
            message,
            currentlyEnabled ? "Confirm disable" : "Confirm enable",
            new ConfirmOptions
            {
                OkButtonText = currentlyEnabled ? "Disable" : "Enable",
                CancelButtonText = "Cancel"
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
            var verb = currentlyEnabled ? "Disable" : "Enable";
            setActionError($"{verb} failed: {ex.Message}");
            refreshUi();
        }
    }
}
