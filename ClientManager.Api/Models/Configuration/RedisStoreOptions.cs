namespace ClientManager.Api.Models.Configuration;

/// <summary>
/// Connection and behavior settings for a Redis-backed storage role.
/// <para>
/// These options are not bound independently from configuration — they are referenced
/// either as a default (<see cref="PersistenceOptions.DefaultRedis"/>) or from a
/// per-role <see cref="StorageRoleBinding"/>.
/// </para>
/// </summary>
public class RedisStoreOptions
{
    /// <summary>
    /// The Redis connection string (e.g. <c>localhost:6379</c>).
    /// </summary>
    public required string ConnectionString { get; set; }

    /// <summary>
    /// Whether to encrypt the connection with TLS.
    /// </summary>
    public bool UseTls { get; set; }

    /// <summary>
    /// Path to a PFX client certificate used for TLS mutual authentication.
    /// </summary>
    public string? TlsCertificatePath { get; set; }

    /// <summary>
    /// Password for the PFX client certificate specified in <see cref="TlsCertificatePath"/>.
    /// </summary>
    public string? TlsCertificatePassword { get; set; }

    /// <summary>
    /// Skips server certificate validation. Intended for development environments only.
    /// </summary>
    public bool AllowInsecureTls { get; set; }

    /// <summary>
    /// Maximum time in milliseconds to wait when establishing a connection to the server.
    /// </summary>
    public int ConnectTimeoutMilliseconds { get; set; } = 5000;

    /// <summary>
    /// Maximum time in milliseconds to wait for a synchronous operation to complete.
    /// </summary>
    public int SyncTimeoutMilliseconds { get; set; } = 5000;

    /// <summary>
    /// The zero-based Redis database index to use.
    /// </summary>
    public int DatabaseIndex { get; set; }

    /// <summary>
    /// Optional password when the connection string does not embed credentials.
    /// </summary>
    public string? Password { get; set; }
}
