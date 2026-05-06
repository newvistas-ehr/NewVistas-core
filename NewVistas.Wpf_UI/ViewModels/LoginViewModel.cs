// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NewVistas.Wpf_UI.Services;

namespace NewVistas.Wpf_UI.ViewModels;

/// <summary>
/// Login screen ViewModel with MFA two-step support.
/// Step 1: Enter Access Code (username) + Verify Code (password).
/// Step 2: If MFA enabled, enter TOTP code to complete authentication.
/// </summary>
public partial class LoginViewModel : ObservableObject
{
    private readonly AuthService _auth;
    private string? _mfaChallengeToken;

    public LoginViewModel(AuthService auth)
    {
        _auth = auth;
    }

    /// <summary>
    /// Raised when login succeeds (including MFA completion). App shows MainWindow.
    /// </summary>
    public event Action? LoginSucceeded;

    [ObservableProperty]
    private string _userName = string.Empty;

    [ObservableProperty]
    private string _password = string.Empty;

    [ObservableProperty]
    private string _mfaCode = string.Empty;

    [ObservableProperty]
    private string? _error;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _isMfaStep;

    [RelayCommand]
    public async Task LoginAsync()
    {
        if (IsMfaStep)
        {
            await VerifyMfaAsync();
            return;
        }

        if (string.IsNullOrWhiteSpace(UserName) || string.IsNullOrWhiteSpace(Password))
        {
            Error = "Please enter both access code and verify code.";
            return;
        }

        IsLoading = true;
        Error = null;

        try
        {
            var (success, error, mfaToken) = await _auth.LoginAsync(UserName, Password);
            if (!success)
            {
                Error = error ?? "Sign-in failed.";
                return;
            }

            if (mfaToken != null)
            {
                // MFA required — switch to TOTP entry
                _mfaChallengeToken = mfaToken;
                IsMfaStep = true;
                MfaCode = string.Empty;
                return;
            }

            // Full login success
            LoginSucceeded?.Invoke();
        }
        catch (Exception ex)
        {
            Error = $"Connection error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task VerifyMfaAsync()
    {
        if (string.IsNullOrWhiteSpace(MfaCode))
        {
            Error = "Please enter the verification code from your authenticator app.";
            return;
        }

        IsLoading = true;
        Error = null;

        try
        {
            var (success, error) = await _auth.VerifyMfaAsync(_mfaChallengeToken!, MfaCode);
            if (!success)
            {
                Error = error ?? "MFA verification failed.";
                return;
            }

            LoginSucceeded?.Invoke();
        }
        catch (Exception ex)
        {
            Error = $"Connection error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Reset to initial state (called when showing the login window again after logout).
    /// </summary>
    public void Reset()
    {
        UserName = string.Empty;
        Password = string.Empty;
        MfaCode = string.Empty;
        Error = null;
        IsLoading = false;
        IsMfaStep = false;
        _mfaChallengeToken = null;
    }
}
