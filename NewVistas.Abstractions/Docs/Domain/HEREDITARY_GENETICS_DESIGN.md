# Hereditary Genetics & Family History — Design

> **Status: IMPLEMENTED & smoke-verified (2026-06-30).** Closes the remaining genetics-survey gaps:
> germline/hereditary genetics, structured family history, and genetic test results as first-class
> coded data. Together with somatic genomics ([precision oncology](../../Clinical/PrecisionOncology.cs))
> and [pharmacogenomics](PHARMACOGENOMICS_DESIGN.md), NewVistas now covers the three genomic axes —
> somatic, pharmacogenomic, and germline/hereditary.

## Overview

Three gaps, one coherent module — because they're the same workflow:

1. **Genetic test results as first-class data** — an interpreted `GeneticTestReport` (panel, lab,
   method, indication, overall result) carrying coded reportable **`GeneticVariant`s** in **HGVS**
   nomenclature with **ACMG/ClinVar** classification, zygosity, and germline-vs-somatic origin —
   *not* opaque text falling through the generic lab machinery, exactly as the genetics blueprint
   ([genetics-and-family-modeling.md](genetics-and-family-modeling.md)) prescribes.
2. **Germline / hereditary genetics** — a curated knowledge base derives the **hereditary syndrome**
   from a pathogenic germline variant (BRCA1/2 → HBOC, MLH1/MSH2/… → Lynch, APC → FAP, TP53 →
   Li-Fraumeni, …) with management/surveillance guidance and a cascade-testing prompt.
3. **Structured family history** — FHIR `FamilyMemberHistory`-shaped entries (relationship, conditions
   with age at diagnosis, vital status), scanned for **red-flag patterns** (early-onset breast cancer,
   ovarian cancer, Lynch clustering, …) that warrant a **genetics referral**.

This is the blueprint's **"results-back / referral-out"** model that matters for the tribal/IHS market:
the EHR records the interpreted report and the family history, and *acts* on them (risk assessment +
referral), rather than doing in-house sequencing. The "structured flat family history" tier — not a
full GA4GH pedigree (deferred). Read-only decision support — never auto-orders.

## Access model

Reads + writes **open**, gated by one **`HEREDITARY_GENETICS`** flag (Modern, on by default) — matching
the [pharmacogenomics](PHARMACOGENOMICS_DESIGN.md) posture. Germline data is sensitive (GINA / family
implications); a write key is a reasonable future hardening.

---

## Grain set

| Grain | Key | Store | Purpose |
|---|---|---|---|
| `IGenomicsGrain` | patient id | `genomicsStore` | Interpreted genetic test reports + coded reportable variants |
| `IFamilyHistoryGrain` | patient id | `familyHistoryStore` | Structured family history (one entry per relative) |

### State
- `GenomicsState` → `List<GeneticTestReport>`; `GeneticTestReport` (TestName, Lab, `GeneticTestMethod`,
  Indication, Collection/Report dates, `GeneticReportResult`, OrderingProvider, `List<GeneticVariant>`)
- `GeneticVariant` — Gene, **HgvsCoding** (`c.68_69delAG`), **HgvsProtein** (`p.Glu23ValfsTer17`),
  Transcript (`NM_007294.4`), **`VariantClassification`** (ACMG: Pathogenic … Benign), `VariantZygosity`,
  **`VariantOrigin`** (Germline/Somatic), **ClinVarId**, **DbSnpId**
- `FamilyHistoryState` → `List<FamilyMemberHistoryEntry>`; `FamilyMemberHistoryEntry`
  (`FamilyRelationship`, Sex, `FamilyVitalStatus`, AgeYears / AgeAtDeath, CauseOfDeath,
  `List<FamilyConditionEntry>`); `FamilyConditionEntry` (Condition, Code, AgeAtDiagnosis)

### `Clinical.HereditaryRisk` — the curated knowledge base

