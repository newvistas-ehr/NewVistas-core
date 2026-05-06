// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using NewVistas.Abstractions.Security;

namespace NewVistas.WpfDelphiUI.Services;

/// <summary>
/// Manages JWT authentication for the WPF application.
/// Stores the token in-memory and attaches it to the ApiClient's HttpClient.
/// </summary>
public sealed class AuthService
{
    private readonly ApiClient _api;
    private string? _token;

    public AuthService(ApiClient api)
    {
        _api = api;
    }

    public bool IsAuthenticated => _token != null;
    public string? UserId { get; private set; }
    public string? UserName { get; private set; }
    public string? DisplayName { get; private set; }
    public string? UserClass { get; private set; }
    public bool HasElectronicSignature { get; private set; }

    private HashSet<string> _securityKeys = [];
    private HashSet<MenuArea> _accessibleAreas = [MenuArea.General];

    /// <summary>The user's security keys, cached for the session.</summary>
    public IReadOnlySet<string> SecurityKeys => _securityKeys;

    /// <summary>Check if the user can see menu items in the given area.</summary>
    public bool HasMenuAccess(MenuArea area) => _accessibleAreas.Contains(area);

    /// <summary>
    /// Authenticate against the API and store the JWT token.
    /// </summary>
    public async Task<(bool Success, string? Error)> LoginAsync(string userName, string password)
    {
        try
        {
            var response = await _api.LoginAsync(userName, password);
            if (response == null)
                return (false, "Invalid credentials.");

            _token = response.Token;
            UserId = response.UserId;
            UserName = response.UserName;
            DisplayName = response.DisplayName;
            UserClass = response.UserClass;
            HasElectronicSignature = response.HasElectronicSignature;

            _api.SetAuthToken(_token);

            // Load security keys once — all subsequent menu checks are O(1)
            await LoadSecurityKeysAsync(response.UserId);

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
        await _api.LogoutAsync();
        _token = null;
        UserId = null;
        UserName = null;
        DisplayName = null;
        _securityKeys = [];
        _accessibleAreas = [MenuArea.General];
        _api.SetAuthToken(null);
    }

    private async Task LoadSecurityKeysAsync(string userId)
    {
        try
        {
            List<string>? keys = await _api.GetSecurityKeysAsync(userId);
            if (keys is not null)
            {
                _securityKeys = [.. keys];
                _accessibleAreas = MenuAccessMap.GetAccessibleAreas(_securityKeys);
            }
        }
        catch
        {
            // Key fetch failure is non-fatal — user sees General menu only
        }
    }
}
