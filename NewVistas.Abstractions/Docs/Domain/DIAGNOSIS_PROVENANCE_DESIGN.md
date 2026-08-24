# Diagnosis Provenance & Revision Statistics — Design

Concrete shapes for [ADR-006](../Architect-decisions/ADR-006-Diagnosis-Provenance-And-Revision.md).
Design only — not implemented. Flag `DIAGNOSTIC_STEWARDSHIP` (Modern, off by default).

## Grain / type map

| Piece | Key | Store | Role |
| --- | --- | --- | --- |
| `ProblemEntry` (existing) | embedded in `PatientState` | — | Head projection of the assertion chain |
| `IPatientClinicalEventStreamGrain` (existing) | per patient | existing | **The assertion chain** — domain `"PROBLEMS"` |
| `IDiagnosticEpisodeIndexGrain` | `DX-EPISODE:{patientId}` | `dxEpisodeStore` | Per-patient episode list (projection) |
| `IDiagnosisOutcomeIndexGrain` | `DX-OUTCOME:{granularity}:{codeKey}:{yyyy}` | `dxOutcomeStore` | Learned counter shard |
| `IDiagnosisOutcomeAnalyticsGrain` | `DX-OUTCOME-ANALYTICS:{code}` | *(none — `[StatelessWorker]`)* | Read side, merge + thresholds |
| `DiagnosticRevisionCatalog` | — | — | Curated baseline (`Clinical/`, pure static) |
| `DiagnosisCodeRelation` | — | — | Pure prefix rule proposing refinement vs revision |

Store registration (note `CLAUDE.md` is stale here): add `dxEpisodeStore` and `dxOutcomeStore` to
`NewVistas.SiloHost/Program.cs` `AllStoreNames`, **plus** explicit `AddMemoryGrainStorage` lines in
both `NewVistas.FunctionalTests/SharedCluster.cs` and `NewVistas.UnitTests/SharedCluster.cs`. Missing
any of the three is a runtime activation failure, not a compile error.

## 1. `ProblemEntry` additions

Existing ids 0 and 2–19 are unmoved. New fields start at 20.

```csharp
// [Id(1)] RETIRED — former ProblemState.PatientId (string), removed when problems
// became embedded. Rows persisted under the old shape still carry a string here.
// NEVER REUSE.

[Id(20)] public string AssertionId { get; set; } = string.Empty;
[Id(21)] public int RevisionNumber { get; set; }
[Id(22)] public ProblemVerificationStatus VerificationStatus { get; set; }
[Id(23)] public List<EvidenceRef> Evidence { get; set; } = new();
[Id(24)] public string? SupersedesProblemId { get; set; }
[Id(25)] public string? SupersededByProblemId { get; set; }
[Id(26)] public RevisionReason? LastRevisionReason { get; set; }
[Id(27)] public string? LastRevisionNarrative { get; set; }
```

`Clone()` gains all eight. `Evidence` copies as `new List<EvidenceRef>(Evidence)` — a shallow list
copy is correct because `EvidenceRef` is an immutable record, and `Clone()` exists to stop the event
snapshot aliasing live state.

`ProblemSummary` (`CoverSheetState.cs`) gains `[Id(7)] ProblemVerificationStatus VerificationStatus`
— the minimum needed so "suspected" does not render identically to "confirmed" on a cover sheet.

### Default semantics — why migration writes nothing

| Field | Default | Reads as |
| --- | --- | --- |
| `AssertionId` | `""` | No assertion event ever observed |
| `RevisionNumber` | `0` | No assertion event observed — **not** "revision 1" |
| `VerificationStatus` | `Unspecified` | Nobody stated a certainty — **not** "confirmed" |
| `Evidence` | `[]` | No evidence was ever *recorded* — **not** "no evidence existed" |
| `LastRevisionReason` | `null` | Never revised — distinct from `Unspecified` ("revised, unstated") |
| `SupersededByProblemId` | `null` | Not superseded |

## 2. Evidence types

