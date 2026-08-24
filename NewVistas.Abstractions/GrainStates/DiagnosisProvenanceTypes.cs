// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;
using NewVistas.Abstractions.Security;

namespace NewVistas.Abstractions.GrainStates;

// ─── Evidence ───────────────────────────────────────────────────────────────

/// <summary>
/// What kind of record an <see cref="EvidenceRef"/> points at. The kind determines how
/// <see cref="EvidenceRef.SourceId"/> is interpreted.
/// </summary>
public enum EvidenceKind
{
    Unspecified = 0,

    /// <summary>Coded symptom on <c>SYMPTOMS:{patientId}</c>; Code is a SymptomCatalog SNOMED code.</summary>
    Symptom = 1,

    /// <summary>Lab result (<c>LAB-{guid}</c>); Code is LOINC.</summary>
    LabResult = 2,

    /// <summary>Vital sign measurement.</summary>
    Vital = 3,

    /// <summary>Imaging study (<c>RAD-{guid}</c> / <c>IMG-{guid}</c>).</summary>
    Imaging = 4,

    /// <summary>A TIU note (<c>TIU-{guid}</c>) — typically the note carrying the reasoning.</summary>
    Note = 5,

    /// <summary>A prescription (<c>RX-{guid}</c>). Response — or non-response — is evidence.</summary>
    Medication = 6,

    /// <summary>
    /// Another problem (<c>PROB-{guid}</c>). This is the edge that dissolves the
    /// diagnosis/symptom distinction: diabetes cited here is a finding of diabetic
    /// nephropathy while remaining a diagnosis in its own right.
    /// </summary>
    Problem = 7,

    /// <summary>A procedure or surgical finding.</summary>
    Procedure = 8,

    /// <summary>A genomic result (variant, PGx phenotype).</summary>
    Genomic = 9,

    /// <summary>Outside record with no local id. SourceId is null; Note carries the citation.</summary>
    ExternalRecord = 10,

    /// <summary>
    /// Exam finding or clinical gestalt. SourceId is null, and that is honest — a great deal of
    /// real diagnostic evidence has no record id, and forcing one would be a lie.
    /// </summary>
    ClinicalJudgment = 11,

    /// <summary>An emerging-condition cluster (<c>PROTO:{guid}</c>) — see ADR-004.</summary>
    ProtoCondition = 12
}

/// <summary>
/// Which way a piece of evidence points. Replaces the two-boolean
/// (<c>Satisfied</c>, <c>Assessed</c>) shape used by <c>FeatureContribution</c>, which can
/// express the meaningless state "not assessed but satisfied".
/// </summary>
public enum EvidencePolarity
{
    /// <summary>
    /// The check was <b>not performed</b>. This is a positive record of a gap, not padding.
    ///
    /// Absence from an evidence list means nothing at all; presence with this value means
    /// "we know we did not look." Without the distinction, a patient with eight negative
    /// etiologic tests and a patient with eight untested possibilities are indistinguishable
    /// in the record — and they are opposite clinical signals.
    ///
    /// It is the default so a default-constructed ref never claims a finding.
    /// </summary>
    NotAssessed = 0,

    /// <summary>Assessed, and argues for the assertion.</summary>
    Supports = 1,

    /// <summary>Assessed, and argues against it — an informative negative.</summary>
    Refutes = 2,

    /// <summary>Assessed but equivocal: quantity not sufficient, uninterpretable film, borderline value.</summary>
    Indeterminate = 3,

    /// <summary>A result exists but falls outside the window in which it would inform this assertion.</summary>
    Stale = 4
}

