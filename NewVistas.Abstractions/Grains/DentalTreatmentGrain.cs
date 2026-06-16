// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using Orleans.Runtime;

namespace NewVistas.Abstractions.Grains;

public class DentalTreatmentGrain : Grain, IDentalTreatmentGrain
{
    private readonly IPersistentState<DentalTreatmentState> _state;

    public DentalTreatmentGrain(
        [PersistentState("dentalTreatmentState", "dentalTreatmentStore")]
        IPersistentState<DentalTreatmentState> state)
    {
        _state = state;
    }

    public Task<DentalTreatmentState> GetAsync()
        => Task.FromResult(_state.State);

    public async Task CreateAsync(
        string patientId,
        DateTime treatmentDate,
        string procedureCode,
        string procedureDescription,
        DentalProcedureCategory procedureCategory,
        List<int> toothNumbers,
        List<string> surfaces,
        string providerId,
        string providerName,
        string? locationId,
        string? locationName,
        string? diagnosisCode,
        string? anesthesiaType,
        decimal? chargeAmount,
        string? notes)
    {
        _state.State.TreatmentId          = this.GetPrimaryKeyString();
        _state.State.PatientId            = patientId;
        _state.State.TreatmentDate        = treatmentDate;
        _state.State.ProcedureCode        = procedureCode;
        _state.State.ProcedureDescription = procedureDescription;
        _state.State.ProcedureCategory    = procedureCategory;
        _state.State.ToothNumbers         = toothNumbers;
        _state.State.Surfaces             = surfaces;
        _state.State.ProviderId           = providerId;
        _state.State.ProviderName         = providerName;
        _state.State.LocationId           = locationId;
        _state.State.LocationName         = locationName;
        _state.State.DiagnosisCode        = diagnosisCode;
        _state.State.AnesthesiaType       = anesthesiaType;
        _state.State.ChargeAmount         = chargeAmount;
        _state.State.Notes                = notes;
        _state.State.Status               = DentalTreatmentStatus.Planned;
        _state.State.CreatedDate          = DateTime.UtcNow;
        _state.State.LastModifiedDate     = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task CompleteAsync(DateTime completedDate, string completedByUserId, string? notes)
    {
        _state.State.Status               = DentalTreatmentStatus.Completed;
        _state.State.CompletedDate        = completedDate;
        _state.State.LastModifiedByUserId = completedByUserId;
        if (notes != null)
            _state.State.Notes = notes;
        _state.State.LastModifiedDate     = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task CancelAsync(string reason, string cancelledByUserId)
    {
        _state.State.Status               = DentalTreatmentStatus.Cancelled;
        _state.State.StatusReason         = reason;
        _state.State.LastModifiedByUserId = cancelledByUserId;
        _state.State.LastModifiedDate     = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task ReferAsync(string referralReason, string referredByUserId)
    {
        _state.State.Status               = DentalTreatmentStatus.Referred;
        _state.State.StatusReason         = referralReason;
        _state.State.LastModifiedByUserId = referredByUserId;
        _state.State.LastModifiedDate     = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }
}
