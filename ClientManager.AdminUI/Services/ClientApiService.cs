using ClientManager.Shared.Models.Entities;

namespace ClientManager.AdminUI.Services;

public class ClientApiService(IHttpClientFactory httpClientFactory)
    : GenericApiService<ClientConfiguration>(httpClientFactory, "api/v2/clients");
