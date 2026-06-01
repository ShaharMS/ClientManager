using System.Security.Cryptography.X509Certificates;
using ClientManager.DataAccess.Stores.Implementations;
using ClientManager.DataAccess.Stores.Interfaces;
using ClientManager.Shared.Configuration.Storage;
using ClientManager.Shared.Models.Enums;

using MongoDB.Driver;
using StackExchange.Redis;

namespace ClientManager.StorageApi.Utils.Extensions;

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
        var resolved = options ?? new JsonFileStoreOptions();
        var dataDirectory = ResolvePath(resolved.DataDirectory);
        if (!storeCache.TryGetValue(dataDirectory, out var store))
        {
            store = new JsonFileDocumentStore(dataDirectory);
            storeCache[dataDirectory] = store;
        }

        return store;
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

        if (!clientCache.TryGetValue(options.ConnectionString, out var client))
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
                    var certificate = X509CertificateLoader.LoadPkcs12FromFile(
                        options.TlsCertificatePath,
                        options.TlsCertificatePassword);
                    settings.SslSettings.ClientCertificates = [certificate];
                }
            }

            if (options.AllowInsecureTls)
            {
                settings.AllowInsecureTls = true;
            }

            if (options.AuthenticationMechanism is not null)
            {
                settings.Credential = settings.Credential?.WithMechanismProperty(
                    "MECHANISM",
                    options.AuthenticationMechanism);
            }

            client = new MongoClient(settings);
            clientCache[options.ConnectionString] = client;
        }

        return new MongoDBDocumentStore(client.GetDatabase(options.DatabaseName));
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

        var cacheKey = BuildRedisMultiplexerCacheKey(options);
        if (!multiplexerCache.TryGetValue(cacheKey, out var multiplexer))
        {
            var sslEnabled = options.UseSsl || options.UseTls;
            var config = new ConfigurationOptions
            {
                ConnectTimeout = options.ConnectTimeoutMilliseconds,
                ConnectRetry = options.ConnectRetry,
                AbortOnConnectFail = options.AbortOnConnectFail,
                SyncTimeout = options.SyncTimeoutMilliseconds,
                DefaultDatabase = options.DatabaseIndex,
                Ssl = sslEnabled
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

            if (options.UseTls)
            {
                if (options.TlsCertificatePath is not null)
                {
                    var certificate = X509CertificateLoader.LoadPkcs12FromFile(
                        options.TlsCertificatePath,
                        options.TlsCertificatePassword);
                    config.CertificateSelection += (_, _, _, _, _) => certificate;
                }
            }

            if (options.AllowInsecureTls)
            {
                config.CertificateValidation += (_, _, _, _) => true;
            }

            multiplexer = ConnectionMultiplexer.Connect(config);
            multiplexerCache[cacheKey] = multiplexer;
        }

        return new RedisDocumentStore(multiplexer, options.DatabaseIndex, options.GlobalKeyPrefix);
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
        var resolved = options ?? new LuceneStoreOptions();
        var indexDirectory = ResolvePath(resolved.IndexDirectory);
        if (!storeCache.TryGetValue(indexDirectory, out var store))
        {
            store = new LuceneDocumentStore(indexDirectory);
            storeCache[indexDirectory] = store;
        }

        return store;
    }

    private static string ResolvePath(string path) => Path.GetFullPath(path);
}