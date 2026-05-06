// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
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
