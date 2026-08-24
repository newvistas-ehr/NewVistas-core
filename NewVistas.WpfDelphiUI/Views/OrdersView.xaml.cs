// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using System.Windows.Controls;
using NewVistas.WpfDelphiUI.ViewModels;

namespace NewVistas.WpfDelphiUI.Views;

public partial class OrdersView : UserControl
{
    public OrdersView() => InitializeComponent();

    // PasswordBox cannot be data-bound (by WPF design); sync it to the ViewModel by hand.
    private void EsigCodeBox_PasswordChanged(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is OrdersViewModel vm && sender is PasswordBox box)
            vm.SignatureCode = box.Password;
    }
}
