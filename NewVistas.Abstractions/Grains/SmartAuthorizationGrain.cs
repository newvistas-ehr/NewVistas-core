// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using System.Security.Cryptography;
using System.Text;
using Orleans;
using Orleans.Runtime;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// SMART OAuth 2.0 Authorization Grain — manages the full OAuth 2.0 lifecycle
/// for a specific user-client pair.
///
/// Implements §170.315(g)(10) requirements:
///   - Authorization code flow with PKCE (RFC 7636)
///   - Refresh tokens with ≥3 month validity
///   - Token introspection (RFC 7662 / SMART 2.0.0)
///   - Patient authorization revocation within 1 hour
///
/// Grain Key: "SMART-AUTH:{userId}:{clientId}"
/// </summary>
public class SmartAuthorizationGrain : Grain, ISmartAuthorizationGrain
{
    private readonly IPersistentState<SmartAuthorizationState> _state;

    public SmartAuthorizationGrain(
        [PersistentState("smartAuthState", "smartAuthStore")] IPersistentState<SmartAuthorizationState> state)
    {
        _state = state;
    }

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_state.State.UserId))
        {
            string key = this.GetPrimaryKeyString();
            // Key format: "SMART-AUTH:{userId}:{clientId}"
            int firstColon = key.IndexOf(':');
            int lastColon = key.LastIndexOf(':');
            if (firstColon >= 0 && lastColon > firstColon)
            {
                _state.State.UserId = key[(firstColon + 1)..lastColon];
                _state.State.ClientId = key[(lastColon + 1)..];
            }
        }
        return base.OnActivateAsync(cancellationToken);
    }

    public async Task<string> CreateAuthorizationCodeAsync(
        string redirectUri,
        List<string> scopes,
        string? codeChallenge,
        string? codeChallengeMethod,
        string? patientContext,
        string? launchContext)
    {
        if (_state.State.IsRevoked)
            throw new InvalidOperationException("Authorization has been revoked.");

        // Clean up expired codes
        DateTime now = DateTime.UtcNow;
        _state.State.PendingCodes.RemoveAll(c => c.ExpiresDate < now || c.IsUsed);

        string code = GenerateSecureToken(32);

        _state.State.PendingCodes.Add(new SmartAuthorizationCode
        {
            Code = code,
            RedirectUri = redirectUri,
            Scopes = scopes,
            CodeChallenge = codeChallenge,
            CodeChallengeMethod = codeChallengeMethod,
            PatientContext = patientContext,
            LaunchContext = launchContext,
            CreatedDate = now,
            ExpiresDate = now.AddMinutes(5)
        });

        _state.State.ApprovedScopes = scopes;
        _state.State.PatientContext = patientContext;
        _state.State.LastModifiedDate = now;

        await _state.WriteStateAsync();
        return code;
    }

    public async Task<SmartTokenResponse> ExchangeCodeAsync(string code, string redirectUri, string? codeVerifier)
    {
        if (_state.State.IsRevoked)
            throw new InvalidOperationException("Authorization has been revoked.");

        DateTime now = DateTime.UtcNow;

        SmartAuthorizationCode? authCode = _state.State.PendingCodes
            .FirstOrDefault(c => c.Code == code && !c.IsUsed && c.ExpiresDate > now);

        if (authCode == null)
            throw new InvalidOperationException("Invalid or expired authorization code.");

        if (authCode.RedirectUri != redirectUri)
            throw new InvalidOperationException("Redirect URI mismatch.");

        // PKCE validation (RFC 7636)
        if (!string.IsNullOrEmpty(authCode.CodeChallenge))
        {
            if (string.IsNullOrEmpty(codeVerifier))
                throw new InvalidOperationException("PKCE code_verifier required.");

            string computedChallenge = ComputeS256Challenge(codeVerifier);
            if (!string.Equals(computedChallenge, authCode.CodeChallenge, StringComparison.Ordinal))
                throw new InvalidOperationException("PKCE code_verifier does not match code_challenge.");
        }

        // Mark code as used (single-use)
        authCode.IsUsed = true;

        // Generate tokens
        string accessToken = GenerateSecureToken(48);
        string refreshToken = GenerateSecureToken(48);

        _state.State.AccessTokens.Add(new SmartAccessToken
        {
            TokenHash = HashToken(accessToken),
            Scopes = authCode.Scopes,
            IssuedDate = now,
            ExpiresDate = now.AddHours(1),
            PatientContext = authCode.PatientContext,
            UserId = _state.State.UserId,
            ClientId = _state.State.ClientId
        });

        _state.State.RefreshTokens.Add(new SmartRefreshToken
        {
            TokenHash = HashToken(refreshToken),
            Scopes = authCode.Scopes,
            IssuedDate = now,
            ExpiresDate = now.AddMonths(3), // §170.315(g)(10) — ≥3 months
            PatientContext = authCode.PatientContext
        });

        _state.State.LastModifiedDate = now;
        await _state.WriteStateAsync();

        return new SmartTokenResponse
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresIn = 3600,
            Scope = string.Join(" ", authCode.Scopes),
            PatientContext = authCode.PatientContext
        };
    }

    public async Task<SmartTokenResponse> RefreshTokenAsync(string refreshToken)
    {
        if (_state.State.IsRevoked)
            throw new InvalidOperationException("Authorization has been revoked.");

        DateTime now = DateTime.UtcNow;
        string tokenHash = HashToken(refreshToken);

        SmartRefreshToken? token = _state.State.RefreshTokens
            .FirstOrDefault(t => t.TokenHash == tokenHash && !t.IsRevoked && t.ExpiresDate > now);

        if (token == null)
            throw new InvalidOperationException("Invalid or expired refresh token.");

        // Rotate: revoke old refresh token, issue new one (per best practice)
        token.IsRevoked = true;

        string newAccessToken = GenerateSecureToken(48);
        string newRefreshToken = GenerateSecureToken(48);

        _state.State.AccessTokens.Add(new SmartAccessToken
        {
            TokenHash = HashToken(newAccessToken),
            Scopes = token.Scopes,
            IssuedDate = now,
            ExpiresDate = now.AddHours(1),
            PatientContext = token.PatientContext,
            UserId = _state.State.UserId,
            ClientId = _state.State.ClientId
        });

        // §170.315(g)(10) — new refresh token must have ≥3 month validity
        _state.State.RefreshTokens.Add(new SmartRefreshToken
        {
            TokenHash = HashToken(newRefreshToken),
            Scopes = token.Scopes,
            IssuedDate = now,
            ExpiresDate = now.AddMonths(3),
            PatientContext = token.PatientContext
        });

        // Clean up expired/revoked tokens (housekeeping)
        _state.State.AccessTokens.RemoveAll(t => t.ExpiresDate < now || t.IsRevoked);
        _state.State.RefreshTokens.RemoveAll(t => t.ExpiresDate < now && t.IsRevoked);

        _state.State.LastModifiedDate = now;
        await _state.WriteStateAsync();

        return new SmartTokenResponse
        {
            AccessToken = newAccessToken,
            RefreshToken = newRefreshToken,
            ExpiresIn = 3600,
            Scope = string.Join(" ", token.Scopes),
            PatientContext = token.PatientContext
        };
    }

    public Task<SmartTokenIntrospection> IntrospectTokenAsync(string token, bool isAccessToken)
    {
        DateTime now = DateTime.UtcNow;
        string tokenHash = HashToken(token);

        if (_state.State.IsRevoked)
            return Task.FromResult(new SmartTokenIntrospection { Active = false });

        if (isAccessToken)
        {
            SmartAccessToken? at = _state.State.AccessTokens
                .FirstOrDefault(t => t.TokenHash == tokenHash && !t.IsRevoked && t.ExpiresDate > now);

            if (at == null)
                return Task.FromResult(new SmartTokenIntrospection { Active = false });

            return Task.FromResult(new SmartTokenIntrospection
            {
                Active = true,
                Scope = string.Join(" ", at.Scopes),
                ClientId = at.ClientId,
                Sub = at.UserId,
                Exp = new DateTimeOffset(at.ExpiresDate).ToUnixTimeSeconds(),
                Iat = new DateTimeOffset(at.IssuedDate).ToUnixTimeSeconds(),
                TokenType = "Bearer"
            });
        }
        else
        {
            SmartRefreshToken? rt = _state.State.RefreshTokens
                .FirstOrDefault(t => t.TokenHash == tokenHash && !t.IsRevoked && t.ExpiresDate > now);

            if (rt == null)
                return Task.FromResult(new SmartTokenIntrospection { Active = false });

            return Task.FromResult(new SmartTokenIntrospection
            {
                Active = true,
                Scope = string.Join(" ", rt.Scopes),
                ClientId = _state.State.ClientId,
                Sub = _state.State.UserId,
                Exp = new DateTimeOffset(rt.ExpiresDate).ToUnixTimeSeconds(),
                Iat = new DateTimeOffset(rt.IssuedDate).ToUnixTimeSeconds(),
                TokenType = "refresh_token"
            });
        }
    }

    public async Task RevokeAllAsync()
    {
        _state.State.IsRevoked = true;
        _state.State.RevokedDate = DateTime.UtcNow;

        foreach (var token in _state.State.AccessTokens)
            token.IsRevoked = true;
        foreach (var token in _state.State.RefreshTokens)
            token.IsRevoked = true;

        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task RevokeRefreshTokenAsync(string refreshToken)
    {
        string tokenHash = HashToken(refreshToken);
        SmartRefreshToken? token = _state.State.RefreshTokens
            .FirstOrDefault(t => t.TokenHash == tokenHash);

        if (token != null)
        {
            token.IsRevoked = true;
            _state.State.LastModifiedDate = DateTime.UtcNow;
            await _state.WriteStateAsync();
        }
    }

    public Task<bool> IsRevokedAsync() => Task.FromResult(_state.State.IsRevoked);

    public Task<SmartAuthorizationState> GetStateAsync() => Task.FromResult(_state.State);

    // ─── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>Generate a cryptographically secure random token as URL-safe Base64.</summary>
    public static string GenerateSecureToken(int byteLength)
    {
        byte[] bytes = RandomNumberGenerator.GetBytes(byteLength);
        return Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }

    /// <summary>SHA-256 hash a token for storage (never store raw tokens).</summary>
    public static string HashToken(string token)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToBase64String(hash);
    }

    /// <summary>Compute PKCE S256 code challenge from code verifier (RFC 7636).</summary>
    public static string ComputeS256Challenge(string codeVerifier)
    {
        byte[] hash = SHA256.HashData(Encoding.ASCII.GetBytes(codeVerifier));
        return Convert.ToBase64String(hash)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }
}
