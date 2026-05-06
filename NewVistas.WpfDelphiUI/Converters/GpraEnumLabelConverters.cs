// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using System.Globalization;
using System.Windows.Data;
using NewVistas.WpfDelphiUI.ViewModels;

namespace NewVistas.WpfDelphiUI.Converters;

/// <summary>
/// Maps the GpraReportingPeriod int code coming back from the API to the
/// short label shown in the GPRA report list (FullFY/Q1..Q4).
/// </summary>
public sealed class ReportingPeriodLabelConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is int i ? ReportsViewModel.ReportingPeriodLabel(i) : string.Empty;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// Maps the GpraReportStatus int code (0=Draft..3=Error) to its label.
/// </summary>
public sealed class GpraStatusLabelConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is int i ? ReportsViewModel.StatusLabel(i) : string.Empty;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// Maps the GpraClinicalCategory int code (0=Diabetes..9=OB/GYN) to its label.
/// </summary>
public sealed class GpraCategoryLabelConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is int i ? ReportsViewModel.CategoryLabel(i) : string.Empty;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
