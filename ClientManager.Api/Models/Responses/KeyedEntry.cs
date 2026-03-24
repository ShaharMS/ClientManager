namespace ClientManager.Api.Models.Responses;

/// <summary>
/// Wraps a single key-value entry from a dictionary for paginated list responses.
/// </summary>
/// <typeparam name="T">The type of the value.</typeparam>
/// <param name="Key">The dictionary key.</param>
/// <param name="Value">Its attached value.</param>
public record KeyedEntry<T>(string Key, T Value);
