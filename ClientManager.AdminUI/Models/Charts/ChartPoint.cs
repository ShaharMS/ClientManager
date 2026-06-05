namespace ClientManager.AdminUI.Models.Charts;

public record ChartPoint(string Label, double Value)
{
    public double OriginalValue { get; init; } = Value;
}
