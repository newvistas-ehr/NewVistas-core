// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace NewVistas.Wpf_UI.Services;

/// <summary>
/// HTTP client for the two things the WebServer is actually for.
///
/// <b>Authentication</b> — "are you who you say you are". Sign-in, MFA, sign-out and
/// session keepalive live on the WebServer because it owns identity and issues the JWT.
///
/// <b>Outsider-facing endpoints</b> — the FHIR gateway screen exists to exercise the
/// public FHIR surface, so it must go through the same HTTP path an external client would.
/// That screen uses <see cref="Http"/> directly.
///
/// Everything else — every clinical read and write — goes straight to grains through
/// <see cref="OrleansGrainService"/>. <b>Authorization</b> ("you may do A but not B") is a
/// grain concern and must not be fetched over HTTP; see
/// <c>AuthService.LoadSecurityKeysAsync</c>, which reads keys from the AccessControl grain.
///
/// This class previously carried ~40 typed data methods mirroring grain calls. They were
/// all dead once the ViewModels moved to grains, and are removed so the shortcut cannot be
/// taken again by accident.
/// </summary>
public sealed class ApiClient
{
    private readonly HttpClient _http;
    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    public ApiClient(HttpClient http)
    {
        _http = http;
    }

    /// <summary>
    /// The underlying HttpClient, for the FHIR gateway screen only — its whole purpose is
    /// to exercise the outsider-facing endpoint. Do not use this to fetch clinical data
    /// for an ordinary screen; use <see cref="OrleansGrainService"/>.
    /// </summary>
    public HttpClient Http => _http;

    /// <summary>JSON options for deserialization (camelCase web defaults).</summary>
    public static JsonSerializerOptions Json => _json;

    // ── Authentication ────────────────────────────────────────────────────

    /// <summary>Attaches (or clears) the bearer token used by the auth calls below.</summary>
    public void SetAuthToken(string? token)
    {
        _http.DefaultRequestHeaders.Authorization = token != null
            ? new AuthenticationHeaderValue("Bearer", token)
            : null;
    }

    /// <summary>Signs in with access/verify codes. Returns null on rejection.</summary>
    public async Task<LoginResponse?> LoginAsync(string userName, string password)
    {
        var response = await _http.PostAsJsonAsync("api/auth/login", new { UserName = userName, Password = password });
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<LoginResponse>(_json);
    }

    /// <summary>
    /// Completes MFA verification with a challenge token and TOTP code.
    /// Returns a full LoginResponse with session token on success.
    /// </summary>
    public async Task<LoginResponse?> VerifyMfaAsync(string challengeToken, string code)
    {
        var response = await _http.PostAsJsonAsync("api/auth/mfa/verify",
            new { MfaChallengeToken = challengeToken, Code = code });
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<LoginResponse>(_json);
    }

    /// <summary>Ends the Orleans session on the server. Called on logout/app close.</summary>
    public async Task LogoutAsync()
    {
        try { await _http.PostAsync("api/auth/logout", null); }
        catch { /* best-effort — server session will expire on timeout anyway */ }
    }

    /// <summary>Touches the server-side session to prevent timeout (periodic, from AuthService).</summary>
    public async Task TouchSessionAsync()
    {
        try { await _http.PostAsync("api/auth/touch", null); }
        catch { /* best-effort */ }
    }
}

// ── Auth DTOs ────────────────────────────────────────────────────────────────

public record LoginResponse(
    string Token,
    string UserId,
    string UserName,
    string DisplayName,
    string? UserClass,
    bool HasElectronicSignature,
    bool MfaRequired = false,
    string? MfaChallengeToken = null);
