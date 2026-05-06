// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using Orleans.Runtime;

namespace NewVistas.Abstractions.Grains;

public class EdiTransmissionGrain : Grain, IEdiTransmissionGrain
{
    private readonly IPersistentState<EdiTransmissionState> _state;

    public EdiTransmissionGrain(
        [PersistentState("ediTransmissionState", "ediTransmissionStore")]
        IPersistentState<EdiTransmissionState> state)
    {
        _state = state;
    }

    public Task<EdiTransmissionState> GetAsync()
        => Task.FromResult(_state.State);

    public async Task OpenAsync(string batchNumber, string payerId, string payerName, string? notes)
    {
        DateTime now = DateTime.UtcNow;

        _state.State.TransmissionId   = this.GetPrimaryKeyString();
        _state.State.BatchNumber      = batchNumber;
        _state.State.PayerId          = payerId;
        _state.State.PayerName        = payerName;
        _state.State.Status           = EdiTransmissionStatus.Open;
        _state.State.Notes            = notes;
        _state.State.CreatedDate      = now;
        _state.State.LastModifiedDate = now;

        await _state.WriteStateAsync();
    }

    public async Task AddClaimAsync(string claimId, string patientId, decimal totalBilledAmount, string claimType)
    {
        if (!_state.State.ClaimIds.Contains(claimId))
        {
            _state.State.ClaimIds.Add(claimId);
            _state.State.TotalClaims++;
            _state.State.TotalBilledAmount += totalBilledAmount;
        }

        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task SendAsync(DateTime sentDate)
    {
        _state.State.Status           = EdiTransmissionStatus.Sent;
        _state.State.SentDate         = sentDate;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task RecordAcknowledgmentAsync(
        string acknowledgmentCode,
        int acceptedCount,
        int rejectedCount,
        DateTime acknowledgedDate)
    {
        _state.State.AcknowledgmentCode    = acknowledgmentCode;
        _state.State.AcceptedClaimsCount   = acceptedCount;
        _state.State.RejectedClaimsCount   = rejectedCount;
        _state.State.AcknowledgmentDate    = acknowledgedDate;
        _state.State.Status = rejectedCount == 0 ? EdiTransmissionStatus.Accepted
            : acceptedCount == 0 ? EdiTransmissionStatus.Rejected
            : EdiTransmissionStatus.PartiallyAccepted;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }
}
