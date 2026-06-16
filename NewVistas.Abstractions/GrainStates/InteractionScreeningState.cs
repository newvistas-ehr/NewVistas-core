// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// Status of an interaction screening for a prescription.
/// Controls whether the prescription may proceed to fill/refill/verify.
/// </summary>
[GenerateSerializer]
public enum InteractionScreeningStatus
{
    /// <summary>Screening not yet performed — prescription cannot be filled.</summary>
    NotScreened = 0,

    /// <summary>Screening completed with no blocking interactions — prescription may proceed.</summary>
    Cleared = 1,

    /// <summary>Blocking interactions found — prescription fill is blocked until override.</summary>
    BlockedPendingOverride = 2,

    /// <summary>All blocking interactions overridden by pharmacist — prescription may proceed.</summary>
    OverriddenByPharmacist = 3
}

/// <summary>
/// A single drug-drug interaction finding from a screening.
/// Captures the interacting pair, severity, and optional pharmacist override.
/// </summary>
[GenerateSerializer]
public class InteractionFinding
{
    /// <summary>Name of the first drug (the new prescription being screened).</summary>
    [Id(0)]
    public string Drug1Name { get; set; } = string.Empty;

    /// <summary>Ingredient IEN of the first drug.</summary>
    [Id(1)]
    public string Drug1IngredientIen { get; set; } = string.Empty;

    /// <summary>Ingredient name of the first drug.</summary>
    [Id(2)]
    public string Drug1IngredientName { get; set; } = string.Empty;

    /// <summary>Name of the second drug (existing medication).</summary>
    [Id(3)]
    public string Drug2Name { get; set; } = string.Empty;

    /// <summary>Ingredient IEN of the second drug.</summary>
    [Id(4)]
    public string Drug2IngredientIen { get; set; } = string.Empty;

    /// <summary>Ingredient name of the second drug.</summary>
    [Id(5)]
    public string Drug2IngredientName { get; set; } = string.Empty;

    /// <summary>Clinical severity of the interaction (from VistA File #56).</summary>
    [Id(6)]
    public InteractionSeverity Severity { get; set; }

    /// <summary>Clinical description of the interaction.</summary>
    [Id(7)]
    public string Description { get; set; } = string.Empty;

    /// <summary>Clinical effects if known.</summary>
    [Id(8)]
    public string? ClinicalEffects { get; set; }

    /// <summary>Pharmacodynamic/pharmacokinetic mechanism.</summary>
    [Id(9)]
    public string? Mechanism { get; set; }

    /// <summary>Clinical management guidance.</summary>
    [Id(10)]
    public string? Management { get; set; }

    /// <summary>Whether this interaction blocks the fill (Significant or Contraindicated).</summary>
    [Id(11)]
    public bool IsBlocking { get; set; }

    /// <summary>Whether a pharmacist has overridden this blocking interaction.</summary>
    [Id(12)]
    public bool IsOverridden { get; set; }

    /// <summary>Pharmacist who overrode this interaction.</summary>
    [Id(13)]
    public string? OverriddenBy { get; set; }

    /// <summary>Documented clinical reason for the override.</summary>
    [Id(14)]
    public string? OverrideReason { get; set; }

    /// <summary>UTC timestamp of the override.</summary>
    [Id(15)]
    public DateTime? OverrideDate { get; set; }
}

/// <summary>
/// Full state of an interaction screening for a prescription.
/// One screening is created per prescription at time of entry or pre-fill.
///
/// VistA reference: DRGINT.m integration in PSO fill workflow.
/// Key format: IXSCREEN:{prescriptionId}
/// </summary>
[GenerateSerializer]
public class InteractionScreeningState
{
    /// <summary>Screening identifier (grain primary key).</summary>
    [Id(0)]
    public string ScreeningId { get; set; } = string.Empty;

    /// <summary>Prescription this screening was performed for.</summary>
    [Id(1)]
    public string PrescriptionId { get; set; } = string.Empty;

    /// <summary>Patient this prescription belongs to.</summary>
    [Id(2)]
    public string PatientId { get; set; } = string.Empty;

    /// <summary>Drug name from the prescription being screened.</summary>
    [Id(3)]
    public string DrugName { get; set; } = string.Empty;

    /// <summary>Screening status controlling fill/refill gating.</summary>
    [Id(4)]
    public InteractionScreeningStatus Status { get; set; } = InteractionScreeningStatus.NotScreened;

    /// <summary>All interaction findings from the screening.</summary>
    [Id(5)]
    public List<InteractionFinding> Findings { get; set; } = new();

    /// <summary>Number of blocking interactions (Significant or Contraindicated).</summary>
    [Id(6)]
    public int BlockingCount { get; set; }

    /// <summary>Total number of interactions found (all severities).</summary>
    [Id(7)]
    public int TotalInteractionCount { get; set; }

    /// <summary>Number of blocking interactions that have been overridden.</summary>
    [Id(8)]
    public int OverriddenCount { get; set; }

    /// <summary>Pharmacist or system that performed the screening.</summary>
    [Id(9)]
    public string? ScreenedBy { get; set; }

    /// <summary>UTC timestamp when screening was performed.</summary>
    [Id(10)]
    public DateTime? ScreenedDate { get; set; }

    /// <summary>UTC timestamp of creation.</summary>
    [Id(11)]
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    /// <summary>UTC timestamp of last modification.</summary>
    [Id(12)]
    public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;

    /// <summary>Clinical notes.</summary>
    [Id(13)]
    public string? Notes { get; set; }
}

/// <summary>
/// Lightweight index entry for interaction screening lists.
/// Stored in the per-patient index grain.
/// </summary>
[GenerateSerializer]
public class InteractionScreeningIndexEntry
{
    /// <summary>Screening identifier.</summary>
    [Id(0)]
    public string ScreeningId { get; set; } = string.Empty;

    /// <summary>Prescription identifier.</summary>
    [Id(1)]
    public string PrescriptionId { get; set; } = string.Empty;

    /// <summary>Drug name.</summary>
    [Id(2)]
    public string DrugName { get; set; } = string.Empty;

    /// <summary>Current screening status.</summary>
    [Id(3)]
    public InteractionScreeningStatus Status { get; set; }

    /// <summary>Number of blocking interactions.</summary>
    [Id(4)]
    public int BlockingCount { get; set; }

    /// <summary>Total interactions found.</summary>
    [Id(5)]
    public int TotalInteractionCount { get; set; }

    /// <summary>When screening was performed.</summary>
    [Id(6)]
    public DateTime? ScreenedDate { get; set; }
}

/// <summary>
/// State for the per-patient interaction screening index grain.
/// Key format: IXSCREEN-IDX:{patientId}
/// </summary>
[GenerateSerializer]
public class InteractionScreeningIndexState
{
    /// <summary>All screening index entries for this patient.</summary>
    [Id(0)]
    public List<InteractionScreeningIndexEntry> Entries { get; set; } = new();
}
