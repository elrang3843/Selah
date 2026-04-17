using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace Selah.App.Converters;

// ─────────────────────────────────────────────────────────────
// BoolToVisibilityConverter  (Invert=false: true→Visible)
// ─────────────────────────────────────────────────────────────
[ValueConversion(typeof(bool), typeof(Visibility))]
public class BoolToVisibilityConverter : IValueConverter
{
    public bool Invert { get; set; }

    public object Convert(object value, Type t, object p, CultureInfo c)
    {
        bool b = value is bool bv && bv;
        if (Invert) b = !b;
        return b ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type t, object p, CultureInfo c)
        => value is Visibility v && v == Visibility.Visible;
}

// ─────────────────────────────────────────────────────────────
// InverseBoolToVisibilityConverter  (true → Collapsed)
// ─────────────────────────────────────────────────────────────
[ValueConversion(typeof(bool), typeof(Visibility))]
public class InverseBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type t, object p, CultureInfo c)
        => value is bool b && b ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type t, object p, CultureInfo c)
        => value is Visibility v && v == Visibility.Collapsed;
}

// ─────────────────────────────────────────────────────────────
// InverseBoolConverter  (bool → !bool)
// ─────────────────────────────────────────────────────────────
[ValueConversion(typeof(bool), typeof(bool))]
public class InverseBoolConverter : IValueConverter
{
    public object Convert(object value, Type t, object p, CultureInfo c)
        => value is bool b && !b;

    public object ConvertBack(object value, Type t, object p, CultureInfo c)
        => value is bool b && !b;
}

// ─────────────────────────────────────────────────────────────
// NullToVisibilityConverter  (null → Collapsed, else Visible)
// ─────────────────────────────────────────────────────────────
[ValueConversion(typeof(object), typeof(Visibility))]
public class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type t, object p, CultureInfo c)
        => value == null ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type t, object p, CultureInfo c)
        => DependencyProperty.UnsetValue;
}

// ─────────────────────────────────────────────────────────────
// InstalledColorConverter  (bool IsInstalled → Color)
// ─────────────────────────────────────────────────────────────
[ValueConversion(typeof(bool), typeof(Color))]
public class InstalledColorConverter : IValueConverter
{
    private static readonly Color InstalledColor = Color.FromRgb(0xA6, 0xE3, 0xA1); // green
    private static readonly Color NotInstalledColor = Color.FromRgb(0x45, 0x47, 0x5A); // gray

    public object Convert(object value, Type t, object p, CultureInfo c)
        => value is bool b && b ? InstalledColor : NotInstalledColor;

    public object ConvertBack(object value, Type t, object p, CultureInfo c)
        => DependencyProperty.UnsetValue;
}

// ─────────────────────────────────────────────────────────────
// InstalledTextBrushConverter  (bool IsInstalled → Brush)
// ─────────────────────────────────────────────────────────────
[ValueConversion(typeof(bool), typeof(Brush))]
public class InstalledTextBrushConverter : IValueConverter
{
    private static readonly Brush InstalledBrush =
        new SolidColorBrush(Color.FromRgb(0xA6, 0xE3, 0xA1));
    private static readonly Brush NotInstalledBrush =
        new SolidColorBrush(Color.FromRgb(0x7F, 0x84, 0x9C));

    static InstalledTextBrushConverter()
    {
        InstalledBrush.Freeze();
        NotInstalledBrush.Freeze();
    }

    public object Convert(object value, Type t, object p, CultureInfo c)
        => value is bool b && b ? InstalledBrush : NotInstalledBrush;

    public object ConvertBack(object value, Type t, object p, CultureInfo c)
        => DependencyProperty.UnsetValue;
}

// ─────────────────────────────────────────────────────────────
// InstalledStatusConverter  (bool IsInstalled → "설치됨" / "미설치")
// ─────────────────────────────────────────────────────────────
[ValueConversion(typeof(bool), typeof(string))]
public class InstalledStatusConverter : IValueConverter
{
    public object Convert(object value, Type t, object p, CultureInfo c)
        => value is bool b && b ? Loc.Get("Model_Installed") : Loc.Get("Model_NotInstalled");

    public object ConvertBack(object value, Type t, object p, CultureInfo c)
        => DependencyProperty.UnsetValue;
}

// ─────────────────────────────────────────────────────────────
// PlayPauseConverter  (bool IsPlaying → "⏸" / "▶")
// ─────────────────────────────────────────────────────────────
[ValueConversion(typeof(bool), typeof(string))]
public class PlayPauseConverter : IValueConverter
{
    public object Convert(object value, Type t, object p, CultureInfo c)
        => value is bool b && b ? "⏸" : "▶";

    public object ConvertBack(object value, Type t, object p, CultureInfo c)
        => DependencyProperty.UnsetValue;
}

// ─────────────────────────────────────────────────────────────
// HexToColorConverter  ("#4A9EFF" → Color)
// ─────────────────────────────────────────────────────────────
[ValueConversion(typeof(string), typeof(Color))]
public class HexToColorConverter : IValueConverter
{
    public object Convert(object? value, Type t, object p, CultureInfo c)
    {
        if (value is not string hex) return Colors.SteelBlue;
        hex = hex.TrimStart('#');
        try
        {
            if (hex.Length == 6)
            {
                byte r = System.Convert.ToByte(hex[..2], 16);
                byte g = System.Convert.ToByte(hex[2..4], 16);
                byte b = System.Convert.ToByte(hex[4..6], 16);
                return Color.FromRgb(r, g, b);
            }
            if (hex.Length == 8)
            {
                byte a = System.Convert.ToByte(hex[..2], 16);
                byte r = System.Convert.ToByte(hex[2..4], 16);
                byte g = System.Convert.ToByte(hex[4..6], 16);
                byte b = System.Convert.ToByte(hex[6..8], 16);
                return Color.FromArgb(a, r, g, b);
            }
        }
        catch { }
        return Colors.SteelBlue;
    }

    public object ConvertBack(object value, Type t, object p, CultureInfo c)
        => DependencyProperty.UnsetValue;
}

// ─────────────────────────────────────────────────────────────
// BoolToBrushConverter  (bool → configurable Brush pair)
// ─────────────────────────────────────────────────────────────
[ValueConversion(typeof(bool), typeof(Brush))]
public class BoolToBrushConverter : IValueConverter
{
    public Brush TrueBrush  { get; set; } = Brushes.Transparent;
    public Brush FalseBrush { get; set; } = Brushes.Transparent;

    public object Convert(object value, Type t, object p, CultureInfo c)
        => value is bool b && b ? TrueBrush : FalseBrush;

    public object ConvertBack(object value, Type t, object p, CultureInfo c)
        => DependencyProperty.UnsetValue;
}