/// <summary>
/// A structured citation supporting, refuting, or explicitly absent from a diagnostic assertion.
///
/// This replaces provenance-as-prose. Today the emerging-conditions promotion path writes
/// <c>"Recoded from emerging cluster '{name}' (proto {id})"</c> into <c>ProblemEntry.Comments</c> —
/// a real identifier embedded in an English sentence, which cannot be navigated, counted, or
/// invalidated when the source record is retracted.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record EvidenceRef
{
    /// <summary>What kind of record this points at.</summary>
    [Id(0)] public EvidenceKind Kind { get; init; }

    /// <summary>
    /// Grain key / record id of the source. Null only for <see cref="EvidenceKind.ClinicalJudgment"/>
    /// and <see cref="EvidenceKind.ExternalRecord"/>, where no local record exists.
    /// </summary>
    [Id(1)] public string? SourceId { get; init; }

    /// <summary>Code system: "LOINC", "SNOMED", "ICD-10", "CPT", "RxNorm".</summary>
    [Id(2)] public string? CodeSystem { get; init; }

    /// <summary>The code within <see cref="CodeSystem"/>.</summary>
    [Id(3)] public string? Code { get; init; }

    /// <summary>Human-readable label, denormalized so the citation stays legible if the source moves.</summary>
    [Id(4)] public string Display { get; init; } = string.Empty;

    /// <summary>Which way this evidence points.</summary>
    [Id(5)] public EvidencePolarity Polarity { get; init; }

    /// <summary>
    /// The value exactly as recorded — "88", "NEGATIVE", "4.2". Never a sentence; the
    /// interpretation belongs in <see cref="Polarity"/>, not in prose that has to be re-parsed.
    /// </summary>
    [Id(6)] public string? ObservedValue { get; init; }

    /// <summary>Units for <see cref="ObservedValue"/>, when it is a measurement.</summary>
    [Id(7)] public string? ObservedUnit { get; init; }

    /// <summary>
    /// When the evidence was <b>observed</b> — not when it was cited. The gap between the two
    /// is what makes <see cref="EvidencePolarity.Stale"/> computable.
    /// </summary>
    [Id(8)] public DateTime? ObservedUtc { get; init; }

    /// <summary>
    /// True when a machine (the proto matcher, a rules engine) supplied this citation rather
    /// than a clinician. Kept because a machine-inferred citation and a clinician's are
    /// indistinguishable once written down, and the difference matters when auditing.
    /// </summary>
    [Id(9)] public bool IsMachineCited { get; init; }

    /// <summary>The originating <c>ProtoFeature.FeatureId</c> when this came from the cluster matcher.</summary>
    [Id(10)] public string? FeatureId { get; init; }

    /// <summary>
    /// Free-text qualifier only. Structured facts belong in the fields above — anything put
    /// here is invisible to every query this feature exists to answer.
    /// </summary>
    [Id(11)] public string? Note { get; init; }

    /// <summary>
    /// Deterministic representation for the hash chain. Uses "^" as its separator; callers
    /// embedding a list of these in an event must hash each ref rather than concatenating,
    /// because free text may contain any separator we pick.
    /// </summary>
    public string Canonicalize() => string.Join("^",
        (int)Kind,
        SourceId ?? string.Empty,
        CodeSystem ?? string.Empty,
        Code ?? string.Empty,
        Display,
        (int)Polarity,
        ObservedValue ?? string.Empty,
        ObservedUnit ?? string.Empty,
        ObservedUtc?.ToString("O") ?? string.Empty,
        IsMachineCited.ToString(),
        FeatureId ?? string.Empty,
        Note ?? string.Empty);

    /// <summary>
    /// Canonical form of a whole evidence list: each ref is hashed, then the hashes joined.
    /// Hashing first means free text containing "|" or "^" cannot shift field boundaries and
    /// silently produce two different lists with the same canonical string.
    /// </summary>
    public static string CanonicalizeList(IEnumerable<EvidenceRef>? refs)
        => refs is null
            ? string.Empty
            : string.Join(",", refs.Select(r => HashChain.Compute(r.Canonicalize(), string.Empty)));

    /// <summary>Identity for de-duplication when evidence is appended across assessments.</summary>
    public (EvidenceKind, string, string) DedupeKey()
        => (Kind,
            SourceId ?? string.Empty,
            // A ref with neither a source record nor a code — a free-text exam finding or a
            // recorded gap — has only its text as identity. Without this fallback, every
            // second free-text ref of the same kind would silently collide with the first.
            !string.IsNullOrEmpty(Code) ? Code
                : string.IsNullOrEmpty(SourceId) ? Display : string.Empty);
}

