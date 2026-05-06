// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using System.Security.Cryptography;
using System.Text;
using Orleans;
using Orleans.Runtime;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// SMART on FHIR Client Registration Grain — stores third-party app registration data.
/// §170.315(g)(10) application registration requirement.
/// </summary>
public class SmartClientGrain : Grain, ISmartClientGrain
{
    private readonly IPersistentState<SmartClientState> _state;

    public SmartClientGrain(
        [PersistentState("smartClientState", "smartClientStore")] IPersistentState<SmartClientState> state)
    {
        _state = state;
    }

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_state.State.ClientId))
        {
            _state.State.ClientId = this.GetPrimaryKeyString();
        }
        return base.OnActivateAsync(cancellationToken);
    }

    public async Task RegisterAsync(
        string clientName,
        List<string> redirectUris,
        string clientType,
        string? clientSecret,
        List<string> grantedScopes,
        string? launchUrl,
        string? logoUri,
        string? jwksUri,
        List<string>? contacts,
        string tokenEndpointAuthMethod)
    {
        _state.State.ClientName = clientName;
        _state.State.RedirectUris = redirectUris;
        _state.State.ClientType = clientType;
        _state.State.GrantedScopes = grantedScopes;
        _state.State.LaunchUrl = launchUrl;
        _state.State.LogoUri = logoUri;
        _state.State.JwksUri = jwksUri;
        _state.State.Contacts = contacts ?? new();
        _state.State.TokenEndpointAuthMethod = tokenEndpointAuthMethod;
        _state.State.IsActive = true;
        _state.State.LastModifiedDate = DateTime.UtcNow;

        if (!string.IsNullOrEmpty(clientSecret))
        {
            _state.State.ClientSecretHash = HashSecret(clientSecret);
        }

        await _state.WriteStateAsync();
    }

    public Task<SmartClientState> GetClientAsync() => Task.FromResult(_state.State);

    public Task<bool> ValidateRedirectUriAsync(string redirectUri)
        => Task.FromResult(_state.State.RedirectUris.Contains(redirectUri));

    public Task<bool> ValidateSecretAsync(string clientSecret)
    {
        if (string.IsNullOrEmpty(_state.State.ClientSecretHash))
            return Task.FromResult(false);

        string hash = HashSecret(clientSecret);
        return Task.FromResult(string.Equals(hash, _state.State.ClientSecretHash, StringComparison.Ordinal));
    }

    public Task<bool> IsScopeGrantedAsync(string scope)
        => Task.FromResult(_state.State.GrantedScopes.Contains(scope));

    public async Task DeactivateAsync()
    {
        _state.State.IsActive = false;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task ReactivateAsync()
    {
        _state.State.IsActive = true;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    private static string HashSecret(string secret)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(secret));
        return Convert.ToBase64String(hash);
    }
}

/// <summary>
/// Index grain for listing all registered SMART clients.
/// </summary>
public class SmartClientIndexGrain : Grain, ISmartClientIndexGrain
{
    private readonly IPersistentState<SmartClientIndexState> _state;

    public SmartClientIndexGrain(
        [PersistentState("smartClientIndexState", "smartClientIndexStore")] IPersistentState<SmartClientIndexState> state)
    {
        _state = state;
    }

    public async Task AddClientAsync(SmartClientSummary summary)
    {
        _state.State.Clients.RemoveAll(c => c.ClientId == summary.ClientId);
        _state.State.Clients.Add(summary);
        await _state.WriteStateAsync();
    }

    public async Task RemoveClientAsync(string clientId)
    {
        _state.State.Clients.RemoveAll(c => c.ClientId == clientId);
        await _state.WriteStateAsync();
    }

    public Task<List<SmartClientSummary>> GetAllClientsAsync()
        => Task.FromResult(_state.State.Clients.ToList());

    public Task<List<SmartClientSummary>> GetActiveClientsAsync()
        => Task.FromResult(_state.State.Clients.Where(c => c.IsActive).ToList());
}
