// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;

namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// Lightweight projection of a prescription used in per-patient index queries.
/// </summary>
[GenerateSerializer]
public class PrescriptionIndexEntry
{
    [Id(0)]
    public string PrescriptionId { get; set; } = string.Empty;

    /// <summary>Drug name (.01)</summary>
    [Id(1)]
    public string DrugName { get; set; } = string.Empty;

    /// <summary>Pointer to DRUG file (#50)</summary>
    [Id(2)]
    public string? DrugId { get; set; }

    /// <summary>ACTIVE / DISCONTINUED / EXPIRED / HOLD / SUSPENDED</summary>
    [Id(3)]
    public string Status { get; set; } = string.Empty;

    [Id(4)]
    public DateTime? IssueDate { get; set; }

    [Id(5)]
    public DateTime? ExpirationDate { get; set; }

    [Id(6)]
    public int? Refills { get; set; }

    [Id(7)]
    public int? RefillsRemaining { get; set; }

    /// <summary>ROUTINE / URGENT / STAT</summary>
    [Id(8)]
    public string Priority { get; set; } = "ROUTINE";

    [Id(9)]
    public bool IsVerified { get; set; }

    [Id(10)]
    public bool CounselingRequired { get; set; }

    [Id(11)]
    public string? ProviderId { get; set; }

    [Id(12)]
    public string? ProviderName { get; set; }

    /// <summary>Dosage / strength string for display</summary>
    [Id(13)]
    public string? Dosage { get; set; }
}

/// <summary>
/// Persistent state for the per-patient prescription index grain.
/// Keyed by "PSO-INDEX:{patientId}".
/// </summary>
[GenerateSerializer]
public class PatientPrescriptionIndexState
{
    [Id(0)]
    public List<PrescriptionIndexEntry> Prescriptions { get; set; } = new();

    [Id(1)]
    public int TotalPrescriptions { get; set; }
}
