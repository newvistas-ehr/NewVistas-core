// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;

namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// State for the PatientLabBatch grain.
/// Holds detailed lab results for a 6-month time partition.
/// Grain key format: PatientLabBatch/{patientIcn}/{year}-{half}
/// </summary>
[GenerateSerializer]
public class PatientLabBatchState
{
    /// <summary>
    /// All lab results in this time partition, ordered by date descending.
    /// </summary>
    [Id(0)]
    public List<LabResultDetail> Results { get; set; } = new();

    [Id(1)]
    public string PatientIcn { get; set; } = string.Empty;

    [Id(2)]
    public int Year { get; set; }

    [Id(3)]
    public LabHalfYear Half { get; set; }
}

[GenerateSerializer]
public enum LabHalfYear
{
    H1,
    H2
}

/// <summary>
/// Detailed lab result stored in a PatientLabBatch grain.
/// Maps to VistA LAB DATA file (#63) entries.
/// </summary>
[GenerateSerializer]
public class LabResultDetail
{
    /// <summary>
    /// Unique ID for this result.
    /// </summary>
    [Id(0)]
    public string ResultId { get; set; } = string.Empty;

    [Id(1)]
    public string LoincCode { get; set; } = string.Empty;

    [Id(2)]
    public string TestName { get; set; } = string.Empty;

    [Id(3)]
    public string Value { get; set; } = string.Empty;

    [Id(4)]
    public string Units { get; set; } = string.Empty;

    [Id(5)]
    public string ReferenceRange { get; set; } = string.Empty;

    [Id(6)]
    public LabAbnormalFlag AbnormalFlag { get; set; }

    [Id(7)]
    public DateTimeOffset ResultDate { get; set; }

    [Id(8)]
    public DateTimeOffset? CollectionDate { get; set; }

    [Id(9)]
    public string OrderingProvider { get; set; } = string.Empty;

    [Id(10)]
    public string FacilityCode { get; set; } = string.Empty;

    [Id(11)]
    public string FacilityName { get; set; } = string.Empty;

    /// <summary>
    /// Specimen type (e.g., "Blood", "Urine", "Serum").
    /// Maps to VistA TOPOGRAPHY FIELD (File 61).
    /// </summary>
    [Id(12)]
    public string Specimen { get; set; } = string.Empty;

    /// <summary>
    /// Panel/battery this result belongs to, if any (e.g., "CMP", "CBC").
    /// </summary>
    [Id(13)]
    public string? PanelName { get; set; }

    /// <summary>
    /// Free-text comment from the lab or ordering provider.
    /// </summary>
    [Id(14)]
    public string? Comment { get; set; }
}
