using ClientManager.Api.Interfaces;
using ClientManager.Api.Models.Exceptions;
using ClientManager.Api.Models.Responses;
using ClientManager.DataAccess.Interfaces;
using ClientManager.Shared.Models.Entities;

namespace ClientManager.Api.Services;

/// <summary>
/// Evaluates deny-by-default access policies for clients against services.
/// Combines client configuration lookup with rate limit evaluation at all three scopes.
/// </summary>
public class AccessControlService : IAccessControlService
{
    private readonly ILogger<AccessControlService> _logger;
    private readonly IClientConfigurationRepository _clientConfigRepository;
    private readonly IEntityRepository<Service> _serviceRepository;
    private readonly IRateLimitService _rateLimitService;

    /// <summary>
    /// Initializes a new instance of <see cref="AccessControlService"/>.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="clientConfigRepository">Repository for client configurations.</param>
    /// <param name="serviceRepository">Repository for service definitions.</param>
    /// <param name="rateLimitService">Service for evaluating rate limits.</param>
    public AccessControlService(
        ILogger<AccessControlService> logger,
        IClientConfigurationRepository clientConfigRepository,
        IEntityRepository<Service> serviceRepository,
        IRateLimitService rateLimitService)
    {
        _logger = logger;
        _clientConfigRepository = clientConfigRepository;
        _serviceRepository = serviceRepository;
        _rateLimitService = rateLimitService;
    }

    /// <inheritdoc />
    public async Task<AccessCheckResponse> CheckAccessAsync(
        string clientId,
        string serviceId,
        CancellationToken cancellationToken = default)
    {
        var config = await _clientConfigRepository.GetByIdAsync(clientId, cancellationToken);
        if (config is null)
        {
            throw new NotFoundException($"Client '{clientId}' not found");
        }

        if (!config.IsEnabled)
        {
            throw new ClientDisabledException(clientId);
        }

        var service = await _serviceRepository.GetByIdAsync(serviceId, cancellationToken);
        if (service is null)
        {
            throw new NotFoundException($"Service '{serviceId}' not found");
        }

        if (!service.IsEnabled)
        {
            throw new NotFoundException($"Service '{serviceId}' is disabled");
        }

        if (!config.Services.TryGetValue(serviceId, out var serviceSettings) || !serviceSettings.IsAllowed)
        {
            throw new AccessDeniedException(clientId, serviceId);
        }

        // Check global service rate limit before per-client limits
        var globalResult = await _rateLimitService.CheckGlobalServiceLimitAsync(clientId, serviceId, cancellationToken);
        if (!globalResult.IsAllowed)
        {
            throw new RateLimitedException("Global service rate limit exceeded", globalResult.RetryAfterSeconds);
        }

        // Check per-client rate limit (this increments the counter)
        var result = await _rateLimitService.CheckAndIncrementAsync(clientId, serviceId, cancellationToken);
        if (!result.IsAllowed)
        {
            throw new RateLimitedException("Rate limit exceeded", result.RetryAfterSeconds);
        }

        _logger.LogInformation("Access granted | ClientId={ClientId}, ServiceId={ServiceId}",
            clientId, serviceId);

        return new AccessCheckResponse
        {
            ClientId = clientId,
            ServiceId = serviceId,
            RemainingRequests = result.RemainingRequests
        };
    }

    /// <inheritdoc />
    public async Task<ClientAccessibilityResponse> GetClientAccessibilityAsync(
        string clientId,
        CancellationToken cancellationToken = default)
    {
        var config = await _clientConfigRepository.GetByIdAsync(clientId, cancellationToken);
        if (config is null)
        {
            throw new NotFoundException($"Client '{clientId}' not found");
        }

        var services = await _serviceRepository.GetAllAsync(cancellationToken);

        var serviceAccessibilities = new List<ServiceAccessibility>();
        foreach (var service in services)
        {
            var hasAccess = config.Services.TryGetValue(service.Id, out var settings) && settings.IsAllowed && service.IsEnabled;

            var isRateLimited = false;
            int? remainingRequests = null;

            if (hasAccess)
            {
                var rateLimitStatus = await _rateLimitService.CheckWithoutIncrementAsync(clientId, service.Id, cancellationToken);
                isRateLimited = !rateLimitStatus.IsAllowed;
                remainingRequests = rateLimitStatus.RemainingRequests;
            }

            serviceAccessibilities.Add(new ServiceAccessibility
            {
                ServiceId = service.Id,
                HasAccess = hasAccess,
                IsCurrentlyRateLimited = isRateLimited,
                RemainingRequests = remainingRequests
            });
        }

        return new ClientAccessibilityResponse
        {
            ClientId = clientId,
            Services = serviceAccessibilities
        };
    }
}
