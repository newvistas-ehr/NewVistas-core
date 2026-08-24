// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;

namespace NewVistas.Abstractions.GrainStates;

/// <summary>How coarsely a shard aggregates.</summary>
public enum DiagnosisCodeGranularity
{
    /// <summary>Exact normalized code ("E119").</summary>
    Code = 0,
    /// <summary>3-character ICD-10 category ("E11").</summary>
    Category = 1,
    /// <summary>Site-wide across all diagnoses — the comparison baseline.</summary>
    All = 2
}

/// <summary>
/// One (from → to) outcome pair with its count.
/// </summary>
[GenerateSerializer]
public class DiagnosisRevisionStat
{
    [Id(0)] public string OutcomeCode { get; set; } = string.Empty;
    [Id(1)] public string OutcomeDisplay { get; set; } = string.Empty;
    [Id(2)] public int Count { get; set; }
    [Id(3)] public int CountUnexposed { get; set; }
    [Id(4)] public DateTime LastSeenUtc { get; set; }
}

/// <summary>
/// One candidate discriminating test, with both arms.
///
/// Both arms are mandatory, and that is the whole defence against the obvious failure: every
/// worked-up patient gets a CBC, so counting only the revised arm learns that the CBC diagnoses
/// everything. The not-revised arm costs nothing — those episodes are already being walked for
/// the denominator.
/// </summary>
[GenerateSerializer]
public class DiscriminatorStat
{
    /// <summary>Namespaced key ("L:33959-8", "R:72148", "C:NEUROLOGY").</summary>
    [Id(0)] public string TestKey { get; set; } = string.Empty;
    [Id(1)] public DiagnosticTestKind Kind { get; set; }
    [Id(2)] public string Display { get; set; } = string.Empty;

    [Id(3)] public int NewInRevised { get; set; }
    [Id(4)] public int NewInNotRevised { get; set; }
    [Id(5)] public int NewAndAbnormalInRevised { get; set; }
    [Id(6)] public int NewAndAbnormalInNotRevised { get; set; }

    /// <summary>
    /// Episodes where the clinician already had this result at assertion and still got there
    /// wrong. Kept because it is the counter-argument to the advice: suggesting a test the
    /// clinician already had is noise, and this is how the system can know.
    /// </summary>
    [Id(7)] public int AlreadyPresentAtAssertion { get; set; }

    // Exposure-partitioned twins. Without these the feedback loop runs unchecked: the advisory
    // names test T → clinicians order more T → T precedes more revisions → T's own lift climbs.
    // Reported lift uses these, never the totals above.
    [Id(8)] public int NewInRevisedUnexposed { get; set; }
    [Id(9)] public int NewInNotRevisedUnexposed { get; set; }
    [Id(10)] public int NewAndAbnormalInRevisedUnexposed { get; set; }
    [Id(11)] public int NewAndAbnormalInNotRevisedUnexposed { get; set; }

    [Id(12)] public DateTime LastSeenUtc { get; set; }
}

/// <summary>
/// An adjudication outcome that mapped to no code — free text the clinician typed.
/// Kept rather than dropped: an accumulating pile of unmapped outcomes is itself a finding
/// (the vocabulary is missing something), and silently discarding them would hide it.
/// </summary>
[GenerateSerializer]
public class UnmappedOutcomeNote
{
    [Id(0)] public string Text { get; set; } = string.Empty;
    [Id(1)] public int Count { get; set; }
    [Id(2)] public DateTime LastSeenUtc { get; set; }
}

/// <summary>
/// Learned outcome counters for one diagnosis at one granularity for one assertion year.
/// Grain key <c>DX-OUTCOME:{granularity}:{codeKey}:{yyyy}</c>.
///
/// Bucketed by <b>assertion</b> year, and the reason is clinical validity rather than write
/// volume — a 50,000-patient practice generates roughly three writes a day on its commonest
/// working diagnosis. Diagnostic criteria move (Sepsis-2 → Sepsis-3, the 2017 hypertension
/// threshold, CKD-EPI dropping the race coefficient), so a 2016 revision rate for a code is
/// evidence about a different definition of that code. An unbucketed shard would average across
/// definitional changes silently and forever.
/// </summary>
[GenerateSerializer]
public class DiagnosisOutcomeState
{
    [Id(0)] public string CodeKey { get; set; } = string.Empty;
    [Id(1)] public DiagnosisCodeGranularity Granularity { get; set; }
    [Id(2)] public int AssertionYear { get; set; }

