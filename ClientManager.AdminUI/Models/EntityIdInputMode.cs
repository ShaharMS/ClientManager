namespace ClientManager.AdminUI.Models;

/// <summary>
/// Controls how <c>EntityIdInput</c> validates the value a user types as they type it.
/// </summary>
public enum EntityIdInputMode
{
    /// <summary>
    /// The value should reference an entity that already exists. Existing IDs are offered as
    /// autocomplete suggestions, and a warning is shown while the typed value does not match
    /// any known ID (likely a typo or a not-yet-created reference).
    /// </summary>
    Reference,

    /// <summary>
    /// The value must be a new, unused ID. Existing IDs are offered as suggestions so the user
    /// can see what is already taken, and an error is shown the moment the typed value collides
    /// with an existing ID (for example, trying to create a service that already exists).
    /// </summary>
    Unique
}
