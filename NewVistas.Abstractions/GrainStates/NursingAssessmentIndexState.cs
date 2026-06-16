// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// Per-patient index of nursing assessments — VistA File #212.1.
/// Maintained in newest-first order.
/// </summary>
[GenerateSerializer]
public class NursingAssessmentIndexState
{
    /// <summary>Patient this index belongs to.</summary>
    [Id(0)] public string PatientId { get; set; } = string.Empty;

    /// <summary>Assessment summaries, newest first.</summary>
    [Id(1)] public List<NursingAssessmentIndexEntry> Assessments { get; set; } = new();
}

/// <summary>Summary entry stored in the per-patient assessment index.</summary>
[GenerateSerializer]
public class NursingAssessmentIndexEntry
{
    [Id(0)] public string AssessmentId { get; set; } = string.Empty;
    [Id(1)] public DateTime AssessmentDateTime { get; set; }

    /// <summary>Assessment type string (Initial, Shift, Focused, Discharge, PRN).</summary>
    [Id(2)] public string AssessmentType { get; set; } = string.Empty;

    [Id(3)] public string NurseId { get; set; } = string.Empty;
    [Id(4)] public string NurseName { get; set; } = string.Empty;
    [Id(5)] public NursingAssessmentStatus Status { get; set; }
    [Id(6)] public string? LocationName { get; set; }

    /// <summary>Pain score (0–10) for quick reference.</summary>
    [Id(7)] public int? PainScore { get; set; }

    /// <summary>Morse Fall Scale score for quick reference.</summary>
    [Id(8)] public int? MorseScore { get; set; }

    /// <summary>Braden Scale score for quick reference.</summary>
    [Id(9)] public int? BradenScore { get; set; }
}
