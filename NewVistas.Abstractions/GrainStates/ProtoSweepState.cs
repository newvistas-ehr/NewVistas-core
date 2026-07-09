// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;

namespace NewVistas.Abstractions.GrainStates;

/// <summary>Record of one screening sweep run (audit / dashboard).</summary>
[GenerateSerializer]
public record ProtoSweepRun
{
    [Id(0)] public string ProtoConditionId { get; set; } = string.Empty;
    [Id(1)] public DateTime StartedAt { get; set; }
    [Id(2)] public int PatientsScreened { get; set; }
    [Id(3)] public int MatchedCount { get; set; }
    /// <summary>True if this was a targeted re-cluster over an explicit patient list (the split path).</summary>
    [Id(4)] public bool TargetedMode { get; set; }
    [Id(5)] public string RunBy { get; set; } = string.Empty;
}

/// <summary>
/// State for the singleton sweep coordinator (grain key <c>PROTO-SWEEP</c>). Keeps a bounded
/// history of runs. There is NO timer in v1 — a sweep is always an explicit epidemiologist action
/// (introducing the codebase's first timer-driven population job deserves its own decision, not a
/// side effect of this feature).
/// </summary>
[GenerateSerializer]
public class ProtoSweepState
{
    [Id(0)] public List<ProtoSweepRun> Runs { get; set; } = new();
}
