// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using Orleans.Runtime;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// Blind Rehabilitation Admission Grain — grain key: "BR-ADMIT:{admitId}"
/// </summary>
public class BRAdmissionGrain : Grain, IBRAdmissionGrain
{
    private readonly IPersistentState<BRAdmissionState> _state;

    public BRAdmissionGrain(
        [PersistentState("brAdmissionState", "brAdmissionStore")]
        IPersistentState<BRAdmissionState> state)
    {
        _state = state;
    }

    public Task<BRAdmissionState> GetAsync() => Task.FromResult(_state.State);

    public async Task CreateAsync(
        string admitId,
        string patientId,
        string centerId,
        string centerName,
        DateTime admitDate,
        DateTime? plannedDischargeDate,
        List<BRTrainingArea> programAreas,
        BRAdmissionPriority priority,
        string referringProviderId,
        string referringProviderName,
        string? goals,
        string? notes)
    {
        _state.State.AdmitId = admitId;
        _state.State.PatientId = patientId;
        _state.State.CenterId = centerId;
        _state.State.CenterName = centerName;
        _state.State.AdmitDate = admitDate;
        _state.State.PlannedDischargeDate = plannedDischargeDate;
        _state.State.ProgramAreas = programAreas;
        _state.State.Priority = priority;
        _state.State.ReferringProviderId = referringProviderId;
        _state.State.ReferringProviderName = referringProviderName;
        _state.State.Goals = goals;
        _state.State.Notes = notes;
        _state.State.Status = BRAdmissionStatus.Pending;
        _state.State.CreatedDate = DateTime.UtcNow;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task AddProgressNoteAsync(string note, string authorId, string authorName)
    {
        _state.State.ProgressNotes.Add(new BRProgressNote
        {
            Note = note,
            AuthorId = authorId,
            AuthorName = authorName,
            RecordedDate = DateTime.UtcNow
        });
        if (_state.State.Status == BRAdmissionStatus.Pending || _state.State.Status == BRAdmissionStatus.Accepted)
            _state.State.Status = BRAdmissionStatus.Active;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task DischargeAsync(
        DateTime dischargeDate,
        BRDischargeDisposition disposition,
        string dischargeSummary,
        List<BRTrainingArea> areasCompleted,
        string? followUpPlan)
    {
        _state.State.ActualDischargeDate = dischargeDate;
        _state.State.DischargeDisposition = disposition;
        _state.State.DischargeSummary = dischargeSummary;
        _state.State.AreasCompleted = areasCompleted;
        _state.State.FollowUpPlan = followUpPlan;
        _state.State.Status = BRAdmissionStatus.Discharged;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task CancelAsync(string reason)
    {
        _state.State.Status = BRAdmissionStatus.Cancelled;
        _state.State.CancellationReason = reason;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }
}
