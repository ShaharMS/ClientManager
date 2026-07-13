using System.Security.Cryptography.X509Certificates;
using ClientManager.Api.Storage.Stores.Implementations;
using ClientManager.Api.Storage.Stores.Interfaces;
using ClientManager.Shared.Configuration.Storage;
using ClientManager.Shared.Models.Enums;
using MongoDB.Driver;
using StackExchange.Redis;

namespace ClientManager.Api.Services.Storage.Extensions;

/// <summary>
/// Creates MongoDB and Redis document-store implementations from configured storage bindings.
/// </summary>
internal static class DocumentStoreFactory
{
    internal static IDocumentStore CreateDocumentStore(
        StorageRoleBinding binding,
        Dictionary<string, IMongoClient> mongoClients,
        Dictionary<string, IConnectionMultiplexer> redisMultiplexers)
    {
        return binding.Provider switch
        {
            PersistenceProvider.MongoDb => CreateMongoStore(binding.MongoDb, mongoClients),
            PersistenceProvider.Redis => CreateRedisStore(binding.Redis, redisMultiplexers),
            _ => throw new InvalidOperationException($"Unsupported persistence provider: {binding.Provider}")
        };
    }

    private static MongoDBDocumentStore CreateMongoStore(
        MongoDbStoreOptions? options,
        Dictionary<string, IMongoClient> clientCache)
    {
        if (options is null)
        {
            throw new InvalidOperationException("MongoDb settings are required for roles using the MongoDb provider.");
        }

        var client = GetOrCreate(clientCache, options.ConnectionString, () => CreateMongoClient(options));
        return new MongoDBDocumentStore(client.GetDatabase(options.DatabaseName));
    }

    private static IMongoClient CreateMongoClient(MongoDbStoreOptions options)
    {
        var settings = MongoClientSettings.FromConnectionString(options.ConnectionString);
        settings.ConnectTimeout = TimeSpan.FromSeconds(options.ConnectTimeoutSeconds);
        settings.MaxConnectionPoolSize = options.MaxConnectionPoolSize;
        settings.RetryWrites = options.RetryWrites;

        if (options.UseTls)
        {
            settings.UseTls = true;
            settings.SslSettings ??= new SslSettings();
            if (options.TlsCertificatePath is not null)
            {
                settings.SslSettings.ClientCertificates =
                    [LoadCertificate(options.TlsCertificatePath, options.TlsCertificatePassword)];
            }
        }

        if (options.AllowInsecureTls)
        {
            settings.AllowInsecureTls = true;
        }

        if (options.AuthenticationMechanism is not null)
        {
            settings.Credential = settings.Credential?.WithMechanismProperty("MECHANISM", options.AuthenticationMechanism);
        }

        return new MongoClient(settings);
    }

    private static RedisDocumentStore CreateRedisStore(
        RedisStoreOptions? options,
        Dictionary<string, IConnectionMultiplexer> multiplexerCache)
    {
        if (options is null)
        {
            throw new InvalidOperationException("Redis settings are required for roles using the Redis provider.");
        }

        var multiplexer = GetOrCreate(multiplexerCache, BuildRedisMultiplexerCacheKey(options), () => ConnectionMultiplexer.Connect(BuildRedisConfiguration(options)));
        return new RedisDocumentStore(multiplexer, options.DatabaseIndex, options.GlobalKeyPrefix);
    }

    private static ConfigurationOptions BuildRedisConfiguration(RedisStoreOptions options)
    {
        var config = new ConfigurationOptions
        {
            ConnectTimeout = options.ConnectTimeoutMilliseconds,
            ConnectRetry = options.ConnectRetry,
            AbortOnConnectFail = options.AbortOnConnectFail,
            SyncTimeout = options.SyncTimeoutMilliseconds,
            DefaultDatabase = options.DatabaseIndex,
            Ssl = options.UseSsl || options.UseTls
        };
        config.EndPoints.Add(options.Host, options.Port);

        if (!string.IsNullOrWhiteSpace(options.User))
        {
            config.User = options.User;
        }

        if (options.Password is not null)
        {
            config.Password = options.Password;
        }

        if (options.UseTls && options.TlsCertificatePath is not null)
        {
            var certificate = LoadCertificate(options.TlsCertificatePath, options.TlsCertificatePassword);
            config.CertificateSelection += (_, _, _, _, _) => certificate;
        }

        if (options.AllowInsecureTls)
        {
            config.CertificateValidation += (_, _, _, _) => true;
        }

        return config;
    }

    private static string BuildRedisMultiplexerCacheKey(RedisStoreOptions options) =>
        string.Join('|', options.Host, options.Port, options.UseSsl, options.UseTls, options.User ?? string.Empty, options.Password ?? string.Empty, options.TlsCertificatePath ?? string.Empty, options.AllowInsecureTls);

    private static TStore GetOrCreate<TKey, TStore>(Dictionary<TKey, TStore> cache, TKey key, Func<TStore> create) where TKey : notnull
    {
        if (cache.TryGetValue(key, out var existing))
        {
            return existing;
        }

        var created = create();
        cache[key] = created;
        return created;
    }

    private static X509Certificate2 LoadCertificate(string path, string? password) =>
        X509CertificateLoader.LoadPkcs12FromFile(path, password);
}
