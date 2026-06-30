# Pharmacogenomics (PGx) — Design

> **Status: IMPLEMENTED & smoke-verified (2026-06-30).** Closes the #1 gap from the genetics survey:
> the system had robust *somatic/tumor* genomics (precision oncology) but **no pharmacogenomics** —
> the drug-interaction/DUR engine was genotype-blind. This module stores coded gene results and wires
> drug-gene decision support into the existing DUR engine, so a contraindication like *"CYP2C19 poor
> metabolizer → clopidogrel"* surfaces at prescribing time.

## Overview

A patient's **pharmacogenomic profile** is the set of coded gene results (star-allele **diplotype** +
CPIC **phenotype**) that come back from a genotyping lab. NewVistas stores these as discrete data —
not raw sequence — exactly as the genetics blueprint
([genetics-and-family-modeling.md](genetics-and-family-modeling.md)) prescribes:

> *"Pharmacogenomic star alleles — `CYP2C19 *2/*17`, DPYD variants, TPMT status — as discrete codes so
> CDS can fire … 'don't prescribe clopidogrel — patient is a CYP2C19 poor metabolizer.'"*

For the tribal/IHS market the blueprint frames the workflow as **referral-out, results-back** (no
in-house sequencing): the EHR records the interpreted result and *acts on it*. That is precisely this
module — a coded result store feeding a curated **CPIC/FDA knowledge base**, read by the **DUR engine**
so drug-gene checks run alongside drug-drug, drug-allergy, renal/hepatic, and duplicate-therapy checks.

The matcher is **read-only decision support** — it never auto-orders or auto-cancels anything; an
Avoid/Contraindicated pairing holds the fill in the existing DUR review queue for pharmacist override.

## Access model

Reads + writes are **open**, gated by one **`PHARMACOGENOMICS` site feature flag** (Modern, on by
default for demos). This matches the neonatal/OB open posture, not the oncology key-gate. Genetic data
is sensitive, so a **write key** (e.g. a `PGX MANAGER` lab/pharmacy key) is a reasonable future
hardening — deliberately not added here to keep the module self-contained. The DUR PGx check is a
no-op when the flag is off (and when the patient has no PGx results).

---

## Grain set

| Grain | Key | Store | Purpose |
|---|---|---|---|
| `IPharmacogenomicsGrain` | patient id | `pharmacogenomicsStore` | The patient's PGx profile — one coded result per gene (upsert by gene) |

No index grain — one profile per patient, read directly. The DUR engine reads it during
`PerformDurAsync`.

### `PharmacogenomicsState`

- `PatientId`, `List<PgxResultEntry> Results`, `CreatedDate`, `LastModifiedDate`
- `PgxResultEntry` — `ResultId`, `Gene` (e.g. "CYP2C19", "HLA-B*57:01"), `Diplotype` ("*2/*2"),
  `Phenotype` (`PgxPhenotype`), `ActivityScore` (CYP2D6), `Status` (Pending/Final/Superseded),
  `TestDate`, `Lab`, `Method`, `Notes`, `RecordedBy`, `RecordedDate`
- `PgxPhenotype` — the CPIC phenotype vocabulary spanning the metabolizer scale (Ultrarapid → Rapid →
  Normal → Intermediate → Poor), function levels (Increased/Normal/Decreased/Poor — transporters/
  enzymes), allele presence (Positive/Negative — HLA), and G6PD (Deficient/Variable)

### `Clinical.Pharmacogenomics` — the curated knowledge base

