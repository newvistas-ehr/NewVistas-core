// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// Lightweight entry in the per-patient personal policy index.
/// Used for fast policy list views without loading individual policy grains.
/// </summary>
[GenerateSerializer]
public record PersonalPolicyIndexEntry
{
    /// <summary>Unique policy identifier. Matches PersonalPolicyGrain key suffix.</summary>
    [Id(0)] public string PolicyId { get; init; } = string.Empty;

    /// <summary>Group plan identifier this policy is enrolled in. Null if not in system.</summary>
    [Id(1)] public string? GroupPlanId { get; init; }

    /// <summary>Display name of the group plan.</summary>
    [Id(2)] public string GroupPlanName { get; init; } = string.Empty;

    /// <summary>Plan type (MEDICARE, MEDICAID, CHAMPVA, TRICARE, COMMERCIAL, etc.).</summary>
    [Id(3)] public string? PlanType { get; init; }

    /// <summary>Member/subscriber ID assigned by the insurance company.</summary>
    [Id(4)] public string SubscriberId { get; init; } = string.Empty;

    /// <summary>Whether this is the primary insurance policy.</summary>
    [Id(5)] public bool IsPrimary { get; init; }

    /// <summary>Whether this policy is currently active.</summary>
    [Id(6)] public bool IsActive { get; init; }

    /// <summary>Policy effective date for sorting and display.</summary>
    [Id(7)] public DateTime? EffectiveDate { get; init; }
}

/// <summary>
/// Per-patient index of all personal insurance policies (File #355.7).
/// Grain key: "IB-POLICY-IDX:{patientId}"
/// </summary>
[GenerateSerializer]
public class PersonalPolicyIndexState
{
    /// <summary>Patient whose policies are indexed here.</summary>
    [Id(0)] public string PatientId { get; set; } = string.Empty;

    /// <summary>All personal policy entries for this patient.</summary>
    [Id(1)] public List<PersonalPolicyIndexEntry> Entries { get; set; } = new();
}
