// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using System.Windows;
using System.Windows.Controls;
using NewVistas.Wpf_UI.ViewModels;

namespace NewVistas.Wpf_UI.Views;

public partial class NotesView : UserControl
{
    public NotesView() { InitializeComponent(); }

    // PasswordBox cannot be data-bound (by WPF design, to keep the secret out of the
    // binding system); sync it to the ViewModel by hand.
    private void EsigCodeBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is NotesViewModel vm && sender is PasswordBox box)
            vm.SignatureCode = box.Password;
    }
}