Deterministic, rule-based, NCCN/ACMG-aligned & illustrative (same pattern as `PrecisionOncology` /
`Pharmacogenomics`):
- **Gene → syndrome** table (~17 genes): BRCA1/2, PALB2 → HBOC/hereditary breast; MLH1/MSH2/MSH6/PMS2/
  EPCAM → Lynch; APC → FAP, MUTYH → MAP; TP53 → Li-Fraumeni; PTEN → Cowden; STK11 → Peutz-Jeghers;
  CDH1 → HDGC; CDKN2A → hereditary melanoma/pancreatic; RET → MEN2; VHL → von Hippel-Lindau — each with
  inheritance + a management/surveillance summary.
- `AssessVariants(variants)` → a `HereditaryFinding` for each **pathogenic / likely-pathogenic GERMLINE**
  variant in a known gene (VUS, benign, and somatic variants are ignored).
- `AssessFamilyHistory(members)` → `FamilyRiskFlag`s for representative referral criteria: ovarian
  cancer (any age), breast cancer < 50, male breast cancer, ≥2 relatives with breast cancer,
  colorectal/endometrial < 50 (Lynch), ≥3 Lynch-spectrum relatives, pancreatic + breast/ovarian, and a
  known syndrome/pathogenic variant in a relative (→ cascade testing). Representative — **not** the full
  NCCN / Amsterdam II rule set.

## Workflow grain methods

New partial `PatientWorkflowGrain.Genomics.cs` + `IPatientWorkflowGrain` declarations (open):
- `RecordGeneticTestReportAsync(...)` → reportId ; `AddGeneticVariantAsync(reportId, …)` ; `RemoveGeneticReportAsync` ; `GetGenomicsProfileAsync` ; **`GetHereditaryFindingsAsync`** (assessment)
- `AddFamilyMemberAsync(...)` → memberId ; `AddFamilyConditionAsync(memberId, …)` ; `RemoveFamilyMemberAsync` ; `GetFamilyHistoryAsync` ; **`GetFamilyRiskFlagsAsync`** (assessment)

## Wiring / surfaces

- **Flag:** `SiteFeatures.HereditaryGenetics = "HEREDITARY_GENETICS"` (Modern, default-on; `/api/site/features` + `editions.js`).
- **Stores:** `genomicsStore`, `familyHistoryStore` (SiloHost + both test `SharedCluster`s).
- **REST:** `GeneticsController` at `api/genetics` — genomics / hereditary-findings / reports+variants / family-history / family-members+conditions / family-risk-flags.
- **Blazor:** `Genetics.razor` at `/genetics` — **Hereditary Risk** (findings + family red-flags), **Genetic Results** (reports + variant tables + add forms), **Family History** (relatives + conditions); patient-context, grains-direct, flag-gated, no key gate. Nav: "Genetics & Family Hx".
- **Tests:** `HereditaryRiskTests` (unit — the KB) + `GenomicsWorkflowTests` (functional — reports/variants/family + the end-to-end BRCA1→HBOC finding and ovarian→family-flag).

## Demo

`HereditaryGeneticsSeed.cs` → new patient **P9004 "HEREDITARY, HOPE"**: a germline **BRCA1 c.68_69delAG
(p.Glu23ValfsTer17) pathogenic** variant (+ an incidental ATM VUS) on an 84-gene hereditary-cancer
panel, and a 3-generation maternal family history (mother breast cancer dx 44, maternal aunt ovarian
cancer dx 58, maternal grandmother breast cancer dx 60, maternal uncle pancreatic cancer) — producing
an **HBOC** hereditary finding and several family-history **referral red-flags**.

## Open / deferred

- **True pedigree** (GA4GH/PED — consanguinity, twins, deceased/proband markers) vs. the flat
  family-history tier built here; external pedigree-tool integration (PhenoTips/Progeny).
- **Identity vs. role separation** (PersonGrain) to solve the blueprint's "triplication problem" (a
  relative who is also a patient).
- **Cross-module links:** surface a germline BRCA finding to the oncology PARP-inhibitor logic; a
  Lynch finding to clinical reminders / screening; carrier results into OB/prenatal.
- **FHIR Genomics Reporting IG** conformance for results-back exchange; genetic-counseling referral
  tracking as a first-class workflow; a `GENETICS MANAGER` write key.
