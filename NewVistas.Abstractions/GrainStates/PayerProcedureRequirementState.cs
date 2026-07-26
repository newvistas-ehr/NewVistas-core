// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;

namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// Accumulated statistics for one requirement category on a (payer, procedure) pair — how often the
/// payer denied for this category, and how often it was satisfied on an approval.
/// </summary>
[GenerateSerializer]
public record CategoryStat
{
    [Id(0)] public PriorAuthRequirementCategory Category { get; set; }
    [Id(1)] public int DenialCount { get; set; }
    [Id(2)] public DateTime? LastDeniedOn { get; set; }
    [Id(3)] public string? LastSampleReason { get; set; }
    [Id(4)] public int ApprovalSatisfiedCount { get; set; }
}

/// <summary>A denial reason that did not map to a real requirement category — kept, never dropped.</summary>
[GenerateSerializer]
public record UnmappedDenial
{
    [Id(0)] public string ReasonText { get; set; } = string.Empty;
    [Id(1)] public int Count { get; set; }
    [Id(2)] public DateTime LastSeen { get; set; }
}

/// <summary>
/// The learned half of the requirements KB: a reverse-index shard for ONE (payer, procedure) pair that
/// accumulates observed denial reasons (by category) and approval evidence from real procedure-PA
/// outcomes. Grain key: <c>PAYER-PROC:{payerId}:{cptCode}</c>. Copies the SdohCohortIndex accumulation
/// shape. NOTE the payerId contains hyphens (e.g. PAYER-BCBS-FL); the grain parses its key by stripping
/// the "PAYER-PROC:" prefix then splitting the remainder on the LAST ':'.
/// </summary>
[GenerateSerializer]
public class PayerProcedureRequirementState
{
    [Id(0)] public string PayerId { get; set; } = string.Empty;
    [Id(1)] public string CptCode { get; set; } = string.Empty;
    [Id(2)] public List<CategoryStat> CategoryStats { get; set; } = new();
    [Id(3)] public int TotalDenials { get; set; }
    [Id(4)] public int TotalApprovals { get; set; }
    [Id(5)] public List<UnmappedDenial> UnmappedDenials { get; set; } = new();
}

/// <summary>Read snapshot of a (payer, procedure) learned profile, returned to the merge.</summary>
[GenerateSerializer]
public record PayerProcedureRequirementProfile
{
    [Id(0)] public string PayerId { get; set; } = string.Empty;
    [Id(1)] public string CptCode { get; set; } = string.Empty;
    [Id(2)] public IReadOnlyList<CategoryStat> CategoryStats { get; set; } = new List<CategoryStat>();
    [Id(3)] public int TotalDenials { get; set; }
    [Id(4)] public int TotalApprovals { get; set; }
    [Id(5)] public IReadOnlyList<UnmappedDenial> UnmappedDenials { get; set; } = new List<UnmappedDenial>();
}
