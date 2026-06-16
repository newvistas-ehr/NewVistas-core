// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using Orleans.Runtime;

namespace NewVistas.Abstractions.Grains;

public class PatientEnrollmentGrain : Grain, IPatientEnrollmentGrain
{
    private readonly IPersistentState<PatientEnrollmentState> _state;

    public PatientEnrollmentGrain(
        [PersistentState("patientEnrollmentState", "patientEnrollmentStore")]
        IPersistentState<PatientEnrollmentState> state)
    {
        _state = state;
    }

    public Task<PatientEnrollmentState> GetAsync()
        => Task.FromResult(_state.State);

    public async Task InitializeAsync(
        string patientId,
        string? enrollmentSource,
        string? enrollmentSiteId,
        string? enrollmentSiteName)
    {
        if (!string.IsNullOrEmpty(_state.State.PatientId))
            return; // already initialized

        _state.State.PatientId         = patientId;
        _state.State.EnrollmentSource  = enrollmentSource;
        _state.State.EnrollmentSiteId  = enrollmentSiteId;
        _state.State.EnrollmentSiteName = enrollmentSiteName;
        _state.State.ApplicationDate   = DateTime.UtcNow;
        _state.State.CreatedDate       = DateTime.UtcNow;
        _state.State.LastModifiedDate  = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task UpdateStatusAsync(EnrollmentStatus status, string changedByUserId, string? notes)
    {
        _state.State.EnrollmentStatus          = status;
        _state.State.LastStatusChangeDate      = DateTime.UtcNow;
        _state.State.LastStatusChangedByUserId = changedByUserId;
        if (notes != null)
            _state.State.Notes = notes;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task SetPriorityGroupAsync(
        string priorityGroup,
        string? prioritySubgroup,
        bool meansTestRequired,
        bool copayExempt,
        string? copayExemptionReason)
    {
        _state.State.PriorityGroup         = priorityGroup;
        _state.State.PrioritySubgroup      = prioritySubgroup;
        _state.State.MeansTestRequired     = meansTestRequired;
        _state.State.CopayExempt           = copayExempt;
        _state.State.CopayExemptionReason  = copayExemptionReason;
        _state.State.LastModifiedDate      = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task SetDatesAsync(DateTime? applicationDate, DateTime? effectiveDate, DateTime? terminationDate)
    {
        if (applicationDate.HasValue) _state.State.ApplicationDate  = applicationDate;
        if (effectiveDate.HasValue)   _state.State.EffectiveDate    = effectiveDate;
        if (terminationDate.HasValue) _state.State.TerminationDate  = terminationDate;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }
}
