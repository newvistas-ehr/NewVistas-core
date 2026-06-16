// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;

namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// State for a periodontal charting assessment.
/// Maps to IHS RPMS DENT periodontal charting and VistA Dental Record Manager (File #220).
/// Captures full-mouth 6-point probing depths and clinical indicators per tooth.
/// Universal numbering system (1-32 for permanent teeth).
/// </summary>
[GenerateSerializer]
public class PeriodontalChartState
{
    /// <summary>Unique chart ID (grain key, e.g., "PERIO:{guid}").</summary>
    [Id(0)]
    public string ChartId { get; set; } = string.Empty;

    /// <summary>Patient this chart belongs to.</summary>
    [Id(1)]
    public string PatientId { get; set; } = string.Empty;

    /// <summary>Patient name for display.</summary>
    [Id(2)]
    public string PatientName { get; set; } = string.Empty;

    /// <summary>Examining provider ID.</summary>
    [Id(3)]
    public string ProviderId { get; set; } = string.Empty;

    /// <summary>Examining provider name.</summary>
    [Id(4)]
    public string ProviderName { get; set; } = string.Empty;

    /// <summary>Status: DRAFT, FINALIZED, ADDENDED.</summary>
    [Id(5)]
    public string Status { get; set; } = "DRAFT";

    /// <summary>Per-tooth data keyed by tooth number (1-32).</summary>
    [Id(6)]
    public Dictionary<int, PeriodontalToothData> TeethData { get; set; } = new();

    /// <summary>Missing teeth with reasons.</summary>
    [Id(7)]
    public Dictionary<int, string> MissingTeeth { get; set; } = new();

    /// <summary>AAP/EFP classification: HEALTHY, GINGIVITIS, STAGE_I, STAGE_II, STAGE_III, STAGE_IV.</summary>
    [Id(8)]
    public string? Classification { get; set; }

    /// <summary>Treatment plan based on assessment.</summary>
    [Id(9)]
    public string? TreatmentPlan { get; set; }

    /// <summary>General notes.</summary>
    [Id(10)]
    public string? Notes { get; set; }

    /// <summary>Addendum notes after finalization.</summary>
    [Id(11)]
    public string? AddendumNotes { get; set; }

    /// <summary>Total teeth charted.</summary>
    [Id(12)]
    public int TeethCharted { get; set; }

    /// <summary>Count of sites with probing depth >= 4mm.</summary>
    [Id(13)]
    public int DeepPocketCount { get; set; }

    /// <summary>Count of sites with bleeding on probing.</summary>
    [Id(14)]
    public int BleedingSiteCount { get; set; }

    [Id(15)]
    public DateTime ExamDate { get; set; } = DateTime.UtcNow;

    [Id(16)]
    public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Periodontal data for a single tooth — 6-point probing depths and clinical indicators.
/// Sites: MB (mesiobuccal), B (buccal), DB (distobuccal), ML (mesiolingual), L (lingual), DL (distolingual).
/// </summary>
[GenerateSerializer]
public class PeriodontalToothData
{
    /// <summary>6-point probing depths in mm (MB, B, DB, ML, L, DL).</summary>
    [Id(0)]
    public int[] ProbingDepths { get; set; } = new int[6];

    /// <summary>6-point recession measurements in mm (negative = hyperplasia).</summary>
    [Id(1)]
    public int[] Recession { get; set; } = new int[6];

    /// <summary>6-point bleeding on probing (true = bleeding at that site).</summary>
    [Id(2)]
    public bool[] BleedingOnProbing { get; set; } = new bool[6];

    /// <summary>Furcation involvement: NONE, CLASS_I, CLASS_II, CLASS_III.</summary>
    [Id(3)]
    public string Furcation { get; set; } = "NONE";

    /// <summary>Mobility grade: 0, 1, 2, 3.</summary>
    [Id(4)]
    public int Mobility { get; set; }

    /// <summary>Plaque present on this tooth.</summary>
    [Id(5)]
    public bool PlaquePresent { get; set; }

    /// <summary>Calculus present on this tooth.</summary>
    [Id(6)]
    public bool CalculusPresent { get; set; }

    /// <summary>Suppuration (pus) present.</summary>
    [Id(7)]
    public bool Suppuration { get; set; }
}

/// <summary>
/// Helper for recording multiple teeth at once.
/// </summary>
[GenerateSerializer]
public class PeriodontalToothEntry
{
    [Id(0)]
    public int ToothNumber { get; set; }

    [Id(1)]
    public PeriodontalToothData Data { get; set; } = new();
}

/// <summary>
/// Index entry for periodontal charts.
/// </summary>
[GenerateSerializer]
public class PeriodontalChartIndexEntry
{
    [Id(0)]
    public string ChartId { get; set; } = string.Empty;

    [Id(1)]
    public string PatientId { get; set; } = string.Empty;

    [Id(2)]
    public string PatientName { get; set; } = string.Empty;

    [Id(3)]
    public string ProviderId { get; set; } = string.Empty;

    [Id(4)]
    public string ProviderName { get; set; } = string.Empty;

    [Id(5)]
    public string Status { get; set; } = string.Empty;

    [Id(6)]
    public string? Classification { get; set; }

    [Id(7)]
    public int TeethCharted { get; set; }

    [Id(8)]
    public int DeepPocketCount { get; set; }

    [Id(9)]
    public int BleedingSiteCount { get; set; }

    [Id(10)]
    public DateTime ExamDate { get; set; }
}

[GenerateSerializer]
public class PeriodontalChartIndexState
{
    [Id(0)]
    public Dictionary<string, PeriodontalChartIndexEntry> Entries { get; set; } = new();
}