```csharp
public enum EvidenceKind
{
    Unspecified = 0,
    Symptom = 1,          // SYMPTOMS:{patientId}; Code is a SymptomCatalog SNOMED code
    LabResult = 2,        // LAB-{guid}; Code is LOINC
    Vital = 3,
    Imaging = 4,          // RAD-{guid} | IMG-{guid}
    Note = 5,             // TIU-{guid} — the note containing the reasoning
    Medication = 6,       // RX-{guid} — response, or non-response, as evidence
    Problem = 7,          // PROB-{guid} — another problem as evidence (the blurry-line edge)
    Procedure = 8,
    Genomic = 9,
    ExternalRecord = 10,  // outside record, no local id; SourceId null, Note carries the citation
    ClinicalJudgment = 11,// exam finding / gestalt. SourceId null, and that is honest
    ProtoCondition = 12   // PROTO:{guid}
}

public enum EvidencePolarity
{
    NotAssessed = 0,   // never checked — a POSITIVE record of a gap
    Supports = 1,
    Refutes = 2,       // assessed, argues against — an informative negative
    Indeterminate = 3, // equivocal: QNS, uninterpretable film, borderline
    Stale = 4          // a result exists but outside the useful window for this assertion
}

[GenerateSerializer, Immutable]
public sealed record EvidenceRef
{
    [Id(0)]  public EvidenceKind Kind { get; init; }
    [Id(1)]  public string? SourceId { get; init; }        // null only for ClinicalJudgment / ExternalRecord
    [Id(2)]  public string? CodeSystem { get; init; }      // "LOINC" | "SNOMED" | "ICD-10" | "CPT" | "RxNorm"
    [Id(3)]  public string? Code { get; init; }
    [Id(4)]  public string Display { get; init; } = string.Empty;
    [Id(5)]  public EvidencePolarity Polarity { get; init; }
    [Id(6)]  public string? ObservedValue { get; init; }   // verbatim: "88", "NEGATIVE" — never a sentence
    [Id(7)]  public string? ObservedUnit { get; init; }
    [Id(8)]  public DateTime? ObservedUtc { get; init; }   // when OBSERVED, not when cited
    [Id(9)]  public bool IsMachineCited { get; init; }
    [Id(10)] public string? FeatureId { get; init; }       // ProtoFeature.FeatureId when from the matcher
    [Id(11)] public string? Note { get; init; }            // qualifier only; never structured facts

    public string Canonicalize() => string.Join("^", /* every field, in id order */);
}
```

`NotAssessed = 0` makes the default honest: a default-constructed ref never claims a finding.

**Hazard.** `SymptomCatalog` is closed and `PatientSymptomGrain` *silently drops* non-catalog codes.
A `Kind = Symptom` ref must be resolved through `IPatientSymptomGrain` before construction; if the
symptom is not there, reject the ref rather than write a dangling code.

**`FeatureContribution` migration.** Do not change that type. Add a pure mapper used at proto
promotion: `Assessed == false → NotAssessed`; `Assessed && Satisfied → Supports`;
`Assessed && !Satisfied → Refutes`; the pre-rendered `Evidence` string goes into `Note` unparsed;
`FeatureId` carries across. This replaces the prose sentence in
`PatientWorkflowGrain.EmergingConditions.cs`.

## 3. Certainty and revision

```csharp
public enum ProblemVerificationStatus   // 1:1 with FHIR Condition.verificationStatus
{
    Unspecified = 0,   // legacy/import. NOT a synonym for Confirmed — bucket separately
    Unconfirmed = 1,   // suspected; on the list to work up
    Provisional = 2,   // probable; supporting evidence, criterion not met
    Differential = 3,  // one of several competing hypotheses carried at once
    Confirmed = 4,     // met a stated criterion — and the criterion is an EvidenceRef, not an adjective
    Refuted = 5,       // actively disproved. The assertion happened and was wrong; it stays visible
    EnteredInError = 6 // should never have existed. Not a clinical fact about this patient
}

public enum RevisionReason
{
    Unspecified = 0,      // not stated. Never inferred from context
    Refinement = 1,       // same disease, finer code. TRUE at its level of resolution. NOT an error
    Correction = 2,       // the earlier assertion was WRONG — the diagnostic-error signal
    Progression = 3,      // was true; the disease has since changed (CKD 3→4, MGUS→myeloma)
    Resolution = 4,       // was true and has resolved
    Duplicate = 5,        // same condition twice; prevalence must count the patient once
    Recode = 6,           // the CODE SET changed (ICD-9→10, proto promoted). No clinical fact changed
    EnteredInError = 7,   // void. Excluded from EVERY numerator AND denominator
    Reclassification = 8, // moved between problem and finding-of-another-problem
    Amendment = 9         // non-clinical field corrected. Exists so ordinary edits stop being silent
}

/// The ONLY place the statistical meaning of a reason is defined. No consumer re-derives this.
public static class RevisionSemantics
{
    /// true  — prior assertion remains true for its interval; earlier concept still counts
    /// false — prior assertion is revoked; earlier concept must NOT count
    /// null  — unknowable. Callers MUST surface as its own bucket, never fold into either answer
    public static bool? PriorAssertionRemainsTrue(RevisionReason r) => r switch
    {
        Refinement or Progression or Resolution or Recode or Amendment or Reclassification => true,
        Correction or Duplicate or EnteredInError                                          => false,
        _                                                                                  => null
    };

    public static bool IsNonEvent(RevisionReason r) => r == RevisionReason.EnteredInError;
}
```

