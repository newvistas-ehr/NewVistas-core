// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;

namespace NewVistas.Abstractions.GrainStates;

/// <summary>A latest lab result in a feature snapshot (LOINC-keyed).</summary>
[GenerateSerializer]
public record SnapshotLab
{
    [Id(0)] public string Loinc { get; set; } = string.Empty;
    /// <summary>Raw result value as charted (may be non-numeric, e.g. "Negative").</summary>
    [Id(1)] public string Value { get; set; } = string.Empty;
    [Id(2)] public DateTime? ResultedDate { get; set; }

    /// <summary>
    /// The lab's own abnormal flag, carried through from <see cref="LabTestSummaryEntry"/>.
    ///
    /// Previously dropped when building the snapshot, which meant a cluster definition could
    /// not say "abnormal, whatever the number" — it had to name a threshold per analyte. That
    /// is why the seeded cluster describes itself as "flu-negative" in free prose: the model
    /// could not express it.
    /// </summary>
    [Id(3)] public LabAbnormalFlag AbnormalFlag { get; set; }
}

/// <summary>A latest vital in a feature snapshot; BP is pre-split into SYS/DIA rows.</summary>
[GenerateSerializer]
public record SnapshotVital
{
    /// <summary>Vital type key: "SPO2", "TEMP", "HR", "RR", "BP_SYS", "BP_DIA", "WT", "HT".</summary>
    [Id(0)] public string Type { get; set; } = string.Empty;
    /// <summary>Parsed numeric value, or null if the charted value did not parse.</summary>
    [Id(1)] public double? Numeric { get; set; }
    /// <summary>Raw charted value (evidence text, never guessed).</summary>
    [Id(2)] public string Raw { get; set; } = string.Empty;
    [Id(3)] public DateTime? Measured { get; set; }
}

/// <summary>
/// A read-model snapshot of the patient signals the proto matcher needs, assembled once by the
/// screening worker and evaluated (purely) against any number of proto-conditions. Keeping the
/// matcher's input a plain data bag is what makes the matcher deterministic and unit-testable.
/// </summary>
[GenerateSerializer]
public record PatientFeatureSnapshot
{
    [Id(0)] public string PatientId { get; set; } = string.Empty;

    /// <summary>Active problem-list ICD-10 codes (normalized upper-case).</summary>
    [Id(1)] public List<string> Problems { get; set; } = new();

    /// <summary>Latest lab per LOINC.</summary>
    [Id(2)] public List<SnapshotLab> Labs { get; set; } = new();

    /// <summary>Latest coded symptom answer per code (Present / Absent / Unknown).</summary>
    [Id(3)] public Dictionary<string, SymptomPresence> Symptoms { get; set; } = new();

    /// <summary>Latest vitals (BP already split).</summary>
    [Id(4)] public List<SnapshotVital> Vitals { get; set; } = new();

    // ── Demographics ────────────────────────────────────────────────────
    [Id(5)] public int? Age { get; set; }
    [Id(6)] public string? Sex { get; set; }
    [Id(7)] public string? City { get; set; }
    [Id(8)] public string? Race { get; set; }

    /// <summary>Facilities the patient has been treated at (exposure signal).</summary>
    [Id(9)] public List<string> Facilities { get; set; } = new();

    /// <summary>When the snapshot was assembled (recency reference point).</summary>
    [Id(10)] public DateTime AssembledAt { get; set; }
}
