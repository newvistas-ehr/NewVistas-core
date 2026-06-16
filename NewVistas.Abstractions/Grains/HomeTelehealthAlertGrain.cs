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
/// Home Telehealth Alert Grain — an alert generated from an out-of-range reading.
/// Grain key: "HT-ALERT:{guid}"
/// </summary>
public class HomeTelehealthAlertGrain : Grain, IHomeTelehealthAlertGrain
{
    private readonly IPersistentState<HomeTelehealthAlertState> _state;

    public HomeTelehealthAlertGrain(
        [PersistentState("htAlertState", "htAlertStore")] IPersistentState<HomeTelehealthAlertState> state)
    {
        _state = state;
    }

    public Task<HomeTelehealthAlertState> GetAsync() => Task.FromResult(_state.State);

    public async Task CreateAsync(
        string alertId,
        string patientId,
        string readingId,
        HtMeasurementType measurementType,
        decimal? value1,
        decimal? value2,
        HtAlertSeverity severity,
        string alertDescription)
    {
        _state.State.AlertId = alertId;
        _state.State.PatientId = patientId;
        _state.State.ReadingId = readingId;
        _state.State.MeasurementType = measurementType;
        _state.State.Value1 = value1;
        _state.State.Value2 = value2;
        _state.State.Severity = severity;
        _state.State.AlertDescription = alertDescription;
        _state.State.Status = HtAlertStatus.Active;
        _state.State.AlertDateTime = DateTime.UtcNow;
        _state.State.CreatedDate = DateTime.UtcNow;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task AcknowledgeAsync(string clinicianId, string clinicianName, string? clinicalResponse)
    {
        _state.State.Status = HtAlertStatus.Acknowledged;
        _state.State.AcknowledgedById = clinicianId;
        _state.State.AcknowledgedByName = clinicianName;
        _state.State.AcknowledgedDate = DateTime.UtcNow;
        _state.State.ClinicalResponse = clinicalResponse;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task ResolveAsync(string clinicianId, string clinicianName, string? clinicalResponse)
    {
        _state.State.Status = HtAlertStatus.Resolved;
        _state.State.AcknowledgedById ??= clinicianId;
        _state.State.AcknowledgedByName ??= clinicianName;
        _state.State.AcknowledgedDate ??= DateTime.UtcNow;
        _state.State.ClinicalResponse = clinicalResponse;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task DismissAsync(string clinicianId, string clinicianName, string? clinicalResponse)
    {
        _state.State.Status = HtAlertStatus.Dismissed;
        _state.State.AcknowledgedById ??= clinicianId;
        _state.State.AcknowledgedByName ??= clinicianName;
        _state.State.AcknowledgedDate ??= DateTime.UtcNow;
        _state.State.ClinicalResponse = clinicalResponse;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }
}
