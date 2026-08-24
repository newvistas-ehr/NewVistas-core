// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Clinical;

/// <summary>
/// Floors and thresholds for diagnostic stewardship (ADR-006). Every number here exists to keep
/// the system quiet when it does not know something.
/// </summary>
public static class DiagnosticStewardshipThresholds
{
    /// <summary>
    /// Adjudicated episodes required before a rate is reported at all.
    ///
    /// Higher than the proto-condition analytics floor of 10, deliberately: a noisy cluster
    /// signal makes an epidemiologist look twice, but a noisy revision rate makes a clinician
    /// doubt a correct diagnosis. Higher stakes, higher floor.
    /// </summary>
    public const int MinAdjudicatedForRate = 20;

    /// <summary>Revisions to one alternative before that alternative is named.</summary>
    public const int MinRevisionsForAlternative = 3;

    /// <summary>Revised episodes before any discriminator is scored.</summary>
    public const int MinRevisedForDiscriminator = 5;

    /// <summary>
    /// Not-revised episodes required before a lift is computed. Both arms are mandatory —
    /// a lift with an empty comparison arm is not a lift, it is a raw count wearing a ratio's
    /// clothes.
    /// </summary>
    public const int MinNotRevisedForComparison = 5;

    /// <summary>
    /// Distinct adjudicating providers required. One idiosyncratic clinician must never
    /// <i>be</i> the statistic that is then shown back to their colleagues.
    /// </summary>
    public const int MinDistinctProviders = 2;

    /// <summary>
    /// Below this adjudication coverage the verdict is forced to Insufficient.
    ///
    /// Clinicians adjudicate more often when something interesting happened, which biases the
    /// rate upward. The corollary is a design rule, not a preference: <b>adjudication must never
    /// be made mandatory</b> — a required field produces default-clicked garbage, which is worse
    /// than missing data because it cannot be detected.
    /// </summary>
    public const double MinAdjudicationCoverage = 0.50;

    /// <summary>Below this rate nothing is worth saying, however solid the statistics.</summary>
    public const double MinRateToReport = 0.10;

    /// <summary>Days before adjudication within which arriving evidence counts as the delta.</summary>
    public const int DeltaWindowDays = 30;

    /// <summary>Assertion-year buckets read back, including the current one.</summary>
    public const int ReadWindowYears = 5;

    /// <summary>
    /// Fraction of otherwise-eligible episodes held out from seeing the learned rate, so an
    /// unexposed comparison arm keeps growing after the advisory reaches everyone.
    ///
    /// <b>Never applies to Critical curated baseline lines.</b> A held-out clinician still
    /// receives every literature-backed safety pairing; only the local learned percentage is
    /// withheld. That asymmetry is what makes the holdout defensible — what is withheld is an
    /// unvalidated descriptive statistic, which is exactly what a rollout holdout is for, not a
    /// safety warning.
    /// </summary>
    public const double LearnedRateHoldoutFraction = 0.10;

    /// <summary>Lift at/above which a discriminator is a signal. Shared with the proto analytics.</summary>
    public const double SignalLift = ProtoConditionAnalytics.SignalLift;

    /// <summary>Lift at/below which a discriminator is noise. Shared with the proto analytics.</summary>
    public const double NoiseLiftCeiling = ProtoConditionAnalytics.NoiseLiftCeiling;
}

/// <summary>How a revision rate compares to the site-wide baseline.</summary>
public enum RevisionRateBand
{
    /// <summary>Below a floor, coverage too low, or too few distinct providers. Say nothing.</summary>
    Insufficient = 0,
    /// <summary>At or near the site-wide rate — this diagnosis is not unusual.</summary>
    Typical = 1,
    /// <summary>
    /// In the ambiguous middle. Deliberately <b>not</b> forced into a verdict — reporting
    /// "borderline" is honest; rounding it to elevated or typical is not.
    /// </summary>
    Borderline = 2,
    /// <summary>Materially above the site-wide rate and above the reporting floor.</summary>
    Elevated = 3
}

/// <summary>How much harm follows from missing the alternative diagnosis.</summary>
public enum DiagnosticHarmIfMissed
{
    Unspecified = 0,
    Routine = 1,
    Serious = 2,
    /// <summary>
    /// Time-critical and potentially fatal or disabling. These render even at n = 0 — the
    /// min-N floors gate only the <i>learned</i> percentage, never the curated arrow, because
    /// dizziness → posterior stroke will never reach n = 20 at one clinic and that is exactly
    /// where silence would be most harmful.
    /// </summary>
    Critical = 3
}

/// <summary>Where a displayed rate came from, so the UI can label its own confidence.</summary>
public enum RateProvenance
{
    /// <summary>No learned rate — curated baseline only.</summary>
    ColdStart = 0,
    /// <summary>This exact code, this site.</summary>
    ExactCode = 1,
    /// <summary>Widened to the 3-character category to clear the floors.</summary>
    Category = 2
}

/// <summary>
/// A named alternative this diagnosis turns out to be.
/// </summary>
[GenerateSerializer]
public class DiagnosisAlternative
{
    [Id(0)] public string Code { get; set; } = string.Empty;
    [Id(1)] public string Display { get; set; } = string.Empty;

    /// <summary>Count — primary. See the display contract on <see cref="DiagnosisRevisionAdvisory"/>.</summary>
    [Id(2)] public int Count { get; set; }

    /// <summary>Denominator for <see cref="Count"/>, so the pair reads as "9 of 41".</summary>
    [Id(3)] public int OutOf { get; set; }

    /// <summary>True when this came from the curated baseline rather than local data.</summary>
    [Id(4)] public bool FromBaseline { get; set; }

