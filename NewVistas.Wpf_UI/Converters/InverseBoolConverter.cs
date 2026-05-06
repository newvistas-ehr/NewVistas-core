// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using System.Globalization;
using System.Windows.Data;

namespace NewVistas.Wpf_UI.Converters;

/// <summary>
/// Converts bool to its inverse (true → false, false → true).
/// Used for IsEnabled bindings where IsLoading should disable controls.
/// </summary>
public sealed class InverseBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is bool b ? !b : value;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => value is bool b ? !b : value;
}
