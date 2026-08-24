// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;

namespace NewVistas.Abstractions.GrainStates;

/// <summary>Lifecycle of a proto-condition — the scientific method as a state machine.</summary>
[GenerateSerializer]
public enum ProtoConditionStatus
{
    /// <summary>Being defined; not yet screening patients.</summary>
    Draft = 0,
    /// <summary>Live: screening runs, candidates accrue, alerts can fire.</summary>
    Active = 1,
    /// <summary>A real code arrived — definition frozen, handed off to the coded pipeline.</summary>
    Promoted = 2,
    /// <summary>Abandoned (false alarm / merged away).</summary>
    Retired = 3
}

/// <summary>What kind of patient signal a feature reads.</summary>
[GenerateSerializer]
public enum ProtoFeatureKind
{
    Symptom = 0,
    LabResult = 1,
    Vital = 2,
    Diagnosis = 3,
    Demographic = 4,
    Exposure = 5
}

/// <summary>How a feature participates in matching.</summary>
[GenerateSerializer]
public enum ProtoFeatureRule
{
    /// <summary>Contributes its weight to the score when satisfied.</summary>
    Weighted = 0,
    /// <summary>Must be satisfied or the patient cannot match at all.</summary>
    HardInclude = 1,
    /// <summary>If satisfied, the patient is disqualified (score forced to non-match).</summary>
    HardExclude = 2
}

/// <summary>Comparison operator for evaluating a feature against a patient's data.</summary>
[GenerateSerializer]
public enum ProtoFeatureOperator
{
    /// <summary>Coded signal is present (symptom Present / diagnosis on problem list / exposure).</summary>
    Present = 0,
    /// <summary>Coded signal is explicitly absent (symptom answered Absent).</summary>
    Absent = 1,
    /// <summary>Numeric/string value equals <c>Value</c> (demographic sex; exact match).</summary>
    Equals = 2,
    GreaterThan = 3,
    GreaterOrEqual = 4,
    LessThan = 5,
    LessOrEqual = 6,
    /// <summary>Numeric value between <c>Value</c> and <c>Value2</c> inclusive (age band).</summary>
    InRange = 7,
    /// <summary>
    /// Lab result carries any abnormal flag, whatever the value. Lets a definition say
    /// "abnormal" without restating a reference range per analyte — the laboratory has
    /// already made that judgement and it is carried on the result.
    /// </summary>
    Abnormal = 8
}

/// <summary>How a proto-condition came into existence.</summary>
public enum ProtoConditionOrigin
{
    /// <summary>Defined by hand by an epidemiologist.</summary>
    HandDefined = 0,

    /// <summary>
    /// Drafted from a single patient's snapshot by a clinician who found that nothing matched.
    /// A definition derived from one chart is over-fitted by construction, so a draft of this
    /// origin carries that warning and must be edited before it can be activated.
    /// </summary>
    DraftedFromPatient = 1
}

/// <summary>Membership state of a patient within a proto-condition cluster.</summary>
[GenerateSerializer]
public enum ProtoMemberStatus
{
    /// <summary>Machine- or human-proposed; awaiting epidemiologist review.</summary>
    Candidate = 0,
    /// <summary>Reviewed and accepted into the cluster (a clinical assertion).</summary>
    Confirmed = 1,
    /// <summary>Reviewed and rejected — never silently resurrected.</summary>
    Excluded = 2
}

/// <summary>How a patient first entered the cluster.</summary>
[GenerateSerializer]
public enum ProtoMemberSource
{
    /// <summary>Surfaced by the matcher.</summary>
    Machine = 0,
    /// <summary>Explicitly suggested by a clinician — persists even if it stops matching.</summary>
    ManualSuggestion = 1
}

/// <summary>State of a per-member problem-list recode after promotion.</summary>
[GenerateSerializer]
public enum ProtoMigrationStatus
{
    Pending = 0,
    Migrated = 1,
    Skipped = 2
}

/// <summary>Merge/split provenance of a proto-condition.</summary>
[GenerateSerializer]
public enum ProtoLineageKind
{
    None = 0,
    /// <summary>This proto was split OUT of a parent (the parent is <c>ParentProtoId</c>).</summary>
    SplitChild = 1,
    /// <summary>This proto was merged INTO a target (recorded in <c>ChildProtoIds</c>).</summary>
    MergedInto = 2
}

