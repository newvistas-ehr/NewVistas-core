// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

public class QMIncidentGrain : Grain, IQMIncidentGrain
{
    private readonly IPersistentState<QMIncidentState> _state;

    public QMIncidentGrain(
        [PersistentState("qmIncidentState", "qmIncidentStore")] IPersistentState<QMIncidentState> state)
    {
        _state = state;
    }

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_state.State.IncidentId))
        {
            _state.State.IncidentId = this.GetPrimaryKeyString();
            _state.State.CreatedDate = DateTime.UtcNow;
            _state.State.LastModifiedDate = DateTime.UtcNow;
        }
        return base.OnActivateAsync(cancellationToken);
    }

    public async Task ReportIncidentAsync(
        string patientId,
        string patientName,
        DateTime occurrenceDate,
        OccurrenceCategory category,
        string description,
        string location,
        string wardUnit,
        OccurrenceSeverity severity,
        string reportedBy,
        string reportedByTitle,
        string immediateAction,
        string diagnosisAtTime,
        string procedureAtTime,
        string medicationInvolved,
        string equipmentInvolved)
    {
        _state.State.PatientId = patientId;
        _state.State.PatientName = patientName;
        _state.State.OccurrenceDate = occurrenceDate;
        _state.State.Category = category;
        _state.State.Description = description;
        _state.State.Location = location;
        _state.State.WardUnit = wardUnit;
        _state.State.Severity = severity;
        _state.State.ReportedBy = reportedBy;
        _state.State.ReportedByTitle = reportedByTitle;
        _state.State.ImmediateAction = immediateAction;
        _state.State.DiagnosisAtTime = diagnosisAtTime;
        _state.State.ProcedureAtTime = procedureAtTime;
        _state.State.MedicationInvolved = medicationInvolved;
        _state.State.EquipmentInvolved = equipmentInvolved;
        _state.State.ReportedDate = DateTime.UtcNow;
        _state.State.Status = IncidentStatus.Reported;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task UpdateOutcomeAsync(string outcomeDescription, bool patientNotified, bool familyNotified)
    {
        _state.State.OutcomeDescription = outcomeDescription;
        _state.State.PatientNotified = patientNotified;
        _state.State.FamilyNotified = familyNotified;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task AddStaffInvolvedAsync(string staffName)
    {
        if (!_state.State.StaffInvolved.Contains(staffName))
        {
            _state.State.StaffInvolved.Add(staffName);
            _state.State.LastModifiedDate = DateTime.UtcNow;
            await _state.WriteStateAsync();
        }
    }

    public async Task AddReviewToIncidentAsync(string reviewId, QMReviewType reviewType)
    {
        if (!_state.State.ReviewIds.Contains(reviewId))
            _state.State.ReviewIds.Add(reviewId);
        _state.State.Status = reviewType == QMReviewType.RootCauseAnalysis
            ? IncidentStatus.RCAInProgress
            : IncidentStatus.PeerReviewAssigned;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task SetRootCauseIdentifiedAsync(bool identified, string correctiveActionsSummary)
    {
        _state.State.RootCauseIdentified = identified;
        _state.State.CorrectiveActionsSummary = correctiveActionsSummary;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task CloseIncidentAsync()
    {
        _state.State.Status = IncidentStatus.Closed;
        _state.State.ClosedDate = DateTime.UtcNow;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task VoidIncidentAsync(string reason)
    {
        _state.State.VoidReason = reason;
        _state.State.Status = IncidentStatus.Voided;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public Task<QMIncidentState> GetIncidentAsync() => Task.FromResult(_state.State);
}
