# Home-Based Care (HBPC) — Design

> **Status: IMPLEMENTED (Phase 1, 2026-06-29).** This is the agreed Phase 1 (VA Home-Based
> Primary Care) design, deliberately shaped so that Phase 2 (full Medicare skilled home health —
> OASIS / PDGM / certification / EVV / billing) bolts on without reworking the core grains.
> See the [Phase 2 extension seams](#phase-2-the-medicare-extension-seams) for how each hook
> lights up.
>
> **Implementation notes (delta from this design):**
> - A pre-existing, orphaned home-health module (`HBPCPatient`/`HHCVisit` grains keyed per-patient,
>   `HomeHealthController`, `/home-health` Blazor page, WPF view, tests) was **retired** and
>   replaced by the GUID-keyed episode model below (James chose the clean rebuild).
> - The team-member record is `HomeCareTeamMember` (not `CareTeamMember`, which already existed).
> - Added a 6th grain — `IHomeVisitIndexGrain` (`HHC-VISIT-INDEX`) — for the cross-patient daily
>   visit schedule the in-home mobile clients need.
> - **Phase 3 = an in-home mobile app (Android first).** Because of it, the REST API
>   (`api/homecare`, `HomeCareController`) is **complete** — every operation has an endpoint —
>   even though the Blazor front end reaches grains directly. Patient-scoped routes are prefixed
>   `{patientId}/...`; facility-wide caseload/visit-schedule reads hit the singleton grains directly.
> - "Established by" / acting-provider identity in the UI uses the shared `<ProviderPicker>`
>   (the codebase convention) rather than a user-context field.
> - Demo: **SICK, EXTREME LEE (P9001)** is seeded onto HBPC (3-member team, plan with 3 problems,
>   comprehensive assessment, 2 completed + 1 upcoming visit).
> - Coverage: 22 functional tests (`HomeCareWorkflowTests`); whole module flag-gated `HOME_BASED_CARE`
>   (Modern, ON by default), writes gated by `HBHC MANAGER`, reads open.

## Overview

"Home health" is really **two** models (see the research brief). NewVistas builds the lighter,
heritage-aligned one first:

- **Phase 1 — Home-Based Primary Care (HBPC):** team-based, *longitudinal* primary care in the
  patient's home for complex/chronic patients. Open-ended; eligibility is clinical need (not the
  Medicare "homebound" rule). This is what VistA actually has (the `HBH` package) — and it mostly
  rides on the existing chart + scheduling, so it's cheap to stand up.
- **Phase 2 — Medicare skilled home health:** episodic, *certified*, OASIS/PDGM-driven 60-day
  certification / 30-day payment periods with homebound + skilled-need gates and claims. The real
  compliance surface; opt-in.

The same core entities — **Episode, Care Team, Plan of Care, Home Visit, Assessment, Census** —
serve both. Phase 1 uses them in "longitudinal" mode; Phase 2 layers certification periods, OASIS
items, a PDGM grouper, EVV, and billing **on top of the same spine**.

## VistA Heritage

| VistA / VA component | NewVistas equivalent |
|---|---|
| `HBH` package (Home Based Primary Care — workload/case-mix reporting to Austin) | `IHomeCareCensusGrain` — caseload/census + workload roll-up |
| HBPC interdisciplinary team (IDT) | `CareTeamMember` list on `HomeCareEpisodeState` (syncs from [[provider-directory]] where present) |
| CPRS chart used by HBPC for charting | Existing NewVistas chart (Problems, Meds, Notes, Vitals) — home-care grains *reference*, don't duplicate |
| Home visit / encounter (PCE) | `IHomeVisitGrain` (+ existing encounter/PCE capture) |
| *(Phase 2)* OASIS data set | `IHomeCareAssessmentGrain` with `AssessmentType = Oasis*` |
| *(Phase 2)* CMS-485 plan of care + certification | certification fields on `HomeCarePlanState` |
| *(Phase 2)* HH PPS / PDGM grouper | `HomeHealthGrouper` deterministic service (mirrors `Clinical.PrecisionOncology`) |

VistA's HBPC `HBH` package is **only** a workload/census reporter — the clinical record lives in
CPRS. NewVistas mirrors that division: the **census grain is the HBH analog**, while clinical
content reuses the existing chart.

---

