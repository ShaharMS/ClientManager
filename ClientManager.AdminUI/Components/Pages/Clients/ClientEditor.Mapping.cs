using ClientManager.AdminUI.Models.Client;
using ClientManager.Shared.Models.Entities;
using ClientManager.Shared.Models.Enums;

namespace ClientManager.AdminUI.Components.Pages.Clients;

public partial class ClientEditor
{
    private async Task LoadExistingClientAsync()
    {
        var config = await ClientApi.GetByIdAsync(Id!);
        if (config is null)
        {
            _error = L["Pages.Clients.NotFound"];
            return;
        }

        _model = new ClientFormModel
        {
            Id = config.Id,
            Name = config.Name,
            IsEnabled = config.IsEnabled,
            ContributesToGlobalLimits = config.ContributesToGlobalLimits,
            ExemptFromGlobalLimits = config.ExemptFromGlobalLimits
        };

        if (config.GlobalRateLimit is not null)
        {
            _hasGlobalRateLimit = true;
            _globalRateLimit = new ClientRateLimitEntryModel
            {
                Strategy = config.GlobalRateLimit.Strategy,
                MaxRequests = config.GlobalRateLimit.MaxRequests,
                TokensPerRefill = config.GlobalRateLimit.TokensPerRefill
            };
            _globalRateLimitWindowSeconds = config.GlobalRateLimit.Window.TotalSeconds;
        }

        _serviceEntries = config.Services.Select(kvp => new ServiceEntryModel
        {
            ServiceId = kvp.Key,
            IsAllowed = kvp.Value.IsAllowed,
            ContributesToGlobalLimit = kvp.Value.ContributesToGlobalLimit,
            ExemptFromGlobalLimit = kvp.Value.ExemptFromGlobalLimit,
            HasRateLimit = kvp.Value.RateLimit is not null,
            RateLimitStrategy = kvp.Value.RateLimit?.Strategy ?? RateLimitStrategy.FixedWindow,
            RateLimitMaxRequests = kvp.Value.RateLimit?.MaxRequests ?? 100,
            RateLimitWindowSeconds = kvp.Value.RateLimit?.Window.TotalSeconds ?? 60,
            RateLimitTokensPerRefill = kvp.Value.RateLimit?.TokensPerRefill
        }).ToList();

        _poolEntries = config.ResourcePools.Select(kvp => new PoolEntryModel
        {
            PoolId = kvp.Key,
            MaxSlots = kvp.Value.MaxSlots
        }).ToList();
    }

    private ClientConfiguration BuildConfiguration() => new()
    {
        Id = _model.Id,
        Name = _model.Name,
        IsEnabled = _model.IsEnabled,
        ContributesToGlobalLimits = _model.ContributesToGlobalLimits,
        ExemptFromGlobalLimits = _model.ExemptFromGlobalLimits,
        GlobalRateLimit = _hasGlobalRateLimit
            ? new ClientRateLimit
            {
                Strategy = _globalRateLimit.Strategy,
                MaxRequests = _globalRateLimit.MaxRequests,
                Window = TimeSpan.FromSeconds(_globalRateLimitWindowSeconds),
                TokensPerRefill = _globalRateLimit.Strategy == RateLimitStrategy.TokenBucket
                    ? _globalRateLimit.TokensPerRefill : null
            }
            : null,
        Services = _serviceEntries.ToDictionary(
            e => e.ServiceId,
            e => new ServiceAccessSettings
            {
                IsAllowed = e.IsAllowed,
                ContributesToGlobalLimit = e.ContributesToGlobalLimit,
                ExemptFromGlobalLimit = e.ExemptFromGlobalLimit,
                RateLimit = e.HasRateLimit
                    ? new ClientRateLimit
                    {
                        Strategy = e.RateLimitStrategy,
                        MaxRequests = e.RateLimitMaxRequests,
                        Window = TimeSpan.FromSeconds(e.RateLimitWindowSeconds),
                        TokensPerRefill = e.RateLimitStrategy == RateLimitStrategy.TokenBucket
                            ? e.RateLimitTokensPerRefill : null
                    }
                    : null
            }),
        ResourcePools = _poolEntries.ToDictionary(
            e => e.PoolId,
            e => new ResourcePoolSettings { MaxSlots = e.MaxSlots })
    };
}
