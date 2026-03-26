namespace ClientManager.Api.Models.Configuration;

/// <summary>
/// Settings for JSON-file-based storage. Primarily intended for local development
/// or single-instance deployments with a shared persistent volume.
/// <para>
/// These options are not bound independently from configuration — they are referenced
/// either as a default (<see cref="PersistenceOptions.DefaultJsonFile"/>) or from a
/// per-role <see cref="StorageRoleBinding"/>.
/// </para>
/// </summary>
public class JsonFileStoreOptions
{
    /// <summary>
    /// The directory path where JSON data files are stored.
    /// </summary>
    public string DataDirectory { get; set; } = "./data";

    /// <summary>
    /// Whether to format the JSON output with indentation for readability.
    /// </summary>
    public bool PrettyPrint { get; set; } = true;
}
