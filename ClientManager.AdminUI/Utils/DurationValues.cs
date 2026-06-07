using ClientManager.AdminUI.Models;

namespace ClientManager.AdminUI.Utils;

public static class DurationValues
{
    public static (double Amount, DurationUnit Unit) FromTotalSeconds(double totalSeconds)
    {
        if (totalSeconds <= 0)
        {
            return (0, DurationUnit.Seconds);
        }

        if (totalSeconds >= 3600 && totalSeconds % 3600 == 0)
        {
            return (totalSeconds / 3600, DurationUnit.Hours);
        }

        if (totalSeconds >= 60 && totalSeconds % 60 == 0)
        {
            return (totalSeconds / 60, DurationUnit.Minutes);
        }

        return (totalSeconds, DurationUnit.Seconds);
    }

    public static double ToAmount(double totalSeconds, DurationUnit unit) => unit switch
    {
        DurationUnit.Hours => totalSeconds / 3600,
        DurationUnit.Minutes => totalSeconds / 60,
        _ => totalSeconds
    };

    public static double ToTotalSeconds(double amount, DurationUnit unit) => unit switch
    {
        DurationUnit.Hours => amount * 3600,
        DurationUnit.Minutes => amount * 60,
        _ => amount
    };
}
