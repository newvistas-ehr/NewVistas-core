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
/// Lab EDI Order Grain — represents an electronic lab order sent to an
/// external reference laboratory for send-out testing.
///
/// Key: "LAB-EDI-ORD:{guid}"
/// Store: labEdiOrderStore
/// </summary>
public class LabEdiOrderGrain : Grain, ILabEdiOrderGrain
{
    private readonly IPersistentState<LabEdiOrderState> _state;

    public LabEdiOrderGrain(
        [PersistentState("labEdiOrder", "labEdiOrderStore")]
        IPersistentState<LabEdiOrderState> state)
    {
        _state = state;
    }

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_state.State.OrderId))
        {
            _state.State.OrderId = this.GetPrimaryKeyString();
            _state.State.CreatedDate = DateTime.UtcNow;
            _state.State.LastModifiedDate = DateTime.UtcNow;
        }

        return base.OnActivateAsync(cancellationToken);
    }

    public Task<LabEdiOrderState> GetOrderAsync()
        => Task.FromResult(_state.State);

    public async Task CreateOrderAsync(
        string patientId,
        string patientName,
        string referenceLab,
        string referenceLabId,
        string orderingProviderId,
        string orderingProviderName,
        List<LabEdiTestRequest> testsRequested,
        string? clinicalHistory,
        string? specimenType,
        string? specimenId,
        DateTime? collectionDate,
        string? priority)
    {
        _state.State.PatientId = patientId;
        _state.State.PatientName = patientName;
        _state.State.ReferenceLab = referenceLab;
        _state.State.ReferenceLabId = referenceLabId;
        _state.State.OrderingProviderId = orderingProviderId;
        _state.State.OrderingProviderName = orderingProviderName;
        _state.State.TestsRequested = testsRequested;
        _state.State.ClinicalHistory = clinicalHistory;
        _state.State.SpecimenType = specimenType;
        _state.State.SpecimenId = specimenId;
        _state.State.CollectionDate = collectionDate;
        _state.State.Priority = priority ?? "ROUTINE";
        _state.State.Status = LabEdiOrderStatus.Draft;
        _state.State.LastModifiedDate = DateTime.UtcNow;

        await _state.WriteStateAsync();
    }

    public async Task MarkTransmittedAsync(string hl7MessageId, DateTime transmittedDate)
    {
        _state.State.Hl7MessageId = hl7MessageId;
        _state.State.TransmittedDate = transmittedDate;
        _state.State.Status = LabEdiOrderStatus.Transmitted;
        _state.State.LastModifiedDate = DateTime.UtcNow;

        await _state.WriteStateAsync();
    }

    public async Task MarkAcknowledgedAsync(string acknowledgmentCode, DateTime acknowledgedDate)
    {
        _state.State.AcknowledgmentCode = acknowledgmentCode;
        _state.State.AcknowledgedDate = acknowledgedDate;
        _state.State.Status = LabEdiOrderStatus.Acknowledged;
        _state.State.LastModifiedDate = DateTime.UtcNow;

        await _state.WriteStateAsync();
    }

    public async Task MarkResultReceivedAsync(DateTime receivedDate)
    {
        _state.State.ResultReceivedDate = receivedDate;
        _state.State.Status = LabEdiOrderStatus.ResultReceived;
        _state.State.LastModifiedDate = DateTime.UtcNow;

        await _state.WriteStateAsync();
    }

    public async Task MarkRejectedAsync(string rejectionReason, DateTime rejectedDate)
    {
        _state.State.RejectedReason = rejectionReason;
        _state.State.RejectedDate = rejectedDate;
        _state.State.Status = LabEdiOrderStatus.Rejected;
        _state.State.LastModifiedDate = DateTime.UtcNow;

        await _state.WriteStateAsync();
    }

    public async Task CancelOrderAsync(string reason, string cancelledBy)
    {
        _state.State.CancelledReason = reason;
        _state.State.CancelledBy = cancelledBy;
        _state.State.Status = LabEdiOrderStatus.Cancelled;
        _state.State.LastModifiedDate = DateTime.UtcNow;

        await _state.WriteStateAsync();
    }
}
