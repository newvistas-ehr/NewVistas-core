# ADR-006 — Diagnosis Provenance & Revision Statistics

**Status:** Proposed — design only, not implemented (2026-08)

Reviewed against [`ADR-006-Review-Notes.md`](ADR-006-Review-Notes.md), which validated the
three-structure shape and raised four findings; all four are incorporated below.

## Context

A diagnosis and a symptom are not different kinds of thing. They are the same concept playing
different roles: diabetes is a diagnosis on its own problem-list line and a *finding* supporting
diabetic nephropathy on another. Patients also receive invalid diagnoses routinely — and the system
today cannot represent that at all.

Three facts about the current implementation constrain everything that follows:

1. **The problem list has no history.** `PatientGrain.UpdateProblemAsync` is a silent full-object
   overwrite that emits no event; `RemoveProblemAsync` is a hard delete with no tombstone. Only
   `AddProblemAsync` and `InactivateProblemAsync` are event-sourced, so **event replay and live grain
   state already disagree permanently** for any chart where a problem was edited or removed. A
   diagnosis code can be changed from `E11.9` to `C34.90` and the hash chain shows nothing.

2. **Provenance is English prose.** `PatientWorkflowGrain.EmergingConditions.cs` writes
   *"Recoded from emerging cluster '{name}' (proto {id}) on promotion to {dx}."* into
   `ProblemEntry.Comments`; `PatientWorkflowGrain.Sdoh.cs` does the same with a screening id. Both
   embed a **real identifier inside a sentence** — unnavigable, uncountable, and impossible to
   invalidate when the source record is retracted.

3. **No causal link exists between problems.** `ProblemInactivatedV1` carries only a `ProblemId` and
   a `DateResolved`. X→Y cannot be recovered from temporal adjacency: *"influenza inactivated
   Tuesday, fractured radius added Wednesday"* is not a revision. Any design that mines the problem
   list for adjacent inactivate/add pairs manufactures a misdiagnosis rate out of noise and then
   shows it to a clinician — the worst available failure of this feature.

The goal is to record what the diagnosis is, what evidence led to it, when it changed and why — and
then to aggregate across patients so a clinician can be told: *this working diagnosis is revised
often; it usually turns out to be Y; the result that most often preceded that revision is test T.*

## Decision

Three structures, because there are three different questions. Flag-gated
(`DIAGNOSTIC_STEWARDSHIP`, Modern, **off by default**).

| Layer | Structure | Where it lives |
| --- | --- | --- |
| **Provenance** | Assertion chain — each assertion cites structured evidence and states a certainty | The **existing** hash-chained clinical event stream, domain `"PROBLEMS"` |
| **Episode** | `(working diagnosis → outcome)` with evidence-at-assertion and evidence-delta | `DX-EPISODE:{patientId}` — a projection, not a second truth |
| **Population** | Counter shards with a denominator ladder and a two-armed lift | `DX-OUTCOME:{granularity}:{codeKey}:{yyyy}` |

### 1. The assertion chain is the event stream we already built

`ProblemEntry` remains the **head projection**. History lives in
`IPatientClinicalEventStreamGrain` — already SHA-256 chained, already replayable as-of-a-date via
`ReplayUntilAsync`, already federation-replicating, already filtered by the `"PROBLEMS"` domain.

A second persisted chain (nested `List<ProblemAssertion>`, or a `PROBLEMHX:{patientId}` grain) is
rejected: it would be a second source of truth that can disagree with the first, which is precisely
the defect described in Context §1. `PatientState` is also an 85-field aggregate deserialized on the
cover-sheet hot path; a patient with 40 problems × 5 assertions × 4 evidence refs would put ~800
nested records on every read for no benefit the stream does not already provide.

New head fields begin at `[Id(20)]`. **`[Id(1)]` is permanently retired** — the class numbering jumps
0→2 because that slot held a `string PatientId` before problems became embedded (commit `9bc23279`).
Any row ever persisted under the old shape has a string there. Reusing the id would silently corrupt
deserialization, and this is exactly the case the serialization rules in `CLAUDE.md` warn about.

### 2. `EvidenceRef` — structured provenance, including what was never checked