/// <summary>
/// One clause of a proto-condition's case definition. Unified across all signal kinds so the
/// matcher iterates a single list; the operator vocabulary mirrors the eCR / lab-taxonomy engines.
/// </summary>
[GenerateSerializer]
public record ProtoFeature
{
    /// <summary>Stable id within the proto (survives display/threshold edits).</summary>
    [Id(0)] public string FeatureId { get; set; } = string.Empty;
    /// <summary>Which patient signal to read.</summary>
    [Id(1)] public ProtoFeatureKind Kind { get; set; }
    /// <summary>Human-readable label.</summary>
    [Id(2)] public string Display { get; set; } = string.Empty;
    /// <summary>The code to read: SNOMED symptom, LOINC lab, ICD-10 dx, demographic key ("AGE"/"SEX"), facility id.</summary>
    [Id(3)] public string Code { get; set; } = string.Empty;
    /// <summary>Comparison operator.</summary>
    [Id(4)] public ProtoFeatureOperator Operator { get; set; }
    /// <summary>Comparison value (numeric threshold, expected code/string).</summary>
    [Id(5)] public string? Value { get; set; }
    /// <summary>Upper bound for <see cref="ProtoFeatureOperator.InRange"/>.</summary>
    [Id(6)] public string? Value2 { get; set; }
    /// <summary>Unit of measure for numeric features (documentation only).</summary>
    [Id(7)] public string? Unit { get; set; }
    /// <summary>Contribution weight when satisfied (Weighted rule).</summary>
    [Id(8)] public double Weight { get; set; } = 1.0;
    /// <summary>Weighted / HardInclude / HardExclude.</summary>
    [Id(9)] public ProtoFeatureRule Rule { get; set; }
    /// <summary>Only consider data no older than this many days (null = any age).</summary>
    [Id(10)] public int? RecencyWindowDays { get; set; }
}

/// <summary>
/// Per-feature explanation of why a patient scored as they did — the explainability contract.
/// Records unassessed features too, so the survey knows "we never asked this patient about hearing".
/// </summary>
[GenerateSerializer]
public record FeatureContribution
{
    [Id(0)] public string FeatureId { get; set; } = string.Empty;
    [Id(1)] public string Display { get; set; } = string.Empty;
    [Id(2)] public ProtoFeatureKind Kind { get; set; }
    /// <summary>True if the feature's condition was met.</summary>
    [Id(3)] public bool Satisfied { get; set; }
    /// <summary>True if the patient's data could answer this feature (false = never asked/measured).</summary>
    [Id(4)] public bool Assessed { get; set; }
    /// <summary>The feature's weight (fixed-denominator scoring includes unassessed as 0).</summary>
    [Id(5)] public double Weight { get; set; }
    /// <summary>Quoted evidence ("SpO2 88%", "anosmia: Present", "not asked").</summary>
    [Id(6)] public string Evidence { get; set; } = string.Empty;
}

/// <summary>A patient's membership record within a proto-condition, with the evidence snapshot.</summary>
[GenerateSerializer]
public record ProtoMember
{
    [Id(0)] public string PatientId { get; set; } = string.Empty;
    [Id(1)] public ProtoMemberStatus Status { get; set; }
    /// <summary>Match score (0..1) at last evaluation.</summary>
    [Id(2)] public double Score { get; set; }
    /// <summary>Definition version this evaluation was computed against (staleness detector).</summary>
    [Id(3)] public int EvaluatedAtVersion { get; set; }
    [Id(4)] public ProtoMemberSource Source { get; set; }
    [Id(5)] public string? SuggestedBy { get; set; }
    [Id(6)] public string? StatusChangedBy { get; set; }
    [Id(7)] public DateTime StatusChangedDate { get; set; }
    /// <summary>Set when a Confirmed member no longer matches the current definition (needs re-review).</summary>
    [Id(8)] public bool ReviewFlag { get; set; }
    [Id(9)] public string? ReviewReason { get; set; }
    [Id(10)] public List<FeatureContribution> Contributions { get; set; } = new();
    [Id(11)] public DateTime FirstSeenDate { get; set; }
}

