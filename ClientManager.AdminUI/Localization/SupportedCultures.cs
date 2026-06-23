using System.Globalization;

namespace ClientManager.AdminUI.Localization;

public static class SupportedCultures
{
    public const string Default = "en";
    public const string ProbeKey = "Common.AppName";

    public static readonly IReadOnlyList<string> Codes = ["en", "he-IL"];

    public static bool IsSupported(string? culture) =>
        !string.IsNullOrWhiteSpace(culture)
        && Codes.Contains(culture, StringComparer.OrdinalIgnoreCase);

    public static string Normalize(string? culture) =>
        IsSupported(culture)
            ? Codes.First(c => c.Equals(culture, StringComparison.OrdinalIgnoreCase))
            : Default;

    public static bool IsRtl(string culture) =>
        CultureInfo.GetCultureInfo(Normalize(culture)).TextInfo.IsRightToLeft;

    public static string MatchBest(IEnumerable<string>? candidates)
    {
        if (candidates is null)
        {
            return Default;
        }

        var supported = new HashSet<string>(Codes, StringComparer.OrdinalIgnoreCase);

        foreach (var candidate in candidates)
        {
            if (string.IsNullOrWhiteSpace(candidate))
            {
                continue;
            }

            var trimmed = candidate.Trim();
            if (supported.Contains(trimmed))
            {
                return Normalize(trimmed);
            }

            var language = trimmed.Split('-')[0];
            var prefixMatch = Codes.FirstOrDefault(
                code => code.Equals(language, StringComparison.OrdinalIgnoreCase)
                    || code.StartsWith(language + "-", StringComparison.OrdinalIgnoreCase));
            if (prefixMatch is not null)
            {
                return prefixMatch;
            }
        }

        return Default;
    }

    public static IEnumerable<string> ParseAcceptLanguage(string? header)
    {
        if (string.IsNullOrWhiteSpace(header))
        {
            yield break;
        }

        foreach (var part in header.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var token = part.Split(';')[0].Trim();
            if (!string.IsNullOrEmpty(token))
            {
                yield return token;
            }
        }
    }
}