    // ── The denominator ladder: Revised ⊆ Adjudicated ⊆ Asserted ─────────────

    /// <summary>Episodes opened. Not a denominator — many are still open.</summary>
    [Id(3)] public int AssertedCount { get; set; }

    /// <summary>
    /// Episodes where someone said how it turned out. <b>This is the denominator.</b>
    /// </summary>
    [Id(4)] public int AdjudicatedCount { get; set; }

    [Id(5)] public int ConfirmedCount { get; set; }

    /// <summary>Episodes the clinician coded as a genuine correction. <b>The numerator.</b></summary>
    [Id(6)] public int RevisedCount { get; set; }

    /// <summary>Not an error — in the denominator, out of the numerator.</summary>
    [Id(7)] public int RefinedCount { get; set; }

    /// <summary>Not an error — in the denominator, out of the numerator.</summary>
    [Id(8)] public int BroadenedCount { get; set; }

    [Id(9)] public int ResolvedWithoutAlternateCount { get; set; }

    /// <summary>In neither numerator nor denominator; tracked to make coverage computable.</summary>
    [Id(10)] public int ClosedUnadjudicatedCount { get; set; }

    /// <summary>
    /// Episodes closed by a code-set change (cluster promotion, ICD revision). In neither
    /// numerator nor denominator, and deliberately NOT counted toward adjudication coverage
    /// either — a recode is not a clinician declining to adjudicate, so it must not drag the
    /// coverage ratio down and silence an otherwise reportable rate.
    /// </summary>
    [Id(21)] public int RecodedCount { get; set; }

    // ── Exposure control ────────────────────────────────────────────────────

    /// <summary>Adjudicated episodes where the advisory was NOT shown. Reported rate denominator.</summary>
    [Id(11)] public int AdjudicatedUnexposedCount { get; set; }

    /// <summary>Revised episodes where the advisory was NOT shown. Reported rate numerator.</summary>
    [Id(12)] public int RevisedUnexposedCount { get; set; }

    /// <summary>
    /// When an unexposed episode was last recorded. Once the advisory reaches everyone this
    /// stops moving and the reported rate silently becomes a historical snapshot — which is why
    /// the holdout exists (see <c>DiagnosticStewardshipThresholds.LearnedRateHoldoutFraction</c>).
    /// </summary>
    [Id(13)] public DateTime? LastUnexposedRecordedUtc { get; set; }

    // ── Learned detail ──────────────────────────────────────────────────────

    [Id(14)] public List<DiagnosisRevisionStat> RevisedTo { get; set; } = new();
    [Id(15)] public List<DiscriminatorStat> Discriminators { get; set; } = new();
    [Id(16)] public List<UnmappedOutcomeNote> UnmappedOutcomes { get; set; } = new();

    /// <summary>
    /// Distinct adjudicating providers — <b>for a count only</b>.
    ///
    /// There is deliberately no method anywhere that returns a per-provider breakdown, and none
    /// should be added. The set exists solely so a rate built on one idiosyncratic clinician can
    /// be suppressed. This feature must never become a leaderboard; the moment it can rank
    /// individuals it stops being a stewardship tool and clinicians stop adjudicating honestly,
    /// which destroys the data it runs on.
    /// </summary>
    [Id(17)] public HashSet<string> AdjudicatingProviderIds { get; set; } = new();

    [Id(18)] public DateTime LastRecordedUtc { get; set; }

    /// <summary>
    /// Revisions whose outcome code is an unspecified / NOS form.
    ///
    /// A rising rate here is the strongest signal this design can emit that clinicians are
    /// systematically failing to reach a diagnosis at all — the shape an unnamed emerging
    /// disease makes on its way through a problem list, before any code for it exists.
    /// </summary>
    [Id(19)] public int NosTerminatingRevisedCount { get; set; }

    [Id(20)] public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
}