`EvidenceRef` replaces prose with a navigable citation carrying kind, source record id, code system
and code, the value **as recorded**, observation time, and a polarity.

`EvidencePolarity` collapses the two booleans of `FeatureContribution` (`Satisfied`, `Assessed`) into
one enum — `NotAssessed(0) | Supports | Refutes | Indeterminate | Stale`. This makes the meaningless
state `(Assessed = false, Satisfied = true)` unrepresentable, and promotes *stale* and *indeterminate*
— currently buried in pre-rendered strings like `"stale result (2019-11-02)"` — to first-class values.

The rule that earns the whole design:

> **An `EvidenceRef` with `Polarity = NotAssessed` is a positive record that a check was not done.**

Absence from the evidence list means nothing. Presence with `NotAssessed` means *we know we did not
look*. Without that distinction, "eight negative etiologic tests" and "eight untested possibilities"
are indistinguishable in the record — and they are opposite clinical signals. It is also what makes
*"what fraction of our type-2 diabetes assertions were made without an HbA1c"* answerable at all;
today it is unanswerable in either direction.

### 3. The blurry line: role is relational, not intrinsic

**No `IsSymptom` flag.** The same concept is a diagnosis and a finding *simultaneously*, so a flag
forces a false global choice. The relation needs no new field:

> `EvidenceRef { Kind = Problem, SourceId = "PROB-{diabetes}", Polarity = Supports }` on the
> nephropathy assertion **is** the statement "diabetes is a finding of this."

Diabetes remains a full problem with its own onset, status and billing code, *and* is a citation on
another problem. One fact, one representation, both roles.

Certainty uses a single `ProblemVerificationStatus` aligned 1:1 with FHIR
`Condition.verificationStatus` (`Unspecified | Unconfirmed | Provisional | Differential | Confirmed |
Refuted | EnteredInError`), so USCDI export needs no translation table. `Unspecified(0)` is the
honest legacy default and **is not a synonym for Confirmed**; population queries must bucket it
separately.

Rejected: a second `DiagnosticCertainty` enum alongside it (creates combinations unrepresentable in
FHIR, e.g. `Confirmed` + `Refuted`); a float confidence score (invented precision — clinicians do not
emit calibrated probabilities); and a `bool IsEnteredInError` mirroring `AllergyEntry [Id(19)]`
(conflates "this record is void" with "this assertion was disproved," which are *statistically
opposite* — see §5).

### 4. Refinement is not correction

`RevisionReason` is a closed vocabulary: `Unspecified | Refinement | Correction | Progression |
Resolution | Duplicate | Recode | EnteredInError | Reclassification | Amendment`. A single helper,
`RevisionSemantics.PriorAssertionRemainsTrue(reason) → bool?`, is the only place the statistical
meaning is defined; it returns `null` for `Unspecified` so the system never guesses and the unknown
bucket stays reportable.

Conflating refinement with correction breaks the feature in **both** directions:

- File refinements as corrections and the error rate is dominated by *good* clinical practice —
  specificity rising as a workup proceeds — burying the real signal.
- File corrections as refinements — the socially comfortable direction, since nobody enjoys recording
  *"I was wrong"* — and the rate goes to zero and the system reports that it never misdiagnoses
  anyone.

It also separates *what was true* from *what was believed*. Auditing a decision made in June 2024
requires knowing what was believed **then**; the drug started may have been entirely reasonable on
the evidence available. So a `Correction` must not retroactively erase the belief. Only the coded
distinction lets one chain answer both questions: belief = replay everything; truth = replay
excluding assertions later revoked.

**Who decides: the system proposes, the clinician confirms.** A pure string function over ICD-10
prefixes (same 3-character category ⇒ `Refinement`) pre-selects a default; the clinician's choice is
what gets counted. This handles laterality and encounter suffixes (`S72.001A → S72.001D`) and sibling
specificity for free, correctly flags `E11.9 → E10.9` (type 2 → type 1 diabetes) as a real revision,
and over-forgives in the safe direction. No SNOMED/ICD hierarchy grain is required — `Icd10State` has
no parent pointer and prefix matching makes one unnecessary.

