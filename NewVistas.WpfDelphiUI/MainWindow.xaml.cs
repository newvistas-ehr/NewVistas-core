// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using System.Windows;
using NewVistas.WpfDelphiUI.ViewModels;

namespace NewVistas.WpfDelphiUI;

/// <summary>
/// Interaction logic for MainWindow.xaml — the CPRS chart frame.
/// DataContext is set to MainViewModel by App.xaml.cs.
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }
}
