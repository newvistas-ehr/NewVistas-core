// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;
using Orleans.Runtime;
using NewVistas.PT.GrainInterfaces;
using NewVistas.PT.GrainStates;
using NewVistas.PT.Models;

namespace NewVistas.PT.Grains;

/// <summary>
/// Grain representing a single PT referral from an external referring provider.
/// Write-once create with incremental status and visit-count updates.
/// </summary>
public class PTReferralGrain : Grain, IPTReferralGrain
{
    private readonly IPersistentState<PTReferralState> _state;

    public PTReferralGrain(
        [PersistentState("ptReferralState", "physTherapyReferralStore")]
        IPersistentState<PTReferralState> state)
    {
        _state = state;
    }

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_state.State.ReferralId))
        {
            _state.State.ReferralId = this.GetPrimaryKeyString();
            _state.State.CreatedDate = DateTime.UtcNow;
            _state.State.LastModifiedDate = DateTime.UtcNow;
        }
        return base.OnActivateAsync(cancellationToken);
    }

    public Task<PTReferralState> GetReferralAsync() => Task.FromResult(_state.State);

    public async Task CreateReferralAsync(
        string patientId,
        string patientName,
        string? referringProviderName,
        string? referringProviderId,
        string? referringProviderSpecialty,
        string? referringFacilityName,
        string? diagnosis,
        string? diagnosisCode,
        List<BodyGroup>? bodyGroups,
        string? reasonForReferral,
        string? precautions,
        int authorizedVisits,
        DateTime? authorizationExpirationDate,
        DateTime referralDate,
        DateTime? receivedDate,
        string? notes)
    {
        _state.State.PatientId = patientId;
        _state.State.PatientName = patientName;
        _state.State.ReferringProviderName = referringProviderName;
        _state.State.ReferringProviderId = referringProviderId;
        _state.State.ReferringProviderSpecialty = referringProviderSpecialty;
        _state.State.ReferringFacilityName = referringFacilityName;
        _state.State.Diagnosis = diagnosis;
        _state.State.DiagnosisCode = diagnosisCode;
        _state.State.BodyGroups = bodyGroups ?? new();
        _state.State.ReasonForReferral = reasonForReferral;
        _state.State.Precautions = precautions;
        _state.State.AuthorizedVisits = authorizedVisits;
        _state.State.AuthorizationExpirationDate = authorizationExpirationDate;
        _state.State.ReferralDate = referralDate;
        _state.State.ReceivedDate = receivedDate;
        _state.State.Notes = notes;
        _state.State.Status = PTReferralStatus.Active;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();

        await UpdateIndexAsync();
    }

    public async Task UpdateStatusAsync(PTReferralStatus status, string? notes)
    {
        _state.State.Status = status;
        if (notes != null)
            _state.State.Notes = notes;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();

        await UpdateIndexAsync();
    }

    public async Task<int> IncrementVisitCountAsync()
    {
        _state.State.UsedVisits++;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();

        await UpdateIndexAsync();

        return _state.State.UsedVisits;
    }

    public async Task UpdateAuthorizationAsync(int authorizedVisits, DateTime? expirationDate)
    {
        _state.State.AuthorizedVisits = authorizedVisits;
        _state.State.AuthorizationExpirationDate = expirationDate;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();

        await UpdateIndexAsync();
    }

    public async Task UpdateNotesAsync(string notes)
    {
        _state.State.Notes = notes;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    private Task UpdateIndexAsync()
    {
        if (string.IsNullOrEmpty(_state.State.PatientId))
            return Task.CompletedTask;

        IPTReferralIndexGrain indexGrain = GrainFactory.GetGrain<IPTReferralIndexGrain>(
            $"PTREF-IDX:{_state.State.PatientId}");

        return indexGrain.AddOrUpdateAsync(new PTReferralIndexEntry
        {
            ReferralGrainKey = _state.State.ReferralId,
            PatientId = _state.State.PatientId,
            ReferringProviderName = _state.State.ReferringProviderName,
            Diagnosis = _state.State.Diagnosis,
            AuthorizedVisits = _state.State.AuthorizedVisits,
            UsedVisits = _state.State.UsedVisits,
            Status = _state.State.Status,
            ReferralDate = _state.State.ReferralDate,
            AuthorizationExpirationDate = _state.State.AuthorizationExpirationDate
        });
    }
}
