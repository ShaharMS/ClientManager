using ClientManager.Api.Models.Configuration;
using Microsoft.Extensions.Options;

namespace ClientManager.Tests.Unit;

public sealed class ObservabilityConfigurationTests
{
    private readonly ObservabilityOptionsValidator _validator = new();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Empty_otlp_endpoint_is_valid(string? endpoint)
    {
        var result = _validator.Validate(null, new ObservabilityOptions { OtlpEndpoint = endpoint });
        Assert.True(result.Succeeded);
    }

    [Theory]
    [InlineData("http://localhost:4317")]
    [InlineData("https://collector.example:4318")]
    public void Absolute_otlp_endpoint_is_valid(string endpoint)
    {
        var result = _validator.Validate(null, new ObservabilityOptions { OtlpEndpoint = endpoint });
        Assert.True(result.Succeeded);
    }

    [Theory]
    [InlineData("not-a-uri")]
    [InlineData("ftp://collector.example")]
    public void Invalid_otlp_endpoint_is_rejected(string endpoint)
    {
        var result = _validator.Validate(null, new ObservabilityOptions { OtlpEndpoint = endpoint });
        Assert.False(result.Succeeded);
        Assert.Contains("OtlpEndpoint", result.FailureMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void Program_otlp_gate_matches_validator_for_http_endpoint()
    {
        const string endpoint = "http://localhost:4317";
        var validator = _validator.Validate(null, new ObservabilityOptions { OtlpEndpoint = endpoint });
        Assert.True(validator.Succeeded);
        Assert.True(Uri.TryCreate(endpoint, UriKind.Absolute, out var parsed));
        Assert.Equal("http", parsed!.Scheme);
    }
}
