// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using System.Windows;
using System.Windows.Input;
using NewVistas.Wpf_UI.ViewModels;

namespace NewVistas.Wpf_UI;

/// <summary>
/// Code-behind for LoginWindow. Handles PasswordBox binding (WPF PasswordBox
/// doesn't support two-way binding for security reasons) and Enter key.
/// </summary>
public partial class LoginWindow : Window
{
    public LoginWindow()
    {
        InitializeComponent();

        // Sync PasswordBox → ViewModel on every keystroke
        PasswordBox.PasswordChanged += (_, _) =>
        {
            if (DataContext is LoginViewModel vm)
                vm.Password = PasswordBox.Password;
        };
    }

    private void PasswordBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && DataContext is LoginViewModel vm)
        {
            if (vm.LoginCommand.CanExecute(null))
                vm.LoginCommand.Execute(null);
        }
    }
}
