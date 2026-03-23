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
            < 1_000_000 => (original / 1_000).ToString("N1") + "K",
            _ => (original / 1_000_000).ToString("N1") + "M"
        };
    }
}
