// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;
using Orleans.Runtime;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// Patient Access Control Grain — manages sensitive record flags, authorized provider lists,
/// and break-the-glass audit trail.
///
/// Mirrors VistA DG SENSITIVITY routines and DG SECURITY LOG (File #38.1).
/// Key: "PAC:{patientId}"
/// </summary>
public class PatientAccessControlGrain : Grain, IPatientAccessControlGrain
{
    private readonly IPersistentState<PatientAccessControlState> _state;

    public PatientAccessControlGrain(
        [PersistentState("patientAccess", "patientAccessStore")] IPersistentState<PatientAccessControlState> state)
    {
        _state = state;
    }

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_state.State.PatientId))
        {
            _state.State.PatientId = this.GetPrimaryKeyString();
            _state.State.CreatedDate = DateTime.UtcNow;
        }
        return base.OnActivateAsync(cancellationToken);
    }

    public Task<PatientAccessControlState> GetAccessControlAsync()
        => Task.FromResult(_state.State);

    public async Task SetSensitivityAsync(bool isSensitive, string sensitivityLevel, List<string> categories)
    {
        _state.State.IsSensitive = isSensitive;
        _state.State.SensitivityLevel = sensitivityLevel;
        _state.State.SensitivityCategories = categories;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task AddAuthorizedProviderAsync(string providerId)
    {
        if (!_state.State.AuthorizedProviderIds.Contains(providerId))
        {
            _state.State.AuthorizedProviderIds.Add(providerId);
            _state.State.LastModifiedDate = DateTime.UtcNow;
            await _state.WriteStateAsync();
        }
    }

    public async Task RemoveAuthorizedProviderAsync(string providerId)
    {
        if (_state.State.AuthorizedProviderIds.Remove(providerId))
        {
            _state.State.LastModifiedDate = DateTime.UtcNow;
            await _state.WriteStateAsync();
        }
    }

    public Task<bool> CheckAccessAsync(string userId)
    {
        if (!_state.State.IsSensitive)
            return Task.FromResult(true);

        bool authorized = _state.State.AuthorizedProviderIds.Contains(userId);
        return Task.FromResult(authorized);
    }

    public async Task RecordAccessAsync(string userId, string userName, string accessReason, bool wasBreakTheGlass, string? justificationText)
    {
        var entry = new PatientAccessLog
        {
            UserId = userId,
            UserName = userName,
            AccessDateTime = DateTime.UtcNow,
            AccessReason = accessReason,
            WasBreakTheGlass = wasBreakTheGlass,
            JustificationText = justificationText
        };
        _state.State.AccessLog.Add(entry);
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public Task<List<PatientAccessLog>> GetAccessLogAsync()
        => Task.FromResult(_state.State.AccessLog);

    public async Task ClearAccessLogAsync()
    {
        _state.State.AccessLog.Clear();
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task SetPart2ConsentAsync(bool hasConsent, DateTime? consentDate, string? scope)
    {
        _state.State.HasPart2Consent = hasConsent;
        _state.State.Part2ConsentDate = consentDate;
        _state.State.Part2ConsentScope = scope ?? string.Empty;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public Task<bool> HasPart2ConsentAsync()
        => Task.FromResult(_state.State.HasPart2Consent);
}
