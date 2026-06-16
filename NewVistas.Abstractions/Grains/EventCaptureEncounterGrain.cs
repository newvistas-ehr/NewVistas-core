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
/// Event Capture Encounter Grain — manages a single workload/encounter capture record.
/// Persists to "ecEncounterStore".
/// </summary>
public class EventCaptureEncounterGrain : Grain, IEventCaptureEncounterGrain
{
    private readonly IPersistentState<EventCaptureEncounterState> _state;

    public EventCaptureEncounterGrain(
        [PersistentState("ecEncounterState", "ecEncounterStore")]
        IPersistentState<EventCaptureEncounterState> state)
    {
        _state = state;
    }

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_state.State.EncounterId))
        {
            _state.State.EncounterId = this.GetPrimaryKeyString();
            _state.State.CreatedDate = DateTime.UtcNow;
            _state.State.LastModifiedDate = DateTime.UtcNow;
        }
        return base.OnActivateAsync(cancellationToken);
    }

    public Task<EventCaptureEncounterState> GetEncounterAsync() => Task.FromResult(_state.State);

    public async Task CreateAsync(
        string patientId,
        DateTime encounterDateTime,
        string dssUnitId,
        string dssUnitName,
        string? dssUnitCode,
        string? clinicId,
        string? clinicName,
        string? locationId,
        string? locationName,
        string primaryProviderId,
        string primaryProviderName,
        string? attendingProviderId,
        string? attendingProviderName,
        EcEncounterType encounterType,
        EcPatientCategory patientCategory,
        string? primaryStopCode,
        string? creditStopCode,
        string? comments)
    {
        _state.State.PatientId = patientId;
        _state.State.EncounterDateTime = encounterDateTime;
        _state.State.DssUnitId = dssUnitId;
        _state.State.DssUnitName = dssUnitName;
        _state.State.DssUnitCode = dssUnitCode;
        _state.State.ClinicId = clinicId;
        _state.State.ClinicName = clinicName;
        _state.State.LocationId = locationId;
        _state.State.LocationName = locationName;
        _state.State.PrimaryProviderId = primaryProviderId;
        _state.State.PrimaryProviderName = primaryProviderName;
        _state.State.AttendingProviderId = attendingProviderId;
        _state.State.AttendingProviderName = attendingProviderName;
        _state.State.EncounterType = encounterType;
        _state.State.PatientCategory = patientCategory;
        _state.State.PrimaryStopCode = primaryStopCode;
        _state.State.CreditStopCode = creditStopCode;
        _state.State.Comments = comments;
        _state.State.Status = EcEncounterStatus.Open;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task AddProcedureAsync(
        string cptCode,
        string procedureDescription,
        int quantity,
        string providerId,
        string providerName,
        string? modifierCode)
    {
        // Replace existing entry for same CPT + provider combination, or add new
        int idx = _state.State.Procedures.FindIndex(
            p => p.CptCode == cptCode && p.ProviderId == providerId);

        EcProcedureEntry entry = new()
        {
            CptCode = cptCode,
            ProcedureDescription = procedureDescription,
            Quantity = quantity,
            ProviderId = providerId,
            ProviderName = providerName,
            ModifierCode = modifierCode,
            AddedDate = DateTime.UtcNow,
        };

        if (idx >= 0)
            _state.State.Procedures[idx] = entry;
        else
            _state.State.Procedures.Add(entry);

        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task RemoveProcedureAsync(string cptCode, string providerId)
    {
        _state.State.Procedures.RemoveAll(
            p => p.CptCode == cptCode && p.ProviderId == providerId);
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task AddDiagnosisAsync(string icd10Code, string description, bool isPrimary)
    {
        // If marking as primary, clear existing primary flag
        if (isPrimary)
        {
            foreach (EcDiagnosisEntry d in _state.State.Diagnoses)
                d.IsPrimary = false;
        }

        // Replace if same code already exists, otherwise add
        int idx = _state.State.Diagnoses.FindIndex(d => d.Icd10Code == icd10Code);
        EcDiagnosisEntry entry = new()
        {
            Icd10Code = icd10Code,
            Description = description,
            IsPrimary = isPrimary,
        };

        if (idx >= 0)
            _state.State.Diagnoses[idx] = entry;
        else
            _state.State.Diagnoses.Add(entry);

        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task CompleteAsync(DateTime checkOutDateTime, int? visitLengthMinutes)
    {
        _state.State.Status = EcEncounterStatus.Complete;
        _state.State.CheckOutDateTime = checkOutDateTime;
        _state.State.VisitLengthMinutes = visitLengthMinutes;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task DeleteAsync(string deletedByProviderId, string deletedByProviderName, string? reason)
    {
        _state.State.Status = EcEncounterStatus.Deleted;
        _state.State.DeletedByProviderId = deletedByProviderId;
        _state.State.DeletedByProviderName = deletedByProviderName;
        _state.State.DeleteReason = reason;
        _state.State.DeletedDate = DateTime.UtcNow;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task UpdateDssUnitAsync(string dssUnitId, string dssUnitName, string? dssUnitCode)
    {
        _state.State.DssUnitId = dssUnitId;
        _state.State.DssUnitName = dssUnitName;
        _state.State.DssUnitCode = dssUnitCode;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }
}
