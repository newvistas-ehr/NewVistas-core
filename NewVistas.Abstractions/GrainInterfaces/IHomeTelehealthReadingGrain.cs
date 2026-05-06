// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Home Telehealth Reading Grain — a single remote patient monitoring measurement.
/// Based on VistA HOME TELEHEALTH MEASUREMENT file (#720.5).
/// Grain key: "HT-READING:{guid}"
/// MUMPS routines: HTMONREC.m, HTMEASUR.m
/// </summary>
public interface IHomeTelehealthReadingGrain : IGrainWithStringKey
{
    /// <summary>Returns the full reading record.</summary>
    Task<HomeTelehealthReadingState> GetAsync();

    /// <summary>
    /// Records a new physiological measurement.
    /// Corresponds to VistA HTMEASUR CREATE.
    /// </summary>
    Task RecordAsync(
        string readingId,
        string patientId,
        HtMeasurementType measurementType,
        decimal? value1,
        decimal? value2,
        string unit,
        DateTime readingDateTime,
        HtReadingSource source,
        string? deviceId,
        string? notes);

    /// <summary>
    /// Marks this reading as having generated an alert.
    /// </summary>
    Task SetAlertGeneratedAsync(string alertId);

    /// <summary>
    /// Records clinician review of this reading.
    /// Corresponds to VistA HTMONREC REVIEW.
    /// </summary>
    Task MarkReviewedAsync(string reviewedById, string reviewedByName);
}
