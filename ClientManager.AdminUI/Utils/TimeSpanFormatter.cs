namespace ClientManager.AdminUI.Utils;

/// <summary>
/// Compact duration labels for rate-limit windows and pool TTLs.
/// </summary>
public static class TimeSpanFormatter
{
    public static string FormatCompact(TimeSpan value)
    {
        if (value.TotalHours >= 1)
        {
            return $"{value.TotalHours:0.#}h";
        }

        if (value.TotalMinutes >= 1)
        {
            return $"{value.TotalMinutes:0.#}m";
        }

        return $"{value.TotalSeconds:0.#}s";
    }
}
