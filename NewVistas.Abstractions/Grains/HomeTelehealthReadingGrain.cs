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
/// Home Telehealth Reading Grain — a single remote patient monitoring measurement.
/// Grain key: "HT-READING:{guid}"
/// </summary>
public class HomeTelehealthReadingGrain : Grain, IHomeTelehealthReadingGrain
{
    private readonly IPersistentState<HomeTelehealthReadingState> _state;

    public HomeTelehealthReadingGrain(
        [PersistentState("htReadingState", "htReadingStore")] IPersistentState<HomeTelehealthReadingState> state)
    {
        _state = state;
    }

    public Task<HomeTelehealthReadingState> GetAsync() => Task.FromResult(_state.State);

    public async Task RecordAsync(
        string readingId,
        string patientId,
        HtMeasurementType measurementType,
        decimal? value1,
        decimal? value2,
        string unit,
        DateTime readingDateTime,
        HtReadingSource source,
        string? deviceId,
        string? notes)
    {
        _state.State.ReadingId = readingId;
        _state.State.PatientId = patientId;
        _state.State.MeasurementType = measurementType;
        _state.State.Value1 = value1;
        _state.State.Value2 = value2;
        _state.State.Unit = unit;
        _state.State.ReadingDateTime = readingDateTime;
        _state.State.Source = source;
        _state.State.DeviceId = deviceId;
        _state.State.Notes = notes;
        _state.State.CreatedDate = DateTime.UtcNow;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task SetAlertGeneratedAsync(string alertId)
    {
        _state.State.AlertGenerated = true;
        _state.State.AlertId = alertId;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task MarkReviewedAsync(string reviewedById, string reviewedByName)
    {
        _state.State.IsReviewed = true;
        _state.State.ReviewedById = reviewedById;
        _state.State.ReviewedByName = reviewedByName;
        _state.State.ReviewedDate = DateTime.UtcNow;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }
}
