namespace ClientManager.Api.Models.Exceptions;

/// <summary>
/// Thrown when the persistence configuration is invalid or incomplete — for example,
/// when a provider is selected but its required connection settings are missing.
/// </summary>
public class PersistenceConfigurationException(string message) : Exception(message);
