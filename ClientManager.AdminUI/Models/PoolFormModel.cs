namespace ClientManager.AdminUI.Models;

public class PoolFormModel
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public uint MaxSlots { get; set; } = 10;
    public double AllocationTtlSeconds { get; set; } = 300;
    public bool IsEnabled { get; set; } = true;
}
