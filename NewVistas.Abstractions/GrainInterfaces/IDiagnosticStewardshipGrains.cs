// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.Clinical;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Per-patient diagnostic episodes (ADR-006). Grain key <c>DX-EPISODE:{patientId}</c>.
///
/// A projection over the assertion chain, not a second source of truth: it exists so that
/// "what evidence arrived between assertion and adjudication" does not require re-walking the
/// event stream on every read.
/// </summary>
public interface IDiagnosticEpisodeIndexGrain : IGrainWithStringKey
{
    /// <summary>
    /// Open an episode for a newly asserted working diagnosis. Idempotent per problem id —
    /// a second call for the same open problem returns the existing episode id.
    /// </summary>
    Task<string> OpenEpisodeAsync(
        string problemId, string workingCode, string workingDisplay,
        DateTime assertedUtc, List<EvidenceRef> evidenceAtAssertion);

    /// <summary>
    /// Adjudicate an open episode: state how it turned out, with the evidence that arrived in
    /// between. Returns the completed episode, or null when there is no matching open episode.
    /// </summary>
    Task<DiagnosticEpisode?> AdjudicateAsync(
        string problemId, DiagnosticEpisodeOutcome outcome,
        string? outcomeCode, string? outcomeDisplay, RevisionReason? reason,
        string? outcomeNote, List<string> newEvidenceKeys, List<string> abnormalKeys,
        string? adjudicatingProviderId, DateTime adjudicatedUtc);

    /// <summary>Record that the advisory was displayed during this episode (the exposure flag).</summary>
    Task MarkAdvisoryShownAsync(string problemId);

    /// <summary>Stamp an episode as counted into the population shards — the at-most-once guard.</summary>
    Task MarkReportedAsync(string episodeId, DateTime reportedUtc);

    Task<List<DiagnosticEpisode>> GetEpisodesAsync();
    Task<DiagnosticEpisode?> GetOpenEpisodeForProblemAsync(string problemId);
}

/// <summary>
/// Learned outcome counters for one diagnosis, granularity and assertion year (ADR-006).
/// Grain key <c>DX-OUTCOME:{granularity}:{codeKey}:{yyyy}</c>.
///
/// A pure counter. It holds no patient identifiers and offers no method that could produce a
/// per-provider breakdown — both omissions are deliberate.
/// </summary>
public interface IDiagnosisOutcomeIndexGrain : IGrainWithStringKey
{
    /// <summary>Count a newly asserted working diagnosis.</summary>
    Task RecordAssertionAsync();

    /// <summary>
    /// Count an adjudicated episode: the outcome, the (from → to) pair, and both discriminator
    /// arms. <paramref name="advisoryWasShown"/> partitions every counter that feeds a reported
    /// number.
    /// </summary>
    Task RecordAdjudicationAsync(
        DiagnosticEpisodeOutcome outcome,
        string? outcomeCode, string? outcomeDisplay, string? outcomeNote,
        List<string> newEvidenceKeys, List<string> abnormalKeys,
        List<string> presentAtAssertionKeys,
        string? adjudicatingProviderId, bool advisoryWasShown, DateTime occurredUtc);

    Task<DiagnosisOutcomeState> GetStateAsync();
}

/// <summary>
/// Read side for diagnostic stewardship (ADR-006). Stateless — merges shards across the read
/// window, applies the floors, and produces the clinician-facing advisory.
/// </summary>
public interface IDiagnosisOutcomeAnalyticsGrain : IGrainWithStringKey
{
    /// <summary>
    /// Build the advisory for a working diagnosis code. Returns curated-baseline-only content
    /// when local data is insufficient, and never a bare percentage.
    /// </summary>
    Task<DiagnosisRevisionAdvisory> GetAdvisoryAsync(string workingCode, string workingDisplay);
}
