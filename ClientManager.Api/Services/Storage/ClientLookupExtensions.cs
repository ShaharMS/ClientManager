namespace ClientManager.Api.Services.Storage;

/// <summary>
/// Resolves <see cref="ClientLookup{T}"/> into a value or throws the caller-supplied domain exceptions.
/// </summary>
public static class ClientLookupExtensions
{
    public static T RequireClientValue<T>(
        this ClientLookup<T> lookup,
        string clientId,
        Func<string, Exception> missingClient,
        Func<Exception> missingValue) =>
        !lookup.ClientExists
            ? throw missingClient(clientId)
            : lookup.Value ?? throw missingValue();
}
