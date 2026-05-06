// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using Orleans.Runtime;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// Patient portal account grain implementation.
/// Grain key: "PORTAL-ACCT:{patientId}".
/// </summary>
public class PatientAccountGrain(
    [PersistentState("patientAccountState", "patientAccountStore")]
    IPersistentState<PatientAccountState> state) : Grain, IPatientAccountGrain
{
    private readonly IPersistentState<PatientAccountState> _state = state;

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_state.State.PatientId))
        {
            string key = this.GetPrimaryKeyString();
            // Key is "PORTAL-ACCT:{patientId}" — extract patientId
            _state.State.PatientId = key.StartsWith("PORTAL-ACCT:")
                ? key["PORTAL-ACCT:".Length..]
                : key;
        }
        return base.OnActivateAsync(cancellationToken);
    }

    public async Task<bool> RegisterAsync(string email, string passwordHash, string? displayName)
    {
        if (_state.State.IsRegistered)
            return false; // Already registered

        _state.State.Email = email;
        _state.State.PasswordHash = passwordHash;
        _state.State.DisplayName = displayName ?? string.Empty;
        _state.State.IsRegistered = true;
        _state.State.IsActive = true;
        _state.State.CreatedDate = DateTime.UtcNow;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
        return true;
    }

    public Task<bool> VerifyCredentialsAsync(string passwordHash)
    {
        if (!_state.State.IsRegistered || !_state.State.IsActive)
            return Task.FromResult(false);
        return Task.FromResult(_state.State.PasswordHash == passwordHash);
    }

    public Task<PatientAccountState> GetAccountAsync()
        => Task.FromResult(_state.State);

    public Task<bool> IsRegisteredAsync()
        => Task.FromResult(_state.State.IsRegistered);

    public async Task UpdateDisplayNameAsync(string displayName)
    {
        _state.State.DisplayName = displayName;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task UpdateEmailAsync(string email)
    {
        _state.State.Email = email;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task ChangePasswordAsync(string newPasswordHash)
    {
        _state.State.PasswordHash = newPasswordHash;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

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

    public async Task RecordLoginAsync()
    {
        _state.State.LastLoginDate = DateTime.UtcNow;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }
}
