// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using Orleans.Runtime;

namespace NewVistas.Abstractions.Grains;

public class EpcsProviderCredentialGrain : Grain, IEpcsProviderCredentialGrain
{
    private readonly IPersistentState<EpcsProviderCredentialState> _state;

    public EpcsProviderCredentialGrain(
        [PersistentState("epcsProviderState", "epcsProviderStore")]
        IPersistentState<EpcsProviderCredentialState> state)
    {
        _state = state;
    }

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_state.State.CredentialId))
        {
            _state.State.CredentialId = this.GetPrimaryKeyString();
            _state.State.CreatedDate = DateTime.UtcNow;
            _state.State.LastModifiedDate = DateTime.UtcNow;
        }
        return base.OnActivateAsync(cancellationToken);
    }

    public Task<EpcsProviderCredentialState> GetAsync() => Task.FromResult(_state.State);

    public async Task SaveAsync(
        string providerId, string providerName,
        string? npi, string? deaNumber,
        IdentityProofingLevel identityProofingLevel,
        DateTime? identityProofingDate,
        List<EpcsTwoFactorMethod>? configuredTwoFactorMethods,
        string? certificateThumbprint, DateTime? certificateExpiration)
    {
        _state.State.ProviderId = providerId;
        _state.State.ProviderName = providerName;
        _state.State.Npi = npi;
        _state.State.DeaNumber = deaNumber;
        _state.State.IdentityProofingLevel = identityProofingLevel;
        _state.State.IdentityProofingDate = identityProofingDate;
        _state.State.ConfiguredTwoFactorMethods = configuredTwoFactorMethods ?? new();
        _state.State.CertificateThumbprint = certificateThumbprint;
        _state.State.CertificateExpiration = certificateExpiration;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task ActivateAsync()
    {
        _state.State.CredentialStatus = EpcsCredentialStatus.Active;
        _state.State.ActivatedDate = DateTime.UtcNow;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task SuspendAsync()
    {
        _state.State.CredentialStatus = EpcsCredentialStatus.Suspended;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task RevokeAsync()
    {
        _state.State.CredentialStatus = EpcsCredentialStatus.Revoked;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task RecordUsageAsync()
    {
        _state.State.LastUsedDate = DateTime.UtcNow;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }
}
