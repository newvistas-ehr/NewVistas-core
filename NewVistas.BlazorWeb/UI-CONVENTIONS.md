# NewVistas Blazor UI Conventions — PROPOSED (for review)

> Status: **proposal / style guide only. No code has been changed.** This documents the
> single house style we should adopt and lists every page that is currently off-convention,
> so the migration can be reviewed and scheduled. Surfaced by the automated UI test run,
> which needed three different strategies to drive screens that should behave identically.

## Why this matters

A clinician who learns a screen should be able to predict every other screen: *type the
patient ID → press **Load** → pick a tab → fill the form → click the **primary** button →
read the **green** success / **red** error banner.* Today that muscle memory breaks between
pages. The same inconsistency is why the screens are hard to test and automate.

## The problem, measured (147 page files)

There are effectively **two design languages** plus a third tab variant:

| Concern | "Clinical/custom" idiom | "Bootstrap" idiom | Other |
|---|---|---|---|
| Patient input | `input.lookup-input` (39) | `form-control` + `placeholder="Patient ID"` (79) | — |
| Tabs | `.tab` (124) | `.nav-link` (18) | `.tab-btn` (44) |
| Submit / primary | `btn-primary` (111) | `btn-success` (27) | — |
| Error banner | `.alert-error` (78) | `.alert-danger` (31) | bare `.error` |
| Success banner | `.alert-success` (77) | (varies) | — |
| Form fields | `form-input` (26) | `form-control` (31) | — |
| Create action | a **tab** | `+ New X` reveal button (16) | inline |

## Canonical house style (proposed)

Pick the **clinical/custom** idiom (it is the majority for tabs, submit, and banners) and
enforce it through **shared components** so consistency is structural, not per-page discipline.

### 1. Patient context → `<PatientBar />` (new shared component)
One bar on every patient-scoped page. Internally: `input.lookup-input` (placeholder
`Patient ID`) + a button labeled **Load** + Enter-to-load. It reads/writes the shared
`PatientContextService`, so **the selected patient carries across pages** — you set it once.
```razor
<PatientBar OnPatientLoaded="Reload" />   @* replaces every ad-hoc patient input+Load *@
```

### 2. Tabs → `.tab` (via `<TabStrip />`)
```razor
<button class="tab @(active=="view" ? "active":"")" @onclick='() => active="view"'>View</button>
<button class="tab @(active=="add"  ? "active":"")" @onclick='() => active="add"'>Add</button>
```
Retire `.nav-link` and `.tab-btn`.

### 3. Create action is always a **tab** named "Add X" / "Record X" / "New X"
Retire the `+ New X` reveal-button pattern — the create form is a tab like View/History.