`Refuted` and `EnteredInError` must be treated **oppositely**: a refuted diabetes workup means the
patient *was screened* and belongs in the screening denominator; an entered-in-error row means
nothing happened to this patient at all and must leave both numerator and denominator. A single bool
cannot express that.

### The proposal rule

```csharp
public static class DiagnosisCodeRelation
{
    // Blunt and deliberate: same 3-character ICD-10 category ⇒ never counted as a revision.
    // Handles laterality/encounter suffixes (S72.001A→D) and sibling specificity.
    // Correctly flags E11.9 → E10.9 (type 2 → type 1) as a real revision.
    // Over-forgives G43.909 → G44.1 — the safe direction.
    public static DiagnosticEpisodeOutcome Propose(string from, string to) =>
        from == to                    ? Confirmed
      : to.StartsWith(from)           ? Refined      // E11 → E119
      : from.StartsWith(to)           ? Broadened    // E119 → E11
      : Cat3(from) == Cat3(to)        ? Refined      // E119 → E1165
      :                                 Revised;
}
```

The proposal pre-selects a radio button. **The clinician's choice is what is counted.**

## 4. Event payloads

Under `Events/Clinical/Problems/`, domain `"PROBLEMS"`, `[GenerateSerializer, Immutable] sealed
record : IClinicalEvent`, ids 0–5 reserved for interface members, payload from `[Id(6)]`.

| Event | Payload beyond ids 0–5 |
| --- | --- |
| `ProblemAssertedV1` | `ProblemId`, `Snapshot`, `VerificationStatus`, `Evidence`, `DerivedFrom` |
| `ProblemRevisedV1` | `ProblemId`, `Snapshot`, `RevisionNumber`, `Reason`, `Narrative`, `VerificationStatus`, `Evidence`, `PriorDiagnosis`, `PriorDiagnosisCode`, `PriorVerificationStatus` |
| `ProblemAssessedV1` | `ProblemId`, `Evidence`, `VerificationStatus`, `PriorVerificationStatus`, `Narrative` |
| `ProblemSupersededV1` | `SupersededProblemId`, `SupersedingProblemId`, `Reason`, `Narrative`, `EffectiveUtc` |
| `ProblemEnteredInErrorV1` | `ProblemId`, `Reason` (required, non-empty) |
| `ProblemBaselineImportedV1` | `ProblemId`, `Snapshot`, `BaselineSource`, `ClaimedRecordedDate` |

`ProblemRevisedV1` denormalizes the prior diagnosis/code so **one envelope answers "what changed"
without walking back** — necessary under federation, where the earlier envelope may not have arrived.

`ProblemAddedV1` becomes read-only legacy: replay keeps handling it, nothing new emits it. It cannot
simply gain the new fields because its `Canonicalize()` enumerates specific properties, so provenance
on the snapshot would sit **outside the hash** — a persisted `VerificationStatus` could be flipped
`Unconfirmed → Confirmed` and `VerifyChainAsync()` would still pass.

**Canonicalizing an evidence list.** `Canonicalize()` joins with `|` and `EvidenceRef` with `^`, so
free text containing either separator is ambiguous. Hash each ref and join the hex instead:

