using System.Collections;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using ClothingStore.Core;

namespace ClothingStore.Desktop.Converters;

/// <summary>Currency symbol used by <see cref="MoneyConverter"/>; updated when settings load or change.</summary>
public static class CurrencyFormat
{
    public static string Symbol { get; set; } = "$";
    public static string Format(decimal value) => Money.Format(value, Symbol);
}

public sealed class MoneyConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value switch
    {
        decimal d => CurrencyFormat.Format(d),
        double d => CurrencyFormat.Format((decimal)d),
        int i => CurrencyFormat.Format(i),
        null => "",
        _ => value.ToString() ?? "",
    };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => Binding.DoNothing;
}

/// <summary>true -> Visible. Pass "invert" as parameter to flip.</summary>
public sealed class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var flag = value is true;
        if (parameter is "invert") flag = !flag;
        return flag ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is Visibility.Visible ^ parameter is "invert";
}

public sealed class InverseBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value is not true;
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => value is not true;
}

/// <summary>Non-null (and non-empty string/collection) -> Visible. "invert" flips.</summary>
public sealed class HasValueToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var has = value switch
        {
            null => false,
            string s => s.Length > 0,
            int i => i != 0,
            decimal d => d != 0,
            ICollection c => c.Count > 0,
            _ => true,
        };
        if (parameter is "invert") has = !has;
        return has ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => Binding.DoNothing;
}

/// <summary>Compares an enum/value to the parameter; used for RadioButton groups bound to an enum.</summary>
public sealed class EqualsConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is not null && parameter is not null && value.ToString() == parameter.ToString();

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true && parameter is string s ? Enum.Parse(targetType, s) : Binding.DoNothing;
}

/// <summary>Splits PascalCase enum names for display: MobileWallet -> "Mobile Wallet".</summary>
public sealed class EnumDisplayConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is Enum e ? Humanize(e.ToString()) : value?.ToString() ?? "";

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => Binding.DoNothing;

    public static string Humanize(string name)
    {
        var sb = new System.Text.StringBuilder(name.Length + 4);
        for (var i = 0; i < name.Length; i++)
        {
            if (i > 0 && char.IsUpper(name[i]) && !char.IsUpper(name[i - 1])) sb.Append(' ');
            sb.Append(name[i]);
        }
        return sb.ToString();
    }
}

/// <summary>Negative numbers -> red-ish brush key name used for variance display.</summary>
public sealed class SignToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var d = value switch { decimal m => m, int i => i, _ => 0m };
        var key = d < 0 ? "DangerBrush" : d > 0 ? "SuccessBrush" : "TextBrush";
        return Application.Current.TryFindResource(key) ?? DependencyProperty.UnsetValue;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => Binding.DoNothing;
}
