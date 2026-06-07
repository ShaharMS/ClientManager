using ClientManager.AdminUI.Models.Charts;
using ClientManager.Shared.Models.Entities;

namespace ClientManager.AdminUI.Models.Monitor;

public sealed class MonitorLoadContext
{
    public const string AllServicesId = "__all__";
    public static readonly TimeSpan RecentWindow = TimeSpan.FromMinutes(5);

    public required string SelectedServiceId { get; init; }
    public required IEnumerable<string>? SelectedClientIds { get; init; }
    public required TimeRangePreset TimeRange { get; init; }
    public required List<Service> AllServices { get; init; }
    public required List<ClientConfiguration> AllClients { get; init; }
}

public sealed record MonitorLoadResult(
    List<TargetChartData> Charts,
    List<MonitorClientRow> ClientRows,
    List<ServiceSummaryRow> ServiceStats);
