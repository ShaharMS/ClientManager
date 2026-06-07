namespace ClientManager.AdminUI.Models;

/// <summary>
/// Controls how <c>EntityIdInput</c> validates the value a user types as they type it.
/// </summary>
public enum EntityIdInputMode
{
    /// <summary>
    /// The value must be one of the known IDs. A filterable dropdown opens on focus; only
    /// selections from the list are valid (green), unknown values show as invalid (red).
    /// </summary>
    Reference,

    /// <summary>
    /// The value must be a new, unused ID. Existing IDs are offered as autocomplete hints;
    /// a collision with an existing ID is invalid (red), an available ID is valid (green).
    /// </summary>
    Unique
}
