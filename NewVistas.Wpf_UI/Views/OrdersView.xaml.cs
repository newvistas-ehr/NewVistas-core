// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using System.Windows.Controls;
using NewVistas.Wpf_UI.ViewModels;

namespace NewVistas.Wpf_UI.Views;

public partial class OrdersView : UserControl
{
    public OrdersView() { InitializeComponent(); }

    private void OrderSetSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DataContext is OrdersViewModel vm && vm.SelectedOrderSet != null)
            vm.SelectOrderSetCommand.Execute(null);
    }
}
