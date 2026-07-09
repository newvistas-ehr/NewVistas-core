// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;

namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// Roll-up row for one proto-condition, shown on the surveillance list. All counts are
/// PROJECTIONS maintained by the proto grain — the grain remains the source of truth.
/// </summary>
[GenerateSerializer]
public record ProtoConditionSummary
{
    [Id(0)] public string ProtoConditionId { get; set; } = string.Empty;
    [Id(1)] public string Name { get; set; } = string.Empty;
    [Id(2)] public ProtoConditionStatus Status { get; set; }
    [Id(3)] public int ConfirmedCount { get; set; }
    [Id(4)] public int CandidateCount { get; set; }
    /// <summary>Members whose evaluation predates the current definition version (re-sweep needed).</summary>
    [Id(5)] public int StaleCount { get; set; }
    [Id(6)] public int DefinitionVersion { get; set; }
    [Id(7)] public DateTime LastModifiedDate { get; set; }
    /// <summary>Recommended isolation precaution (surfaced so the list can flag airborne clusters).</summary>
    [Id(8)] public BedIsolationType? IsolationRecommendation { get; set; }
    /// <summary>The code the proto promoted to, once promoted (e.g. "U07.1").</summary>
    [Id(9)] public string? PromotedCode { get; set; }
}

/// <summary>
/// Directory of all proto-conditions. Grain key: <c>PROTOCONDITION-INDEX</c>.
/// </summary>
[GenerateSerializer]
public class ProtoConditionIndexState
{
    [Id(0)] public List<ProtoConditionSummary> Entries { get; set; } = new();
}

/// <summary>
/// Reverse-index shard: the CONFIRMED members of one proto-condition. Grain key:
/// <c>PROTO-COHORT:{protoConditionId}</c>. Powers cohort algebra (analytics, future dispatch).
/// </summary>
[GenerateSerializer]
public class ProtoCohortState
{
    [Id(0)] public string ProtoConditionId { get; set; } = string.Empty;
    [Id(1)] public HashSet<string> PatientIds { get; set; } = new();
}
