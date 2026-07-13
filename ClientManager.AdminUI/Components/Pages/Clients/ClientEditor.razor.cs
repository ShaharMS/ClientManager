using ClientManager.AdminUI.Models.Client;
using ClientManager.AdminUI.Services;
using ClientManager.Shared.Models.Entities;
using Microsoft.AspNetCore.Components;

namespace ClientManager.AdminUI.Components.Pages.Clients;

public partial class ClientEditor : ComponentBase
{
    [Parameter] public string? Id { get; set; }
    [Inject] private ClientApiService ClientApi { get; set; } = null!;
    [Inject] private ServiceApiService ServiceApi { get; set; } = null!;
    [Inject] private NavigationManager Nav { get; set; } = null!;
    [Inject] private ApiErrorLocalizer Errors { get; set; } = null!;

    private bool _isNew => string.IsNullOrEmpty(Id) || Id == "new";
    private bool _loading = true;
    private string? _error;

    private ClientFormModel _model = new();
    private bool _hasGlobalRateLimit;
    private ClientRateLimitEntryModel _globalRateLimit = new();
    private double _globalRateLimitWindowSeconds = 60;
    private List<ServiceEntryModel> _serviceEntries = [];
    private List<string> _existingClientIds = [];
    private List<string> _serviceIds = [];

    protected override async Task OnInitializedAsync()
    {
        try
        {
            _existingClientIds = (await ClientApi.GetAllAsync()).Select(c => c.Id).ToList();
            _serviceIds = (await ServiceApi.GetAllAsync()).Select(s => s.Id).ToList();

            if (!_isNew)
            {
                await LoadExistingClientAsync();
            }
        }
        catch (HttpRequestException ex)
        {
            _error = Errors.Format("Api.UnableToConnect", ex);
        }
        finally
        {
            _loading = false;
        }
    }

    private async Task SaveAsync()
    {
        var config = BuildConfiguration();

        try
        {
            if (_isNew)
            {
                await ClientApi.CreateAsync(config);
            }
            else
            {
                await ClientApi.UpdateAsync(Id!, config);
            }

            Nav.NavigateTo("/clients");
        }
        catch (HttpRequestException ex)
        {
            _error = Errors.Format("Pages.Clients.SaveFailed", ex);
        }
    }

    private void OnGlobalRateLimitToggled(bool enabled)
    {
        _hasGlobalRateLimit = enabled;
        if (enabled)
        {
            _globalRateLimit = new ClientRateLimitEntryModel();
            _globalRateLimitWindowSeconds = 60;
        }
    }
}
