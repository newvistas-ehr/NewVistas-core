# ADR-004 — Emerging-Condition Surveillance (ProtoCondition)

**Status:** Accepted — implemented (2026-07)

## Context

Almost no EHR can help with a *new* disease. In early COVID (or early AIDS) a hospital
must keep treating and billing before a code exists — you name and bill what you *can*
("respiratory failure") while the actual thing is still "a bunch of symptoms and tests."
The clinical process is a scientific method: cast a wide net (ask about smell, hearing,
sensation; order broad tests; flu comes back negative) → compare unknown-etiology
patients → find which features are statistically shared (hearing change: background;
smell change: 60% → significant) → the net closes → maybe the cluster splits → eventually
someone isolates the pathogen, a test exists, a code is issued (U07.1), and "having it"
becomes a positive test.

The system already contained three half-built ancestors of this idea — PCC Surveillance
(a case definition with no matcher), eCR screening (a working config-driven matcher +
eICR generation), and the Lab Surveillance Taxonomy (a codes→condition vocabulary) — plus
a proven cohort algebra (`DrugClassCohortIndexGrain` + the drug-safety advisory dispatch).
What was missing: **coded symptom capture** (nowhere in the system could a clinician record
"anosmia: present/absent" — chief complaint/HPI are free text, so a dismissed symptom is
*invisible*, not *absent*), a **matcher engine**, **net-closing analytics**, a **versioned
hypothesis lifecycle**, and **promotion-to-code**.

## Decision

Add a flag-gated module (`EMERGING_CONDITIONS`, Modern, on by default) — the **ProtoCondition**:
a living, versioned cluster of patients, signals, and hypotheses representing an emerging
disease pattern *before* it has an ICD/SNOMED code. Temporariness is the design — on
promotion the definition freezes and the hypothesis hands off to the coded pipeline.

### 1. Structured symptom capture (the missing input)

- `SymptomCatalog` (`Clinical/`) — a curated closed vocabulary of coded symptoms (SNOMED
  primary + ICD-10-R cross-ref, 9 body systems, a core "wide net" screen, curated
  background prevalence). Prevalence math is meaningless over free codes, so the vocabulary
  is closed.
- `IPatientSymptomGrain` (`SYMPTOMS:{patientId}`) — append-only history + latest-per-code
  projection. Answers are **trinary: Present / Absent / Unknown** — "asked and absent" is a
  real, informative negative; "never asked" is not. You cannot subtract what was never
  asked, which is exactly why structured beats free-text-with-AI.
- `ISymptomCohortIndexGrain` (`SYMPTOM-COHORT:{code}`) — reverse shards holding **two** sets,
  Present and Assessed, so every rate divides by "patients we actually asked."

### 2. The ProtoCondition (`IProtoConditionGrain`, `PROTO:{guid}`)

One unified `ProtoFeature` list across symptom / lab / vital / diagnosis / demographic /
exposure, each with an operator, a weight, and a rule (Weighted / HardInclude / HardExclude).
Membership lives on the grain (Candidate / Confirmed / Excluded) with **full per-feature
evidence snapshots** — including unassessed rows, so the survey knows what to go ask.
Invariants (in `UpsertEvaluationAsync`): stale-version results are dropped; **Excluded is
never resurrected by the machine; Confirmed is never silently reversed** (a no-longer-matching
confirmed member is flagged for re-review); a machine candidate that stops matching is removed,
a human-suggested one persists. `DefinitionVersion` bumps only on matching-semantics edits.

### 3. Deterministic, explainable matcher (`Clinical/ProtoConditionMatcher.cs`)

No ML. "Why is this patient here" must answer with a feature list, so every feature yields a
contribution with quoted evidence, and unparseable / unmeasured values are reported as "not
assessed" — never guessed. Scoring uses a **fixed denominator** (satisfied weight ÷ all
weight) so scores are comparable across patients regardless of how much data each had.

### 4. Net-closing analytics (`Clinical/ProtoConditionAnalytics.cs`)

Feature lift over the confirmed cohort using **assessed denominators**, background from the
assessed population (curated-catalog fallback when too thin, labeled as such), documented
lift heuristics with minimum-N guards (Signal / Noise / Insufficient — explicitly not
p-values), refinement suggestions (raise / drop), and pairwise anti-correlation as split
evidence. This is **local operational triage** (who to isolate, the working case definition
for the front door today) and an input to a future public-health report — NOT etiological
discovery, which is the CDC's job across sites.

### 5. Workflow hooks & promotion

A cover-sheet precaution banner (non-suppressible spine, confirmed members only), a
count-threshold alert (fires once as the confirmed count crosses the threshold, cooldown-gated),
and promotion — which freezes the definition, expires candidates, queues confirmed members for
problem-list recoding (with a source citation), and emits an **eCR trigger** so newly coded
encounters flow into the official reporting pipeline the system already has.

## What we explicitly did NOT do

- **No universal per-disease grains.** Coded diseases already have homes (ICD/SNOMED identity,
  order sets, clinical/diabetes/cancer registries, reminders). ProtoCondition is justified
  precisely because *no code exists*; on promotion it becomes a historical artifact.
- **No timers.** A sweep is an explicit epidemiologist action (`IProtoSweepGrain`, `PROTO-SWEEP`).
  Introducing the codebase's first timer-driven population job deserves its own decision.
- **No public-health reporting-up in v1** beyond the promotion-time eCR trigger. The
  pre-promotion structured cluster-report and the NSSP / FHIR-MedMorph export seams are phase 2.
- **No AI symptom capture in v1.** Structured survey is primary; nursing-assessment auto-routing
  and AI pre-fill are phase 2 (the `Source` enum is already extensible for them).

## Security

New key **`EPI MANAGER`** (granted to the Administrator role → demo persona QM3 CAMPBELL,DIANE S,
Infection Preventionist) gates create / edit / refine / promote / sweep / confirm / exclude.
Surfacing a patient (Suggest) is a lighter clinician action (`PROVIDER` / `ORELSE` / `EPI MANAGER`).
Recording symptoms is a front-door action (`PROVIDER` / `ORES` / `ORELSE` / `EPI MANAGER`).
**Reads are open** — surveillance is part of the chart, not a privacy silo (the Oncology / Home-Care
model).

## Consequences

- ~11 new grain types (≈2% growth) + two curated `Clinical/` engines, ≥70% reuse of existing
  patterns; one new capture surface clinicians must actually use (a workflow burden no code solves);
  the first population-sweep operational pattern (explicit, not timer-driven).
- Two pre-existing debts are paid down: the system gains its first coded symptom/ROS capture, and
  PCC surveillance finally has a matcher shape to follow.

Registry instantiation at promotion is deferred; the disease-registry template mechanism is the
named follow-on target.