/// <summary>
/// Count-threshold alert on the confirmed cohort. Fires when the confirmed count first reaches the
/// threshold, then again only as the count grows past the high-water-mark and the cooldown elapses
/// (alert-fatigue control). "Fires exactly once for a static cohort" falls straight out of this.
/// </summary>
[GenerateSerializer]
public record ProtoAlertRule
{
    /// <summary>Confirmed-member count that arms the alert.</summary>
    [Id(0)] public int Threshold { get; set; }
    /// <summary>Optional observation window in days (documentation for the recipient).</summary>
    [Id(1)] public int? WindowDays { get; set; }
    /// <summary>User ids to notify.</summary>
    [Id(2)] public List<string> Recipients { get; set; } = new();
    /// <summary>Minimum hours between fires.</summary>
    [Id(3)] public int CooldownHours { get; set; } = 24;
    /// <summary>Confirmed count at the last fire (high-water-mark).</summary>
    [Id(4)] public int LastFiredCount { get; set; }
    /// <summary>When the alert last fired.</summary>
    [Id(5)] public DateTime? LastFiredDate { get; set; }
    /// <summary>How many times this alert has ever fired (testable).</summary>
    [Id(6)] public int TimesFired { get; set; }
}

/// <summary>An audit line in the proto's history.</summary>
[GenerateSerializer]
public record ProtoChangeLogEntry
{
    [Id(0)] public DateTime Timestamp { get; set; }
    [Id(1)] public string User { get; set; } = string.Empty;
    /// <summary>Short kind ("CREATE", "FEATURE", "THRESHOLD", "CONFIRM", "EXCLUDE", "GUIDANCE", "ALERT", "PROMOTE").</summary>
    [Id(2)] public string Kind { get; set; } = string.Empty;
    [Id(3)] public string Detail { get; set; } = string.Empty;
}

/// <summary>Post-promotion problem-list recode bookkeeping for one confirmed member.</summary>
[GenerateSerializer]
public record ProtoMigrationEntry
{
    [Id(0)] public string PatientId { get; set; } = string.Empty;
    [Id(1)] public ProtoMigrationStatus Status { get; set; }
    /// <summary>The problem-list entry id created by migration (when Migrated).</summary>
    [Id(2)] public string? ProblemId { get; set; }
    /// <summary>Skip reason / note.</summary>
    [Id(3)] public string? Reason { get; set; }
    [Id(4)] public DateTime? Date { get; set; }
    [Id(5)] public string? By { get; set; }
}

/// <summary>
/// Result of evaluating one patient against one proto-condition definition — the matcher's output
/// and the input to <c>UpsertEvaluationAsync</c>. Defined here (not in the Clinical matcher) so the
/// grain and the pure matcher share one serializable shape.
/// </summary>
[GenerateSerializer]
public record ProtoMatchResult
{
    [Id(0)] public string PatientId { get; set; } = string.Empty;
    [Id(1)] public string ProtoConditionId { get; set; } = string.Empty;
    /// <summary>Definition version the evaluation was computed against.</summary>
    [Id(2)] public int DefinitionVersion { get; set; }
    /// <summary>Fixed-denominator score = satisfied weights / all weights (0..1).</summary>
    [Id(3)] public double Score { get; set; }
    /// <summary>True if the patient matches (score ≥ threshold, all HardIncludes met, no HardExclude).</summary>
    [Id(4)] public bool Matches { get; set; }
    /// <summary>True if a HardExclude fired (disqualified regardless of score).</summary>
    [Id(5)] public bool HardExcluded { get; set; }
    [Id(6)] public List<FeatureContribution> Contributions { get; set; } = new();
}