Counting *clinician judgement that they were wrong*, rather than a machine's opinion, is also what
makes the statistic defensible and the feature socially survivable.

### 5. Population — the denominator ladder

```
Revised  ⊆  Adjudicated  ⊆  Asserted
```

One level deeper than `SymptomCohortState`'s `Present ⊆ Assessed`. An **Open** episode has not yet
had the chance to be revised; a **ClosedUnadjudicated** episode (lost to follow-up, transferred)
never will. Neither belongs in the denominator. Dividing by `Asserted` would punish careful
clinicians who properly leave a working diagnosis open — exactly the error that dividing by
total-population instead of assessed-population would be.

`Refined` and `Broadened` stay **in** the denominator: they were adjudicated and the verdict was "not
an error." This deflates the reported rate, which is the safe direction — the system's bias should be
toward *not* telling a clinician they were wrong.

Shards are keyed `DX-OUTCOME:{granularity}:{codeKey}:{yyyy}` at three granularities (`CODE`, `CAT`,
`ALL`) and bucketed by **assertion year**, not adjudication year, so a bucket means "the diagnostic
practice of 2026."

Write-hotness is *not* the reason to bucket, and the ADR should not pretend otherwise: a
50,000-patient practice generates roughly 3 writes per day on its commonest working diagnosis, cooler
than the existing `PAYER-PROC` shard. The reason is **clinical validity**. Diagnostic criteria move —
Sepsis-2 → Sepsis-3 (2016), the 2017 ACC/AHA hypertension threshold, CKD-EPI dropping the race
coefficient (2021), high-sensitivity troponin changing ACS rule-out. A 2016 revision rate for a code
is evidence about a *different definition* of that code. An unbucketed shard averages across
definitional changes silently and forever.

Reads fan in over a bounded window (current year + 4 prior) and **score the current bucket against
prior buckets**, not merely pooled. A pooled window gives statistical power but destroys change
detection: 200 anomalous cases against four normal years may never clear a lift of 2.0, and a *change*
is what an emerging problem looks like.

### 6. "Try test T" — derived from data, with a control arm

At adjudication the episode already holds the evidence present at assertion and the evidence that
arrived afterwards. The candidate discriminators for a revision are exactly the results that arrived
*between* "I think it's X" and "actually it's Y", windowed to 30 days before adjudication so an
eight-month episode does not absorb the annual physical.

**The trap:** every worked-up patient receives a CBC and a metabolic panel, so raw counting learns
that the discriminating test for everything is a CBC.

**The fix costs nothing:** count the same delta for the adjudicated-but-**not**-revised episodes and
score by lift. That comparison arm is free because those episodes are already being walked for the
denominator. `SignalVerdict`, `SignalLift = 2.0` and `NoiseLiftCeiling = 1.3` are reused **verbatim**
from `ProtoConditionAnalytics` so one meaning holds site-wide, and the 1.3–2.0 band stays deliberately
`Insufficient` rather than being forced into a verdict.

**Reverse causation is unfixable from observational data.** The troponin does not discriminate ACS
from GERD; the clinician who ordered the troponin already suspected ACS. This cannot be corrected
statistically, so the mitigation is the *phrasing*, and it is binding on the DTO: never *"this test
would have gotten you there,"* only *"in revised episodes this result arrived before the revision in
9 of 12 cases, against 11 of 66 confirmed episodes."* That is literally what the counters contain.

### 7. Feedback control — both arms

Once the advisory says "this is often actually PE", clinicians diagnose more PE, the shard observes
more PE revisions, the advisory says it louder, and the statistic has trained on its own output.

The control is an **exposure-partitioned denominator**: the episode records whether the advisory was
shown, the shard keeps unexposed counters in parallel, and **the reported rate is computed over the
unexposed arm only**. The exposed arm is still counted — it is the effect-measurement arm, and the
difference between the two is itself honest and interesting.

**This must extend to the discriminator counters, not only the rate.** The loop is identical there:
the advisory names test T → clinicians order more T → T precedes more revisions → T's lift
self-reinforces. Every discriminator arm is partitioned by the same exposure flag. The episodes are
already being walked, so the cost is near zero. This is a requirement, not an enhancement.

