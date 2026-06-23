namespace ClientManager.AdminUI.Models.Charts;

public record ClientAreaSeries(string ClientId, string ClientName, List<ChartPoint> Points, bool Hidden = false);
