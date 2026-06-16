// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// A single household member's income information for means test purposes
/// (VistA File #408.13 INCOME PERSON).
/// </summary>
[GenerateSerializer]
public record IncomePerson
{
    /// <summary>Unique identifier for this income person record.</summary>
    [Id(0)] public string PersonId { get; init; } = string.Empty;

    /// <summary>Relationship type to the veteran patient.</summary>
    [Id(1)] public string RelationshipType { get; init; } = string.Empty;

    /// <summary>Full name of the household member.</summary>
    [Id(2)] public string Name { get; init; } = string.Empty;

    /// <summary>Social Security Number of the household member (masked for display).</summary>
    [Id(3)] public string? Ssn { get; init; }

    /// <summary>Date of birth of the household member.</summary>
    [Id(4)] public DateTime? DateOfBirth { get; init; }

    /// <summary>Gross annual income for the reporting year.</summary>
    [Id(5)] public decimal? GrossAnnualIncome { get; init; }

    /// <summary>Total net worth (assets minus liabilities).</summary>
    [Id(6)] public decimal? NetWorth { get; init; }

    /// <summary>Calendar year this income data applies to.</summary>
    [Id(7)] public int IncomeYear { get; init; }

    /// <summary>Whether this record represents the veteran themselves (self).</summary>
    [Id(8)] public bool IsVeteranSelf { get; init; }
}

/// <summary>
/// Household income and means test record for a patient (VistA File #408.13 INCOME PERSON).
/// Used by DG MEANS TEST MUMPS routines (DGMTU.m, DGMTEE1.m).
/// </summary>
[GenerateSerializer]
public class IncomeHouseholdState
{
    /// <summary>Patient identifier.</summary>
    [Id(0)] public string PatientId { get; set; } = string.Empty;

    /// <summary>Calendar year for which household income is being reported.</summary>
    [Id(1)] public int ReportingYear { get; set; }

    /// <summary>All household members included in the income calculation.</summary>
    [Id(2)] public List<IncomePerson> HouseholdMembers { get; set; } = new();

    /// <summary>Sum of gross annual income across all household members.</summary>
    [Id(3)] public decimal TotalHouseholdIncome { get; set; }

    /// <summary>Sum of net worth across all household members.</summary>
    [Id(4)] public decimal TotalNetWorth { get; set; }

    /// <summary>Date the means test was completed.</summary>
    [Id(5)] public DateTime? MeansTestDate { get; set; }

    /// <summary>Decision outcome: e.g., EXEMPT, COPAY REQUIRED, HIGH INCOME.</summary>
    [Id(6)] public string? MeansTestDecision { get; set; }

    /// <summary>Date the means test decision was recorded.</summary>
    [Id(7)] public DateTime? MeansTestDecisionDate { get; set; }

    /// <summary>Income threshold applied at the time of the means test decision.</summary>
    [Id(8)] public decimal? ThresholdApplied { get; set; }

    /// <summary>Free-text notes about this means test or income record.</summary>
    [Id(9)] public string? Notes { get; set; }

    /// <summary>UTC timestamp when this record was first created.</summary>
    [Id(10)] public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    /// <summary>UTC timestamp of the most recent modification.</summary>
    [Id(11)] public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;
}
