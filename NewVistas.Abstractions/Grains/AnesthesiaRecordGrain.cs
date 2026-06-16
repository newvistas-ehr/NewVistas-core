// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;
using Orleans.Runtime;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

public class AnesthesiaRecordGrain : Grain, IAnesthesiaRecordGrain
{
    private readonly IPersistentState<AnesthesiaRecordState> _state;

    public AnesthesiaRecordGrain(
        [PersistentState("anesthesiaRecordState", "anesthesiaRecordStore")]
        IPersistentState<AnesthesiaRecordState> state) { _state = state; }

    public Task<AnesthesiaRecordState> GetRecordAsync() => Task.FromResult(_state.State);

    public async Task<AnesthesiaRecordState> CreateRecordAsync(
        string patientId, string patientName, string surgeryId, string procedureName,
        string anesthesiaType, string anesthesiologistId, string anesthesiologistName,
        string asaClassification, string? airwayClass, string? preOpNotes)
    {
        _state.State.RecordId = this.GetPrimaryKeyString();
        _state.State.PatientId = patientId;
        _state.State.PatientName = patientName;
        _state.State.SurgeryId = surgeryId;
        _state.State.ProcedureName = procedureName;
        _state.State.AnesthesiaType = anesthesiaType;
        _state.State.AnesthesiologistId = anesthesiologistId;
        _state.State.AnesthesiologistName = anesthesiologistName;
        _state.State.AsaClassification = asaClassification;
        _state.State.AirwayClass = airwayClass;
        _state.State.PreOpNotes = preOpNotes;
        _state.State.Status = "DRAFT";
        _state.State.CreatedDate = DateTime.UtcNow;
        _state.State.LastModifiedDate = DateTime.UtcNow;

        await _state.WriteStateAsync();
        await UpdateIndexAsync();
        return _state.State;
    }

    public async Task AddAgentAsync(AnesthesiaAgent agent)
    {
        if (_state.State.Status == "FINALIZED")
            throw new InvalidOperationException("Cannot modify a finalized record.");

        _state.State.Agents.Add(agent);
        if (_state.State.Status == "DRAFT") _state.State.Status = "IN_PROGRESS";
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
        await UpdateIndexAsync();
    }

    public async Task RecordAirwayManagementAsync(string airwayDevice, string? airwaySize, string? airwayNotes, string performedByName)
    {
        if (_state.State.Status == "FINALIZED")
            throw new InvalidOperationException("Cannot modify a finalized record.");

        _state.State.AirwayDevice = airwayDevice;
        _state.State.AirwaySize = airwaySize;
        _state.State.AirwayNotes = airwayNotes;
        if (_state.State.Status == "DRAFT") _state.State.Status = "IN_PROGRESS";
        _state.State.LastModifiedDate = DateTime.UtcNow;

        _state.State.Events.Add(new AnesthesiaEvent
        {
            Timestamp = DateTime.UtcNow, EventType = "INTUBATION",
            Description = $"Airway: {airwayDevice} {airwaySize ?? ""}".Trim(),
            RecordedByName = performedByName
        });

        await _state.WriteStateAsync();
    }

    public async Task RecordVitalsAsync(AnesthesiaVitalEntry vitals)
    {
        if (_state.State.Status == "FINALIZED")
            throw new InvalidOperationException("Cannot modify a finalized record.");

        _state.State.VitalEntries.Add(vitals);
        if (_state.State.Status == "DRAFT") _state.State.Status = "IN_PROGRESS";
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task RecordEventAsync(string eventType, string description, string recordedByName)
    {
        if (_state.State.Status == "FINALIZED")
            throw new InvalidOperationException("Cannot modify a finalized record.");

        _state.State.Events.Add(new AnesthesiaEvent
        {
            Timestamp = DateTime.UtcNow, EventType = eventType,
            Description = description, RecordedByName = recordedByName
        });
        if (_state.State.Status == "DRAFT") _state.State.Status = "IN_PROGRESS";
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task RecordInductionAsync(DateTime inductionTime, string inductionMethod, string performedByName)
    {
        _state.State.InductionTime = inductionTime;
        _state.State.InductionMethod = inductionMethod;
        if (_state.State.Status == "DRAFT") _state.State.Status = "IN_PROGRESS";
        _state.State.LastModifiedDate = DateTime.UtcNow;

        _state.State.Events.Add(new AnesthesiaEvent
        {
            Timestamp = inductionTime, EventType = "NOTE",
            Description = $"Induction: {inductionMethod}", RecordedByName = performedByName
        });

        await _state.WriteStateAsync();
    }

    public async Task RecordEmergenceAsync(DateTime emergenceTime, string? emergenceNotes, string performedByName)
    {
        _state.State.EmergenceTime = emergenceTime;
        _state.State.EmergenceNotes = emergenceNotes;
        _state.State.LastModifiedDate = DateTime.UtcNow;

        _state.State.Events.Add(new AnesthesiaEvent
        {
            Timestamp = emergenceTime, EventType = "EXTUBATION",
            Description = $"Emergence{(emergenceNotes != null ? $": {emergenceNotes}" : "")}",
            RecordedByName = performedByName
        });

        await _state.WriteStateAsync();
    }

    public async Task RecordPacuHandoffAsync(string pacuNurse, int aldretScore, string? handoffNotes)
    {
        _state.State.PacuNurse = pacuNurse;
        _state.State.AldretScore = aldretScore;
        _state.State.PacuHandoffNotes = handoffNotes;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task FinalizeRecordAsync(string finalizedByName)
    {
        _state.State.Status = "FINALIZED";
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
        await UpdateIndexAsync();
    }

    public async Task AddendRecordAsync(string addendumNote, string addendedByName)
    {
        if (_state.State.Status != "FINALIZED" && _state.State.Status != "ADDENDED")
            throw new InvalidOperationException("Only finalized records can be addended.");

        _state.State.Status = "ADDENDED";
        _state.State.AddendumNotes = string.IsNullOrEmpty(_state.State.AddendumNotes)
            ? $"[{DateTime.UtcNow:g} {addendedByName}] {addendumNote}"
            : $"{_state.State.AddendumNotes}\n[{DateTime.UtcNow:g} {addendedByName}] {addendumNote}";
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
        await UpdateIndexAsync();
    }

    private async Task UpdateIndexAsync()
    {
        var index = GrainFactory.GetGrain<IAnesthesiaRecordIndexGrain>("ANES-IDX");
        await index.AddOrUpdateAsync(new AnesthesiaRecordIndexEntry
        {
            RecordId = _state.State.RecordId, PatientId = _state.State.PatientId,
            PatientName = _state.State.PatientName, ProcedureName = _state.State.ProcedureName,
            AnesthesiaType = _state.State.AnesthesiaType,
            AnesthesiologistName = _state.State.AnesthesiologistName,
            AnesthesiologistId = _state.State.AnesthesiologistId,
            AsaClassification = _state.State.AsaClassification,
            Status = _state.State.Status, AgentCount = _state.State.Agents.Count,
            CreatedDate = _state.State.CreatedDate
        });
    }
}
