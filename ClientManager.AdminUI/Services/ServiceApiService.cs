using ClientManager.Shared.Models.Entities;

namespace ClientManager.AdminUI.Services;

public class ServiceApiService(IHttpClientFactory httpClientFactory)
    : GenericApiService<Service>(httpClientFactory, "api/v2/services");
