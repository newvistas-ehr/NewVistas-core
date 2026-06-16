// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;

namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// State for the MPI Search/Index grain — holds the searchable patient index
/// for cross-facility patient lookup and identification.
///
/// Maintains two data structures:
///   - Patients: Dictionary keyed by ICN for O(1) direct lookup
///   - SsnIndex: Dictionary mapping SSN → list of ICNs for fast SSN-based search
///
/// This mirrors VistA's cross-references on the PATIENT file (#2):
///   "B"  cross-reference — by patient name
///   "BS" cross-reference — by SSN
///   "AICN" cross-reference — by ICN
/// </summary>
[GenerateSerializer]
public class MpiSearchState
{
    /// <summary>
    /// All patients in the index, keyed by ICN.
    /// </summary>
    [Id(0)]
    public Dictionary<string, MpiSearchEntry> Patients { get; set; } = new();

    /// <summary>
    /// SSN → list of ICNs index for fast SSN-based lookup.
    /// Multiple patients may share an SSN (edge cases, data quality).
    /// </summary>
    [Id(1)]
    public Dictionary<string, List<string>> SsnIndex { get; set; } = new();

    /// <summary>
    /// Total number of patients in the index.
    /// </summary>
    [Id(2)]
    public int TotalPatients { get; set; }

    /// <summary>
    /// Total number of facility correlations across all patients.
    /// </summary>
    [Id(3)]
    public int TotalCorrelations { get; set; }

    /// <summary>
    /// When the index was last updated.
    /// </summary>
    [Id(4)]
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Lightweight patient entry stored in the MPI search index.
/// Contains only the fields needed for search and display.
/// </summary>
[GenerateSerializer]
public class MpiSearchEntry
{
    /// <summary>
    /// Integration Control Number — national unique patient identifier.
    /// </summary>
    [Id(0)]
    public string Icn { get; set; } = string.Empty;

    /// <summary>
    /// Patient name in Last,First format.
    /// </summary>
    [Id(1)]
    public string PatientName { get; set; } = string.Empty;

    /// <summary>
    /// Social Security Number (9-digit string without dashes).
    /// </summary>
    [Id(2)]
    public string Ssn { get; set; } = string.Empty;

    /// <summary>
    /// Date of birth.
    /// </summary>
    [Id(3)]
    public DateTime? DateOfBirth { get; set; }

    /// <summary>
    /// Sex. "M" = Male, "F" = Female.
    /// </summary>
    [Id(4)]
    public string? Sex { get; set; }

    /// <summary>
    /// Number of facilities where this patient has correlations.
    /// </summary>
    [Id(5)]
    public int FacilityCount { get; set; }

    /// <summary>
    /// Whether this patient is deceased.
    /// </summary>
    [Id(6)]
    public bool IsDeceased { get; set; }

    /// <summary>
    /// If this ICN was merged into another (the survivor of a duplicate-patient
    /// merge), the surviving ICN. Searches by this ICN should redirect to it.
    /// Null when the record is the live primary record for the patient.
    /// </summary>
    [Id(7)]
    public string? MergedIntoIcn { get; set; }
}

/// <summary>
/// Search result returned by the MPI search grain.
/// Includes treating facility names for display.
/// </summary>
[GenerateSerializer]
public class MpiSearchResult
{
    /// <summary>
    /// Integration Control Number.
    /// </summary>
    [Id(0)]
    public string Icn { get; set; } = string.Empty;

    /// <summary>
    /// Patient name in Last,First format.
    /// </summary>
    [Id(1)]
    public string PatientName { get; set; } = string.Empty;

    /// <summary>
    /// Social Security Number.
    /// </summary>
    [Id(2)]
    public string Ssn { get; set; } = string.Empty;

    /// <summary>
    /// Date of birth.
    /// </summary>
    [Id(3)]
    public DateTime? DateOfBirth { get; set; }

    /// <summary>
    /// Sex.
    /// </summary>
    [Id(4)]
    public string? Sex { get; set; }

    /// <summary>
    /// List of treating facility names.
    /// </summary>
    [Id(5)]
    public List<string> TreatingFacilities { get; set; } = new();

    /// <summary>
    /// Whether this patient is deceased.
    /// </summary>
    [Id(6)]
    public bool IsDeceased { get; set; }

    /// <summary>
    /// If this ICN was merged into another (the survivor of a duplicate-patient
    /// merge), the surviving ICN. Allows clinician UIs to flag the result and
    /// follow the alias to the surviving record.
    /// </summary>
    [Id(7)]
    public string? MergedIntoIcn { get; set; }
}
