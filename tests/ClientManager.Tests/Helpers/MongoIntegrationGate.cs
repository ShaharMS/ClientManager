using MongoDB.Bson;
using MongoDB.Driver;

namespace ClientManager.Tests.Helpers;

/// <summary>
/// Detects a local MongoDB replica set for optional integration tests.
/// </summary>
public static class MongoIntegrationGate
{
    public const string ConnectionStringEnvVar = "CLIENTMANAGER_TEST_MONGO_CONNECTION";

    public static string ConnectionString { get; } =
        Environment.GetEnvironmentVariable(ConnectionStringEnvVar)
        ?? "mongodb://localhost:27017/?replicaSet=rs0";

    private static readonly Lazy<bool> AvailableLazy = new(TryConnect);

    public static bool IsAvailable => AvailableLazy.Value;

    private static bool TryConnect()
    {
        try
        {
            var settings = MongoClientSettings.FromConnectionString(ConnectionString);
            settings.ServerSelectionTimeout = TimeSpan.FromSeconds(3);
            var client = new MongoClient(settings);
            var admin = client.GetDatabase("admin");
            var status = admin.RunCommand<BsonDocument>(new BsonDocument("replSetGetStatus", 1));
            return status.Contains("ok")
                && status["ok"].ToInt32() == 1
                && status.GetValue("myState", 0).ToInt32() == 1;
        }
        catch
        {
            return false;
        }
    }
}


/// <summary>Marks a test as skipped at discovery when no MongoDB replica set is available.</summary>
public sealed class MongoIntegrationFactAttribute : FactAttribute
{
    public MongoIntegrationFactAttribute()
    {
        if (!MongoIntegrationGate.IsAvailable)
        {
            Skip =
                $"MongoDB replica set unavailable. Start compose/dev.mongo.yml or set "
                + $"{MongoIntegrationGate.ConnectionStringEnvVar}.";
        }
    }
}