```csharp
private string EvidenceCanon() =>
    string.Join(",", Evidence.Select(x => HashChain.Compute(x.Canonicalize(), string.Empty)));
```

### Replay — `PatientStateSnapshot.Apply`

**Invariant: replay never invents a head.** Only `ProblemAssertedV1`, `ProblemAddedV1` and
`ProblemBaselineImportedV1` may create.

| Event | Behaviour |
| --- | --- |
| `ProblemAssertedV1` | If id present, return. Else add `Snapshot.Clone()`; set `AssertionId`, `RevisionNumber = 1`, `VerificationStatus`, `Evidence` |
| `ProblemRevisedV1` | Find head; **absent ⇒ return, do not create**. Copy clinical fields; set revision fields; **replace** `Evidence` wholesale. Never overwrite `ProblemId`, `CreatedDate`, `DateRecorded` — those anchor incidence |
| `ProblemAssessedV1` | Find head or return. **Append** evidence deduped by `(Kind, SourceId, Code)`. Set `VerificationStatus`. **Do not touch `RevisionNumber`** — an assessment is not a revision |
| `ProblemSupersededV1` | Find both heads independently; apply whichever half is present |
| `ProblemEnteredInErrorV1` | Find head; **never remove**. Set `VerificationStatus = EnteredInError`, `Status = "INACTIVE"` |
| `ProblemBaselineImportedV1` | If id present, return. Else add `Snapshot.Clone()`. **Must NOT set** `RevisionNumber`, `AssertionId` or `VerificationStatus` — the import asserts nothing clinical |

Two notes. `Status = "INACTIVE"` on a correction is semantically imperfect (it implies resolved) but
deliberate: every current consumer filters on that string, and a corrected diagnosis that stays
ACTIVE keeps displaying as current — a patient-safety failure. The string is the legacy safety
projection; `VerificationStatus` carries the truth. Separately, `RecentEventIds` caps at 1000, so on
a long replay a duplicate beyond that window re-applies — which is why the `ProblemAssessedV1` dedupe
is a correctness requirement, not a nicety.

### Grain-layer changes

- **Delete** `IPatientGrain.RemoveProblemAsync` → `MarkProblemEnteredInErrorAsync(problemId, reason)`.
- **Delete** `IPatientGrain.UpdateProblemAsync` → `ReviseProblemAsync` / `AssessProblemAsync` taking
  command records (the current signature also lets a caller silently rewrite `ProblemId` and
  `CreatedDate`).
- Only callers today are tests — take the breaking change now rather than leave the traps armed.
- `GetActiveProblemsAsync` additionally excludes `Refuted` and `EnteredInError`.
- New read: `GetProblemProvenanceAsync(problemId)` folds `ReadByDomainAsync("PROBLEMS", …)` into a
  `ProblemProvenance` DTO. A projection type, not persisted state.

## 5. Episode projection

```csharp
public enum DiagnosticEpisodeOutcome
{
    Open = 0,                     // not yet adjudicated — in NO denominator
    Confirmed = 1,
    Revised = 2,                  // ← THE numerator
    Refined = 3,                  // NOT an error — stays in the denominator
    Broadened = 4,                // NOT an error — stays in the denominator
    ResolvedWithoutAlternate = 5,
    ClosedUnadjudicated = 6       // lost to follow-up — in NEITHER numerator nor denominator
}
```

`DiagnosticEpisode` holds `EpisodeId`, `ProblemId`, normalized `WorkingCode`, `AssertedUtc`,
`EvidenceAtAssertion`, `Outcome`, `OutcomeCode`, `AdjudicatedUtc`, `NewEvidence`,
`AbnormalAmongNewEvidence`, `OutcomeNote`, `AdvisoryWasShown`, and `ReportedToShardUtc`.

Two details carry weight:

- **Test keys are namespaced** — `"L:{loinc}"`, `"R:{cpt}"`, `"C:{consultService}"`. LOINC and CPT
  number spaces overlap; an unprefixed `72148` is ambiguous.
- **`ReportedToShardUtc` is the at-most-once guard.** `PayerProcedureRequirementIndexGrain` has no
  such guard and gets away with it — a double-counted denial is cosmetic. A double-counted
  misdiagnosis is not. The guard lives patient-side so the shard stays a pure counter with no
  unbounded dedupe set.

