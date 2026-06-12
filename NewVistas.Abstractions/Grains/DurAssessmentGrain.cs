// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using Orleans;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// Grain implementation for a single Drug Utilization Review assessment.
/// Stores all DUR check results and supports pharmacist override workflow.
///
/// VistA reference: PSOORED.m, PSOVER1.m, PSODRDUP.m
/// Key format: DUR:{guid}
/// </summary>
public class DurAssessmentGrain : Grain, IDurAssessmentGrain
{
    private readonly IPersistentState<DurAssessmentState> _state;

    public DurAssessmentGrain(
        [PersistentState("durAssessmentState", "durAssessmentStore")]
        IPersistentState<DurAssessmentState> state)
    {
        _state = state;
    }

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_state.State.AssessmentId))
        {
            _state.State.AssessmentId = this.GetPrimaryKeyString();
            _state.State.CreatedDate = DateTime.UtcNow;
            _state.State.LastModifiedDate = DateTime.UtcNow;
        }
        return base.OnActivateAsync(cancellationToken);
    }

    public async Task CreateAsync(
        string prescriptionId,
        string patientId,
        string drugName,
        string? drugId,
        string? drugClass,
        string? dosage,
        string? route,
        string? schedule,
        int? daysSupply,
        int? quantity,
        string? performedBy,
        List<DurCheckResult> checks)
    {
        _state.State.PrescriptionId = prescriptionId;
        _state.State.PatientId = patientId;
        _state.State.DrugName = drugName;
        _state.State.DrugId = drugId;
        _state.State.DrugClass = drugClass;
        _state.State.Dosage = dosage;
        _state.State.Route = route;
        _state.State.Schedule = schedule;
        _state.State.DaysSupply = daysSupply;
        _state.State.Quantity = quantity;
        _state.State.PerformedBy = performedBy;
        _state.State.PerformedDate = DateTime.UtcNow;
        _state.State.Checks = checks;

        // Calculate counts and overall outcome
        _state.State.FailedCheckCount = checks.Count(c => c.Outcome == DurOutcome.Fail);
        _state.State.WarningCheckCount = checks.Count(c => c.Outcome == DurOutcome.Warning);
        _state.State.OverriddenCheckCount = 0;

        // Unavailable = reference data missing; blocks like Fail but is not
        // pharmacist-overridable (OverrideCheckAsync only overrides Fail).
        bool hasUnavailable = checks.Any(c => c.Outcome == DurOutcome.Unavailable);

        if (_state.State.FailedCheckCount > 0 || hasUnavailable)
        {
            _state.State.OverallOutcome = hasUnavailable && _state.State.FailedCheckCount == 0
                ? DurOutcome.Unavailable
                : DurOutcome.Fail;
            _state.State.Status = DurAssessmentStatus.Failed;
        }
        else if (_state.State.WarningCheckCount > 0)
        {
            _state.State.OverallOutcome = DurOutcome.Warning;
            _state.State.Status = DurAssessmentStatus.Pending;
        }
        else
        {
            _state.State.OverallOutcome = DurOutcome.Pass;
            _state.State.Status = DurAssessmentStatus.Passed;
        }

        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public Task<DurAssessmentState> GetAsync()
        => Task.FromResult(_state.State);

    public async Task OverrideCheckAsync(
        DurCheckType checkType,
        string pharmacistId,
        string reason)
    {
        DurCheckResult? check = _state.State.Checks.FirstOrDefault(c => c.CheckType == checkType);
        if (check is null || check.Outcome != DurOutcome.Fail)
            return;

        check.Outcome = DurOutcome.Override;
        check.OverriddenBy = pharmacistId;
        check.OverrideReason = reason;
        check.OverrideDate = DateTime.UtcNow;

        // Recalculate counts
        _state.State.FailedCheckCount = _state.State.Checks.Count(c => c.Outcome == DurOutcome.Fail);
        _state.State.OverriddenCheckCount = _state.State.Checks.Count(c => c.Outcome == DurOutcome.Override);

        // If no more failed checks, update status. An Unavailable check keeps
        // the assessment Failed — it cannot be overridden away.
        if (_state.State.FailedCheckCount == 0
            && !_state.State.Checks.Any(c => c.Outcome == DurOutcome.Unavailable))
        {
            _state.State.Status = DurAssessmentStatus.OverriddenByPharmacist;
            _state.State.OverallOutcome = DurOutcome.Override;
        }

        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task AcknowledgeAsync(string pharmacistId, string? notes)
    {
        _state.State.AcknowledgedBy = pharmacistId;
        _state.State.AcknowledgedDate = DateTime.UtcNow;
        _state.State.Status = DurAssessmentStatus.Acknowledged;

        if (!string.IsNullOrEmpty(notes))
        {
            _state.State.Notes = string.IsNullOrEmpty(_state.State.Notes)
                ? notes
                : $"{_state.State.Notes}\n{notes}";
        }

        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task AddNotesAsync(string notes)
    {
        _state.State.Notes = string.IsNullOrEmpty(_state.State.Notes)
            ? notes
            : $"{_state.State.Notes}\n{notes}";

        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }
}
