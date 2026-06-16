// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// Lightweight entry in the global insurance plan lookup index.
/// Used for fast plan search without loading individual plan grains.
/// </summary>
[GenerateSerializer]
public record InsurancePlanIndexEntry
{
    /// <summary>Unique plan identifier. Matches InsurancePlanGrain key suffix.</summary>
    [Id(0)] public string PlanId { get; init; } = string.Empty;

    /// <summary>Name of the group insurance plan.</summary>
    [Id(1)] public string GroupPlanName { get; init; } = string.Empty;

    /// <summary>Name of the insurance company offering this plan.</summary>
    [Id(2)] public string InsuranceCompanyName { get; init; } = string.Empty;

    /// <summary>Plan type (MEDICARE, MEDICAID, CHAMPVA, TRICARE, COMMERCIAL, etc.). File #355.1.</summary>
    [Id(3)] public string? PlanType { get; init; }

    /// <summary>Whether this plan is currently active.</summary>
    [Id(4)] public bool IsActive { get; init; }
}

/// <summary>
/// Singleton global index of all Group Insurance Plans (File #355.3).
/// Used for plan search by name, company, or type.
/// Grain key: "IB-PLAN-INDEX"
/// </summary>
[GenerateSerializer]
public class InsurancePlanIndexState
{
    /// <summary>All insurance plan entries, kept sorted by GroupPlanName.</summary>
    [Id(0)] public List<InsurancePlanIndexEntry> Entries { get; set; } = new();
}