**Post-freeze policy.** As the advisory reaches everyone the unexposed arm stops growing and the
reported rate ossifies into a snapshot of pre-advisory practice. Making that visible is not a policy,
so the decision is taken here: **hold out the learned rate from a small random fraction (~10%) of
otherwise-eligible episodes** so an unexposed arm keeps accruing.

The ethical justification is an asymmetry that must be preserved in implementation: **curated
baseline lines marked `Critical` are never held out.** A held-out clinician still receives every
literature-backed safety pairing; only the *local learned percentage* is withheld. What is withheld
is an unvalidated descriptive statistic, which is precisely what an instrumented rollout holdout
exists for — not a safety warning.

### 8. Where the advisory appears

**Primary display is at assertion**, as a collapsed pull-only line — not exclusively at adjudication.
Adjudication-time display arrives to congratulate the correction; the clinical value of this feature
(*consider a lactate before committing to UTI*) exists only at the moment the working diagnosis is
being made. The self-suppressing gate **is** the alert-fatigue control: only diagnoses whose revision
rate clears the threshold ever display, so assertion-time surfacing is rare by construction.

The advisory is also available on demand at adjudication and via an explicit "second look" action.

The single push case is a `Critical` curated baseline rule at cold start — dizziness → posterior
circulation stroke, where the literature is strong and local data is irrelevant. It reuses
`ProtoAlertRule`'s high-water-mark plus cooldown, keyed per (provider, code) with a 7-day cooldown,
never more than once per encounter. Every fire and override routes through `DsiEventGrain`, so the
override rate is measurable and the site can gate the feature off when it exceeds 80%. A clinical
decision support feature that cannot measure its own override rate has no business firing.

### 9. Display contract — natural frequencies, binding on every DTO

- Counts are **primary and mandatory**; the percentage is derived, secondary and optional.
  *"17 of 78 patients"*, with a percentage in parentheses if at all.
- **Nothing shaped like "sensitivity 90% / specificity 95%" reaches a screen.** It is unfolded into
  the thousand-person story before display.
- The frequency tree (1,000 people splitting sick/well, then positive/negative) is the canonical
  visual whenever a test must be explained.

The rationale is empirical, not stylistic. Physicians reliably fail conditional-probability questions
— Casscells (1978) found ~18% correct on a basic predictive-value question with a modal answer of 95%
against a correct answer near 2%; Manrai (2014) replicated at ~23% correct with the median answer
still 95% — and reliably succeed with natural frequencies (Gigerenzer). The representation is the
fix, not education. Our counters already store counts natively; the percentage is the derived and
dangerous form.

### 10. Migration invents nothing

Every new head field's CLR default already reads as *unknown*: `AssertionId` empty means no assertion
event was ever observed; `RevisionNumber = 0` means the same and is **not** "revision 1";
`VerificationStatus = Unspecified` means nobody stated a certainty; an empty `Evidence` list means no
evidence was ever *recorded*, not that none existed; `LastRevisionReason = null` means never revised,
distinct from `Unspecified` ("revised, reason unstated"). Head-state migration is therefore **zero
lines of code**.

Three temptations are forbidden:

1. **Backfilling `RevisionNumber = 1`** — asserts a deliberate assertion where many rows are ZWR
   imports from `^AUPNPROB`, i.e. a copy of someone else's list, not an assertion made here.
2. **Parsing `Comments` prose into `EvidenceRef`s** — the proto id is *right there* in the sentence
   and regex-able, which is exactly why not: a machine-inferred citation is indistinguishable from a
   clinician's once written, and it launders a guess into the legal record.
3. **Synthesizing back-dated assertion envelopes** — forges hash-chained records with fabricated
   timestamps and actors.

`ProblemBaselineImportedV1` is the one honest event: its `OccurredUtc` is the migration run time,
never the row's claimed date, and it asserts only *"on this date we observed this row, which claims
the following."*

**Bulk recode.** `RevisionReason.Recode` exists for code-set changes, but a bulk application path must
be specified with it. When `U07.1` shipped in 2020, every prior `B34.2` row needed remapping; the
prefix rule proposes `Revised` for `B34 → U07`, so accepting defaults at scale would teach the shard
that `B34.2` is wrong 100% of the time and poison it permanently. A coded bulk recode operation writes
`Recode` and touches no numerator.