## Design principles (the seams that make Phase 2 natural)

1. **Episodes are GUID-keyed, not per-patient.** A patient has a *history* of episodes found via a
   per-patient index. HBPC = one long-lived episode; Medicare = many episodes/cert periods over
   time — same model.
2. **`ProgramType` enum on every episode** switches behavior without a schema change:
   `HomeBasedPrimaryCare` (Phase 1) → `MedicareSkilledHomeHealth` (Phase 2).
3. **`Eligibility` is one embedded record** holding *both* worlds: HBPC's clinical-need narrative
   **and** Medicare's `Homebound` + `SkilledNeed` fields (the latter simply unset in Phase 1).
4. **`CertificationPeriod` is a list on the episode** — empty in Phase 1, populated with 60-day
   periods (each carrying its two 30-day payment periods) in Phase 2.
5. **Assessments are typed.** Phase 1 records a `ComprehensiveHbpc` assessment; Phase 2 adds the
   `Oasis*` types into the *same* grain, with a reserved `OasisData` payload.
6. **Visits carry EVV + visit-type fields from day one** — dormant in Phase 1, mandatory in Phase 2.
7. **A reserved grouping result** (`PdgmGroupingResult?`) hangs off each certification period; the
   grouper service is added in Phase 2 but the field exists now.
8. **Billing is a separate future grain** (`IHomeHealthBillingGrain`) referenced by ID — no billing
   coupling in Phase 1.

> **Serialization note:** because we reserve Phase-2 fields now, the `[Id(n)]` numbering already
> accounts for them. Adding the Phase-2 *behavior* later touches grain logic, not state shape —
> no risky `[Id]` renumbering of persisted HBPC data.

---

## Grain set

| Grain | Key | Store | Purpose |
|---|---|---|---|
| `IHomeCareEpisodeGrain` | `HHC-EPISODE:{guid}` | `homeCareEpisodeStore` | The spine: admission, program type, eligibility, team, links to plan/visits/assessments, status, discharge |
| `IHomeVisitGrain` | `HHC-VISIT:{guid}` | `homeVisitStore` | A single scheduled/completed home visit by one discipline; note + (Phase 2) EVV |
| `IHomeCarePlanGrain` | `HHC-POC:{guid}` | `homeCarePlanStore` | Interdisciplinary problem-oriented plan of care; periodic review; (Phase 2) certification |
| `IHomeCareAssessmentGrain` | `HHC-ASSESS:{guid}` | `homeCareAssessmentStore` | Comprehensive HBPC assessment; (Phase 2) OASIS data set |
| `IHomeCareCensusGrain` | `HHC-CENSUS:{siteId}` (default `HHC-CENSUS:DEFAULT`) | `homeCareCensusStore` | Program caseload/census + workload roll-up (the `HBH` heritage) |

Per-patient episode lookup reuses the existing **`IPatientHistoryIndexGrain`** keyed
`{patientId}:HomeCare` (same pattern oncology/pharmacy use) — no new per-patient index grain.

### `HomeCareEpisodeState`  (`[Id]` 0–~22, Phase-2 fields reserved)

- `EpisodeId`, `PatientId`
- `ProgramType` *(enum)* — `HomeBasedPrimaryCare` | `MedicareSkilledHomeHealth`
- `AdmissionDate`, `AdmissionSource` *(enum: Community, AcuteHospital, SNF/PostAcute, …)*
- `ReferringProviderId`, `ReferringProviderName`
- `PrimaryDiagnosisCode`, `PrimaryDiagnosisText` *(ICD-10)*
- `Eligibility` *(embedded `HomeCareEligibility`)*
- `Team` *(`List<CareTeamMember>`)*
- `PlanOfCareId` *(current active POC)*
- `VisitIds`, `AssessmentIds` *(`List<string>`)*
- `Status` *(enum: Active, OnHold, Discharged)*, `OnHoldReason`
- `DischargeDate`, `DischargeReason`, `DischargeDisposition`
- **Reserved (Phase 2):** `CertificationPeriods` *(`List<CertificationPeriod>` — empty in P1)*
- `CreatedDate`, `LastModifiedDate`

`HomeCareEligibility` *(embedded record)* — `ClinicalNeedNarrative` (HBPC); **reserved P2:**
`IsHomebound` (bool?), `HomeboundJustification`, `SkilledNeed` *(enum?: SkilledNursing, PhysicalTherapy,
SpeechTherapy, None)*.

