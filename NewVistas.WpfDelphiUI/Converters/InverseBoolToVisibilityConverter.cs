// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace NewVistas.WpfDelphiUI.Converters;

/// <summary>Converts bool → Visibility (True=Collapsed, False=Visible).</summary>
public sealed class InverseBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is true ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => value is not Visibility.Visible;
}
