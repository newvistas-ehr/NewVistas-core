// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;
using Orleans.Runtime;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// Optional feature grain for automated prescription refill scheduling.
/// Keyed by "RX-AUTOREFILL:{guid}".
/// </summary>
public class AutoRefillGrain : Grain, IAutoRefillGrain
{
    private readonly IPersistentState<AutoRefillState> _state;

    public AutoRefillGrain(
        [PersistentState("autoRefillState", "autoRefillStore")]
        IPersistentState<AutoRefillState> state)
    {
        _state = state;
    }

    public Task<AutoRefillState> GetEnrollmentAsync() => Task.FromResult(_state.State);

    public async Task<AutoRefillState> EnrollAsync(
        string patientId, string patientName,
        string prescriptionId, string drugName, string drugClass,
        int daysSupply, int refillsRemaining, DateTime lastFillDate,
        string pharmacyId, string pharmacyName,
        string enrolledByProviderId, string enrolledByProviderName)
    {
        _state.State.EnrollmentId = this.GetPrimaryKeyString();
        _state.State.PatientId = patientId;
        _state.State.PatientName = patientName;
        _state.State.PrescriptionId = prescriptionId;
        _state.State.DrugName = drugName;
        _state.State.DrugClass = drugClass;
        _state.State.DaysSupply = daysSupply;
        _state.State.RefillsRemaining = refillsRemaining;
        _state.State.LastFillDate = lastFillDate;
        _state.State.LeadTimeDays = 7;
        _state.State.NextRefillDate = CalculateNextRefillDate(lastFillDate, daysSupply, 7);
        _state.State.PharmacyId = pharmacyId;
        _state.State.PharmacyName = pharmacyName;
        _state.State.Status = refillsRemaining > 0 ? "ACTIVE" : "NO_REFILLS";
        _state.State.EnrolledByProviderId = enrolledByProviderId;
        _state.State.EnrolledByProviderName = enrolledByProviderName;
        _state.State.TotalRefillsGenerated = 0;
        _state.State.CreatedDate = DateTime.UtcNow;
        _state.State.LastModifiedDate = DateTime.UtcNow;

        _state.State.RefillHistory.Add(new AutoRefillEvent
        {
            EventDate = DateTime.UtcNow,
            EventType = "ENROLLED",
            PerformedByName = enrolledByProviderName,
            Details = $"Enrolled {drugName} ({daysSupply}-day supply, {refillsRemaining} refills remaining)"
        });

        await _state.WriteStateAsync();
        await UpdateIndexAsync();
        return _state.State;
    }

    public async Task UpdateDaysSupplyAsync(int daysSupply)
    {
        _state.State.DaysSupply = daysSupply;
        _state.State.NextRefillDate = CalculateNextRefillDate(
            _state.State.LastFillDate, daysSupply, _state.State.LeadTimeDays);
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
        await UpdateIndexAsync();
    }

    public async Task UpdateRefillsRemainingAsync(int refillsRemaining)
    {
        _state.State.RefillsRemaining = refillsRemaining;
        if (refillsRemaining <= 0 && _state.State.Status == "ACTIVE")
            _state.State.Status = "NO_REFILLS";
        else if (refillsRemaining > 0 && _state.State.Status == "NO_REFILLS")
            _state.State.Status = "ACTIVE";
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
        await UpdateIndexAsync();
    }

    public async Task RecordFillAsync(DateTime fillDate, int newRefillsRemaining)
    {
        _state.State.LastFillDate = fillDate;
        _state.State.RefillsRemaining = newRefillsRemaining;
        _state.State.NextRefillDate = CalculateNextRefillDate(
            fillDate, _state.State.DaysSupply, _state.State.LeadTimeDays);

        if (newRefillsRemaining <= 0)
            _state.State.Status = "NO_REFILLS";
        else if (_state.State.Status is "REFILL_PENDING" or "NO_REFILLS")
            _state.State.Status = "ACTIVE";

        _state.State.RefillHistory.Add(new AutoRefillEvent
        {
            EventDate = DateTime.UtcNow,
            EventType = "FILL_RECORDED",
            PerformedByName = "SYSTEM",
            Details = $"Filled on {fillDate:d}, {newRefillsRemaining} refills remaining, next due {_state.State.NextRefillDate:d}"
        });

        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
        await UpdateIndexAsync();
    }

