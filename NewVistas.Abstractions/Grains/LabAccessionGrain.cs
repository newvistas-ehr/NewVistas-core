// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using Orleans.Runtime;

namespace NewVistas.Abstractions.Grains;

public class LabAccessionGrain : Grain, ILabAccessionGrain
{
    private readonly IPersistentState<LabAccessionState> _state;

    public LabAccessionGrain(
        [PersistentState("labAccessionState", "labAccessionStore")]
        IPersistentState<LabAccessionState> state)
    { _state = state; }

    public Task<LabAccessionState> GetAsync() => Task.FromResult(_state.State);

    public async Task<string> CreateAsync(
        string patientId, List<string> labTestIds, string specimenType,
        string? collectionTube, DateTime collectionDateTime,
        string? labSection, string? accessionedByUserId, string? accessionedByUserName, string? notes)
    {
        string accessionNumber = this.GetPrimaryKeyString();
        DateTime now = DateTime.UtcNow;
        _state.State.AccessionNumber       = accessionNumber;
        _state.State.PatientId             = patientId;
        _state.State.LabTestIds            = labTestIds;
        _state.State.SpecimenType          = specimenType;
        _state.State.CollectionTube        = collectionTube;
        _state.State.CollectionDateTime    = collectionDateTime;
        _state.State.ReceivedDateTime      = now;
        _state.State.AccessionedDateTime   = now;
        _state.State.Status                = SpecimenStatus.Accessioned;
        _state.State.LabSection            = labSection;
        _state.State.AccessionedByUserId   = accessionedByUserId;
        _state.State.AccessionedByUserName = accessionedByUserName;
        _state.State.Notes                 = notes;
        _state.State.CreatedDate           = now;
        _state.State.LastModifiedDate      = now;
        await _state.WriteStateAsync();
        return accessionNumber;
    }

    public async Task MarkInProcessAsync()
    {
        _state.State.Status           = SpecimenStatus.InProcess;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task MarkCompletedAsync()
    {
        _state.State.Status           = SpecimenStatus.Completed;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task RejectAsync(SpecimenRejectReason reason, string? rejectNotes,
        string rejectedByUserId, bool orderRecollect)
    {
        _state.State.Status            = SpecimenStatus.Rejected;
        _state.State.IsRejected        = true;
        _state.State.RejectReason      = reason;
        _state.State.RejectNotes       = rejectNotes;
        _state.State.RejectedDateTime  = DateTime.UtcNow;
        _state.State.RejectedByUserId  = rejectedByUserId;
        _state.State.RecollectOrdered  = orderRecollect;
        _state.State.LastModifiedDate  = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }
}
