namespace ClientManager.AdminUI.Models;

/// <summary>
/// Visual validation state for outlined inputs with a notched border label.
/// </summary>
public enum InputValidationState
{
    /// <summary>No validation feedback (empty, disabled, or not yet evaluated).</summary>
    Neutral,

    /// <summary>Value passes validation (green notched border).</summary>
    Valid,

    /// <summary>Value fails validation (red notched border).</summary>
    Invalid
}