## Security & gating

Flag-gated (`DIAGNOSTIC_STEWARDSHIP`, **off by default**, matching `EMERGING_CONDITIONS`). When the
flag is off, reads return an empty snapshot and writes are rejected with a clear error, so a site that
does not want the module sees nothing of it.

The point-of-care advisory requires only `GMPL PROBLEM` (the key already held by anyone editing a
problem list). The population/stewardship view requires `EPI MANAGER`. Writes occur only through the
`PatientWorkflowGrain` façade, where the security-key and audit filters already fire.

**Not a leaderboard.** The shard stores a *set* of adjudicating provider ids solely to report a
distinct count, and exposes no method capable of producing a per-provider breakdown. The omission is
deliberate and is documented on the state class so the next reader does not "fix" it.

Registered as an HTI-1 §170.315(b)(11) **predictive** DSI (the conservative call — a rate learned from
local episodes is a relationship derived from example data), reusing `DsiPredictiveTransparency`. The
`PerformanceMetrics` field states plainly: *"None. This is a descriptive frequency over local
episodes, not a validated predictive model. No sensitivity, specificity, or AUROC has been
established."*

## Non-goals

Stated explicitly so nobody believes the system does something it does not:

- **Per-presentation conditioning.** The shard keys on the diagnosis alone. It can report *"UTI was
  revised to sepsis in 9 of 41 adjudicated episodes"* but never *"in patients presenting like this
  one."* This is the right v1 trade — presentation-keyed shards collapse to n=1 cohorts — but it is
  the difference between epidemiology and advice, and it is the structural cause of the base-rate
  hazard in §9. Extension path: presentation-signature-keyed shards, resolved hierarchically
  (signature → chief-complaint category → diagnosis-only fallback), added only once structured symptom
  capture at intake exists.
- **Prospective warning beyond the threshold set.** Only diagnoses clearing the revision-rate gate
  display at assertion. Everything else is silent by design.
- **Automatic proto-condition creation.** Detection and assembly may be automated; publication to a
  chart stays human (see Forward references).
- **Etiological discovery.** As with ADR-004, this is local operational triage. No p-values, no
  confidence intervals on the lift, no claim of causality.

## Consequences

- New: `EvidenceRef` and its enums, `ProblemVerificationStatus`, `RevisionReason` +
  `RevisionSemantics`, eight fields on `ProblemEntry`, six event payloads under
  `Events/Clinical/Problems/`, six replay branches, a `DX-EPISODE:{patientId}` grain, a
  `DX-OUTCOME:…` shard, a `[StatelessWorker]` analytics grain, a curated `Clinical/` catalog, and a
  workflow façade partial. Two `IPatientGrain` methods are **deleted** (`UpdateProblemAsync`,
  `RemoveProblemAsync`) and replaced with emitting equivalents; their only callers today are tests.
- `GetActiveProblemsAsync` must additionally exclude `Refuted` and `EnteredInError`. The coarse
  `Status` string remains the safety filter every existing consumer already honours
  (`iCareDashboardGrain`, cover sheet, NDW export); `VerificationStatus` carries the truth alongside
  it.
- The existing divergence between replay and live state (Context §1) is closed as a side effect. This
  is worth stating: the feature's first deliverable is a bug fix.

### The novel-disease story

Worked as a stress test — *what happens when COVID-19 walks in?* — and recorded here because it
demonstrates that the failure mode is the safe one.

**Patient #1, before any code exists.** The system fails by *silence, not invention*. The clinician's
`Provisional` assertion, the `Refutes`-polarity influenza PCR and the `NotAssessed` rows for what was
never checked are all recorded structurally. The population layer correctly says nothing: a counting
system cannot hallucinate a disease it has never seen. The curated baseline is a closed list of known
pairings and is therefore structurally blind to novelty — it will unhelpfully but harmlessly suggest
the pneumonia → heart-failure rule. What the chart retains is enough that when the code finally
exists, `Recode` makes the early journeys retroactively legible **without backdating a single event**
— the migration-honesty principle paying off in a second place.

