// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace NewVistas.Wpf_UI.Converters;

[ValueConversion(typeof(object), typeof(Visibility))]
public class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object parameter, CultureInfo culture)
        => value is null ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
