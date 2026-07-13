using System.Net.Http.Json;
using System.Text.Json;
using ClientManager.Tests.Helpers;

namespace ClientManager.Tests.Integration;

public sealed class OtlpExportTests : IAsyncLifetime
{
    private ClientManagerApiFactory? _factory;
    private HttpClient? _client;

    [JaegerIntegrationFact]
    public async Task Jaeger_receives_service_after_api_request_when_collector_is_running()
    {
        Assert.NotNull(_client);
        var startedAtUnixMicroseconds = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() * 1000;
        var response = await _client.GetAsync("api/v1/statistics/overview");
        response.EnsureSuccessStatusCode();

        using var jaeger = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        var deadline = DateTime.UtcNow.AddSeconds(15);
        while (DateTime.UtcNow < deadline)
        {
            var traces = await jaeger.GetFromJsonAsync<JaegerTracesResponse>(
                $"http://localhost:16686/api/traces?service=ClientManager.Api&start={startedAtUnixMicroseconds}&limit=20");
            if (traces is { Data.ValueKind: JsonValueKind.Array } && traces.Data.GetArrayLength() > 0)
            {
                return;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(500));
        }

        Assert.Fail("Jaeger did not receive a new ClientManager.Api trace for the request.");
    }

    public Task InitializeAsync()
    {
        _factory = new ClientManagerApiFactory().Configure(settings =>
        {
            settings["Observability:OtlpEndpoint"] = "http://localhost:4317";
        });
        _client = _factory.CreateClientWithBaseAddress();
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        if (_factory is not null)
        {
            await _factory.DisposeAsync();
        }
    }

    private sealed record JaegerTracesResponse
    {
        public JsonElement Data { get; init; }
    }
}

internal static class JaegerIntegrationGate
{
    public static async Task<bool> IsAvailableAsync()
    {
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
            using var response = await client.GetAsync("http://localhost:16686/");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
}

internal sealed class JaegerIntegrationFactAttribute : FactAttribute
{
    public JaegerIntegrationFactAttribute()
    {
        if (!JaegerIntegrationGate.IsAvailableAsync().GetAwaiter().GetResult())
        {
            Skip = "Jaeger unavailable. Start compose/otel.yml to run OTLP integration coverage.";
        }
    }
}