**Patient #200.** Live local revision rates become computable the week they become true, months ahead
of literature. The discriminator lift tracks a new test's lifecycle with no rule updates:
scarce-and-discriminating → high lift → advisory; universally ordered → lift ≈ 1 → correct silence.
The signal available is *"influenza is revised at several times the site rate; the result most often
preceding revision is a **negative** influenza PCR; the outcome is usually an unspecified viral
pneumonia code"* — which is, in the only vocabulary the system has, *we are systematically wrong and
we do not know what this is.*

**Acknowledged limits.** During surge chaos the adjudication-coverage gate forces `Insufficient`, and
that is correct — a rate computed from a 30%-adjudicated sample is biased toward the memorable cases
and silence beats garbage at the bedside. But the *combination* of coverage falling while the
revision rate rises is itself information, so it is emitted as a separately typed
**diagnostic-instability signal**: no percentage attached, routed to surveillance and epidemiology
rather than to the treating clinician. The first year's bucket is permanently polluted by the
pre-code months; assertion-year bucketing contains that, it does not clean it. Refutation-rate spikes
by time and geography are additionally a public-health surveillance signal — cross-link the existing
electronic case reporting (`EcrScreeningGrain` / `EcrCaseGrain`).

## Forward references (candidate ADR-007)

Named here, designed elsewhere:

1. **Proto-condition living lab profile.** For a cluster at n ≈ 50–100, clinicians order broadly and
   most results return normal. The deliverable is not a "these tests are unhelpful" warning but a
   pull-based living lab profile on the proto-condition view, because a test given to a novel-disease
   cluster has three distinct fates: uniformly normal (itself a finding — normal procalcitonin in
   early COVID argued *viral, not bacterial*), uniformly abnormal (the emerging lab profile patient
   #101's clinician should inherit), and variable-but-predicting-nothing (the only truly unhelpful
   category, and the hardest to establish). Silence ends claim-by-claim: uniformly-normal claims gate
   on the rule of three (zero abnormals in n puts the 95% upper bound at ≈ 3/n; render at n ≈ 30 and
   show the bound), while positive and severity claims gate on the existing min-N floors — and **no
   test moves to the "silent" column until the severe-subgroup view clears its own min-N**, because
   at n = 80 the severe subgroup may be n = 6 and the test may be abnormal precisely there.

2. **Auto-assembled proto-condition drafts.** ADR-004 requires a human to *notice* a cluster before
   defining it; nobody noticed for weeks in late 2019. The detectable signature is
   negative-workup-convergence-failure: confirmed abnormal physiology, an etiologic panel returning
   `Refutes` across the board, and no assertion ever reaching `Confirmed` — a signature expressible
   only because of §2's `Refutes`-versus-`NotAssessed` distinction. The system may auto-detect and
   auto-**assemble** a draft cluster with members, shared features and honest denominators, queued for
   `EPI MANAGER` review. It must never auto-**publish**: the line is drawn at visibility to the
   treating clinician, because naming a cluster creates its population — once a named entity appears
   on a cover sheet, ambiguous cases are attributed to it, which grows the cohort, which appears to
   confirm it. Traps: a season-adjusted baseline (or the system drafts every November), and a check
   against known rare-disease signatures before proposing novelty.

## Cross-references

- [`ADR-006-Review-Notes.md`](ADR-006-Review-Notes.md) — the design review these decisions incorporate.
- [`ADR-004-Emerging-Conditions-Surveillance.md`](ADR-004-Emerging-Conditions-Surveillance.md) — the
  proto-condition machinery; `Recode` provenance from a promoted cluster is the novel-disease on-ramp.
- [`DIAGNOSIS_PROVENANCE_DESIGN.md`](../Domain/DIAGNOSIS_PROVENANCE_DESIGN.md) — concrete class
  shapes, event payloads, replay table, shard grammar, thresholds.
- `Diagnosis-Journey-Concept-Paper.docx`, `Portal-Outreach-Concept-Paper.docx` (repo root) — the prose
  companions; the first describes this feature for clinical readers.
