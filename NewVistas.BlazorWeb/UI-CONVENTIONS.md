# NewVistas Modern UI Conventions — for review

> Status: **style guide only. No code has been changed.** Documents the single house style for
> the two *modern* UIs, the auth/nav model, the patient-safety alerting standard, and lists every
> page currently off-convention so the migration can be reviewed and scheduled. Surfaced by the
> automated UI test run, which needed three strategies to drive screens that should be identical.

## Scope — which UIs this governs

NewVistas ships several front ends on purpose; they are **not** held to the same bar:

| UI | Role | Convention bar |
|---|---|---|
| **Blazor (main)** | Modern web app | **This guide** — modern, consistent, safe |
| **WPF (main)** | Modern desktop app | **This guide** — same *interaction* model (own component pass) |
| **CharUI** | Throwback — VistA character UI | Judge by **fidelity to VistA**, not modern norms. Out of scope. |
| **WpfDelphiUI** | Throwback — CPRS / RPMS Delphi front end | Judge by **fidelity to CPRS/RPMS**. Out of scope. |

The throwbacks deliberately preserve legacy muscle memory and must stay faithful to their
originals. This guide standardizes **only the two modern UIs**. Markup examples below are Blazor;
WPF-main adopts the same interaction model via its own shared controls.

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

> **Decided:** canonical = the **clinical/custom idiom**; patient input = **`lookup-input`** (via
> `<PatientBar>`); form field = **`form-input`** (via `<FormField>`). (Both chosen over the
> Bootstrap variants for semantic clarity; the shared component makes the class uniform anyway.)

## 7. Authentication & navigation

- **Anonymous = login only.** Before sign-in, render *only* the login screen — no app shell, no
  nav, no site map. Today the full menu renders behind the login redirect; that exposure must go.
  If you're not logged in, you don't see the system's map.
- **Nav is generated from the user's role / security keys**, not hand-listed. A doctor sees
  clinical sections, billing sees financial, a nurse sees nursing — each user sees only what their
  keys grant (`RequiresSecurityKey` / `AccessControlGrain` already model this). Build the nav
  *from* permissions via a shared `<RoleNav>`.
- **Why:** least privilege, less clutter, and a safety benefit — you can't wander into a tool you
  shouldn't operate.

## 8. Patient-safety alerting (tiered)

The governing rule is counterintuitive: **warn rarely.** Universal pop-ups train clinicians to
dismiss everything (*alert fatigue*) — which is exactly how the lethal warning gets clicked
through. Alerts are **severity-tiered**, and the top tier is **reserved and unmistakable**.

| Tier | When | Interaction |
|---|---|---|
| **0 — inline** | low-risk (formulary note) | non-blocking hint, no interruption |
| **1 — soft stop** | moderate (minor interaction) | one acknowledge, logged |
| **2 — hard stop** | dangerous: dose > max / lethal range, allergy contraindication, severe interaction, duplicate opioid | the **anti-muscle-memory** pattern below |

### `<SafetyConfirm>` — the reserved Tier-2 pattern (identical in Blazor and WPF)
A Tier-2 confirmation **must not be dismissible by reflex**:
1. **State the specific hazard with real numbers, in plain language** — not "Are you sure?":
   *"Nucynta 50 mg × 100/day = 5,000 mg/day. Max recommended 600 mg/day — lethal range."*
2. **Safe choice is the default** — *Cancel / Modify* is highlighted and is the keyboard default;
   the override is visually secondary and **not pre-focused**.
3. **Override requires active engagement, not a click** — proceed stays disabled until the
   prescriber **types the value to confirm** ("to override, type the daily dose: `5000`") **and**
   enters a **reason**. The proceed button is **not in the routine-OK position**, so the reflex
   misses.
4. **Reserved styling** — the Tier-2 danger treatment is used *only* here, so a real hazard never
   looks like a routine prompt.
5. **Lethal class = true hard stop (co-sign).** For the most dangerous class (e.g. a Schedule-II
   opioid overdose), a solo override is **not** allowed — it requires a **pharmacist or attending
   co-sign**. *(Approved policy.)*
6. **Every override is audited** — who, when, the hazard, the reason → `LogAuditEventAsync`.

Hazard *detection* already exists (`ScreenPrescriptionForInteractionsAsync`, DUR, allergy checks);
this standardizes how it is **presented and confirmed**. Because the Tier-2 pattern is reserved and
identical everywhere, clinicians instantly recognize a genuine hazard — an inconsistent danger
signal is itself a safety risk.

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

## Decisions (approved)
1. Canonical idiom = **clinical/custom** (not Bootstrap).
2. Patient-input class = **`lookup-input`** (via `<PatientBar>`).
3. Form-field class = **`form-input`** (via `<FormField>`).
4. Navigation = **login-first + role/key-generated** (`<RoleNav>`); no pre-auth site map.
5. Patient safety = **tiered alerts**; reserved **Tier-2 `<SafetyConfirm>`** (type-to-confirm +
   reason + reserved styling + audit); **lethal class requires co-sign**.
6. Rollout = **shared components + pilot**, then migrate by cluster; add a CI guard against drift.
7. **Status / severity badges** = canonical `.badge` + `.badge-danger/.warning/.info/.success/.neutral` (and `.row-danger/.row-warning` table tints) in app.css — replaces Bootstrap `badge bg-*`. Use ONLY for clinically-meaningful at-a-glance cues (ESI acuity, pain score, order/task priority); keep rare so the color stays meaningful. (A patient-safety signal — don't drop these in a migration.)
8. **No danger/warning button variant.** Buttons are only `btn btn-primary` (navy) and `btn btn-secondary` (grey). Destructive/cautionary actions (Discontinue, Stop, Revoke, Merge, Hold, Override, Remove) use `btn btn-primary` — patient-safety friction is enforced by the `<SafetyConfirm>` Tier-2 gate (§8), not by button color.

Scope: the **two modern UIs only** (Blazor-main, WPF-main). CharUI and WpfDelphiUI stay faithful throwbacks.

## Enforcement — CI drift guard

`scripts/check-ui-conventions.sh` scans `NewVistas.BlazorWeb/Components/Pages/*.razor` and fails (exit 1) if any off-convention marker reappears: `tab-btn`, `nav-link`/`nav-tabs`, `btn-success`/`btn-outline`/`btn-danger`/`btn-warning`/`btn-info`, standalone `btn-primary`, `alert-danger`, `form-control`/`form-select`, Bootstrap `table table-*`, `badge bg-*`, and dark-theme residue (`#1a1a2e`/`#16213e`/`#0d1117`/`#0f1419`). It runs on every push/PR via `.github/workflows/ui-conventions.yml`.

- **Out of scope (not scanned):** `PatientPortal.razor` (patient-facing UI, its own treatment) and `Login.razor` (pre-auth). The WPF CharUI / WpfDelphiUI families are separate projects (deliberate VistA/CPRS throwbacks).
- **Nested sub-tabs** are named `.sub-tab` (not `.sub-tab-btn`); compact tables use `.data-table-compact` on top of `.data-table` — both stay clear of the guard's patterns.
- For a deliberate, reviewed exception, add the file to `EXEMPT` in the script.
