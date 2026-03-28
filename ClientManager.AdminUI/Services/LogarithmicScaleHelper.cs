namespace ClientManager.AdminUI.Services;

public static class LogarithmicScaleHelper
{
    public static double Transform(double value)
    {
        return Math.Log10(value + 1);
    }

    public static double InverseTransform(double transformed)
    {
        return Math.Pow(10, transformed) - 1;
    }

    public static string FormatAxisLabel(object transformedValue)
    {
        if (transformedValue is not double d) return "";
        var original = InverseTransform(d);
        return original switch
        {
            < 1 => original.ToString("F1"),
            < 1_000 => original.ToString("N0"),
            < 1_000_000 => FormatCompact(original / 1_000) + "K",
            _ => FormatCompact(original / 1_000_000) + "M"
        };
    }

    public static string FormatLinearAxisLabel(object value)
    {
        if (value is not double d) return "";
        return d switch
        {
            < 1 => d.ToString("F1"),
            < 1_000 => d.ToString("N0"),
            < 1_000_000 => FormatCompact(d / 1_000) + "K",
            _ => FormatCompact(d / 1_000_000) + "M"
        };
    }

    private static string FormatCompact(double value)
    {
        var rounded = Math.Round(value, 1);
        return rounded % 1 == 0 ? rounded.ToString("N0") : rounded.ToString("N1");
    }
}
