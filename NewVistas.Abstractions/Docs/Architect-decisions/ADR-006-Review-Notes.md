# ADR-006 — Review Notes (Diagnosis Provenance & Revision Statistics)

**Status:** Review input — not an ADR. This document accompanies the proposed ADR-006 and does not
occupy a number in the decision sequence. Produced 2026-08-11 from a design-review session (Cowork)
with James; written as input for the session that finalizes the ADR-006 design documents.

**Verdict:** the proposed design is sound and should keep its shape. The three-structure split
(assertion chain in the existing hash-chained stream / per-patient episode projection / population
counter shards), the `Polarity = NotAssessed` positive record of an unperformed check, the two-armed
lift, the `Revised ⊆ Adjudicated ⊆ Asserted` denominator ladder, the adjudication-rate gate, and the
no-invented-history migration stance are all correct — preserve them. The findings below are the four
places the review asked for changes or explicit decisions, plus two extensions worked out in the same
session.

## Finding 1 (required): exposure control must also cover DiscriminatorStat

The `AdvisoryWasShown` / unexposed-arm mechanism protects the revision **rate** from the feedback
loop, but the discriminator counters have no arms of their own. The loop is just as real there: the
advisory names test T → clinicians order more T → T precedes more revisions → T's lift
self-reinforces. Partition `DiscriminatorStat` counts by the same exposure flag; the episodes are
already being walked for the denominator, so the cost is near zero. Treat as a required fix, not an
enhancement.

## Finding 2 (decide now): post-freeze policy for the unexposed arm

`LastUnexposedRecordedUtc` makes the freeze visible but is not a policy. Once the advisory shows
everywhere, the unexposed arm stops growing and the reported rate ossifies into a snapshot of
pre-advisory practice. Two defensible options; the ADR should pick one explicitly:

1. Render the vintage on the advisory itself ("based on practice through 2027") and accept
   ossification, or
2. Hold out a small random fraction of eligible episodes from display as an instrumented rollout.
   The holdout withholds only an unvalidated descriptive statistic, which is the accepted standard
   for exactly that; note the ethical reasoning in the ADR either way.

Choosing later means choosing under pressure.

## Finding 3 (largest concern): advisory timing — assertion, not adjudication only

"Pull at the point of adjudication" surfaces the statistics when the clinician is already revising —
the advisory arrives to congratulate the correction. The motivating case for this entire feature
(chief complaint dropped from a workup; the discriminating exam never done) needs the collapsed,
pull-only line at **assertion** for the minority of diagnoses whose revision rate clears the
threshold. The self-suppressing gate is the alert-fatigue control that makes assertion-time display
safe. If assertion-time display is rejected, the ADR must state prospective warning as an explicit
non-goal so nobody believes the system does something it doesn't.

## Finding 4 (name the scope cut): per-diagnosis, not per-presentation

The shard key (`DX-OUTCOME:{granularity}:{code}:{yyyy}`) conditions on diagnosis only. The advisory
can say "UTI is revised to sepsis N% of the time" but not "in patients presenting like this." That is
the right v1 trade (it dodges cohort collapse), but it is the difference between epidemiology and
advice, and it should be a stated non-goal with the extension path named: presentation-signature-keyed
shards, hierarchical (signature → chief-complaint category → diagnosis-only fallback), added only
after structured symptom capture at intake exists.

## Consequences material: the novel-disease story

Worked through as a stress test ("what happens when COVID-19 walks in?"); recommended for the ADR's
Consequences section because it demonstrates the design's failure mode is the safe one.

- **Patient #1 (no code exists):** the system fails by *silence, not invention*. The clinician's
  Provisional assertion and the Refutes-polarity flu test are recorded structurally; the population
  layer correctly says nothing — a counting system cannot hallucinate a disease it has never seen.
  The existing emerging-cluster machinery plus the `Recode` RevisionReason makes the early journeys
  retroactively legible when the code finally exists, without backdating events — the migration
  honesty principle paying off in a second place.
- **Patient #200 (code + test exist):** live local revision rates are computable the week they become
  true, months ahead of literature. The discriminator lift tracks the new test's lifecycle without
  any rule updates: scarce-and-discriminating → high lift → advisory; universally ordered → lift ≈ 1
  → correct silence.
