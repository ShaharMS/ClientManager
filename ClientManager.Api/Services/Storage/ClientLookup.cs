namespace ClientManager.Api.Services.Storage;

public sealed record ClientLookup<T>(bool ClientExists, T? Value);