// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
namespace NewVistas.Abstractions.GrainStates;

// ── Person identity (ADR-002) — the anchor for a HUMAN across roles ──
// A Person unifies the patient-role (a chart, keyed by ICN), the staff-role (a NewPerson/File #200
// record, keyed USER:{userId}), and relative-appearances (on others' charts). It sits ABOVE the
// ICN/MPI patient-identity layer — it answers "is this patient also that provider/relative?", NOT
// "is this the same patient across facilities?" (that's the ICN). Key pattern: "PERSON:{guid}".

/// <summary>How confident a person↔role link is (deliberate, never auto-merged).</summary>
public enum PersonLinkConfidence
{
    Unspecified = 0,
    Probabilistic = 1,          // demographic match, not yet confirmed
    ConfirmedByRegistration = 2,
    ConfirmedByClinician = 3,
    ConfirmedByPatient = 4
}

/// <summary>Where a relative-appearance came from.</summary>
public enum PersonRelativeSource
{
    Unknown = 0,
    RelatedPerson = 1,   // emergency contact / next of kin
    FamilyHistory = 2    // structured family-member-history entry
}

/// <summary>A patient chart this Person owns.</summary>
[GenerateSerializer]
public class PersonPatientRole
{
    /// <summary>The patient grain key (ICN once ADR-001 lands; the patient id today).</summary>
    [Id(0)] public string PatientId { get; set; } = string.Empty;
    [Id(1)] public string FacilityId { get; set; } = string.Empty;
    /// <summary>The primary/most-active chart when this Person has more than one.</summary>
    [Id(2)] public bool Primary { get; set; }
    [Id(3)] public PersonLinkConfidence Confidence { get; set; }
    [Id(4)] public string LinkedBy { get; set; } = string.Empty;
    [Id(5)] public DateTime LinkedDate { get; set; } = DateTime.UtcNow;
}

/// <summary>A staff/provider (File #200) record this Person is.</summary>
[GenerateSerializer]
public class PersonStaffRole
{
    /// <summary>The user id — the NewPerson grain is keyed "USER:{UserId}".</summary>
    [Id(0)] public string UserId { get; set; } = string.Empty;
    [Id(1)] public PersonLinkConfidence Confidence { get; set; }
    [Id(2)] public string LinkedBy { get; set; } = string.Empty;
    [Id(3)] public DateTime LinkedDate { get; set; } = DateTime.UtcNow;
}

/// <summary>Where this Person appears as a relative on ANOTHER patient's chart.</summary>
[GenerateSerializer]
public class PersonRelativeAppearance
{
    /// <summary>The patient whose chart this human is a relative on.</summary>
    [Id(0)] public string OnPatientId { get; set; } = string.Empty;
    [Id(1)] public string Relationship { get; set; } = string.Empty;  // e.g. "Mother"
    [Id(2)] public PersonRelativeSource Source { get; set; }
    /// <summary>The source entry id (family-member id, etc.) so the appearance is idempotent.</summary>
    [Id(3)] public string SourceEntryId { get; set; } = string.Empty;
    [Id(4)] public string LinkedBy { get; set; } = string.Empty;
    [Id(5)] public DateTime LinkedDate { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// A Person — the identity anchor for one human. Roles point AT this Person via nullable back-pointers
/// on their own records; this state carries the reverse references + the identity spine.
/// </summary>
[GenerateSerializer]
public class PersonState
{
    [Id(0)] public string PersonId { get; set; } = string.Empty;

    // ── Identity spine ──
    [Id(1)] public string Name { get; set; } = string.Empty;   // Last,First
    [Id(2)] public DateTime? DateOfBirth { get; set; }
    [Id(3)] public string Sex { get; set; } = string.Empty;
    /// <summary>SSN last-4 only — enough for candidate matching; the full SSN lives on the role records.</summary>
    [Id(4)] public string SsnLast4 { get; set; } = string.Empty;
    [Id(5)] public List<string> Aliases { get; set; } = new();

    // ── Role references ──
    [Id(6)] public List<PersonPatientRole> PatientRoles { get; set; } = new();
    [Id(7)] public List<PersonStaffRole> StaffRoles { get; set; } = new();
    [Id(8)] public List<PersonRelativeAppearance> RelativeAppearances { get; set; } = new();

    /// <summary>
    /// True when this Person has BOTH a patient-role and a staff-role — an "employee-patient", which is
    /// sensitive. The cross-role view of such a Person is a privileged, audited, break-the-glass
    /// operation (enforced in ADR-002 Phase 4). Set automatically as roles are linked/unlinked.
    /// </summary>
    [Id(9)] public bool IsEmployeePatient { get; set; }

    [Id(10)] public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    [Id(11)] public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;
}

/// <summary>Summary entry for the Person directory/search index.</summary>
[GenerateSerializer]
public class PersonIndexEntry
{
    [Id(0)] public string PersonId { get; set; } = string.Empty;
    [Id(1)] public string Name { get; set; } = string.Empty;
    [Id(2)] public DateTime? DateOfBirth { get; set; }
    [Id(3)] public string Sex { get; set; } = string.Empty;
    [Id(4)] public int PatientRoleCount { get; set; }
    [Id(5)] public int StaffRoleCount { get; set; }
    [Id(6)] public bool IsEmployeePatient { get; set; }
}

/// <summary>Persistent state for the singleton Person index grain.</summary>
[GenerateSerializer]
public class PersonIndexState
{
    [Id(0)] public List<PersonIndexEntry> Entries { get; set; } = new();
    [Id(1)] public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;
}
