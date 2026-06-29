// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// The kind of home-care assessment. Phase 1 records <see cref="ComprehensiveHbpc"/>; the
/// reserved <c>Oasis*</c> types activate Medicare OASIS capture in Phase 2 — the same grain,
/// the same time-point model, with the OASIS item set in <see cref="HomeCareAssessmentState.Oasis"/>.
/// </summary>
public enum HomeCareAssessmentType
{
    /// <summary>HBPC interdisciplinary comprehensive assessment.</summary>
    ComprehensiveHbpc,
    /// <summary>Reserved (Phase 2): OASIS at Start of Care.</summary>
    OasisStartOfCare,
    /// <summary>Reserved (Phase 2): OASIS at Resumption of Care.</summary>
    OasisResumption,
    /// <summary>Reserved (Phase 2): OASIS at Recertification (follow-up).</summary>
    OasisRecertification,
    /// <summary>Reserved (Phase 2): OASIS at Transfer to inpatient.</summary>
    OasisTransfer,
    /// <summary>Reserved (Phase 2): OASIS at Discharge.</summary>
    OasisDischarge
}

/// <summary>
/// The HBPC interdisciplinary comprehensive assessment — a structured, lighter-weight relative of
/// OASIS that captures the whole-patient picture HBPC manages over time.
/// </summary>
[GenerateSerializer]
public class HbpcComprehensiveAssessment
{
    /// <summary>Functional status / ADL summary (e.g. bathing, dressing, transferring, toileting).</summary>
    [Id(0)] public string FunctionalStatus { get; set; } = string.Empty;
    /// <summary>Instrumental ADLs (meds, meals, finances, transportation).</summary>
    [Id(1)] public string InstrumentalAdls { get; set; } = string.Empty;
    /// <summary>Home-safety / environment findings (fall hazards, accessibility).</summary>
    [Id(2)] public string HomeSafety { get; set; } = string.Empty;
    /// <summary>Caregiver / social support situation.</summary>
    [Id(3)] public string CaregiverSupport { get; set; } = string.Empty;
    /// <summary>Cognitive &amp; mental-status findings.</summary>
    [Id(4)] public string CognitiveMentalStatus { get; set; } = string.Empty;
    /// <summary>Nutrition / weight findings.</summary>
    [Id(5)] public string Nutrition { get; set; } = string.Empty;
    /// <summary>Medication-reconciliation summary.</summary>
    [Id(6)] public string MedicationReconciliation { get; set; } = string.Empty;
    /// <summary>Fall-risk assessment summary.</summary>
    [Id(7)] public string FallRisk { get; set; } = string.Empty;
    /// <summary>Overall narrative / assessment summary.</summary>
    [Id(8)] public string Summary { get; set; } = string.Empty;
}

/// <summary>
/// Reserved (Phase 2): the OASIS-E data set, captured at OASIS time points to drive Medicare
/// payment (PDGM functional level) and quality reporting. Modeled as item code → value so the
/// large, versioned OASIS item set can evolve (OASIS-E1, OASIS-E2…) without state-shape churn.
/// Null in Phase 1.
/// </summary>
[GenerateSerializer]
public class OasisDataSet
{
    /// <summary>OASIS version (e.g. "OASIS-E1", "OASIS-E2").</summary>
    [Id(0)] public string Version { get; set; } = string.Empty;
    /// <summary>OASIS item code (e.g. "M1830") → recorded value.</summary>
    [Id(1)] public Dictionary<string, string> Items { get; set; } = new();
    /// <summary>Whether the data set passed validation ("scrubbing") before submission.</summary>
    [Id(2)] public bool Validated { get; set; }
}

/// <summary>
/// A home-care comprehensive assessment (HBPC) or OASIS assessment (reserved Phase 2), recorded
/// at a defined time point.
/// Key pattern: "HHC-ASSESS:{guid}". VistA File #750 assessment; (Phase 2) OASIS data set.
/// </summary>
[GenerateSerializer]
public class HomeCareAssessmentState
{
    [Id(0)] public string AssessmentId { get; set; } = string.Empty;
    [Id(1)] public string EpisodeId { get; set; } = string.Empty;
    [Id(2)] public string PatientId { get; set; } = string.Empty;

    [Id(3)] public HomeCareAssessmentType AssessmentType { get; set; } = HomeCareAssessmentType.ComprehensiveHbpc;
    [Id(4)] public string AssessorId { get; set; } = string.Empty;
    [Id(5)] public string AssessorName { get; set; } = string.Empty;
    [Id(6)] public DateTime AssessmentDate { get; set; }

    /// <summary>The HBPC comprehensive assessment payload (Phase 1).</summary>
    [Id(7)] public HbpcComprehensiveAssessment Comprehensive { get; set; } = new();

    [Id(8)] public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    [Id(9)] public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;

    /// <summary>Reserved (Phase 2): the OASIS data set for OASIS-typed assessments. Null in Phase 1.</summary>
    [Id(10)] public OasisDataSet? Oasis { get; set; }
}
