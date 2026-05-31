namespace ClientManager.Api.Models.Configuration;

/// <summary>
/// Startup settings that control URL-segment API versioning for the public API.
/// </summary>
public sealed class ApiVersioningSettings
{
    /// <summary>
    /// The configuration section name.
    /// </summary>
    public const string SectionName = "ApiVersioning";

    /// <summary>
    /// The default API version assumed when a request does not specify one (for example "1.0").
    /// </summary>
    public string DefaultVersion { get; init; } = "1.0";
}