`CareTeamMember` *(record)* — `ProviderId`, `Name`, `Discipline` *(enum below)*, `IsPrimary`,
`AssignedDate`, `UnassignedDate?`.

`HomeCareDiscipline` *(enum)* — `Physician`, `NursePractitioner`, `SkilledNursing`,
`PhysicalTherapy`, `OccupationalTherapy`, `SpeechTherapy`, `HomeHealthAide`, `MedicalSocialWork`,
`Dietitian`, `Pharmacy`, `MentalHealth`. *(Covers both the HBPC IDT and the Medicare disciplines.)*

### `HomeVisitState`

- `VisitId`, `EpisodeId`, `PatientId`
- `Discipline`, `ProviderId`, `ProviderName`
- `ScheduledDateTime`, `Status` *(enum: Scheduled, InProgress, Completed, Missed, Cancelled)*
- `VisitType` *(enum: Routine, Initial/SOC, Resumption, Recertification, Discharge, PRN)* — Phase 1 uses Routine/Initial/Discharge; the rest light up in P2
- `Summary` / `NoteId` *(links to a TIU note via the existing signature workflow)*
- `Reason`, `CancellationReason`
- **Reserved (Phase 2 / EVV):** `CheckInTime`, `CheckOutTime`, `CheckInLocation`, `CheckOutLocation`,
  `EvvMethod` *(enum?: GPS, Telephony, FOB, Manual)*
- `CreatedDate`, `LastModifiedDate`

### `HomeCarePlanState`

- `PlanId`, `EpisodeId`, `PatientId`
- `Problems` *(`List<CarePlanProblem>`: `Problem`, `Goals`, `Interventions`, `ResponsibleDiscipline`,
  `Status`)*  — interdisciplinary, problem-oriented
- `EstablishedById`, `EstablishedDate`, `LastReviewDate`, `NextReviewDue`
- **Reserved (Phase 2 / CMS-485 + cert):** `CertifyingProviderId`, `CertificationDate`,
  `CertificationPeriodStart`, `CertificationPeriodEnd`, `FaceToFaceEncounterDate`, `IsRecertification`,
  `OrdersText`, `PhysicianSignatureId`
- `CreatedDate`, `LastModifiedDate`

### `HomeCareAssessmentState`

- `AssessmentId`, `EpisodeId`, `PatientId`
- `AssessmentType` *(enum: `ComprehensiveHbpc` | `OasisStartOfCare` | `OasisResumption` |
  `OasisRecertification` | `OasisTransfer` | `OasisDischarge`)*
- `AssessorId`, `AssessorName`, `AssessmentDate`
- `Comprehensive` *(embedded `HbpcComprehensiveAssessment`: functional status/ADLs, home-safety,
  caregiver/support, cognitive & mental status, nutrition, med-reconciliation summary, fall risk)*
- **Reserved (Phase 2):** `OasisData` *(`OasisDataSet?` — the OASIS-E item set; null in P1)*
- `CreatedDate`, `LastModifiedDate`

### `HomeCareCensusState`  (the `HBH` workload analog)

- `SiteId`
- `Entries` *(`List<HomeCareCensusEntry>`: `EpisodeId`, `PatientId`, `PatientName`, `ProgramType`,
  `PrimaryDiscipline`, `PrimaryProviderId`, `AdmissionDate`, `Status`, `LastVisitDate`,
  `NextVisitDate`, `OpenProblemCount`)*
- `LastModifiedDate`

---

## Workflow grain methods

New partial file **`PatientWorkflowGrain.HomeCare.cs`** + declarations on `IPatientWorkflowGrain`.
Following the oncology access model: **writes gated by `[RequiresSecurityKey(SecurityKeys.HBHC_MANAGER)]`,
reads open** (care coordination — the PCP/specialists must see the home-care picture).

