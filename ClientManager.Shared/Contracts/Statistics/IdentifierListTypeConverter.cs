using System.ComponentModel;
using System.Globalization;

namespace ClientManager.Shared.Contracts.Statistics;

/// <summary>
/// Converts between the comma-separated transport string and <see cref="IdentifierList"/>.
/// </summary>
/// <remarks>
/// Registering this converter on <see cref="IdentifierList"/> lets ASP.NET Core model binding
/// produce an <see cref="IdentifierList"/> directly from a query string, so controllers never
/// need their own string-splitting helpers.
/// </remarks>
public sealed class IdentifierListTypeConverter : TypeConverter
{
    /// <inheritdoc />
    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType) =>
        sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);

    /// <inheritdoc />
    public override object ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value) =>
        value is string raw ? IdentifierList.Parse(raw) : base.ConvertFrom(context, culture, value)!;

    /// <inheritdoc />
    public override bool CanConvertTo(ITypeDescriptorContext? context, Type? destinationType) =>
        destinationType == typeof(string) || base.CanConvertTo(context, destinationType);

    /// <inheritdoc />
    public override object? ConvertTo(
        ITypeDescriptorContext? context,
        CultureInfo? culture,
        object? value,
        Type destinationType) =>
        destinationType == typeof(string) && value is IdentifierList list
            ? list.ToQueryValue()
            : base.ConvertTo(context, culture, value, destinationType);
}
