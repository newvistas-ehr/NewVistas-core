# Neonatal Care — Design

> **Status: Phase 1 IMPLEMENTED & smoke-verified (2026-06-29); Phase 2 (NICU depth) IMPLEMENTED & smoke-verified (2026-06-30).** Closes the gap surfaced when surveying maternity:
> the existing OB module ([PregnancyState.cs](../../GrainStates/PregnancyState.cs)) carries the
> birth through the **mother's** delivery record (infant sex, birth weight, APGAR) but the baby
> then disappears — no chart of their own, no neonatal assessment, no newborn screening, no nursery.
> This module gives the newborn a record and the nursery a workflow.

## Overview

A **newborn** is registered from the mother's delivery and gets its own neonatal chart: birth
data, a gestational-age/growth classification, the newborn physical exam, **newborn screening**
(metabolic, CCHD, hearing, bilirubin), interval measurements/feeding over the birth stay, a
**nursery level of care**, and discharge. It links back to the mother's pregnancy (supporting
multiples) and is surfaced as a **nursery census/board**.

Phase 1 is the *well-newborn + universal-screening + nursery* scope. **Phase 2 (NICU depth) is now
implemented** — respiratory-support timeline, phototherapy, a neonatal problem list, parenteral/
enteral nutrition, bedside procedures, and a Fenton-style growth percentile — see
[**Phase 2 — NICU depth**](#phase-2--nicu-depth-implemented) below.

## VistA / RPMS heritage

| VistA / RPMS / standard | NewVistas equivalent |
|---|---|
| RPMS birth measurements / IHS newborn data | `INewbornGrain` (`NEONATE:{guid}`) — the newborn chart |
| Mother's delivery record (OB, already present) | `PregnancyState.Delivery` → links to `NewbornIds` |
| Newborn metabolic screen / state newborn-screening panels (RUSP), CCHD pulse-ox, hearing (OAE/ABR) | `NewbornScreeningResult` (typed) |
| Ballard / Dubowitz gestational-age assessment; AAP levels of newborn care (I–IV) | `BallardScore` + `NurseryLevelOfCare` |
| Well-baby nursery census | `INewbornNurseryGrain` (`NEONATE-NURSERY:DEFAULT`) |

## Access model

Neonatal **matches the maternity module it extends** — the OB grains are *ungated* (no OB
security key today) and the maternal-newborn team is the same nurses/providers, so neonatal
writes are **open like OB**, and reads are open. The whole area is gated by one **`NEONATAL_CARE`
site feature flag** (Modern, on by default for demos). *(A unified OB+neonatal write key is a
reasonable future hardening for both modules together — deliberately not split here.)*

---

## Grain set

| Grain | Key | Store | Purpose |
|---|---|---|---|
| `INewbornGrain` | `NEONATE:{guid}` | `newbornStore` | The newborn's neonatal chart (birth → discharge) |
| `INewbornNurseryGrain` | `NEONATE-NURSERY:DEFAULT` | `newbornNurseryStore` | Nursery census/board + level-of-care roll-up |

Per-mother linkage reuses the existing OB record: a new `List<string> NewbornIds` on
`PregnancyState` (next `[Id]`), set when a newborn is registered against the pregnancy. The
newborn carries `MotherPatientId` + `PregnancyId` (+ an optional `NewbornPatientId` for when the
baby is later registered as a full patient).

### `NewbornState`  (`NEONATE:{guid}`)

- `NewbornId`, `MotherPatientId`, `PregnancyId`, `NewbornPatientId` *(reserved — full patient promotion)*
- Identity: `Name` (e.g. "BABY GIRL SMITH"), `Sex` *(enum: Male/Female/Ambiguous/Unknown)*, `BirthDateTime`,
  `MultipleBirthOrder` (1 for singleton; 1/2/3… for multiples), `MultipleBirthTotal`
- Birth data: `DeliveryMethod` *(reuse OB `DeliveryMethod`)*, `GestationalAgeWeeks`, `GestationalAgeDays`,
  `BirthWeightGrams`, `LengthCm`, `HeadCircumferenceCm`, `Apgar1Min`, `Apgar5Min`, `Apgar10Min`,
  `ResuscitationProvided`, `ResuscitationDetail`, `CordBloodCollected`, `BloodType`
- Classification (from `Clinical.NeonatalClassifier`): `GestationalAgeClassification`
  *(enum: ExtremelyPreterm/VeryPreterm/Preterm/LatePreterm/Term/PostTerm)*, `BirthWeightClassification`
  *(enum: ELBW/VLBW/LBW/Normal/Macrosomia + SGA/AGA/LGA)*
- Exam: `NewbornExam` *(embedded — general/HEENT/cardiac/respiratory/abdomen/GU/musculoskeletal/neuro/skin
  text fields + an overall impression)*
- Screening: `List<NewbornScreeningResult>`
- Course: `List<NewbornMeasurement>` (interval weight/feeding/bilirubin), `NurseryLevel`
  *(enum: WellNewborn / SpecialCareLevelII / NicuLevelIII / NicuRegionalLevelIV)*, `NurseryLevelReason`
- Status/discharge: `Status` *(enum: Admitted/Discharged/Transferred/Deceased)*, `DischargeDateTime`,
  `DischargeWeightGrams`, `DischargeFeeding`, `DischargeDisposition`, `FollowUpPlan`, `CarSeatTestPassed`
- Providers: `AttendingProviderId/Name` (pediatric/nursery), `BirthLocationName`
- `CreatedDate`, `LastModifiedDate`

`NewbornScreeningResult` — `ScreeningType` *(enum: MetabolicBloodSpot, CriticalCongenitalHeartDisease,
Hearing, Bilirubin, Glucose)*, `Result` *(enum: Pass, ReferOrFail, Pending, Inconclusive, NotDone)*,
`ValueText` (e.g. "Pre-ductal 99% / post-ductal 98%", "TSB 7.2 mg/dL low-risk zone"), `PerformedDate`,
`PerformedBy`, `Notes`.

`NewbornMeasurement` — `MeasuredAt`, `WeightGrams`, `FeedingType` *(enum: Breast/Formula/Mixed/IvTpn/Npo)*,
`FeedingNotes`, `BilirubinMgDl`, `Notes`.

`NewbornNurseryEntry` (census) — `NewbornId`, `NewbornName`, `MotherPatientId`, `Sex`, `BirthDateTime`,
`GestationalAgeWeeks`, `BirthWeightGrams`, `NurseryLevel`, `Status`, `AttendingProviderName`,
`PendingScreenCount`, `OnRespiratorySupport`, `ActiveProblemCount` *(last two: Phase 2 acuity)*.

### `Clinical.NeonatalClassifier`

Deterministic, curated (mirrors `PrecisionOncology` / `HomeHealthGrouper`):
- `GestationalAgeClassification(weeks)` → Extremely/Very/Preterm, LatePreterm (34–36⁶), Term (37–41⁶), PostTerm (≥42).
- `BirthWeightClassification(grams)` → ELBW (<1000), VLBW (<1500), LBW (<2500), Normal, Macrosomia (≥4000);
  and SGA/AGA/LGA from weight-for-GA percentile bands (representative table, not the full Olsen/Fenton curves).
- A small unit test suite pins the thresholds.

---

## Workflow grain methods

New partial `PatientWorkflowGrain.Neonatal.cs` + `IPatientWorkflowGrain` declarations. **Writes open
(matching OB), reads open.** Patient-scoped methods run on the mother's workflow grain; the nursery
census is the singleton.

**Writes**
- `RegisterNewbornFromDeliveryAsync(pregnancyId, name, sex, birthDateTime, gaWeeks, gaDays, birthWeightG, lengthCm, headCircCm, apgar1, apgar5, apgar10, multipleOrder, multipleTotal, attendingProviderId/Name, birthLocation)` → creates the newborn, runs `NeonatalClassifier`, links `PregnancyState.NewbornIds`, seeds the nursery census; returns `newbornId`.
- `RecordNewbornExamAsync(newbornId, exam)` ; `RecordNewbornScreeningAsync(newbornId, type, result, valueText, performedDate, performedBy, notes)` ; `RecordNewbornMeasurementAsync(newbornId, measuredAt, weightG, feedingType, biliMgDl?, notes)`
- `SetNurseryLevelAsync(newbornId, level, reason)` ; `TransferNewbornAsync(newbornId, toLocation, reason)`
- `DischargeNewbornAsync(newbornId, dischargeDateTime, dischargeWeightG, dischargeFeeding, disposition, followUpPlan, carSeatPassed)`

**Reads (open)**
- `GetNewbornAsync(newbornId)` ; `GetNewbornsForPregnancyAsync(pregnancyId)` ; `GetNewbornsForMotherAsync()` (this patient's babies, via her pregnancies)
- Facility-wide (singleton): nursery census `GetActiveAsync` / `GetAllAsync` / by-level / pending-screens, served directly by `INewbornNurseryGrain` (page + controller), like the home-care census.

Helper pattern mirrors home care: `Newborn(id)`, `Nursery()`, `RefreshNurseryAsync(newbornId)`; a
register/screen/discharge updates the newborn then refreshes the census entry (with a live
pending-screen count = the universal screens not yet `Pass`/`NotDone`).

---

## Wiring / surfaces

- **Flag:** `SiteFeatures.NeonatalCare = "NEONATAL_CARE"` (Modern, in the default `Features` set; wired into `/api/site/features` + `editions.js`).
- **Stores:** `newbornStore`, `newbornNurseryStore` (SiloHost `AllStoreNames` + both test `SharedCluster`s).
- **REST:** `NeonatalController` at `api/neonatal` — complete (register / exam / screening / measurement / level / discharge + reads + nursery census), for parity with the other modules / future mobile.
- **Blazor:** `Neonatal.razor` at `/neonatal` — a **Nursery** board (census + level-of-care + pending-screens) and a **Newborn detail** (birth data + classification, exam, screening panel with pass/refer badges, weight/feeding/bili trend, discharge). Reaches grains directly. Nav: a "Neonatal / Nursery" item, gated `IsFeatureEnabled("NEONATAL_CARE")`. Registering a newborn is offered from the mother's delivery context.
- **Tests:** `NeonatalClassifierTests` (unit) + `NeonatalWorkflowTests` (functional, full register→screen→discharge + nursery census + the OB link).

## Demo

The maternity module currently has **no demo data**, so the demo seeds the **whole maternal-newborn
continuum** on a new female patient (e.g. **P9002 "DELIVERED, DONNA"**): demographics → a pregnancy
(GPAL, EDD, low risk) → a couple of prenatal visits (fundal height / FHR progression) → a term SVD
**delivery** (APGAR 8/9) → postpartum (EPDS) → a **newborn** ("BABY GIRL …", 39⁺² wks, 3350 g, AGA/Term),
exam, the three universal screens (metabolic pending→sent, CCHD pass, hearing pass), a day-1/day-2 weight,
Well-Newborn level, and discharge home breastfeeding with a 2-day follow-up. This populates the previously
empty **Prenatal** pages *and* the new **Neonatal** page in one coherent story.

---

## Phase 2 — NICU depth (implemented)

Phase 2 adds the intensive-care depth a Level II–IV nursery needs, as **clinical depth within the same
module** — *not* a separate edition. There is **no new feature flag** (it stays under `NEONATAL_CARE`)
and **no new key** (open access, like the rest of neonatal/OB). Rationale: unlike home-care Phase 2
(Medicare — a genuinely different billing/operating model that earned its own flag), a sicker baby just
uses more of the same neonatal workflow. All Phase 2 data hangs off the existing `INewbornGrain`
(no new grains, no new stores).

**State (`NewbornNicuState.cs`, five lists on `NewbornState` `[Id 43–47]`):**
- `RespiratorySupportEntry` — a support **timeline**: `SupportType` *(RoomAir → NasalCannula → HighFlowNasalCannula → Cpap → Nippv → ConventionalVentilation → Hfov → Ecmo)*, `FiO2Percent`, `Settings`, `RecordedAt`, `EndedAt?`, `Notes`. Recording a new state auto-closes the prior open episode.
- `PhototherapyEntry` — `Intensity` *(Single/Double/Triple/Intensive)*, `Indication`, `BilirubinAtStartMgDl`, `StartedAt`, `EndedAt?` (null = active).
- `NeonatalProblemEntry` — `Problem`, `Icd10Code`, `OnsetDate`, `Status` *(Active/Resolved)*, `Notes` (a neonatal problem list, e.g. RDS/jaundice/apnea of prematurity).
- `NeonatalNutritionEntry` — `Route` *(Npo/IvFluids/Tpn/EnteralGavage/EnteralOral/Mixed)*, `TotalFluidMlPerKgPerDay`, `Detail` (TPN composition / feed orders).
- `NeonatalProcedureEntry` — `ProcedureType` *(Intubation/Surfactant/UVC/UAC/PICC/LP/Exchange-or-Blood-Transfusion/Other)*, `PerformedAt`, `PerformedBy`, `Notes`.

**Census acuity:** `NewbornNurseryEntry` gains `OnRespiratorySupport` (latest open episode is beyond room air) and `ActiveProblemCount`, refreshed on every support/problem write so the board flags NICU acuity at a glance.

**Classifier:** `NeonatalClassifier.WeightPercentileForGestationalAge(gaWeeks, grams)` — a coarse Fenton-style growth percentile (1–99, or −1 unknown) from the same representative band table, for interval growth tracking. Curated, *not* the full Fenton 2013 curves (honest scope boundary, like the rest of the classifier).

**Workflow (`PatientWorkflowGrain.NeonatalNicu.cs`):** `RecordNewbornRespiratorySupportAsync`, `Start/EndNewbornPhototherapyAsync`, `Add/ResolveNewbornProblemAsync`, `RecordNewbornNutritionAsync`, `RecordNewbornProcedureAsync` — acuity-changing writes refresh the census. **REST:** 7 POST endpoints under `api/neonatal/{motherPatientId}/newborns/{newbornId}/…`. **Blazor:** five new panels in the Newborn-Detail tab (each table + inline add form), a "Current support" banner, board acuity badges + an "On Support" tile, and a `%ile (GA)` column on Measurements. **Tests:** 8 percentile unit tests + 8 NICU-workflow functional tests (incl. the auto-close + census-acuity behaviors).

**Phase 2 demo — P9003 "PRETERM, PAULA":** a second maternal-newborn story alongside P9002, exercising the whole NICU layer — a 30+2-week / 1300 g (VLBW, **Very Preterm / AGA**) infant in a **Level III** NICU: ventilator→CPAP timeline, surfactant + UVC/UAC + intubation procedures, a 4-problem list (RDS / prematurity / preterm jaundice / apnea of prematurity, ICD-10 `P22.0` / `P07.33` / `P59.0` / `P28.41`), active double phototherapy, and starter→advancing TPN with trophic feeds. The nursery board now spans the full acuity range (term well-newborn ↔ NICU III).

## Open / deferred
- **Phase 3 (mobile):** an Android device for the nursery/NICU is the same Phase-3 plan as home care; the `api/neonatal` REST is already complete for it.
- **NICU extras:** CAR-T-style depth doesn't apply, but cooling/therapeutic-hypothermia protocols, ventilator-trend charts, and the full Fenton growth curves are natural follow-ons.
- **Full patient promotion:** `NewbornPatientId` is reserved so a newborn can become a first-class
  registered patient (own demographics/MRN) rather than a chart hanging off the mother.
- **Linkage from OB UI:** a "Register newborn" action on the delivery form (Prenatal page) — nice-to-have.
