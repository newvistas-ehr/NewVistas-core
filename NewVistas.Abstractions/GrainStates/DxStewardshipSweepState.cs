// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;

namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// One code-set migration directive: every active problem carrying <see cref="FromCode"/> is
/// re-asserted under <see cref="ToCode"/> with ADR-006 provenance. The semantics are fixed by
/// construction, not proposed per-episode: the revision reason is always
/// <see cref="RevisionReason.Recode"/> and the episode outcome is always
/// <see cref="DiagnosticEpisodeOutcome.Recoded"/> — statistically inert in both directions.
/// <see cref="Clinical.DiagnosisCodeRelation.Propose"/> is deliberately never consulted: it
/// would score B34.2 → U07.1 as a correction and teach the outcome shard that B34.2 was wrong
/// 100% of the time.
/// </summary>
[GenerateSerializer]
public class BulkRecodeCommand
{
    /// <summary>The code being retired (e.g. B34.2 when U07.1 shipped).</summary>
    [Id(0)] public string FromCode { get; set; } = string.Empty;

    /// <summary>The replacement code.</summary>
    [Id(1)] public string ToCode { get; set; } = string.Empty;

    /// <summary>Official display for the replacement code.</summary>
    [Id(2)] public string ToDisplay { get; set; } = string.Empty;

    /// <summary>
    /// Why the code set changed, quoted onto every touched problem (e.g. "U07.1 COVID-19
    /// issued by WHO 2020-03; B34.2 rows remapped per coding directive").
    /// </summary>
    [Id(3)] public string Narrative { get; set; } = string.Empty;

    /// <summary>User running the migration; attributed on the new assertions.</summary>
    [Id(4)] public string RunBy { get; set; } = string.Empty;

    /// <summary>Optional cap on how many patients a population sweep screens.</summary>
    [Id(5)] public int? MaxPatients { get; set; }
}

/// <summary>What a recode did (or declined to do) for one patient.</summary>
[GenerateSerializer]
public enum ProblemRecodeOutcome
{
    /// <summary>No active problem carries the old code.</summary>
    NoMatch = 0,

    /// <summary>Old assertions superseded and re-asserted under the new code.</summary>
    Recoded = 1,

    /// <summary>
    /// The patient already carries the new code as an active problem — nothing is touched, so
    /// a re-run of the same directive is a no-op rather than a duplicate row.
    /// </summary>
    AlreadyCoded = 2,
}

/// <summary>Per-patient recode result.</summary>
[GenerateSerializer]
public class ProblemRecodeResult
{
    [Id(0)] public ProblemRecodeOutcome Outcome { get; set; }

    /// <summary>The new problem asserted under the replacement code, when one was created.</summary>
    [Id(1)] public string? NewProblemId { get; set; }

    /// <summary>The superseded problem ids.</summary>
    [Id(2)] public List<string> RecodedProblemIds { get; set; } = new();

    /// <summary>
    /// How many open diagnostic episodes were closed as Recoded. Pre-feature and imported
    /// rows have no open episode; their problems still recode, they just have no episode to
    /// close — this count is how a run reports that honestly.
    /// </summary>
    [Id(3)] public int EpisodesClosed { get; set; }
}

/// <summary>One bulk-recode run, kept for the audit trail on the sweep grain.</summary>
[GenerateSerializer]
public class BulkRecodeRun
{
    [Id(0)] public string FromCode { get; set; } = string.Empty;
    [Id(1)] public string ToCode { get; set; } = string.Empty;
    [Id(2)] public DateTime StartedAt { get; set; }
    [Id(3)] public string RunBy { get; set; } = string.Empty;

    /// <summary>True when run against an explicit patient list rather than the population.</summary>
    [Id(4)] public bool TargetedMode { get; set; }

    [Id(5)] public int PatientsScreened { get; set; }
    [Id(6)] public int RecodedCount { get; set; }
    [Id(7)] public int AlreadyCodedCount { get; set; }
    [Id(8)] public int NoMatchCount { get; set; }
    [Id(9)] public int EpisodesClosed { get; set; }

    /// <summary>Patients whose read/write failed; the sweep continues past them.</summary>
    [Id(10)] public int FailureCount { get; set; }
}

/// <summary>State for the DX-STEWARDSHIP-SWEEP singleton: bounded run history.</summary>
[GenerateSerializer]
public class DxStewardshipSweepState
{
    [Id(0)] public List<BulkRecodeRun> Runs { get; set; } = new();
}
