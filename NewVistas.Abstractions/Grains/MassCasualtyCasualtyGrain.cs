// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;
using Orleans.Runtime;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

public class MassCasualtyCasualtyGrain : Grain, IMassCasualtyCasualtyGrain
{
    private readonly IPersistentState<MassCasualtyCasualtyState> _state;

    public MassCasualtyCasualtyGrain(
        [PersistentState("mciCasualtyState", "mciCasualtyStore")]
        IPersistentState<MassCasualtyCasualtyState> state) { _state = state; }

    public Task<MassCasualtyCasualtyState> GetCasualtyAsync() => Task.FromResult(_state.State);

    public async Task<MassCasualtyCasualtyState> RegisterCasualtyAsync(
        string incidentId, string triageTag, string triageCategory,
        string? patientId, string? patientName, string? chiefInjury,
        string? arrivalMode, string registeredByName)
    {
        _state.State.CasualtyId = this.GetPrimaryKeyString();
        _state.State.IncidentId = incidentId;
        _state.State.TriageTag = triageTag;
        _state.State.TriageCategory = triageCategory;
        _state.State.PatientId = patientId;
        _state.State.PatientName = patientName ?? "UNIDENTIFIED";
        _state.State.ChiefInjury = chiefInjury;
        _state.State.ArrivalMode = arrivalMode;
        _state.State.Disposition = "PENDING";
        _state.State.RegisteredByName = registeredByName;
        _state.State.RegisteredDate = DateTime.UtcNow;
        _state.State.LastModifiedDate = DateTime.UtcNow;

        await _state.WriteStateAsync();
        await UpdateCasualtyIndexAsync();

        // Increment incident casualty count
        var incident = GrainFactory.GetGrain<IMassCasualtyIncidentGrain>(incidentId);
        var incState = await incident.GetIncidentAsync();
        // We update via a status note; the incident grain tracks its own count
        await incident.AddStatusUpdateAsync(
            $"Casualty registered: Tag {triageTag}, Triage {triageCategory}", registeredByName);

        return _state.State;
    }

    public async Task UpdateTriageCategoryAsync(string triageCategory, string updatedByName)
    {
        _state.State.TriageCategory = triageCategory;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        _state.State.Notes.Add(new MciCasualtyNote
        {
            Timestamp = DateTime.UtcNow, Note = $"Triage changed to {triageCategory}", AuthorName = updatedByName
        });
        await _state.WriteStateAsync();
        await UpdateCasualtyIndexAsync();
    }

    public async Task AssignToAreaAsync(string treatmentArea, string assignedByName)
    {
        _state.State.TreatmentArea = treatmentArea;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        _state.State.Notes.Add(new MciCasualtyNote
        {
            Timestamp = DateTime.UtcNow, Note = $"Assigned to {treatmentArea}", AuthorName = assignedByName
        });
        await _state.WriteStateAsync();
        await UpdateCasualtyIndexAsync();
    }

    public async Task UpdateDispositionAsync(string disposition, string updatedByName)
    {
        _state.State.Disposition = disposition;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        _state.State.Notes.Add(new MciCasualtyNote
        {
            Timestamp = DateTime.UtcNow, Note = $"Disposition: {disposition}", AuthorName = updatedByName
        });
        await _state.WriteStateAsync();
        await UpdateCasualtyIndexAsync();
    }

    public async Task LinkPatientAsync(string patientId, string patientName)
    {
        _state.State.PatientId = patientId;
        _state.State.PatientName = patientName;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
        await UpdateCasualtyIndexAsync();
    }

    public async Task AddNoteAsync(string note, string authorName)
    {
        _state.State.Notes.Add(new MciCasualtyNote
        {
            Timestamp = DateTime.UtcNow, Note = note, AuthorName = authorName
        });
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    private async Task UpdateCasualtyIndexAsync()
    {
        var index = GrainFactory.GetGrain<IMassCasualtyCasualtyIndexGrain>("MCI-CASUALTY-IDX");
        await index.AddOrUpdateAsync(new MassCasualtyCasualtyIndexEntry
        {
            CasualtyId = _state.State.CasualtyId, IncidentId = _state.State.IncidentId,
            TriageTag = _state.State.TriageTag, TriageCategory = _state.State.TriageCategory,
            PatientName = _state.State.PatientName, TreatmentArea = _state.State.TreatmentArea,
            Disposition = _state.State.Disposition, RegisteredDate = _state.State.RegisteredDate
        });
    }
}
