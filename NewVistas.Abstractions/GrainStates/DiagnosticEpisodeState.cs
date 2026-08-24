// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;

namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// How a diagnostic episode ended. The distinction between the members is the denominator
/// ladder: <c>Revised ⊆ Adjudicated ⊆ Asserted</c>.
/// </summary>
public enum DiagnosticEpisodeOutcome
{
    /// <summary>
    /// Still open — the working diagnosis has not been adjudicated. In <b>no</b> denominator:
    /// this episode has not yet had the chance to be revised, and counting it would punish a
    /// clinician for properly leaving a diagnosis open.
    /// </summary>
    Open = 0,

    /// <summary>Adjudicated and the original diagnosis stood.</summary>
    Confirmed = 1,

    /// <summary>
    /// Adjudicated and the diagnosis was wrong. <b>The numerator.</b>
    /// </summary>
    Revised = 2,

    /// <summary>
    /// Same disease, finer code. Stays <b>in</b> the denominator with a verdict of "not an
    /// error", which deflates the reported rate — the safe direction.
    /// </summary>
    Refined = 3,

    /// <summary>Same disease, less specific code. Also not an error; also in the denominator.</summary>
    Broadened = 4,

    /// <summary>Resolved without any alternative diagnosis being reached.</summary>
    ResolvedWithoutAlternate = 5,

    /// <summary>
    /// Closed without adjudication — lost to follow-up, transferred, chart closed. In
    /// <b>neither</b> numerator nor denominator: nobody ever said whether it was right.
    /// </summary>
    ClosedUnadjudicated = 6,

    /// <summary>
    /// Closed because the CODE SET changed underneath it — an emerging cluster promoted to a
    /// real code, or an ICD revision. In <b>neither</b> numerator nor denominator.
    ///
    /// Excluded from the numerator because no clinician was wrong, and excluded from the
    /// <i>denominator</i> too because a code-set change carries no information about
    /// diagnostic accuracy. Counting it as an adjudication would dilute every rate with
    /// non-clinical events — when U07.1 shipped that would have been thousands of them at a
    /// single site, quietly halving the apparent revision rate for every respiratory code.
    /// </summary>
    Recoded = 7
}

/// <summary>What kind of thing a discriminator test key refers to.</summary>
public enum DiagnosticTestKind
{
    Unspecified = 0,
    /// <summary>Laboratory result — key form "L:{loinc}".</summary>
    Lab = 1,
    /// <summary>Imaging study — key form "R:{cpt}".</summary>
    Imaging = 2,
    /// <summary>Consult / referral — key form "C:{service}".</summary>
    Consult = 3,
    /// <summary>Bedside exam manoeuvre or scored instrument — key form "E:{name}".</summary>
    Exam = 4
}

/// <summary>
/// One diagnostic episode: a working diagnosis, what it was asserted on, and how it turned out.
///
/// This is a <b>projection</b> assembled from the assertion chain, not a second source of truth.
/// It exists because computing "what evidence arrived between assertion and adjudication" by
/// re-walking the event stream on every read would be prohibitive.
/// </summary>
[GenerateSerializer]
public class DiagnosticEpisode
{
    [Id(0)] public string EpisodeId { get; set; } = string.Empty;

    /// <summary>The problem whose assertion opened this episode.</summary>
    [Id(1)] public string ProblemId { get; set; } = string.Empty;

    /// <summary>Normalized (dots stripped, upper-cased) working diagnosis code.</summary>
    [Id(2)] public string WorkingCode { get; set; } = string.Empty;

    /// <summary>Display text of the working diagnosis, denormalized so the episode reads alone.</summary>
    [Id(3)] public string WorkingDisplay { get; set; } = string.Empty;

    [Id(4)] public DateTime AssertedUtc { get; set; }

    /// <summary>
    /// Evidence present when the diagnosis was asserted — including
    /// <see cref="EvidencePolarity.NotAssessed"/> rows. The delta against later evidence is what
    /// makes "which test would have discriminated" computable at all.
    /// </summary>
    [Id(5)] public List<EvidenceRef> EvidenceAtAssertion { get; set; } = new();

    [Id(6)] public DiagnosticEpisodeOutcome Outcome { get; set; }

    /// <summary>Normalized code the episode resolved to, when it was revised or refined.</summary>
    [Id(7)] public string? OutcomeCode { get; set; }

    [Id(8)] public string? OutcomeDisplay { get; set; }

    [Id(9)] public DateTime? AdjudicatedUtc { get; set; }

    /// <summary>The reason the clinician gave, when the episode ended in a revision.</summary>
    [Id(10)] public RevisionReason? OutcomeReason { get; set; }

    /// <summary>
    /// Namespaced test keys that arrived between assertion and adjudication, windowed to
    /// <c>DeltaWindowDays</c> before adjudication so a long-running episode does not absorb an
    /// unrelated workup. Keys are namespaced ("L:", "R:", "C:") because LOINC and CPT number
    /// spaces overlap — a bare "72148" is ambiguous.
    /// </summary>
    [Id(11)] public List<string> NewEvidence { get; set; } = new();

    /// <summary>The subset of <see cref="NewEvidence"/> that came back abnormal.</summary>
    [Id(12)] public List<string> AbnormalAmongNewEvidence { get; set; } = new();

    /// <summary>Free-text outcome note, kept when the outcome mapped to no code.</summary>
    [Id(13)] public string? OutcomeNote { get; set; }

    /// <summary>
    /// Whether the revision advisory was displayed to the clinician during this episode.
    ///
    /// This is the exposure flag. The reported rate is computed over unexposed episodes only,
    /// because once an advisory says "this is often actually PE", clinicians diagnose more PE,
    /// the shard observes more PE, and the statistic has trained on its own output.
    /// </summary>
    [Id(14)] public bool AdvisoryWasShown { get; set; }

    /// <summary>
    /// Set once this episode has been counted into the population shards.
    ///
    /// The at-most-once guard lives here, patient-side, rather than as a dedupe set on the shard —
    /// which keeps the shard a pure counter with no unbounded state. The existing
    /// <c>PayerProcedureRequirementIndexGrain</c> has no such guard and gets away with it because
    /// a double-counted denial is cosmetic. A double-counted misdiagnosis is not.
    /// </summary>
    [Id(15)] public DateTime? ReportedToShardUtc { get; set; }

    /// <summary>Provider who adjudicated, for the distinct-provider floor.</summary>
    [Id(16)] public string? AdjudicatingProviderId { get; set; }
}

/// <summary>
/// Per-patient diagnostic episodes. Grain key <c>DX-EPISODE:{patientId}</c>.
/// </summary>
[GenerateSerializer]
public class DiagnosticEpisodeIndexState
{
    [Id(0)] public string PatientId { get; set; } = string.Empty;
    [Id(1)] public List<DiagnosticEpisode> Episodes { get; set; } = new();
    [Id(2)] public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    [Id(3)] public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;
}