`NewEvidence` is windowed to `DeltaWindowDays = 30` before adjudication, so a long-running episode
does not absorb an unrelated workup.

## 6. Shard

Key `DX-OUTCOME:{granularity}:{codeKey}:{yyyy}` — `granularity ∈ CODE | CAT | ALL`, `codeKey`
normalized (dots stripped, upper-cased, alphanumeric only, so a plain `Split(':')` is safe, unlike
`PAYER-PROC` which needed the last-colon trick), `yyyy` = **assertion** year. Three shards written
per adjudication; self-parsing key on activation, per house idiom.

```csharp
public class DiagnosisOutcomeState
{
    [Id(0)]  string CodeKey;  [Id(1)] DiagnosisCodeGranularity Granularity;  [Id(2)] int AssertionYear;

    // Denominator ladder — Revised ⊆ Adjudicated ⊆ Asserted
    [Id(3)]  int AssertedCount;
    [Id(4)]  int AdjudicatedCount;          // = Confirmed+Revised+Refined+Broadened+ResolvedWithoutAlternate
    [Id(5)]  int ConfirmedCount;
    [Id(6)]  int RevisedCount;              // numerator
    [Id(7)]  int RefinedCount;              // NOT an error
    [Id(8)]  int BroadenedCount;            // NOT an error
    [Id(9)]  int ResolvedWithoutAlternateCount;
    [Id(10)] int ClosedUnadjudicatedCount;  // neither numerator nor denominator

    // Exposure control — the REPORTED rate uses these
    [Id(11)] int AdjudicatedUnexposedCount;
    [Id(12)] int RevisedUnexposedCount;
    [Id(13)] DateTime? LastUnexposedRecordedUtc;

    [Id(14)] List<DiagnosisRevisionStat> RevisedTo;
    [Id(15)] List<DiscriminatorStat> Discriminators;
    [Id(16)] List<UnmappedOutcomeNote> UnmappedOutcomes;   // deduped by text; kept, never dropped
    [Id(17)] HashSet<string> AdjudicatingProviderIds;      // COUNT ONLY — see below
    [Id(18)] DateTime LastRecordedUtc;
    [Id(19)] int NosTerminatingRevisedCount;               // revisions landing on unspecified/NOS codes
}
```

`AdjudicatingProviderIds` exists solely to report a distinct count. **No method may expose a
per-provider breakdown** — the omission is deliberate; document it on the class so nobody "fixes" it.

```csharp
public class DiscriminatorStat
{
    [Id(0)] string TestKey;  [Id(1)] DiagnosticTestKind Kind;  [Id(2)] string Display;

    [Id(3)] int NewInRevised;                  [Id(4)] int NewInNotRevised;
    [Id(5)] int NewAndAbnormalInRevised;       [Id(6)] int NewAndAbnormalInNotRevised;
    [Id(7)] int AlreadyPresentAtAssertion;     // clinician HAD it and still got there wrong

    // Exposure-partitioned twins — the advisory naming T drives T's own lift up otherwise
    [Id(8)]  int NewInRevisedUnexposed;        [Id(9)]  int NewInNotRevisedUnexposed;
    [Id(10)] int NewAndAbnormalInRevisedUnexposed;
    [Id(11)] int NewAndAbnormalInNotRevisedUnexposed;
    [Id(12)] DateTime LastSeenUtc;
}
```

### Scoring

```
RevisionRate = RevisedUnexposedCount / AdjudicatedUnexposedCount

revisedRate    = NewInRevisedUnexposed    / RevisedUnexposedCount
notRevisedRate = NewInNotRevisedUnexposed / (AdjudicatedUnexposed − RevisedUnexposed)
Lift           = revisedRate / notRevisedRate

Verdict = Signal        when Lift ≥ SignalLift (2.0)
          Noise         when Lift ≤ NoiseLiftCeiling (1.3)
          Insufficient  otherwise, or below any min-N
```

A universally ordered CBC yields lift ≈ 1.0 → `Noise` → suppressed. That is the whole defence
against learning that the CBC diagnoses everything.

**Change detection**: score the current bucket against prior buckets, not only pooled. Pooling gives
power but hides a sudden change, and a change is what an emerging problem looks like.

