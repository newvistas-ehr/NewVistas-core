// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;

namespace NewVistas.PatientPortal.Services;

/// <summary>
/// Blazor Server authentication state provider for patient portal.
/// Stores the patient JWT in-memory (per-circuit) and attaches it to all HttpClient calls.
/// </summary>
public class PatientAuthStateProvider : AuthenticationStateProvider
{
    private readonly HttpClient _http;
    private string? _token;
    private readonly ClaimsPrincipal _anonymous = new(new ClaimsIdentity());

    public PatientAuthStateProvider(HttpClient http)
    {
        _http = http;
    }

    public string? Token => _token;
    public bool IsAuthenticated => _token != null;

    /// <summary>
    /// Authenticate with the portal API and store the JWT token.
    /// </summary>
    public async Task<PatientLoginResult> LoginAsync(string patientId, string password)
    {
        var response = await _http.PostAsJsonAsync("api/patient-auth/login",
            new { PatientId = patientId, Password = password });

        if (!response.IsSuccessStatusCode)
            return new PatientLoginResult(false, "Invalid credentials.");

        var loginResponse = await response.Content.ReadFromJsonAsync<PatientLoginResponse>();
        if (loginResponse == null)
            return new PatientLoginResult(false, "Invalid server response.");

        _token = loginResponse.Token;
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _token);

        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
        return new PatientLoginResult(true, null, loginResponse.PatientId, loginResponse.DisplayName);
    }

    /// <summary>
    /// Register a new patient account.
    /// </summary>
    public async Task<PatientLoginResult> RegisterAsync(string patientId, string email, string password, string? displayName)
    {
        var response = await _http.PostAsJsonAsync("api/patient-auth/register",
            new { PatientId = patientId, Email = email, Password = password, DisplayName = displayName });

        if (!response.IsSuccessStatusCode)
        {
            string body = await response.Content.ReadAsStringAsync();
            return new PatientLoginResult(false, $"Registration failed: {body}");
        }

        // Auto-login after registration
        return await LoginAsync(patientId, password);
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

public record PatientLoginResult(bool Success, string? Error, string? PatientId = null, string? DisplayName = null);

public record PatientLoginResponse
{
    public string Token { get; init; } = string.Empty;
    public string PatientId { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
}
