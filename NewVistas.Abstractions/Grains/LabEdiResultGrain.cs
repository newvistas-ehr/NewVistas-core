// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;
using Orleans.Runtime;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// Lab EDI Result Grain — represents an inbound result set received
/// from an external reference laboratory via ORU^R01 message.
///
/// Key: "LAB-EDI-RES:{guid}"
/// Store: labEdiResultStore
/// </summary>
public class LabEdiResultGrain : Grain, ILabEdiResultGrain
{
    private readonly IPersistentState<LabEdiResultState> _state;

    public LabEdiResultGrain(
        [PersistentState("labEdiResult", "labEdiResultStore")]
        IPersistentState<LabEdiResultState> state)
    {
        _state = state;
    }

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_state.State.ResultId))
        {
            _state.State.ResultId = this.GetPrimaryKeyString();
            _state.State.CreatedDate = DateTime.UtcNow;
            _state.State.LastModifiedDate = DateTime.UtcNow;
        }

        return base.OnActivateAsync(cancellationToken);
    }

    public Task<LabEdiResultState> GetResultAsync()
        => Task.FromResult(_state.State);

    public async Task RecordResultAsync(
        string orderId,
        string patientId,
        string referenceLab,
        string referenceLabId,
        string hl7MessageId,
        List<LabEdiResultItem> results,
        DateTime resultDate,
        string? performingLabClia)
    {
        _state.State.OrderId = orderId;
        _state.State.PatientId = patientId;
        _state.State.ReferenceLab = referenceLab;
        _state.State.ReferenceLabId = referenceLabId;
        _state.State.Hl7MessageId = hl7MessageId;
        _state.State.Results = results;
        _state.State.ResultDate = resultDate;
        _state.State.PerformingLabClia = performingLabClia;
        _state.State.Status = LabEdiResultStatus.Received;
        _state.State.LastModifiedDate = DateTime.UtcNow;

        await _state.WriteStateAsync();
    }

    public async Task ReviewResultAsync(
        string reviewedBy,
        string reviewedByName,
        DateTime reviewedDate,
        string? comments)
    {
        _state.State.ReviewedBy = reviewedBy;
        _state.State.ReviewedByName = reviewedByName;
        _state.State.ReviewedDate = reviewedDate;
        _state.State.ReviewComments = comments;
        _state.State.Status = LabEdiResultStatus.Reviewed;
        _state.State.LastModifiedDate = DateTime.UtcNow;

        await _state.WriteStateAsync();
    }

    public async Task AcceptResultAsync(string acceptedBy)
    {
        _state.State.AcceptedBy = acceptedBy;
        _state.State.AcceptedDate = DateTime.UtcNow;
        _state.State.Status = LabEdiResultStatus.Accepted;
        _state.State.LastModifiedDate = DateTime.UtcNow;

        await _state.WriteStateAsync();
    }

    public async Task RejectResultAsync(string rejectedBy, string reason)
    {
        _state.State.RejectedBy = rejectedBy;
        _state.State.RejectedReason = reason;
        _state.State.Status = LabEdiResultStatus.Rejected;
        _state.State.LastModifiedDate = DateTime.UtcNow;

        await _state.WriteStateAsync();
    }
}