### Thresholds

```csharp
MinAdjudicatedForRate      = 20   // vs ProtoAnalytics' 10 — a noisy rate makes a doctor doubt a
                                  // correct diagnosis. Higher stakes, higher floor
MinRevisionsForAlternative = 3
MinRevisedForDiscriminator = 5
MinNotRevisedForComparison = 5    // both arms required
MinDistinctProviders       = 2    // one idiosyncratic clinician must not BE the statistic
MinAdjudicationCoverage    = 0.50 // below this, Insufficient — adjudication bias
MinRateToReport            = 0.10
SignalLift                 = 2.0  // REUSED verbatim
NoiseLiftCeiling           = 1.3  // REUSED verbatim
LearnedRateHoldoutFraction = 0.10 // keeps an unexposed arm growing. NEVER applies to Critical baseline
```

Escalation ladder in the analytics grain, relabelling provenance at each step: `CODE` shards over the
window → widen to `CAT` → cold start (no percentage rendered at all) → always fetch `ALL` for the
site-wide comparison → `DiagnosticRevisionCatalog.Merge(baseline, profile, siteWide, generatedAt)`.

## 7. Curated baseline

`DiagnosticRevisionCatalog` (`Clinical/`), house style: private `Rule[]`, static-ctor duplicate
check, `GetBaseline`, pure `Merge` — mirroring `PriorAuthRequirementCatalog`.

```csharp
private sealed record Rule(
    string WorkingCategory,     // 3-char ICD-10, e.g. "R42"
    string AlternativeCode, string AlternativeDisplay,
    string[] DiscriminatorKeys, // "R:70553", "C:NEUROLOGY"
    string DiscriminatorDisplay,
    DiagnosticHarmIfMissed Harm,
    string Citation);
```

**The baseline carries the arrow and the citation. It never carries a percentage.** Payer policy is a
policy fact and can be hand-authored; a misdiagnosis *rate* is site-specific epidemiology, and
hand-authoring one fabricates clinical evidence. This is also what solves the rare-disease problem:
the min-N floor gates only the *learned* percentage, so `Critical` baseline lines render at n = 0 and
sort to the top. Dizziness → posterior stroke will never reach n = 20 at one clinic, and that is
exactly where silence would be most harmful.

Sources: SIDM/AHRQ "Big Three" (vascular events, infections, cancers — ~75% of serious
misdiagnosis-related harm), the AHRQ 2022 ED diagnostic-error evidence review, and established
decision rules (HINTS, Wells/PERC, HEART, Centor, Ottawa). ~15–25 rules — enough that the demo is
never empty, small enough to defend individually.

| Working | Alternative | Discriminator | Harm |
| --- | --- | --- | --- |
| R42 dizziness | I63 cerebral infarction | HINTS exam; MRI brain w/ DWI | Critical |
| R07.9 chest pain | I21 MI | hs-troponin; 12-lead ECG | Critical |
| N39.0 UTI | A41.9 sepsis | lactate; blood culture | Critical |
| M54.5 low back pain | C79.51 / M46.2 | ESR; MRI lumbar (`R:72148`) | Critical |
| G43 migraine | I60 SAH | non-contrast head CT | Critical |
| J18.9 pneumonia | I50 heart failure | NT-proBNP | Serious |
| R10.9 abdominal pain | K35 appendicitis | CT abd/pelvis w/ contrast | Serious |
| F32 depression | E03.9 hypothyroidism | TSH | Routine |

`R:72148` already appears in `PriorAuthRequirementCatalog` — the two catalogs cross-link for free
("consider MRI lumbar" → "here is what this payer requires to approve it").

## 8. DTO and display contract

`DiagnosisRevisionAdvisory` carries `AdjudicatedCount` and `RevisedCount` as **required**;
`RevisionRate` is `double?` and **secondary**. Also: `Band`, `RateProvenance`, `SiteWideRevisionRate`,
`LiftOverSiteWide`, `Alternatives`, `SuggestedTests`, `IsColdStart`, `RefinedCount`, `BroadenedCount`,
`StillOpenCount`, `ClosedUnadjudicatedCount`, a non-suppressible `Disclaimer`, `Transparency`
(reusing `DsiPredictiveTransparency`), `GeneratedAt`.

