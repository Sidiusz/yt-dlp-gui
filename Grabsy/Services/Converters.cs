using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media.Imaging;

namespace Grabsy.Services;

/// <summary>Binds a remote/local image URL string to an ImageSource.</summary>
public sealed class StringToBitmapConverter : IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is string s && !string.IsNullOrWhiteSpace(s) && Uri.TryCreate(s, UriKind.Absolute, out var uri))
            return new BitmapImage(uri);
        return null;
    }
    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotSupportedException();
}

/// <summary>True → Visible, False → Collapsed. Param "invert" flips it.</summary>
public sealed class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var b = value is bool x && x;
        if (parameter is string p && p.Equals("invert", StringComparison.OrdinalIgnoreCase)) b = !b;
        return b ? Visibility.Visible : Visibility.Collapsed;
    }
    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotSupportedException();
}