**Phase 1 — writes (gated)**
- `AdmitToHomeCareAsync(programType, admissionSource, referringProviderId/Name, primaryDx, clinicalNeed)` → creates episode, seeds census entry, returns `episodeId`
- `AssignHomeCareTeamMemberAsync(episodeId, providerId, name, discipline, isPrimary)` / `RemoveHomeCareTeamMemberAsync`
- `CreateHomeCarePlanAsync(episodeId, …)` / `AddHomeCarePlanProblemAsync(planId, problem, goals, interventions, discipline)` / `ReviewHomeCarePlanAsync(planId, reviewDate, nextDue)`
- `ScheduleHomeVisitAsync(episodeId, discipline, providerId/Name, scheduledDateTime, visitType, reason)` → returns `visitId`
- `StartHomeVisitAsync(visitId, ...)` *(check-in; EVV fields stubbed)* / `CompleteHomeVisitAsync(visitId, summary/noteId, ...)` / `CancelHomeVisitAsync(visitId, reason)`
- `RecordHomeCareAssessmentAsync(episodeId, ComprehensiveHbpc payload, assessorId)` → returns `assessmentId`
- `DischargeFromHomeCareAsync(episodeId, dischargeDate, reason, disposition)`

**Phase 1 — reads (open)**
- `GetHomeCareEpisodeAsync(episodeId)`, `GetActiveHomeCareEpisodeAsync(patientId)`, `GetHomeCareEpisodesAsync(patientId)`
- `GetHomeVisitsAsync(episodeId)`, `GetUpcomingHomeVisitsAsync(...)`
- `GetHomeCarePlanAsync(planId)`
- `GetHomeCareAssessmentsAsync(episodeId)`
- `GetHomeCareCensusAsync(siteId)`, `GetHomeCareCaseloadAsync(providerId)`, `GetHomeCareWorkloadStatsAsync(siteId)`

**Reserved — Phase 2 declarations (documented now, not implemented)**
- `CertifyHomeCareEpisodeAsync` / `RecertifyHomeCareEpisodeAsync(episodeId, certifyingProvider, f2fDate, period)`
- `RecordOasisAsync(episodeId, timePoint, OasisDataSet)`
- `ComputePdgmGroupingAsync(certificationPeriodId)` *(calls `HomeHealthGrouper.Group(...)`)*
- `SubmitNoticeOfAdmissionAsync(episodeId)` / `GenerateHomeHealthClaimAsync(periodId)`

Helper pattern mirrors oncology: `HomeEpisode(id)`, `HomeVisit(id)`, `HomeCensus(siteId)`,
`BuildCensusEntry(state)`; writes do `await grain.XAsync(...); state = await grain.GetAsync();
await HomeCensus().UpsertEntryAsync(BuildCensusEntry(state));`.

---

## Security, flags, stores

- **Security key:** new `SecurityKeys.HBHC_MANAGER = "HBHC MANAGER"`. Home-care team members
  (nurse, PT, SW, MD) hold it; gates all home-care *writes*. Reads open. Same read-open/write-gated
  posture as oncology — **not** a privacy silo. *(Finer per-discipline visit gating is a possible
  Phase 1.5 refinement.)* `Nurse`/`Provider`/`PT` demo roles get the key.
- **Feature flags (Modern tier):**
  - `HOME_BASED_CARE` — Phase 1; propose **ON by default** for demos (matches the oncology pattern).
  - `HOME_HEALTH_MEDICARE` — Phase 2 (OASIS/PDGM/cert/billing); **OFF by default** (heavy compliance,
    opt-in). The episodic UI and the `MedicareSkilledHomeHealth` program type are gated on it.
  - Wire both into `SiteFeatures`, the default `Features` set, `GET /api/site/features`, and
    `editions.js` TIER (`modern`).
- **Stores (register in BOTH dev memory + prod ADO.NET in `Program.cs`):**
  `homeCareEpisodeStore`, `homeVisitStore`, `homeCarePlanStore`, `homeCareAssessmentStore`,
  `homeCareCensusStore`.

## REST + Blazor + nav

- **Controller:** `HomeCareController` at `api/homecare` (delegates to the workflow grain), following
  the standard controller pattern. *(Per [[webserver-role]], internal Blazor pages can call the
  workflow grain directly; the controller is for external/portal/FHIR use.)*
- **Blazor:** `HomeCare.razor` at `/home-care`:
  - **Caseload / Census** tab — the program roster + workload counts (the HBH view); "My Caseload"
    filter for the logged-in team member.
  - **Episode detail** — header (program type, admission, eligibility, status), **Team** panel,
    **Plan of Care** panel, **Visits** panel (schedule + complete), **Assessments** panel.
  - Admit / discharge / schedule actions gated on `CanEdit` (`HasKey(HBHC_MANAGER)`); whole area
    gated on `IsFeatureEnabled("HOME_BASED_CARE")`. Phase-2 panels gated on `HOME_HEALTH_MEDICARE`.