```csharp
public enum RevisionRateBand
{
    Insufficient = 0, // below a floor, coverage too low, or too few providers
    Typical      = 1, // lift over site-wide ≤ 1.3
    Borderline   = 2, // 1.3 < lift < 2.0 — DELIBERATELY not forced into a verdict
    Elevated     = 3  // lift ≥ 2.0 AND rate ≥ MinRateToReport
}
```

**Binding on every DTO in this feature:**

1. Counts are primary and mandatory; the percentage is derived and optional. Render *"revised in 9 of
   41 adjudicated episodes at this site"*, not *"22%"*.
2. Nothing shaped like sensitivity/specificity reaches a screen; unfold into the thousand-person story
   first. The frequency tree is the canonical visual.
3. Discriminator phrasing states what the counters contain — *"arrived before the revision in 9 of 12
   revised episodes vs 11 of 66 not-revised"* — **never** *"this test would have gotten you there."*
   Reverse causation is unfixable from observational data; the phrasing is the mitigation.
4. `RevisionRate` is `null` whenever `Band == Insufficient`. Never render a number you do not have.

Rationale: Casscells (1978) ~18% correct on a basic predictive-value question, modal answer 95%
against a correct ~2%; Manrai (2014) ~23%, median still 95%; Gigerenzer on natural frequencies. The
representation is the fix, not education.

## 9. Write path

New partial `PatientWorkflowGrain.DiagnosticStewardship.cs`. Writes occur **only** through the façade,
where security-key and audit filters fire.

- `OpenDiagnosticEpisodeAsync` — from `AddProblemAsync`; snapshots evidence-at-assertion; increments
  `AssertedCount` on the three assertion-year shards. Feature-gated; no-op when off.
- `AdjudicateDiagnosticEpisodeAsync` — the single counting write. Propose via
  `DiagnosisCodeRelation` → accept the clinician's outcome → compute the windowed delta → write the
  episode → fan out to `CODE`/`CAT`/`ALL` for the **assertion** year → stamp `ReportedToShardUtc`.
- `BulkRecodeAsync` — writes `RevisionReason.Recode` across a code-set migration and **touches no
  numerator**. Without it, remapping `B34.2 → U07.1` at scale teaches the shard that `B34.2` is wrong
  100% of the time.
- `InactivateProblemAsync` **prompts** for adjudication; never forces. A required field yields
  default-clicked garbage, which is worse than missing data — the honest-denominator discipline exists
  precisely to tolerate non-response.

Evidence snapshots read `IPatientLabIndexGrain`, whose `LabIndexEntry` already carries `LoincCode`,
`ResultDate` and `AbnormalFlag` — no new read path. That index uses the legacy 5-minute
`EnsureIndexFresh` pattern; irrelevant here since snapshots are days apart. Noted so nobody converts
it to a version-based reader for no reason.

Backfill over imported patients: an operator-triggered `DX-STEWARDSHIP-SWEEP` grain modelled on
`ProtoSweepGrain` — paged `GetAllPatientIdsAsync`, per-patient try/catch, bounded run history, **no
timer** (ADR-004). Backfill may only *open* episodes and count `AssertedCount`; it must never
synthesize outcomes, since imported data carries no clinician adjudication. The resulting low coverage
ratio correctly forces `Insufficient` — the guard working as designed on day one.

## Explicitly out of scope (v1)

- Per-presentation conditioning (see ADR-006 Non-goals); per-(X→Y) discriminator breakdowns, which
  would be empty at any realistic scale — revisit at per-pair n ≥ 30.
- Streaming medians for days-to-revision — needs the raw distribution the shard refuses to keep. Mean,
  honestly labelled.
- Per-provider concentration ratios beyond a distinct count.
- A raw revision event store — counters only, per the `PAYER-PROC` precedent.
- Confidence intervals or chi-square on the lift. The codebase has zero p-values by deliberate policy;
  a CI here would imply rigour the data does not have.
- A SNOMED/ICD hierarchy grain — the 3-char prefix rule plus clinician override is free and sufficient.
- Timer-driven refresh — forbidden by ADR-004.
- Cross-site federation of counters. Out of scope, but the counters-only, no-PHI shape makes it
  federatable later, which is the eventual answer to rare-disease silence.