/// <summary>
/// The one merge rule for evidence lists, shared by BOTH apply mirrors (live grain and
/// snapshot replay) so they cannot drift.
///
/// Same identity (<see cref="EvidenceRef.DedupeKey"/>) + identical content → skip: a
/// re-submitted citation is a duplicate, not new information. Same identity + DIFFERENT
/// content → replace in place: the workup moved. The canonical case is the one the whole
/// design is built around — "influenza PCR: not assessed" later becomes "influenza PCR:
/// negative"; under the old keep-first rule the update was silently discarded and the chart
/// forever read "we never looked". Deterministic on the event stream, so replay agrees with
/// live application.
/// </summary>
public static class EvidenceRefMerge
{
    public static void MergeInto(List<EvidenceRef> target, IEnumerable<EvidenceRef> incoming)
    {
        foreach (EvidenceRef r in incoming)
        {
            int i = target.FindIndex(x => x.DedupeKey() == r.DedupeKey());
            if (i < 0) { target.Add(r); continue; }
            if (target[i].Canonicalize() == r.Canonicalize()) continue;
            target[i] = r;
        }
    }
}

// ─── Certainty ──────────────────────────────────────────────────────────────

/// <summary>
/// How certain the asserting clinician is. Aligned 1:1 with FHIR
/// <c>Condition.verificationStatus</c> so USCDI export needs no translation table.
/// </summary>
public enum ProblemVerificationStatus
{
    /// <summary>
    /// Nobody stated a certainty — the honest default for legacy rows and imports.
    /// <b>Not a synonym for Confirmed.</b> Population queries must bucket it separately;
    /// folding it into "confirmed" would assert a clinical judgement nobody made.
    /// </summary>
    Unspecified = 0,

    /// <summary>Suspected; on the list to be worked up.</summary>
    Unconfirmed = 1,

    /// <summary>Probable — supporting evidence exists but a diagnostic criterion is not yet met.</summary>
    Provisional = 2,

    /// <summary>One of several competing hypotheses being carried at once.</summary>
    Differential = 3,

    /// <summary>
    /// A stated criterion was met. The criterion should itself appear as an
    /// <see cref="EvidenceRef"/> — "confirmed" is a conclusion, not an adjective.
    /// </summary>
    Confirmed = 4,

    /// <summary>
    /// Actively disproved. The assertion genuinely happened and was wrong, so it stays
    /// visible — and the patient stays in the denominator of "who was worked up for this."
    /// </summary>
    Refuted = 5,

    /// <summary>
    /// Should never have existed (wrong chart, duplicate keystroke). Not a clinical fact about
    /// this patient at all, so it leaves <b>both</b> numerator and denominator — the opposite
    /// treatment from <see cref="Refuted"/>, which is why one boolean cannot express both.
    /// </summary>
    EnteredInError = 6
}

// ─── Revision ───────────────────────────────────────────────────────────────

/// <summary>
/// Why a diagnostic assertion changed. The distinction between <see cref="Refinement"/> and
/// <see cref="Correction"/> is the one the whole feature rests on.
/// </summary>
public enum RevisionReason
{
    /// <summary>Not stated. Never inferred from context — see <see cref="RevisionSemantics"/>.</summary>
    Unspecified = 0,

    /// <summary>
    /// Same disease, finer code (diabetes → type 2 diabetes). The earlier assertion was
    /// <b>true at its level of resolution</b>. Not an error, and counting it as one would bury
    /// the real signal under ordinary good practice.
    /// </summary>
    Refinement = 1,

    /// <summary>
    /// The earlier assertion was <b>wrong</b>. This is the diagnostic-error signal, and the
    /// only reason that belongs in the numerator of a revision rate.
    /// </summary>
    Correction = 2,

    /// <summary>Was true; the disease has since changed (CKD 3 → 4, MGUS → myeloma).</summary>
    Progression = 3,

    /// <summary>Was true and has since resolved.</summary>
    Resolution = 4,

    /// <summary>The same condition recorded twice. Prevalence must count the patient once.</summary>
    Duplicate = 5,

    /// <summary>
    /// The <b>code set</b> changed — ICD-9 → ICD-10, or a proto-condition promoted to a real
    /// code. No clinical fact changed and no clinician was wrong.
    /// </summary>
    Recode = 6,

    /// <summary>Void — should never have been recorded. Excluded from every numerator and denominator.</summary>
    EnteredInError = 7,

    /// <summary>Moved between being a problem in its own right and a finding of another problem.</summary>
    Reclassification = 8,

    /// <summary>
    /// A non-clinical field was corrected (spelling, onset date typo). Exists so that ordinary
    /// edits stop being silent — today they are a full-object overwrite that emits no event.
    /// </summary>
    Amendment = 9
}

