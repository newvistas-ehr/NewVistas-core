# Emerging-Condition Surveillance (ProtoCondition) — Design

Feature flag: `EMERGING_CONDITIONS` (Modern, on by default). Security key: `EPI MANAGER`. ADR: ADR-004.

## Grain / engine map

| Type | Key | Store | Role |
| --- | --- | --- | --- |
| `SymptomCatalog` (static) | — | — | Closed coded-symptom vocabulary + survey resolver + curated background |
| `IPatientSymptomGrain` | `SYMPTOMS:{patientId}` | `patientSymptomStore` | Append-only history + latest-per-code; fans out to cohort shards |
| `ISymptomCohortIndexGrain` | `SYMPTOM-COHORT:{code}` | `symptomCohortStore` | Present + Assessed sets (honest denominators) |
| `IProtoConditionGrain` | `PROTO:{guid}` | `protoConditionStore` | Definition, membership+evidence, guidance, alert, promotion, migration |
| `IProtoConditionIndexGrain` | `PROTOCONDITION-INDEX` | `protoConditionIndexStore` | Directory roll-up (counts, isolation, promoted code) |
| `IProtoCohortIndexGrain` | `PROTO-COHORT:{id}` | `protoCohortStore` | Confirmed-member shard (cohort algebra) |
| `ProtoConditionMatcher` (static) | — | — | Deterministic fixed-denominator scoring + evidence |
| `IProtoConditionScreeningGrain` | `PROTO-SCREEN:{patientId}` | — (StatelessWorker) | Snapshot assembly + Evaluate / EvaluateAndRecord |
| `IProtoSweepGrain` | `PROTO-SWEEP` | `protoSweepStore` | Explicit paged population sweep + targeted re-cluster |
| `ProtoConditionAnalytics` (static) | — | — | Feature lift (Signal/Noise), refinement suggestions, split co-occurrence |
| `IProtoAnalyticsGrain` | `PROTO-ANALYTICS:{id}` | — (StatelessWorker) | Assembles backgrounds, runs the analytics engine |

Workflow façade: `PatientWorkflowGrain.Symptoms.cs` (record/read symptoms) and
`PatientWorkflowGrain.EmergingConditions.cs` (suggest / confirm / exclude / migrate / skip +
the cover-sheet precaution-banner assembler). REST: `EmergingConditionsController` (`api/emerging-conditions`).
UI: `/emerging-conditions` (dashboard) and `/symptom-survey` (patient-scoped survey), "Surveillance" nav.

## Data flow

1. **Capture** — nursing/front-door records coded symptom answers (survey) → `IPatientSymptomGrain`
   → cohort shards (Present/Assessed).
2. **Screen** — `IProtoConditionScreeningGrain` assembles a `PatientFeatureSnapshot` (problems, labs,
   vitals, symptoms, demographics, exposures) → `ProtoConditionMatcher.Evaluate` → `ProtoMatchResult`.
   `EvaluateAndRecord` applies it to the proto's membership (Candidate). The sweep does this across the
   active population on explicit EPI action.
3. **Review** — the epidemiologist confirms/excludes candidates (evidence snapshot per member). Confirm
   adds to the cohort shard and evaluates the count-threshold alert inline.
4. **Close the net** — `IProtoAnalyticsGrain` reports each feature's cluster rate vs background lift
   (Signal / Noise / Insufficient) and suggests raises/drops; the epidemiologist edits the definition
   (bumps the version) and re-sweeps.
5. **Promote** — when a code arrives, `PromoteAsync` freezes the definition, expires candidates, queues
   confirmed members for problem-list recoding, and emits an eCR trigger. Migration adds the mapped
   problem (with a source citation) to each member's chart and notifies the primary provider.

## Scoring (matcher)

`score = Σ(satisfied weighted-feature weight) / Σ(all weighted-feature weight)`. HardInclude features
must all be satisfied; a satisfied HardExclude disqualifies. Unassessed features count 0 in the
numerator but stay in the denominator (fixed denominator → comparable across patients). Trinary symptom
semantics; unparseable numeric evidence → "not assessed", never guessed.

## Analytics (net-closing)

Cluster rate = present ÷ **assessed** over confirmed members. Lift = cluster ÷ background. Verdict:
Signal (lift ≥ 2, n ≥ 5, present ≥ 3), Noise (lift ≤ 1.3, guards met), else Insufficient. Documented
heuristics, not p-values. Backgrounds: assessed-population rate from the cohort shards, curated-catalog
fallback when < 10 assessed (labeled). Split evidence = pairwise anti-correlation of Signal features.

## Demo (seeded by `EmergingConditionSeed`)

- Proto `PROTO:outbreak-2019-resp` — Active, features fever/cough/anosmia/hearing/sore-throat, Droplet
  guidance, alert at 10 confirmed → QM3.
- Cluster P9201-P9213 (all fever+cough → all match); anosmia ~62% of the confirmed cohort (**Signal**),
  hearing at background (**Noise** plant). Nine pre-confirmed → the live 10th confirm fires the alert.
- P9214 — symptom-clean, for the survey → screen → candidate → confirm walk-through.
- P9220-P9249 — 30 controls, assessed (mostly absent) so denominators are honest.

Full arc: login **QM3** → candidates → confirm 10th (alert fires) → Analytics (anosmia Signal, hearing
Noise) → refine → sweep → login DOCTOR1 → P9214 survey → screen → QM3 confirms → P9214 cover sheet shows
the Droplet banner → Promote (COVID-19 / U07.1 / SNOMED 840539006) → migrate → member carries U07.1 with
a proto citation → eCR trigger exists → proto read-only.

## Explicitly out of scope (v1)

Timers (sweeps are explicit); AI/nursing symptom pre-fill (phase 2); public-health reporting-up beyond
the promotion eCR trigger (phase 2); registry instantiation at promotion (deferred to the disease-registry
template).
