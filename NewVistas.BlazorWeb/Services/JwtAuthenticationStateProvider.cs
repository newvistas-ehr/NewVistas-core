// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;

namespace NewVistas.BlazorWeb.Services;

/// <summary>
/// Blazor Server authentication state provider backed by JWT tokens.
/// Stores the token in-memory (per-circuit) and attaches it to all HttpClient calls.
/// </summary>
public class JwtAuthenticationStateProvider : AuthenticationStateProvider
{
    private readonly HttpClient _http;
    private string? _token;
    private ClaimsPrincipal _anonymous = new(new ClaimsIdentity());

    public JwtAuthenticationStateProvider(HttpClient http)
    {
        _http = http;
    }

    public string? Token => _token;
    public bool IsAuthenticated => _token != null;

    /// <summary>
    /// Authenticate with the API and store the JWT token.
    /// </summary>
    public async Task<LoginResult> LoginAsync(string userName, string password)
    {
        var response = await _http.PostAsJsonAsync("api/auth/login", new { UserName = userName, Password = password });
        if (!response.IsSuccessStatusCode)
        {
            string body = await response.Content.ReadAsStringAsync();
            return new LoginResult(false, "Invalid credentials.");
        }

        var loginResponse = await response.Content.ReadFromJsonAsync<LoginResponse>();
        if (loginResponse == null)
            return new LoginResult(false, "Invalid server response.");

        _token = loginResponse.Token;
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _token);

        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
        return new LoginResult(true, null, loginResponse.DisplayName, loginResponse.UserClass);
    }

    /// <summary>
    /// Clear the token and revert to anonymous.
    /// </summary>
    public void Logout()
    {
        _token = null;
        _http.DefaultRequestHeaders.Authorization = null;
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }

    public override Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        if (string.IsNullOrEmpty(_token))
            return Task.FromResult(new AuthenticationState(_anonymous));

        var handler = new JwtSecurityTokenHandler();
        JwtSecurityToken jwt;
        try
        {
            jwt = handler.ReadJwtToken(_token);
        }
        catch
        {
            return Task.FromResult(new AuthenticationState(_anonymous));
        }

        // Check expiry
        if (jwt.ValidTo < DateTime.UtcNow)
        {
            _token = null;
            _http.DefaultRequestHeaders.Authorization = null;
            return Task.FromResult(new AuthenticationState(_anonymous));
        }

        var identity = new ClaimsIdentity(jwt.Claims, "jwt");
        var principal = new ClaimsPrincipal(identity);
        return Task.FromResult(new AuthenticationState(principal));
    }
}

public record LoginResult(bool Success, string? Error, string? DisplayName = null, string? UserClass = null);

public record LoginResponse
{
    public string Token { get; init; } = string.Empty;
    public string UserId { get; init; } = string.Empty;
    public string UserName { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string? UserClass { get; init; }
    public bool HasElectronicSignature { get; init; }
}
