// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
namespace NewVistas.Abstractions.GrainStates;

// ── Structured family history (FHIR FamilyMemberHistory-shaped) ──
// One entry per relative — relationship + conditions with age at diagnosis + vital status — feeding
// the hereditary-risk red-flag assessment. This is the "structured flat family history" tier from the
// genetics blueprint (not a full GA4GH pedigree); relatives are described entities, not patient records.

/// <summary>Relationship of a family member to the patient (proband).</summary>
public enum FamilyRelationship
{
    Unknown = 0,
    Mother = 1,
    Father = 2,
    Sister = 3,
    Brother = 4,
    Daughter = 5,
    Son = 6,
    MaternalGrandmother = 7,
    MaternalGrandfather = 8,
    PaternalGrandmother = 9,
    PaternalGrandfather = 10,
    MaternalAunt = 11,
    MaternalUncle = 12,
    PaternalAunt = 13,
    PaternalUncle = 14,
    Niece = 15,
    Nephew = 16,
    Cousin = 17,
    HalfSibling = 18,
    Other = 19
}

/// <summary>Vital status of a family member.</summary>
public enum FamilyVitalStatus
{
    Unknown = 0,
    Alive = 1,
    Deceased = 2
}

/// <summary>A condition affecting a family member, with age at diagnosis.</summary>
[GenerateSerializer]
public class FamilyConditionEntry
{
    [Id(0)] public string Condition { get; set; } = string.Empty;  // e.g. "Breast cancer"
    /// <summary>SNOMED/ICD code when coded; free text otherwise.</summary>
    [Id(1)] public string Code { get; set; } = string.Empty;
    [Id(2)] public int? AgeAtDiagnosis { get; set; }
    [Id(3)] public string Notes { get; set; } = string.Empty;
}

/// <summary>One relative's family-history entry.</summary>
[GenerateSerializer]
public class FamilyMemberHistoryEntry
{
    [Id(0)] public string MemberId { get; set; } = string.Empty;
    [Id(1)] public FamilyRelationship Relationship { get; set; }
    [Id(2)] public string Name { get; set; } = string.Empty;
    /// <summary>Sex, free-text (M/F/etc.) — relatives are described, not registered.</summary>
    [Id(3)] public string Sex { get; set; } = string.Empty;
    [Id(4)] public FamilyVitalStatus VitalStatus { get; set; }
    /// <summary>Current age (if alive).</summary>
    [Id(5)] public int? AgeYears { get; set; }
    /// <summary>Age at death (if deceased).</summary>
    [Id(6)] public int? AgeAtDeath { get; set; }
    [Id(7)] public string CauseOfDeath { get; set; } = string.Empty;
    [Id(8)] public List<FamilyConditionEntry> Conditions { get; set; } = new();
    [Id(9)] public string Notes { get; set; } = string.Empty;
    /// <summary>
    /// Person anchor (ADR-002) — set when this relative is confirmed to be a known Person (e.g. a
    /// relative who is also a patient here). Empty = a described entity only (the default). Enables
    /// cascade testing: "this mother is patient X, a confirmed carrier."
    /// </summary>
    [Id(10)] public string LinkedPersonId { get; set; } = string.Empty;
}

/// <summary>
/// A patient's structured family history. Key pattern: the patient id (one family history per patient).
/// </summary>
[GenerateSerializer]
public class FamilyHistoryState
{
    [Id(0)] public string PatientId { get; set; } = string.Empty;
    [Id(1)] public List<FamilyMemberHistoryEntry> Members { get; set; } = new();
    [Id(2)] public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    [Id(3)] public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;
}