/// <summary>
/// A ProtoCondition — a living, versioned cluster of patients, signals, and hypotheses that
/// represents an emerging disease pattern BEFORE it has a formal ICD/SNOMED code. Grain key:
/// <c>PROTO:{guid}</c>. Temporariness is the design: on promotion the definition freezes, the
/// hypothesis hands off to the coded pipeline (problem-list recode + eCR trigger), and this
/// becomes a historical artifact.
/// </summary>
[GenerateSerializer]
public class ProtoConditionState
{
    /// <summary>Guid portion of the grain key.</summary>
    [Id(0)] public string ProtoConditionId { get; set; } = string.Empty;
    /// <summary>Working name ("Novel respiratory cluster — anosmia-predominant").</summary>
    [Id(1)] public string Name { get; set; } = string.Empty;
    [Id(2)] public string Description { get; set; } = string.Empty;
    [Id(3)] public ProtoConditionStatus Status { get; set; } = ProtoConditionStatus.Draft;
    /// <summary>Bumped whenever the matching semantics (features / threshold) change; starts at 1.</summary>
    [Id(4)] public int DefinitionVersion { get; set; } = 1;
    [Id(5)] public string CreatedBy { get; set; } = string.Empty;
    [Id(6)] public DateTime CreatedDate { get; set; }
    [Id(7)] public DateTime LastModifiedDate { get; set; }

    /// <summary>The case definition — the unified feature list.</summary>
    [Id(8)] public List<ProtoFeature> Features { get; set; } = new();
    /// <summary>Minimum score (0..1) for a machine match.</summary>
    [Id(9)] public double MatchThreshold { get; set; } = 0.5;

    /// <summary>Membership (Candidate / Confirmed / Excluded) with per-patient evidence snapshots.</summary>
    [Id(10)] public List<ProtoMember> Members { get; set; } = new();
    [Id(11)] public List<ProtoChangeLogEntry> ChangeLog { get; set; } = new();

    // ── Guidance (recommendation only — no bed writes from this module) ──────
    /// <summary>Recommended infection-control precautions (typed against the bed module's enum).</summary>
    [Id(12)] public BedIsolationType? IsolationRecommendation { get; set; }
    [Id(13)] public string? PpeNotes { get; set; }
    /// <summary>Associated order-set ids (references only; never auto-executed).</summary>
    [Id(14)] public List<string> AssociatedOrderSetIds { get; set; } = new();
    [Id(15)] public ProtoAlertRule? AlertRule { get; set; }

    // ── Lineage (merge / split) ─────────────────────────────────────────────
    [Id(16)] public string? ParentProtoId { get; set; }
    [Id(17)] public List<string> ChildProtoIds { get; set; } = new();
    [Id(18)] public ProtoLineageKind LineageKind { get; set; }

    // ── Promotion (the code has arrived) ────────────────────────────────────
    [Id(19)] public string? PromotedName { get; set; }
    [Id(20)] public List<string> PromotedIcd10Codes { get; set; } = new();
    [Id(21)] public string? PromotedSnomed { get; set; }
    [Id(22)] public DateTime? PromotedEffectiveFrom { get; set; }
    [Id(23)] public List<string> PromotionJurisdictions { get; set; } = new();
    [Id(24)] public string? PromotionNotes { get; set; }
    [Id(25)] public DateTime? PromotedDate { get; set; }
    [Id(26)] public string? PromotedBy { get; set; }
    /// <summary>The eCR trigger id emitted at promotion (the net closing into the coded pipeline).</summary>
    [Id(27)] public string? EcrTriggerId { get; set; }

    /// <summary>Per-confirmed-member problem-list recode bookkeeping (populated at promotion).</summary>
    [Id(28)] public List<ProtoMigrationEntry> MigrationLog { get; set; } = new();

    // ── Draft provenance (ADR-004 ↔ ADR-006) ────────────────────────────────

    /// <summary>How this cluster came into existence. Default is the historical hand-defined path.</summary>
    [Id(29)] public ProtoConditionOrigin Origin { get; set; }

    /// <summary>
    /// The patient whose snapshot seeded this draft, when <see cref="Origin"/> is
    /// <see cref="ProtoConditionOrigin.DraftedFromPatient"/>. Present so a reviewer can see the
    /// definition came from one chart and is therefore over-fitted until generalised.
    /// </summary>
    [Id(30)] public string? DraftedFromPatientId { get; set; }

    /// <summary>When that snapshot was taken.</summary>
    [Id(31)] public DateTime? DraftedFromSnapshotAt { get; set; }
}
