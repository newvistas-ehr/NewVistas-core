// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;
using NewVistas.Abstractions.GrainStates;
using NewVistas.Abstractions.Security;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// The generic code-set migration sweep (ADR-006 "bulk recode"). Singleton, grain key
/// <c>DX-STEWARDSHIP-SWEEP</c>, modelled on <c>ProtoSweepGrain</c>: paged population walk,
/// per-patient failure isolation, bounded run history, and no timer — a migration is an
/// explicit administrative act, never a background job.
///
/// A recode is a statement that the CODE SET changed, not that anyone was wrong. Every write
/// this sweep makes is pinned to <see cref="RevisionReason.Recode"/> /
/// <see cref="DiagnosticEpisodeOutcome.Recoded"/>, which the outcome shards exclude from the
/// revision numerator, the denominator AND the coverage ratio.
/// </summary>
public interface IDxStewardshipSweepGrain : IGrainWithStringKey
{
    /// <summary>Run the directive across the patient population (optionally capped).</summary>
    [RequiresSecurityKey(SecurityKeys.XUMGR)]
    [AuditAction("PROBLEMS", "BULK_RECODE", EntityType = "CODE_SET", IsClinicalWrite = true)]
    Task<BulkRecodeRun> BulkRecodeAsync(BulkRecodeCommand command);

    /// <summary>Run the directive against an explicit patient list (re-runs, spot fixes).</summary>
    [RequiresSecurityKey(SecurityKeys.XUMGR)]
    [AuditAction("PROBLEMS", "BULK_RECODE", EntityType = "CODE_SET", IsClinicalWrite = true)]
    Task<BulkRecodeRun> BulkRecodePatientsAsync(BulkRecodeCommand command, List<string> patientIds);

    /// <summary>Recent runs, newest first.</summary>
    Task<List<BulkRecodeRun>> GetRecentRunsAsync();
}