    [Id(5)] public DiagnosticHarmIfMissed Harm { get; set; }

    /// <summary>Literature citation, for baseline entries.</summary>
    [Id(6)] public string? Citation { get; set; }
}

/// <summary>
/// A test worth considering, with both arms shown as counts.
/// </summary>
[GenerateSerializer]
public class DiagnosticTestSuggestion
{
    [Id(0)] public string TestKey { get; set; } = string.Empty;
    [Id(1)] public string Display { get; set; } = string.Empty;
    [Id(2)] public DiagnosticTestKind Kind { get; set; }

    [Id(3)] public int ArrivedBeforeRevision { get; set; }
    [Id(4)] public int RevisedTotal { get; set; }
    [Id(5)] public int ArrivedInNotRevised { get; set; }
    [Id(6)] public int NotRevisedTotal { get; set; }

    /// <summary>Ratio of the two arms. Never rendered without the four counts above.</summary>
    [Id(7)] public double? Lift { get; set; }

    [Id(8)] public SignalVerdict Verdict { get; set; }
    [Id(9)] public bool FromBaseline { get; set; }
    [Id(10)] public DiagnosticHarmIfMissed Harm { get; set; }
    [Id(11)] public string? Citation { get; set; }
}

/// <summary>
/// What a clinician is shown about a working diagnosis (ADR-006).
///
/// <b>DISPLAY CONTRACT — binding on every renderer of this type:</b>
/// <list type="number">
/// <item>
/// Counts are primary and mandatory; <see cref="RevisionRate"/> is derived, nullable and
/// secondary. Render "revised in 9 of 41 adjudicated episodes at this site", not "22%".
/// </item>
/// <item>
/// Nothing shaped like "sensitivity 90% / specificity 95%" may reach a screen. Unfold into the
/// thousand-person story first. Physicians reliably fail conditional-probability questions
/// (Casscells 1978: ~18% correct, modal answer 95% against a correct ~2%; Manrai 2014: ~23%,
/// median still 95%) and reliably succeed with natural frequencies. The representation is the
/// fix, not education.
/// </item>
/// <item>
/// Discriminator phrasing states what the counters contain — "arrived before the revision in 9
/// of 12 revised episodes vs 11 of 66 not-revised" — and <b>never</b> "this test would have
/// gotten you there". Reverse causation is unfixable from observational data: the troponin does
/// not discriminate ACS from GERD, the clinician who ordered it already suspected ACS. Phrasing
/// is the only available mitigation, so it is a contract rather than a style note.
/// </item>
/// <item>
/// <see cref="RevisionRate"/> is null whenever <see cref="Band"/> is
/// <see cref="RevisionRateBand.Insufficient"/>. Never render a number you do not have.
/// </item>
/// </list>
/// </summary>
[GenerateSerializer]
public class DiagnosisRevisionAdvisory
{
    [Id(0)] public string WorkingCode { get; set; } = string.Empty;
    [Id(1)] public string WorkingDisplay { get; set; } = string.Empty;

    /// <summary>Denominator — adjudicated episodes in the unexposed arm. Always rendered.</summary>
    [Id(2)] public int AdjudicatedCount { get; set; }

    /// <summary>Numerator — revised episodes in the unexposed arm. Always rendered.</summary>
    [Id(3)] public int RevisedCount { get; set; }

    /// <summary>
    /// Derived and secondary. Null when <see cref="Band"/> is Insufficient. Must never appear
    /// without <see cref="RevisedCount"/> and <see cref="AdjudicatedCount"/> beside it.
    /// </summary>
    [Id(4)] public double? RevisionRate { get; set; }

    [Id(5)] public RevisionRateBand Band { get; set; }
    [Id(6)] public RateProvenance RateProvenance { get; set; }

    /// <summary>The site-wide rate across all diagnoses, as the comparison baseline.</summary>
    [Id(7)] public double? SiteWideRevisionRate { get; set; }

    /// <summary>This diagnosis's rate over the site-wide rate.</summary>
    [Id(8)] public double? LiftOverSiteWide { get; set; }

    [Id(9)] public List<DiagnosisAlternative> Alternatives { get; set; } = new();
    [Id(10)] public List<DiagnosticTestSuggestion> SuggestedTests { get; set; } = new();

    /// <summary>True when there is no local learned rate and only curated content is shown.</summary>
    [Id(11)] public bool IsColdStart { get; set; }

    // Counts that keep the denominator honest and visible rather than implied.
    [Id(12)] public int RefinedCount { get; set; }
    [Id(13)] public int BroadenedCount { get; set; }
    [Id(14)] public int StillOpenCount { get; set; }
    [Id(15)] public int ClosedUnadjudicatedCount { get; set; }

    /// <summary>
    /// Revisions that ended on an unspecified/NOS code. Surfaced because "we changed our mind
    /// and still do not know" is a different and more alarming fact than "we changed our mind".
    /// </summary>
    [Id(16)] public int NosTerminatingRevisedCount { get; set; }

    /// <summary>Distinct adjudicating providers behind this number. Never a breakdown.</summary>
    [Id(17)] public int DistinctAdjudicatingProviders { get; set; }

    /// <summary>Non-suppressible. The UI may not hide or truncate this.</summary>
    [Id(18)] public string Disclaimer { get; set; } =
        "Local descriptive statistics from this site's own adjudicated episodes. Not a validated " +
        "predictive model and not a recommendation. Tests listed arrived before a revision — that " +
        "does not establish they would have changed the diagnosis.";

    [Id(19)] public DateTime GeneratedAt { get; set; }

    /// <summary>Why the rate is being withheld, when it is. Shown instead of a number.</summary>
    [Id(20)] public string? InsufficientReason { get; set; }
}
