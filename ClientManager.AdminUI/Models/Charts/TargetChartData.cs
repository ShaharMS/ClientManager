namespace ClientManager.AdminUI.Models.Charts;

public record TargetChartData(
    string TargetName,
    List<ClientAreaSeries> ClientSeries,
    List<ChartPoint> CapSeries);
