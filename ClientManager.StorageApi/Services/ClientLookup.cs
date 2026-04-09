namespace ClientManager.StorageApi.Services;

public sealed record ClientLookup<T>(bool ClientExists, T? Value);