### 4. Buttons → `btn btn-primary` for the primary/submit action
One primary color. Retire `btn-success` for submits (color shouldn't vary by page).

### 5. Banners → `<AlertBanner />` (`.alert-success` / `.alert-error`)
```razor
@if (error != null)   { <div class="alert alert-error">@error</div> }
@if (success != null) { <div class="alert alert-success">@success</div> }
```
Retire `.alert-danger` and bare `.error`. (Note: success banners are reloaded away on some
pages — keep them visible until the next user action so the result is observable.)

### 6. Form fields → `<FormField Label="…">` (label + `form-input`)
```razor
<div class="form-group"><label>Diagnosis <span class="required">*</span></label>
  <input class="form-input" @bind="…" placeholder="…" /></div>
```
Retire bare `form-control` form fields.

> **Two choices flagged for your call** (the majority differs from the clinical idiom):
> - **Patient input class**: clinical uses `lookup-input` (39) but `form-control`+PID is more
>   common (79). With a shared `<PatientBar>` the underlying class is uniform either way — I
>   recommend `lookup-input` for semantic clarity. Confirm or override.
> - **Form field class**: `form-input` (26) vs `form-control` (31) is nearly even. I recommend
>   `form-input`; say the word if you'd rather standardize on Bootstrap `form-control`.

## Migration checklist (who is off-convention today)

### Error banner → `.alert-error` (currently `.alert-danger`) — 31 pages
ARAgingDashboard, AccountsReceivable, AgentCashier, AutoEligibility, ClaimStatusInquiry, CollectionLetters, Dental, EdiBilling, EligibilityVerification, Epcs, FeeBasis, HomeTelehealth, IVPharmacy, Ifcap, IntegratedBilling, LabTech, Login, NursingCarePlan, NursingTaskWorklist, NursingTriage, PainAssessment, PatientAdvocate, PharmacyPos, Prenatal, RadTech, Registration, RegistrationEnhanced, ReleaseOfInformation, ShiftHandoff, SubstanceAbuseTreatment, TopMatching

### Tabs → `.tab` (currently `.nav-link`) — 18 pages
ARAgingDashboard, ClaimStatusInquiry, CollectionLetters, EligibilityVerification, Epcs, LabTech, NursingCarePlan, NursingTaskWorklist, NursingTriage, PainAssessment, PharmacyPos, Prenatal, RadTech, Registration, RegistrationEnhanced, ShiftHandoff, SubstanceAbuseTreatment, TopMatching

### Tabs → `.tab` (currently `.tab-btn`) — 44 pages
AccountsReceivable, AgentCashier, AmbulatoryCopay, AnesthesiaTracking, AppointmentWaitList, AutoRefill, CancerRegistry, ClinicalCaseRegistries, ClinicalQualityMeasures, CompensationPension, ConsultServiceDirectory, ControlledSubstances, DataSegmentation, DecisionSupport, Dental, DirectMessaging, DrugInteractionData, EdiBilling, ElectronicCaseReporting, EncounterForm, ExternalReferral, FeeBasis, GeriatricsExtendedCare, GpraReporting, HealthSummary, HomeHealth, HomeTelehealth, ICareDashboard, Ifcap, IntegratedBilling, LabShipping, MassCasualty, Medicine, Nursing, Oncology, PatientRecall, PeriodontalChart, PharmacyBenefits, PolytraumaTBI, QualityManagement, RecordTracking, ResearchIRB, Transplant, VoluntaryService

### Submit → `btn-primary` (currently `btn-success`) — 27 pages
AccountsReceivable, AgentCashier, AutoEligibility, ClaimStatusInquiry, CollectionLetters, EdiBilling, EligibilityVerification, Engineering, Epcs, EventCapture, FeeBasis, Ifcap, IntegratedBilling, LabTech, NursingCarePlan, NursingTaskWorklist, NursingTriage, PainAssessment, PatientAdvocate, PharmacyPos, Prenatal, RadTech, RegistrationEnhanced, ReleaseOfInformation, ShiftHandoff, SubstanceAbuseTreatment, TopMatching

### Create action → a tab (currently `+ New X` reveal button) — 16 pages
AccountsReceivable, ControlledSubstances, Dietetics, DrugInteractionData, EdiBilling, HealthSummary, Ifcap, Oncology, PatientAdvocate, PatientPortal, Radiology, ReleaseOfInformation, Reminders, SocialWork, SpinalCordInjury, Surgery

### Patient bar → `<PatientBar />` — 79 pages on `form-control`+`placeholder="Patient ID"`, 39 on `lookup-input`
(All adopt the shared component; the 79 are the bulk of the work.)

## Observation: the off-convention pages cluster

The same names recur (AR/billing: AccountsReceivable, AgentCashier, EdiBilling, FeeBasis,
Ifcap, IntegratedBilling, ClaimStatusInquiry, CollectionLetters; registration: Registration,
RegistrationEnhanced; and several nursing pages: NursingCarePlan, NursingTriage,
NursingTaskWorklist, PainAssessment, ShiftHandoff). These were evidently built in the
Bootstrap idiom. That makes migration **batchable by feature area**, not scattered.

## Proposed rollout (after this guide is approved)

1. **Build the shared components** — `PatientBar`, `TabStrip`, `AlertBanner`, `FormField` + a single CSS partial. (No page changes yet.)
2. **Convert a pilot wave** — the ~5 clinical pages the deep test already covers (Vitals, Problems, Allergies, Care Plan, Mental Health) to prove the components and the test harness collapse to one pattern.
3. **Migrate by cluster** — Nursing pages → Pharmacy → AR/Billing → Registration → the long tail. Each wave re-run through the smoke + deep harness to confirm no regressions.
4. **Add a guard** — a tiny analyzer/test that fails CI if a page reintroduces `nav-link` / `tab-btn` / `btn-success` / `alert-danger` / a bare patient input, so it can't drift again.

## Open questions for review
1. Approve the **clinical/custom idiom** as canonical? (vs. standardizing on Bootstrap.)
2. Patient-input class: `lookup-input` (recommended) or `form-control`?
3. Form-field class: `form-input` (recommended) or `form-control`?
4. Rollout: shared components + pilot first (recommended), and migrate by the feature clusters above?
