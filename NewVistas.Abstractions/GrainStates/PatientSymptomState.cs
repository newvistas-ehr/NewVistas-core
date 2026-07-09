// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;

namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// Answer to a coded symptom question. Trinary on purpose: <see cref="Absent"/> and
/// <see cref="Unknown"/> are DIFFERENT — "we asked and it wasn't there" vs "we never asked".
/// Only structured capture can tell them apart, and the whole net-closing method depends on it.
/// </summary>
[GenerateSerializer]
public enum SymptomPresence
{
    /// <summary>Not assessed — the question was never asked (the "go ask" driver).</summary>
    Unknown = 0,
    /// <summary>Present — the patient has the symptom.</summary>
    Present = 1,
    /// <summary>Absent — asked and denied (a real, informative negative).</summary>
    Absent = 2
}

/// <summary>Where a symptom observation came from. Extensible — later phases add prefill sources.</summary>
[GenerateSerializer]
public enum SymptomSource
{
    /// <summary>Entered on the structured symptom survey.</summary>
    Survey = 0,
    /// <summary>Imported by an epidemiologist from an existing chart review.</summary>
    ChartReview = 1,
    /// <summary>Routed from a structured nursing assessment field (phase 2).</summary>
    NursingAssessment = 2,
    /// <summary>Proposed by AI extraction from H&amp;P / notes, then human-confirmed (phase 2).</summary>
    AiExtraction = 3
}

/// <summary>
/// A single coded symptom observation. Immutable once recorded; a change of answer is a NEW
/// observation appended to history (onset timing and progression are themselves signal).
/// </summary>
[GenerateSerializer]
public record SymptomObservation
{
    /// <summary>SNOMED code from <c>SymptomCatalog</c> (closed vocabulary).</summary>
    [Id(0)] public string Code { get; init; } = string.Empty;
    /// <summary>Denormalized display name at record time (for cheap reads).</summary>
    [Id(1)] public string Display { get; init; } = string.Empty;
    /// <summary>Present / Absent / Unknown.</summary>
    [Id(2)] public SymptomPresence Presence { get; init; }
    /// <summary>When the symptom began, if known (the temporal signal).</summary>
    [Id(3)] public DateTime? OnsetDate { get; init; }
    /// <summary>Optional free-text severity/qualifier (e.g. "mild", "worsening").</summary>
    [Id(4)] public string? Severity { get; init; }
    /// <summary>Capture source.</summary>
    [Id(5)] public SymptomSource Source { get; init; }
    /// <summary>User id who recorded it.</summary>
    [Id(6)] public string RecordedBy { get; init; } = string.Empty;
    /// <summary>Server timestamp of the record.</summary>
    [Id(7)] public DateTime RecordedDate { get; init; }
}

/// <summary>
/// Per-patient coded symptom record. Grain key: <c>SYMPTOMS:{patientId}</c>.
///
/// Append-only <see cref="History"/> (audit-free, keeps onset/progression) plus a
/// latest-per-code projection (<see cref="Latest"/>) for O(1) "what is this patient's current
/// answer for symptom X" reads used by the proto matcher. There is no coded symptom capture
/// anywhere else in the system — this is the surveillance input surface.
/// </summary>
[GenerateSerializer]
public class PatientSymptomState
{
    /// <summary>The patient this record belongs to (grain key suffix).</summary>
    [Id(0)] public string PatientId { get; set; } = string.Empty;

    /// <summary>Every observation ever recorded, in record order (append-only).</summary>
    [Id(1)] public List<SymptomObservation> History { get; set; } = new();

    /// <summary>Latest observation per symptom code — the current answer projection.</summary>
    [Id(2)] public Dictionary<string, SymptomObservation> Latest { get; set; } = new();

    /// <summary>Last write timestamp.</summary>
    [Id(3)] public DateTime LastModifiedDate { get; set; }
}