/// <summary>
/// The single place the statistical meaning of a <see cref="RevisionReason"/> is defined.
/// No consumer may re-derive it: the moment two call sites disagree about whether
/// <c>Refinement</c> counts as an error, the reported rate becomes unexplainable.
/// </summary>
public static class RevisionSemantics
{
    /// <summary>
    /// Whether the prior assertion remains true for the interval it was held.
    ///
    /// <c>true</c>  — still true; the earlier concept legitimately counts for that period.
    /// <c>false</c> — revoked; the earlier concept must not count.
    /// <c>null</c>  — unknowable. Callers <b>must</b> surface this as its own bucket and never
    ///                fold it into either answer; the system refuses to guess, and the size of
    ///                the unknown bucket is itself a reportable data-quality fact.
    /// </summary>
    public static bool? PriorAssertionRemainsTrue(RevisionReason reason) => reason switch
    {
        RevisionReason.Refinement
            or RevisionReason.Progression
            or RevisionReason.Resolution
            or RevisionReason.Recode
            or RevisionReason.Amendment
            or RevisionReason.Reclassification => true,

        RevisionReason.Correction
            or RevisionReason.Duplicate
            or RevisionReason.EnteredInError => false,

        _ => null
    };

    /// <summary>
    /// True when the revision means the record should leave both numerator and denominator —
    /// nothing clinically happened to this patient.
    /// </summary>
    public static bool IsNonEvent(RevisionReason reason)
        => reason == RevisionReason.EnteredInError;

    /// <summary>
    /// True when this revision is the diagnostic-error signal — the numerator of a revision rate.
    /// Deliberately narrow: only an explicit clinician-stated correction counts.
    /// </summary>
    public static bool CountsAsDiagnosticError(RevisionReason reason)
        => reason == RevisionReason.Correction;
}

// ─── Commands ───────────────────────────────────────────────────────────────

/// <summary>
/// A request to revise a diagnosis.
///
/// Deliberately a narrow command rather than a whole <see cref="ProblemEntry"/>: the replaced
/// <c>UpdateProblemAsync(ProblemEntry)</c> let a caller silently rewrite <c>ProblemId</c>,
/// <c>CreatedDate</c> and <c>DateRecorded</c> — the fields "when did this patient first get this
/// problem" is computed from. Those are not revisable, so they are not on this type.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record ProblemRevisionCommand
{
    [Id(0)] public string ProblemId { get; init; } = string.Empty;

    /// <summary>The diagnosis as it now stands.</summary>
    [Id(1)] public string Diagnosis { get; init; } = string.Empty;

    /// <summary>The code as it now stands.</summary>
    [Id(2)] public string? DiagnosisCode { get; init; }

    /// <summary>
    /// Why it changed. The caller is expected to have offered the clinician a default from
    /// <c>DiagnosisCodeRelation.Propose</c> — but what is stored is the clinician's choice.
    /// </summary>
    [Id(3)] public RevisionReason Reason { get; init; }

    /// <summary>The clinician's own words.</summary>
    [Id(4)] public string? Narrative { get; init; }

    /// <summary>Certainty after the revision.</summary>
    [Id(5)] public ProblemVerificationStatus VerificationStatus { get; init; }

    /// <summary>Evidence for the revised assertion. Replaces the prior list wholesale.</summary>
    [Id(6)] public List<EvidenceRef> Evidence { get; init; } = new();

    [Id(7)] public string? Condition { get; init; }
    [Id(8)] public string? Priority { get; init; }
    [Id(9)] public DateTime? DateOfOnset { get; init; }
    [Id(10)] public string? Comments { get; init; }

    /// <summary>New responsible provider, when the revision moves ownership.</summary>
    [Id(11)] public string? ResponsibleProviderId { get; init; }
    [Id(12)] public string? ResponsibleProviderName { get; init; }
}

/// <summary>
/// A request to record new evidence about an existing diagnosis without changing the diagnosis —
/// the workup proceeding. Never moves <c>RevisionNumber</c>.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record ProblemAssessmentCommand
{
    [Id(0)] public string ProblemId { get; init; } = string.Empty;

    /// <summary>Evidence to append, deduped by (kind, source, code).</summary>
    [Id(1)] public List<EvidenceRef> Evidence { get; init; } = new();

    /// <summary>Certainty after taking the new evidence into account.</summary>
    [Id(2)] public ProblemVerificationStatus VerificationStatus { get; init; }

    /// <summary>Optional clinician narrative.</summary>
    [Id(3)] public string? Narrative { get; init; }
}
