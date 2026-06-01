namespace ClientManager.Shared.Configuration.Storage;

/// <summary>
/// Connection and behavior settings for a Redis-backed storage role.
/// <para>
/// These options are not bound independently from configuration. They are referenced
/// either as a default (<see cref="PersistenceOptions.DefaultRedis"/>) or from a
/// per-role <see cref="StorageRoleBinding"/>.
/// </para>
/// </summary>
public class RedisStoreOptions
{
    /// <summary>
    /// Redis host or DNS name (for example <c>redis</c> or <c>cache-01.redis.internal</c>).
    /// </summary>
    public required string Host { get; set; }

    /// <summary>
    /// Redis TCP port.
    /// </summary>
    public int Port { get; set; }

    /// <summary>
    /// Optional ACL user name for Redis authentication.
    /// </summary>
    public string? User { get; set; }

    /// <summary>
    /// Controls the Redis SSL flag directly.
    /// </summary>
    public bool UseSsl { get; set; }

    /// <summary>
    /// Enables TLS behavior and implies SSL. When enabled, certificate settings are
    /// applied when configured.
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
    /// Number of connection retries attempted during the initial connect sequence.
    /// </summary>
    public int ConnectRetry { get; set; } = 5;

    /// <summary>
    /// When false, startup continues and the multiplexer keeps retrying if Redis is
    /// temporarily unavailable.
    /// </summary>
    public bool AbortOnConnectFail { get; set; }

    /// <summary>
    /// Maximum time in milliseconds to wait for a synchronous operation to complete.
    /// </summary>
    public int SyncTimeoutMilliseconds { get; set; } = 5000;

    /// <summary>
    /// The zero-based Redis database index to use.
    /// </summary>
    public int DatabaseIndex { get; set; }

    /// <summary>
    /// Optional prefix applied to every Redis key created by this backend.
    /// Include any delimiter characters you want preserved, such as <c>clientmanager:</c>.
    /// </summary>
    public string? GlobalKeyPrefix { get; set; }

    /// <summary>
    /// Optional password when the connection string does not embed credentials.
    /// </summary>
    public string? Password { get; set; }
}