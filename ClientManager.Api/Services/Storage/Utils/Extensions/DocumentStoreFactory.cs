using System.Security.Cryptography.X509Certificates;
using ClientManager.DataAccess.Stores.Implementations;
using ClientManager.DataAccess.Stores.Interfaces;
using ClientManager.Shared.Configuration.Storage;
using ClientManager.Shared.Models.Enums;

using MongoDB.Driver;
using StackExchange.Redis;

namespace ClientManager.Api.Services.Storage.Utils.Extensions;

/// <summary>
/// Creates concrete document-store implementations from configured storage bindings.
/// </summary>
internal static class DocumentStoreFactory
{
    internal static IDocumentStore CreateDocumentStore(
        StorageRoleBinding binding,
        Dictionary<string, JsonFileDocumentStore> jsonFileStores,
        Dictionary<string, LuceneDocumentStore> luceneStores,
        Dictionary<string, IMongoClient> mongoClients,
        Dictionary<string, IConnectionMultiplexer> redisMultiplexers)
    {
        return binding.Provider switch
        {
            PersistenceProvider.JsonFile => CreateJsonFileStore(binding.JsonFile, jsonFileStores),
            PersistenceProvider.MongoDb => CreateMongoStore(binding.MongoDb, mongoClients),
            PersistenceProvider.Redis => CreateRedisStore(binding.Redis, redisMultiplexers),
            PersistenceProvider.Lucene => CreateLuceneStore(binding.Lucene, luceneStores),
            _ => throw new InvalidOperationException($"Unsupported persistence provider: {binding.Provider}")
        };
    }

    private static JsonFileDocumentStore CreateJsonFileStore(
        JsonFileStoreOptions? options,
        Dictionary<string, JsonFileDocumentStore> storeCache)
    {
        var dataDirectory = ResolvePath((options ?? new JsonFileStoreOptions()).DataDirectory);
        return GetOrCreate(storeCache, dataDirectory, () => new JsonFileDocumentStore(dataDirectory));
    }

    private static MongoDBDocumentStore CreateMongoStore(
        MongoDbStoreOptions? options,
        Dictionary<string, IMongoClient> clientCache)
    {
        if (options is null)
        {
            throw new InvalidOperationException(
                "MongoDb settings are required for roles using the MongoDb provider.");
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

        ApplyMongoTls(settings, options);
        ApplyMongoAuthentication(settings, options);

        return new MongoClient(settings);
    }

    private static void ApplyMongoTls(MongoClientSettings settings, MongoDbStoreOptions options)
    {
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
    }

    private static void ApplyMongoAuthentication(MongoClientSettings settings, MongoDbStoreOptions options)
    {
        if (options.AuthenticationMechanism is null)
        {
            return;
        }

        settings.Credential = settings.Credential?.WithMechanismProperty(
            "MECHANISM",
            options.AuthenticationMechanism);
    }

    private static RedisDocumentStore CreateRedisStore(
        RedisStoreOptions? options,
        Dictionary<string, IConnectionMultiplexer> multiplexerCache)
    {
        if (options is null)
        {
            throw new InvalidOperationException(
                "Redis settings are required for roles using the Redis provider.");
        }

        var multiplexer = GetOrCreate(
            multiplexerCache,
            BuildRedisMultiplexerCacheKey(options),
            () => CreateRedisMultiplexer(options));

        return new RedisDocumentStore(multiplexer, options.DatabaseIndex, options.GlobalKeyPrefix);
    }

    private static IConnectionMultiplexer CreateRedisMultiplexer(RedisStoreOptions options)
    {
        try
        {
            return ConnectionMultiplexer.Connect(BuildRedisConfiguration(options));
        }
        catch (RedisException exception)
        {
            throw new InvalidOperationException(
                $"Failed to initialize the Redis storage provider. {DescribeRedisConnection(options)}",
                exception);
        }
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

    private static string DescribeRedisConnection(RedisStoreOptions options)
    {
        var details = new List<string>
        {
            $"host='{options.Host}'",
            $"port={options.Port}",
            $"user='{(string.IsNullOrWhiteSpace(options.User) ? "(default)" : options.User)}'",
            $"database={options.DatabaseIndex}",
            $"prefix='{(string.IsNullOrEmpty(options.GlobalKeyPrefix) ? "(none)" : options.GlobalKeyPrefix)}'",
            $"ssl={options.UseSsl || options.UseTls}",
            $"tls={options.UseTls}",
            $"abortOnConnectFail={options.AbortOnConnectFail}",
            $"connectTimeoutMs={options.ConnectTimeoutMilliseconds}",
            $"connectRetry={options.ConnectRetry}",
            $"syncTimeoutMs={options.SyncTimeoutMilliseconds}"
        };

        if (!string.IsNullOrWhiteSpace(options.TlsCertificatePath))
        {
            details.Add($"tlsCertificatePath='{options.TlsCertificatePath}'");
        }

        return string.Join(", ", details);
    }

    private static string BuildRedisMultiplexerCacheKey(RedisStoreOptions options)
    {
        return string.Join(
            '|',
            options.Host,
            options.Port,
            options.UseSsl,
            options.UseTls,
            options.User ?? string.Empty,
            options.Password ?? string.Empty,
            options.TlsCertificatePath ?? string.Empty,
            options.AllowInsecureTls);
    }

    private static LuceneDocumentStore CreateLuceneStore(
        LuceneStoreOptions? options,
        Dictionary<string, LuceneDocumentStore> storeCache)
    {
        var indexDirectory = ResolvePath((options ?? new LuceneStoreOptions()).IndexDirectory);
        return GetOrCreate(storeCache, indexDirectory, () => new LuceneDocumentStore(indexDirectory));
    }

    private static TStore GetOrCreate<TKey, TStore>(
        Dictionary<TKey, TStore> cache,
        TKey key,
        Func<TStore> create) where TKey : notnull
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

    private static string ResolvePath(string path) => Path.GetFullPath(path);
}