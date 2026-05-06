// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
namespace NewVistas.Wpf_UI.Views;

public partial class ZwrImportView : System.Windows.Controls.UserControl
{
    public ZwrImportView()
    {
        InitializeComponent();
    }

    private void LogBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        LogBox.ScrollToEnd();
    }
}
