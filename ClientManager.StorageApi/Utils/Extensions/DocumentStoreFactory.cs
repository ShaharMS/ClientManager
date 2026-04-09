using System.Security.Cryptography.X509Certificates;
using ClientManager.DataAccess.Stores.Implementations;
using ClientManager.DataAccess.Stores.Interfaces;
using ClientManager.Shared.Configuration.Storage;
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
        Dictionary<string, IMongoClient> mongoClients,
        Dictionary<string, IConnectionMultiplexer> redisMultiplexers)
    {
        return binding.Provider switch
        {
            ClientManager.Shared.Models.Enums.PersistenceProvider.JsonFile => CreateJsonFileStore(binding.JsonFile),
            ClientManager.Shared.Models.Enums.PersistenceProvider.MongoDb => CreateMongoStore(binding.MongoDb, mongoClients),
            ClientManager.Shared.Models.Enums.PersistenceProvider.Redis => CreateRedisStore(binding.Redis, redisMultiplexers),
            ClientManager.Shared.Models.Enums.PersistenceProvider.Lucene => CreateLuceneStore(binding.Lucene),
            _ => throw new InvalidOperationException($"Unsupported persistence provider: {binding.Provider}")
        };
    }

    private static JsonFileDocumentStore CreateJsonFileStore(JsonFileStoreOptions? options)
    {
        var resolved = options ?? new JsonFileStoreOptions();
        return new JsonFileDocumentStore(resolved.DataDirectory);
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

        if (!multiplexerCache.TryGetValue(options.ConnectionString, out var multiplexer))
        {
            var config = ConfigurationOptions.Parse(options.ConnectionString);
            config.ConnectTimeout = options.ConnectTimeoutMilliseconds;
            config.SyncTimeout = options.SyncTimeoutMilliseconds;
            config.DefaultDatabase = options.DatabaseIndex;

            if (options.Password is not null)
            {
                config.Password = options.Password;
            }

            if (options.UseTls)
            {
                config.Ssl = true;

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
            multiplexerCache[options.ConnectionString] = multiplexer;
        }

        return new RedisDocumentStore(multiplexer);
    }

    private static LuceneDocumentStore CreateLuceneStore(LuceneStoreOptions? options)
    {
        var resolved = options ?? new LuceneStoreOptions();
        return new LuceneDocumentStore(resolved.IndexDirectory);
    }
}