- **Acknowledged limits:** the adjudication-rate gate will force `Insufficient` during surge chaos
  (correct — silence beats garbage — but say so); the first year's bucket is permanently polluted by
  pre-code months (contained by assertion-year bucketing, not cleaned). Refutation-rate spikes by
  time and geography are additionally a public-health surveillance signal; the system already does
  electronic case reporting.

## Extension (candidate ADR-007): the proto-condition living lab profile

For a proto/emerging condition at n ≈ 50–100, clinicians are ordering broadly and most results come
back normal. The right deliverable is not a "these tests are unhelpful" warning but a **living lab
profile attached to the proto-condition itself** (pull-based, a panel on the existing proto-condition
view), because a test given to a novel-disease cluster has three distinct fates:

1. **Uniformly normal** — itself a finding (a signature/rule-out feature: normal procalcitonin in
   early COVID argued "viral, not bacterial"), not the absence of information;
2. **Uniformly abnormal** — the disease's emerging lab profile, exactly what patient #101's
   clinician should inherit from the first hundred;
3. **Variable but predicting nothing** — the only truly unhelpful category, and the hardest to
   establish.

**Silence ends claim-by-claim, not at one global threshold:**

- *Uniformly-normal claims* gate on the **rule of three**: zero abnormals in n patients puts the 95%
  upper bound on the true abnormal rate at ≈ 3/n. Render when the bound drops below 10% (n ≈ 30),
  and render the bound itself: "within range in 62 of 62; true abnormal rate ≤ 5% (95%)."
- *Positive/severity claims* gate on the existing min-N floors, lift thresholds, and `Insufficient`
  band. **Never move a test to the "silent" column until the outcome-stratified (severe-subgroup)
  view clears its own min-N** — at n = 80 the severe subgroup may be n = 6, and the test may be
  abnormal precisely there. When the subgroup is too small, say so on the panel.

**Guards:** counts keyed to a cluster-definition version (material redefinition restarts them;
episodes can be re-walked to recompute); day-of-illness bucketing where onset is capturable (normal
on day 2 ≠ normal on day 7); vintage on every line (the profile shapes ordering, which freezes the
profile); display the count of tests examined (multiple-comparisons honesty). Across a federation:
share profiles, never pool patients — a sibling site's profile beside ours, not a merged number
belonging to no population.

## Display contract (binding on all DTOs in this feature): natural frequencies only

Every statistic shown to a clinician or patient is a **count of people first** — "17 of 78 patients,"
percentage in parentheses if at all. Nothing shaped like "sensitivity 90% / specificity 95%" reaches
a screen; it is unfolded into the thousand-person story ("of 1,000 people, 1 has it and tests
positive; ~50 of the 999 without it also test positive; so 1 of 51 positives is real") before
display. Rationale: physicians reliably fail at conditional probabilities (Casscells 1978: ~18%
correct on a basic predictive-value question, modal answer 95% vs. correct ~2%; Manrai 2014
replication: ~23% correct, median still 95%) and reliably succeed with natural frequencies
(Gigerenzer) — the representation, not education, is the fix. The counters already store counts
natively; the percentage is the derived and dangerous form. This is consistent with the ADR's
existing phrasing rule for discriminators ("arrived before the revision 4.1× more often" → prefer
"in 9 of 12 revised episodes vs. 11 of 66 not-revised"). The frequency tree (1,000 people splitting
sick/well, then positive/negative) is the canonical visual whenever a test must be explained.

## Cross-references

- Proposed ADR-006 (Diagnosis Provenance & Revision Statistics) — the document these notes review.
- ADR-004 (Emerging Conditions Surveillance) — the proto-condition machinery the lab profile
  extends; `Recode` provenance from proto clusters is the novel-disease on-ramp.
- `Portal-Outreach-Concept-Paper.docx`, `Diagnosis-Journey-Concept-Paper.docx` (repo root) — the
  prose companions; the second paper describes this feature for clinical readers and contains the
  safeguards framing reviewers responded to.