    public async Task GenerateRefillRequestAsync(string generatedByName)
    {
        if (_state.State.RefillsRemaining <= 0)
            throw new InvalidOperationException("No refills remaining on this prescription.");
        if (_state.State.Status is "SUSPENDED" or "DISENROLLED" or "EXPIRED")
            throw new InvalidOperationException($"Cannot generate refill for enrollment in {_state.State.Status} status.");

        _state.State.Status = "REFILL_PENDING";
        _state.State.TotalRefillsGenerated++;

        _state.State.RefillHistory.Add(new AutoRefillEvent
        {
            EventDate = DateTime.UtcNow,
            EventType = "REFILL_REQUESTED",
            PerformedByName = generatedByName,
            Details = $"Auto-refill #{_state.State.TotalRefillsGenerated} generated for {_state.State.DrugName}"
        });

        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
        await UpdateIndexAsync();
    }

    public async Task MarkRefillDispensedAsync(DateTime dispensedDate, string dispensedByName)
    {
        _state.State.RefillHistory.Add(new AutoRefillEvent
        {
            EventDate = DateTime.UtcNow,
            EventType = "REFILL_DISPENSED",
            PerformedByName = dispensedByName,
            Details = $"Dispensed on {dispensedDate:d}"
        });

        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task SuspendAsync(string reason, string suspendedByName)
    {
        _state.State.Status = "SUSPENDED";
        _state.State.SuspendReason = reason;

        _state.State.RefillHistory.Add(new AutoRefillEvent
        {
            EventDate = DateTime.UtcNow,
            EventType = "SUSPENDED",
            PerformedByName = suspendedByName,
            Details = $"Suspended: {reason}"
        });

        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
        await UpdateIndexAsync();
    }

    public async Task ResumeAsync(string resumedByName)
    {
        if (_state.State.Status != "SUSPENDED")
            throw new InvalidOperationException("Only suspended enrollments can be resumed.");

        _state.State.Status = _state.State.RefillsRemaining > 0 ? "ACTIVE" : "NO_REFILLS";
        _state.State.SuspendReason = null;

        _state.State.RefillHistory.Add(new AutoRefillEvent
        {
            EventDate = DateTime.UtcNow,
            EventType = "RESUMED",
            PerformedByName = resumedByName,
            Details = "Auto-refill resumed"
        });

        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
        await UpdateIndexAsync();
    }

    public async Task DisenrollAsync(string reason, string disenrolledByName)
    {
        _state.State.Status = "DISENROLLED";
        _state.State.SuspendReason = reason;

        _state.State.RefillHistory.Add(new AutoRefillEvent
        {
            EventDate = DateTime.UtcNow,
            EventType = "DISENROLLED",
            PerformedByName = disenrolledByName,
            Details = $"Disenrolled: {reason}"
        });

        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
        await UpdateIndexAsync();
    }

    public async Task ExpireAsync()
    {
        _state.State.Status = "EXPIRED";

        _state.State.RefillHistory.Add(new AutoRefillEvent
        {
            EventDate = DateTime.UtcNow,
            EventType = "EXPIRED",
            PerformedByName = "SYSTEM",
            Details = "Prescription expired — no refills remaining"
        });

        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
        await UpdateIndexAsync();
    }

    private static DateTime CalculateNextRefillDate(DateTime lastFillDate, int daysSupply, int leadTimeDays) =>
        lastFillDate.AddDays(daysSupply - leadTimeDays);

    private async Task UpdateIndexAsync()
    {
        IAutoRefillIndexGrain index =
            GrainFactory.GetGrain<IAutoRefillIndexGrain>("RX-AUTOREFILL-IDX");

        await index.AddOrUpdateAsync(new AutoRefillIndexEntry
        {
            EnrollmentId = _state.State.EnrollmentId,
            PatientId = _state.State.PatientId,
            PatientName = _state.State.PatientName,
            PrescriptionId = _state.State.PrescriptionId,
            DrugName = _state.State.DrugName,
            Status = _state.State.Status,
            NextRefillDate = _state.State.NextRefillDate,
            RefillsRemaining = _state.State.RefillsRemaining,
            PharmacyId = _state.State.PharmacyId,
            PharmacyName = _state.State.PharmacyName,
            TotalRefillsGenerated = _state.State.TotalRefillsGenerated
        });
    }
}
