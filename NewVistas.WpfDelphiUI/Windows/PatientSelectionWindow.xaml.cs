// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using System.Windows;
using NewVistas.WpfDelphiUI.ViewModels;

namespace NewVistas.WpfDelphiUI.Windows;

public partial class PatientSelectionWindow : Window
{
    public PatientSelectionWindow(PatientSelectionViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }
}
