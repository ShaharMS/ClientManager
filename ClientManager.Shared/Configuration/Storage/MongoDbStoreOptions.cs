namespace ClientManager.Shared.Configuration.Storage;

/// <summary>
/// Connection and behavior settings for a MongoDB-backed storage role.
/// <para>
/// These options are not bound independently from configuration. They are referenced
/// either as a default (<see cref="PersistenceOptions.DefaultMongoDb"/>) or from a
/// per-role <see cref="StorageRoleBinding"/>.
/// </para>
/// </summary>
public class MongoDbStoreOptions
{
    /// <summary>
    /// The MongoDB connection string (for example <c>mongodb://localhost:27017</c>).
    /// </summary>
    public required string ConnectionString { get; set; }

    /// <summary>
    /// The database name to use on the server.
    /// </summary>
    public string DatabaseName { get; set; } = "ClientManager";

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
    /// The authentication mechanism to use (for example <c>SCRAM-SHA-256</c> or
    /// <c>MONGODB-X509</c>). When <c>null</c>, the driver negotiates automatically.
    /// </summary>
    public string? AuthenticationMechanism { get; set; }

    /// <summary>
    /// Maximum time in seconds to wait when establishing a new connection to the server.
    /// </summary>
    public int ConnectTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Maximum number of connections allowed in the connection pool.
    /// </summary>
    public int MaxConnectionPoolSize { get; set; } = 100;

    /// <summary>
    /// Whether write operations should be automatically retried once on transient errors.
    /// </summary>
    public bool RetryWrites { get; set; } = true;
}