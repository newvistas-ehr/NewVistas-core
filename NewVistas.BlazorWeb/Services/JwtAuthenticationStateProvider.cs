// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
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
        HttpResponseMessage response;
        try
        {
            response = await _http.PostAsJsonAsync("api/auth/login", new { UserName = userName, Password = password });
        }
        catch (Exception ex)
        {
            // The WebServer is down, still starting, or on a different port. Saying
            // "Invalid credentials" here sends people off retyping a correct password.
            return new LoginResult(false,
                $"Cannot reach the NewVistas API at {_http.BaseAddress}. Is the WebServer running? ({ex.Message})");
        }

        if (!response.IsSuccessStatusCode)
        {
            // Report what the server actually said. This previously collapsed every
            // failure into "Invalid credentials.", which hid the one message that
            // matters most: after 5 failed attempts the account is locked for 15
            // minutes, so the correct password keeps being rejected with no clue why.
            return new LoginResult(false, await ReadServerErrorAsync(response));
        }

        var loginResponse = await response.Content.ReadFromJsonAsync<LoginResponse>();
        if (loginResponse == null)
            return new LoginResult(false, "Invalid server response.");

        // A 200 with no token means the server issued an MFA challenge, which this UI
        // does not implement yet. Treat it as a failure rather than "signing in" with
        // an empty token — that used to land on a blank, silently-anonymous session.
        if (string.IsNullOrEmpty(loginResponse.Token))
            return new LoginResult(false, "This account requires multi-factor authentication, which this sign-in screen does not support yet.");

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

    /// <summary>
    /// Pull the message out of the API's { "error": "..." } failure body, falling back
    /// to the status code when the body is empty or not the expected shape.
    /// </summary>
    private static async Task<string> ReadServerErrorAsync(HttpResponseMessage response)
    {
        try
        {
            string body = await response.Content.ReadAsStringAsync();
            if (!string.IsNullOrWhiteSpace(body) && body.TrimStart().StartsWith('{'))
            {
                var error = System.Text.Json.JsonSerializer.Deserialize<ApiError>(body,
                    new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (!string.IsNullOrWhiteSpace(error?.Error))
                    return error.Error;
            }
        }
        catch
        {
            // fall through to the status-code message
        }

        return $"Sign-in failed ({(int)response.StatusCode} {response.ReasonPhrase}).";
    }

    private sealed record ApiError
    {
        public string? Error { get; init; }
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
