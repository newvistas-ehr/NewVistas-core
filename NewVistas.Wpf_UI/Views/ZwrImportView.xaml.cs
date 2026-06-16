// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
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