- **Nav:** a "Home Care" section (Caseload, plus the per-patient detail via patient context),
  gated `HasAccess(MenuArea.Clinical) && IsFeatureEnabled("HOME_BASED_CARE")`.

---

## Phase 2 — the Medicare extension seams

When `HOME_HEALTH_MEDICARE` is enabled and an episode's `ProgramType = MedicareSkilledHomeHealth`,
each reserved hook activates — **no core grain rewrite**:

| Seam (reserved in P1) | Phase-2 activation |
|---|---|
| `Eligibility.IsHomebound` / `SkilledNeed` | Enforced as admission gates; surfaced on the episode header |
| `CertificationPeriods` list | Populated with 60-day periods, each holding two 30-day payment periods |
| `HomeCarePlanState` cert fields | CMS-485 content + physician certification/recert e-signature (reuses TIU-sign) + F2F date |
| `HomeCareAssessmentState.OasisData` + `Oasis*` types | OASIS-E item capture at SOC/ROC/Recert/Transfer/Discharge; **OASIS "scrubbing" reuses the grounded-clinical-AI verifier** ([[clinical-ai-summary]]) |
| `HomeVisitState` EVV fields + `VisitType` | Check-in/out with GPS/telephony stamps; LUPA visit-count tracking |
| `CertificationPeriod.PdgmGroupingResult?` | `HomeHealthGrouper` deterministic service — **structurally a clone of `Clinical.PrecisionOncology`** (inputs → classification, curated rules), classifying each 30-day period into a PDGM case-mix group |
| `IHomeHealthBillingGrain` (new) | Notice of Admission (NOA) within 5 days; claims; LUPA adjustment |
| Census workload roll-up | Extends to HH QRP / quality-measure inputs |

---

## Reuse of existing NewVistas building blocks

- **PT discipline** → [NewVistas.PT/](../../../NewVistas.PT/) documentation can be referenced by PT
  home visits (don't duplicate therapy notes).
- **Scheduling / appointments** → home visits are a specialized scheduled encounter; reuse conflict
  logic where useful.
- **Care teams / provider directory** → seed the IDT from the existing care-team/[[provider-directory]]
  data; auto-add the patient to team members' panels (the existing best-effort pattern).
- **TIU notes + signature** → visit notes and (P2) certification signatures.
- **Grounded clinical AI** ([[clinical-ai-summary]]) → OASIS scrubbing is a natural fit in P2.
- **`Clinical.PrecisionOncology`** → the structural template for the P2 `HomeHealthGrouper`.

## Tests

- **Unit:** episode lifecycle (admit→team→visit→discharge), census upsert/roll-up, eligibility
  record round-trip, assessment typing. Mirror `OncologyWorkflowTests` structure (`SharedCluster`).
- **Functional:** full HBPC workflow through `IPatientWorkflowGrain`; read-open/write-gated checks.
- **Demo seed:** put **SICK, EXTREME LEE (P9001)** on HBPC — an IDT, a comprehensive assessment, a
  care plan (e.g., chronic pain + post-cervical-fusion mobility + skin-cancer surveillance), and a
  few scheduled/completed nurse + PT visits — so `/home-care` is populated out of the box (matches
  his chart, and his real PT-plus-visiting-nurse experience).

## Open decisions for review

1. **`HOME_BASED_CARE` default ON or OFF?** (Oncology is ON-by-default for demos; recommend ON.)
2. **One module key (`HBHC MANAGER`) for all writes, or per-discipline gating** of visit notes?
   (Recommend one key for Phase 1, refine later.)
3. **Census scope** — single `HHC-CENSUS:DEFAULT`, or per-team / per-site grains? (Recommend DEFAULT
   now, key by team later.)
4. **Visit notes** — store free-text `Summary` on the visit, or always a full TIU note? (Recommend a
   short structured summary on the visit + optional TIU note link.)
5. **Phase-2 scope confirmation** — OASIS-E2 (April 2026) is the moving target if/when we build it.
