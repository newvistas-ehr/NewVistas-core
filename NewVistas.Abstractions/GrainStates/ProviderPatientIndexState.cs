// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;

namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// Provider Patient Index state — the "My Patients" list for a provider.
/// Tracks all patients assigned to a provider with denormalized demographics for fast search.
///
/// Key pattern: "PROV-PAT-IDX:{providerId}"
/// </summary>
[GenerateSerializer]
public class ProviderPatientIndexState
{
    /// <summary>
    /// Provider ID this index belongs to.
    /// </summary>
    [Id(0)]
    public string ProviderId { get; set; } = string.Empty;

    /// <summary>
    /// All patients assigned to this provider.
    /// </summary>
    [Id(1)]
    public List<ProviderPatientEntry> Patients { get; set; } = new();
}

/// <summary>
/// A single patient entry in a provider's patient index.
/// Contains denormalized demographics for fast search and display without fan-out reads.
/// </summary>
[GenerateSerializer]
public class ProviderPatientEntry
{
    /// <summary>
    /// Patient ID (Orleans grain key).
    /// </summary>
    [Id(0)]
    public string PatientId { get; set; } = string.Empty;

    /// <summary>
    /// Patient display name (VistA format: "LAST,FIRST MI"). Denormalized for search.
    /// </summary>
    [Id(1)]
    public string PatientName { get; set; } = string.Empty;

    /// <summary>
    /// Patient date of birth.
    /// </summary>
    [Id(2)]
    public DateTime? DateOfBirth { get; set; }

    /// <summary>
    /// Last 4 digits of SSN (never full SSN).
    /// </summary>
    [Id(3)]
    public string? SsnLast4 { get; set; }

    /// <summary>
    /// Relationship type — PCP, SPECIALIST, NURSE, ATTENDING, etc.
    /// Matches the role on the care team.
    /// </summary>
    [Id(4)]
    public string Relationship { get; set; } = string.Empty;

    /// <summary>
    /// Date the provider last saw this patient (updated on appointment checkout).
    /// </summary>
    [Id(5)]
    public DateTime? LastSeenDate { get; set; }

    /// <summary>
    /// Date of the patient's next appointment with this provider.
    /// </summary>
    [Id(6)]
    public DateTime? NextAppointmentDate { get; set; }

    /// <summary>
    /// Whether this patient is still actively assigned to the provider.
    /// </summary>
    [Id(7)]
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Date this patient was assigned to the provider.
    /// </summary>
    [Id(8)]
    public DateTime AssignmentDate { get; set; }
}