Deterministic, rule-based, CPIC/FDA-aligned and illustrative (the same "model the data, curate the
rules" pattern as [`PrecisionOncology`](../../Clinical/PrecisionOncology.cs)). A representative set of
high-impact **CPIC Level-A** drug-gene pairs — *not* the full guideline corpus:

| Gene | Drugs | Trigger phenotype → action |
|---|---|---|
| CYP2C19 | clopidogrel, voriconazole | PM → avoid clopidogrel; IM → consider alternative; PM → adjust voriconazole |
| CYP2D6 | codeine, tramadol | UM/PM → avoid (resp. depression / no analgesia) |
| CYP2C9 | warfarin, phenytoin | PM/IM → genotype-guided lower dose (with VKORC1 for warfarin) |
| DPYD | fluorouracil, capecitabine | PM → avoid (fatal toxicity); IM → reduce ~50% |
| TPMT / NUDT15 | azathioprine, mercaptopurine, thioguanine | PM → alternative/drastic reduction; IM → reduce + monitor |
| G6PD | rasburicase, dapsone, primaquine | Deficient → contraindicated / avoid (hemolysis) |
| SLCO1B1 | simvastatin | poor/decreased function → alternative statin / dose limit (myopathy) |
| UGT1A1 | irinotecan | PM → reduce (neutropenia) |
| HLA-B\*57:01 | abacavir | Positive → contraindicated (hypersensitivity) |
| HLA-B\*15:02 | carbamazepine, oxcarbazepine | Positive → avoid (SJS/TEN) |
| HLA-B\*58:01 | allopurinol | Positive → avoid (SCAR) |

- `PgxActionCategory` (escalating): Standard < UseWithCaution < AdjustDose < ConsiderAlternative < Avoid < Contraindicated
- `PgxRecommendationStrength`: CPIC Strong / Moderate / Optional / NoRecommendation
- `Match(results)` → all drug-gene implications for the profile (worst action first) — the patient's "drugs to watch"
- `MatchDrug(results, drugName)` → recommendations for one drug (used by the DUR engine + a "check a drug" lookup); normalizes brand→generic (Plavix→clopidogrel, …) and gene symbols (HLA-B\*57:01 ↔ HLAB5701)

---

## DUR integration (the differentiator)

The drug-gene check is a first-class **DUR check**, not a bolt-on. `DurCheckType` gains
`Pharmacogenomic`, and `PerformDurAsync` (PatientWorkflowGrain.DUR.cs) adds check #12: it loads the
patient's PGx profile and calls `Pharmacogenomics.MatchDrug(profile, drugName)`:

- **no profile** → `NotApplicable` ("No pharmacogenomic results on file")
- **no actionable pairing** → `Pass`
- **Avoid / Contraindicated** → `Fail` (holds the fill in the DUR review queue; pharmacist-overridable
  via the existing `OverrideDurCheckAsync`, exactly like a drug-drug interaction)
- **AdjustDose / ConsiderAlternative / UseWithCaution** → `Warning`

The result flows through the existing `DurAssessment` grain + index + override machinery untouched —
PGx alerts appear in the same DUR review UI as every other safety check.

## Workflow grain methods

New partial `PatientWorkflowGrain.Pharmacogenomics.cs` + `IPatientWorkflowGrain` declarations (open):
- `RecordPharmacogenomicResultAsync(gene, diplotype, phenotype, activityScore?, testDate?, lab, method, notes, recordedBy)` → upserts by gene, returns resultId
- `RemovePharmacogenomicResultAsync(gene)`
- `GetPharmacogenomicProfileAsync()` ; `GetPharmacogenomicRecommendationsAsync()` (whole-profile implications) ; `CheckDrugPharmacogenomicsAsync(drugName)` (one-drug lookup)

## Wiring / surfaces

- **Flag:** `SiteFeatures.Pharmacogenomics = "PHARMACOGENOMICS"` (Modern, default-on; wired into `/api/site/features` + `editions.js`).
- **Store:** `pharmacogenomicsStore` (SiloHost `AllStoreNames` + both test `SharedCluster`s).
- **REST:** `PharmacogenomicsController` at `api/pharmacogenomics` — profile / recommendations / check?drug= / record / remove.
- **Blazor:** `Pharmacogenomics.razor` at `/pharmacogenomics` — drug-gene alerts (severity-coloured), a "check a drug" lookup, and the genotype profile table + add form; patient-context, reaches grains directly, flag-gated, no key gate. Nav: a flag-gated "Pharmacogenomics" item.
- **Tests:** `PharmacogenomicsTests` (unit — the KB) + `PharmacogenomicsWorkflowTests` (functional — record/profile/recommendations + the **DUR drug-gene Fail** integration test).

## Demo

`PharmacogenomicsSeed.cs` adds a representative panel to the existing complex patient **P9001 (SICK,
EXTREME LEE)**: **CYP2C19 \*2/\*2** (poor metabolizer → clopidogrel), **HLA-B\*57:01 positive** (→
abacavir), **DPYD** intermediate (→ fluoropyrimidines), **SLCO1B1** decreased function (→ simvastatin),
plus normal **TPMT / CYP2D6 / G6PD**. Prescribing clopidogrel or abacavir to P9001 makes the DUR fire a
drug-gene **Fail**.

## Open / deferred

- **Germline / hereditary genetics** (BRCA/Lynch carrier status, genetic counseling referral) and
  **structured family history / pedigree** — the other genetics-survey gaps; see the blueprint.
- **Activity-score → phenotype derivation** (CYP2D6) and the full **VKORC1 + CYP2C9 warfarin dosing
  algorithm** rather than a single-gene flag.
- **Genetic test results as first-class lab data** (HGVS / ClinVar / LOINC panel identity) and **FHIR
  Genomics Reporting IG** conformance for results-back exchange.
- **PGx write key** (`PGX MANAGER`) if genetic-data write-gating is wanted.
