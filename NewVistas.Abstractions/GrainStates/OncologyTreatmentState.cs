// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;

namespace NewVistas.Abstractions.GrainStates;

/// <summary>Type of oncology treatment modality.</summary>
[GenerateSerializer]
public enum OncologyTreatmentType
{
    Surgery = 0,
    Radiation = 1,
    Chemotherapy = 2,
    Immunotherapy = 3,
    HormoneTherapy = 4,
    TargetedTherapy = 5,
    Observation = 6,
    BestSupportiveCare = 7,
    Other = 8
}

/// <summary>Current lifecycle status of a treatment episode.</summary>
[GenerateSerializer]
public enum OncologyTreatmentStatus
{
    Planned = 0,
    Active = 1,
    Completed = 2,
    Discontinued = 3
}

/// <summary>Tumor response assessment per RECIST 1.1 / standard oncology criteria.</summary>
[GenerateSerializer]
public enum TreatmentResponseAssessment
{
    NotAssessed = 0,
    CompleteResponse = 1,
    PartialResponse = 2,
    StableDisease = 3,
    ProgressiveDisease = 4,
    NotEvaluable = 5
}

/// <summary>
/// State for an individual oncology treatment episode.
/// Maps to VistA Oncology Treatment file (#165.x).
/// MUMPS routine: ONCTREAT.m
/// Grain key pattern: "ONC-TX:{guid}"
/// </summary>
[GenerateSerializer]
public class OncologyTreatmentState
{
    /// <summary>Unique treatment identifier (grain key). (.01)</summary>
    [Id(0)] public string TreatmentId { get; set; } = string.Empty;

    /// <summary>Parent tumor identifier. (.02)</summary>
    [Id(1)] public string TumorId { get; set; } = string.Empty;

    /// <summary>Patient identifier. (.03)</summary>
    [Id(2)] public string PatientId { get; set; } = string.Empty;

    /// <summary>Treatment modality. (.04)</summary>
    [Id(3)] public OncologyTreatmentType TreatmentType { get; set; } = OncologyTreatmentType.Chemotherapy;

    /// <summary>Name of drug, regimen, or protocol (e.g. "FOLFOX", "Pembrolizumab"). (.05)</summary>
    [Id(4)] public string AgentName { get; set; } = string.Empty;

    /// <summary>Treatment start date. (.06)</summary>
    [Id(5)] public DateTime? StartDate { get; set; }

    /// <summary>Treatment end date (completed or discontinued). (.07)</summary>
    [Id(6)] public DateTime? EndDate { get; set; }

    /// <summary>Current treatment status. (.08)</summary>
    [Id(7)] public OncologyTreatmentStatus Status { get; set; } = OncologyTreatmentStatus.Planned;

    /// <summary>Number of cycles completed (chemotherapy/immunotherapy). (.09)</summary>
    [Id(8)] public int? CyclesCompleted { get; set; }

    /// <summary>Dose/schedule description (e.g. "175 mg/m² q21d"). (.10)</summary>
    [Id(9)] public string? DoseDescription { get; set; }

    /// <summary>Ordering/primary oncology provider ID. (.11)</summary>
    [Id(10)] public string? ProviderId { get; set; }

    /// <summary>Ordering/primary oncology provider name. (.12)</summary>
    [Id(11)] public string? ProviderName { get; set; }

    /// <summary>Facility where treatment is administered. (.13)</summary>
    [Id(12)] public string? FacilityName { get; set; }

    /// <summary>Tumor response assessment at end or latest review. (.14)</summary>
    [Id(13)] public TreatmentResponseAssessment ResponseAssessment { get; set; } = TreatmentResponseAssessment.NotAssessed;

    /// <summary>Date of the most recent response assessment. (.15)</summary>
    [Id(14)] public DateTime? ResponseAssessmentDate { get; set; }

    /// <summary>Reason treatment was discontinued early. (.16)</summary>
    [Id(15)] public string? DiscontinuationReason { get; set; }

    /// <summary>Free-text clinical notes. (.20)</summary>
    [Id(16)] public string? Notes { get; set; }

    /// <summary>Record creation date. (.90)</summary>
    [Id(17)] public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    /// <summary>Last modification date. (.91)</summary>
    [Id(18)] public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;
}

/// <summary>Summary entry stored in the per-patient treatment index.</summary>
[GenerateSerializer]
public class OncologyTreatmentIndexEntry
{
    [Id(0)] public string TreatmentId { get; set; } = string.Empty;
    [Id(1)] public string TumorId { get; set; } = string.Empty;
    [Id(2)] public OncologyTreatmentType TreatmentType { get; set; }
    [Id(3)] public string AgentName { get; set; } = string.Empty;
    [Id(4)] public DateTime? StartDate { get; set; }
    [Id(5)] public DateTime? EndDate { get; set; }
    [Id(6)] public OncologyTreatmentStatus Status { get; set; }
    [Id(7)] public TreatmentResponseAssessment ResponseAssessment { get; set; }
    [Id(8)] public string? ProviderName { get; set; }
}
