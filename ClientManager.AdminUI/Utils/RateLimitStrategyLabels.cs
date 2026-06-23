using ClientManager.AdminUI.Resources;
using ClientManager.Shared.Models.Enums;
using Microsoft.Extensions.Localization;

namespace ClientManager.AdminUI.Utils;

public sealed record RateLimitStrategyOption(RateLimitStrategy Strategy, string Label);

public static class RateLimitStrategyLabels
{
    public static string GetLabel(
        RateLimitStrategy strategy,
        IStringLocalizer<SharedResources> localizer) =>
        localizer[$"RateLimitStrategy.{strategy}"];

    public static IReadOnlyList<RateLimitStrategyOption> GetOptions(
        IStringLocalizer<SharedResources> localizer) =>
        Enum.GetValues<RateLimitStrategy>()
            .Select(strategy => new RateLimitStrategyOption(strategy, GetLabel(strategy, localizer)))
            .ToList();
}
