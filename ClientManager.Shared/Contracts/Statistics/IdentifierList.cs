using System.ComponentModel;

namespace ClientManager.Shared.Contracts.Statistics;

/// <summary>
/// An immutable list of identifiers transported as a single comma-separated value.
/// </summary>
/// <remarks>
/// This type owns the one documented parsing rule for repeated identifier query parameters
/// (such as <c>targetIds</c> and <c>clientIds</c>): the raw value is split on commas, each
/// entry is trimmed, and empty entries are dropped. Controllers bind directly to this type via
/// <see cref="IdentifierListTypeConverter"/> instead of splitting strings themselves.
/// </remarks>
[TypeConverter(typeof(IdentifierListTypeConverter))]
public sealed record IdentifierList
{
    /// <summary>
    /// The character that separates identifiers in the transported value.
    /// </summary>
    public const char Separator = ',';

    private IdentifierList(IReadOnlyList<string> values) => Values = values;

    /// <summary>
    /// An empty identifier list, representing an absent or blank value.
    /// </summary>
    public static IdentifierList Empty { get; } = new(Array.Empty<string>());

    /// <summary>
    /// The parsed, trimmed identifiers in source order.
    /// </summary>
    public IReadOnlyList<string> Values { get; }

    /// <summary>
    /// Whether the list contains at least one identifier.
    /// </summary>
    public bool HasValues => Values.Count > 0;

    /// <summary>
    /// Parses a comma-separated value into an <see cref="IdentifierList"/>, trimming entries
    /// and dropping blanks. Returns <see cref="Empty"/> for null, empty, or whitespace input.
    /// </summary>
    /// <param name="raw">The raw comma-separated value.</param>
    /// <returns>The parsed identifier list.</returns>
    public static IdentifierList Parse(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return Empty;
        }

        var values = raw.Split(Separator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return values.Length == 0 ? Empty : new IdentifierList(values);
    }

    /// <summary>
    /// Joins the supplied identifiers into a single comma-separated value using the shared rule.
    /// </summary>
    /// <param name="values">The identifiers to join.</param>
    /// <returns>The comma-separated representation.</returns>
    public static string Join(IEnumerable<string> values) => string.Join(Separator, values);

    /// <summary>
    /// Returns the comma-separated representation of this list.
    /// </summary>
    /// <returns>The comma-separated value.</returns>
    public string ToQueryValue() => Join(Values);

    /// <inheritdoc />
    public override string ToString() => ToQueryValue();
}
