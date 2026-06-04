using ClientManager.Shared.Models.Entities;

namespace ClientManager.AdminUI.Services;

public class ResourcePoolApiService(IHttpClientFactory httpClientFactory)
    : GenericApiService<ResourcePool>(httpClientFactory, "api/v1/resource-pools");
