// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using Orleans.Runtime;

namespace NewVistas.Abstractions.Grains;

public class FeePatientGrain : Grain, IFeePatientGrain
{
    private readonly IPersistentState<FeePatientState> _state;

    public FeePatientGrain(
        [PersistentState("feePatientState", "feePatientStore")]
        IPersistentState<FeePatientState> state)
    {
        _state = state;
    }

    public Task<FeePatientState> GetAsync()
        => Task.FromResult(_state.State);

    public async Task EnsureInitializedAsync(string patientId)
    {
        if (!string.IsNullOrEmpty(_state.State.PatientId))
            return;

        _state.State.PatientId    = patientId;
        _state.State.CreatedDate  = DateTime.UtcNow;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task UpdateSummaryAsync(decimal totalAuthorized, decimal totalPaid, int activeAuthorizationCount)
    {
        _state.State.TotalAuthorizedAmount  = totalAuthorized;
        _state.State.TotalPaidAmount        = totalPaid;
        _state.State.ActiveAuthorizationCount = activeAuthorizationCount;
        _state.State.LastActivityDate       = DateTime.UtcNow;
        _state.State.LastModifiedDate       = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task SetEligibilityAsync(bool isEligible, DateTime? startDate, DateTime? endDate)
    {
        _state.State.IsEligibleForFeeBasis = isEligible;
        _state.State.EligibilityStartDate  = startDate;
        _state.State.EligibilityEndDate    = endDate;
        _state.State.LastModifiedDate      = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }
}
