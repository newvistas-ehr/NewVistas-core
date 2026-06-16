// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using NewVistas.Abstractions.Security;

namespace NewVistas.Wpf_UI.Services;

/// <summary>
/// Manages JWT authentication for the WPF application.
/// Stores the token in-memory, attaches it to the ApiClient's HttpClient,
/// checks token expiry, supports MFA, and maintains a session keepalive timer.
/// </summary>
public sealed partial class AuthService : ObservableObject, IDisposable
{
    private readonly ApiClient _api;
    private readonly DispatcherTimer _sessionTimer;
    private string? _token;

    public AuthService(ApiClient api)
    {
        _api = api;

        // Session keepalive — touch the server session every 5 minutes
        _sessionTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(5) };
        _sessionTimer.Tick += async (_, _) => await TouchSessionAsync();
    }

    /// <summary>
    /// Raised when the user is forcibly logged out (token expired, 401 response, etc.).
    /// App.xaml.cs subscribes to this to switch back to the login window.
    /// </summary>
    public event Action? LoggedOut;

    [ObservableProperty]
    private bool _isAuthenticated;

    [ObservableProperty]
    private string? _userId;

    [ObservableProperty]
    private string? _userName;

    [ObservableProperty]
    private string? _displayName;

    [ObservableProperty]
    private string? _userClass;

    [ObservableProperty]
    private bool _hasElectronicSignature;

    private HashSet<string> _securityKeys = [];

    /// <summary>Precomputed menu areas the user can access. O(1) lookups.</summary>
    [ObservableProperty]
    private HashSet<MenuArea> _accessibleAreas = [MenuArea.General];

    /// <summary>The user's security keys, cached for the session.</summary>
    public IReadOnlySet<string> SecurityKeys => _securityKeys;

    /// <summary>Check if the user can see menu items in the given area.</summary>
    public bool HasMenuAccess(MenuArea area) => AccessibleAreas.Contains(area);

    public string? Token => _token;

    /// <summary>
    /// Authenticate with the API. Returns an MFA challenge token if MFA is required,
    /// or null on full success.
    /// </summary>
    public async Task<(bool Success, string? Error, string? MfaChallengeToken)> LoginAsync(string userName, string password)
    {
        try
        {
            LoginResponse? response = await _api.LoginAsync(userName, password);
            if (response == null)
                return (false, "Invalid credentials.", null);

            // Check if MFA is required (server returns MfaRequired = true with a challenge token)
            if (response.MfaRequired)
                return (true, null, response.MfaChallengeToken);

            await ApplyLoginResponseAsync(response);
            return (true, null, null);
        }
        catch (HttpRequestException ex)
        {
            return (false, $"Connection error: {ex.Message}", null);
        }
    }

    /// <summary>
    /// Complete MFA verification and obtain the full session token.
    /// </summary>
    public async Task<(bool Success, string? Error)> VerifyMfaAsync(string challengeToken, string code)
    {
        try
        {
            LoginResponse? response = await _api.VerifyMfaAsync(challengeToken, code);
            if (response == null)
                return (false, "MFA verification failed.");

            await ApplyLoginResponseAsync(response);
            return (true, null);
        }
        catch (HttpRequestException ex)
        {
            return (false, $"Connection error: {ex.Message}");
        }
    }

    /// <summary>
    /// End the server-side Orleans session and clear local auth state.
    /// </summary>
    public async Task LogoutAsync()
    {
        _sessionTimer.Stop();
        await _api.LogoutAsync();
        ClearState();
    }

    /// <summary>
    /// Called by the 401 handler to force logout without calling the server.
    /// Must be invoked on the UI thread.
    /// </summary>
    public void ForceLogout()
    {
        _sessionTimer.Stop();
        ClearState();
        LoggedOut?.Invoke();
    }

    /// <summary>
    /// Check if the current JWT token is still valid (not expired).
    /// If expired, triggers a forced logout.
    /// </summary>
    public bool CheckTokenExpiry()
    {
        if (_token == null) return false;

        try
        {
            var handler = new JwtSecurityTokenHandler();
            JwtSecurityToken jwt = handler.ReadJwtToken(_token);
            if (jwt.ValidTo < DateTime.UtcNow)
            {
                ForceLogout();
                return false;
            }
            return true;
        }
        catch
        {
            ForceLogout();
            return false;
        }
    }

    private async Task ApplyLoginResponseAsync(LoginResponse response)
    {
        _token = response.Token;
        _api.SetAuthToken(_token);

        IsAuthenticated = true;
        UserId = response.UserId;
        UserName = response.UserName;
        DisplayName = response.DisplayName;
        UserClass = response.UserClass;
        HasElectronicSignature = response.HasElectronicSignature;

        // Load security keys once — all subsequent menu checks are O(1)
        await LoadSecurityKeysAsync(response.UserId);

        _sessionTimer.Start();
    }

    private async Task LoadSecurityKeysAsync(string userId)
    {
        try
        {
            List<string>? keys = await _api.GetSecurityKeysAsync(userId);
            if (keys is not null)
            {
                _securityKeys = [.. keys];
                AccessibleAreas = MenuAccessMap.GetAccessibleAreas(_securityKeys);
            }
        }
        catch
        {
            // Key fetch failure is non-fatal — user sees General menu only
        }
    }

    private void ClearState()
    {
        _token = null;
        _api.SetAuthToken(null);

        IsAuthenticated = false;
        UserId = null;
        UserName = null;
        DisplayName = null;
        UserClass = null;
        HasElectronicSignature = false;
        _securityKeys = [];
        AccessibleAreas = [MenuArea.General];
    }

    private async Task TouchSessionAsync()
    {
        if (!IsAuthenticated) return;

        // Check token expiry before touching session
        if (!CheckTokenExpiry()) return;

        try
        {
            await _api.TouchSessionAsync();
        }
        catch
        {
            // Session touch is best-effort; server session will timeout on its own
        }
    }

    public void Dispose()
    {
        _sessionTimer.Stop();
    }
}
