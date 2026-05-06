# Clinical Registries and Special Programs

Routes: `/clinical-registries`, `/polytrauma`, `/transplant`

---

## Clinical Case Registries (`/clinical-registries`)

The registries module manages patient enrollment and tracking for chronic disease registries and special population programs.

![Clinical registries dashboard](screenshots/admin-registries-dashboard.png)

### Registry Types

| Registry | Key Data Points |
|---|---|
| **HIV** | CD4 count, viral load, ART regimen, resistance testing, opportunistic infections |
| **Hepatitis C** | Genotype, viral load, DAA regimen, SVR status, fibrosis score |
| **Diabetes** | HbA1c, type (1/2), medications, eye exam, foot exam, nephropathy screening |
| **Asthma** | Severity (INTERMITTENT / MILD_PERSISTENT / MODERATE_PERSISTENT / SEVERE_PERSISTENT), controller meds, exacerbations, spirometry, action plan |

### Enrollment Statuses

| Status | Description |
|---|---|
| **ACTIVE** | Currently enrolled and receiving care |
| **MONITORING** | In monitoring phase, reduced frequency |
| **REMISSION** | Disease in remission, maintenance tracking |
| **INACTIVE** | No longer actively followed |
| **TRANSFERRED** | Transferred to another facility |
| **DECEASED** | Patient deceased |

### Tabs

**Enrolled Patients** — Patient list filtered by registry with status badges, key metrics, last update date, and next due actions.

![Enrolled patients list with status badges](screenshots/admin-registries-enrolled-patients.png)

**Dashboard** — Aggregate enrollment statistics including:

- Enrollment trends over time
- Status distribution (active, monitoring, remission, etc.)
- Care gap identification (overdue screenings, labs, visits)
- Facility performance against national benchmarks

**Site Index** — Cross-facility enrollment aggregation and comparison.

---

## Polytrauma / TBI (`/polytrauma`)

Manages Traumatic Brain Injury screening and polytrauma patient tracking.

![Polytrauma TBI screening tab](screenshots/admin-polytrauma-tbi.png)

### Tab 1: TBI Screenings

**Screening components:**

- Exposure history (blast, vehicle accident, fall, assault)
- Acute symptoms (loss of consciousness, alteration of consciousness, post-traumatic amnesia)
- Current symptoms (headaches, dizziness, memory problems, balance issues, irritability, sleep disturbance)

**Screening results:** POSITIVE, NEGATIVE, INDETERMINATE

> **Note:** A positive TBI screen requires a comprehensive TBI evaluation by a qualified provider. Screening alone does not establish a TBI diagnosis.

### Tab 2: Polytrauma Registry

**Patient record fields:**

| Field | Description |
|---|---|
| Injury Date | Date of polytraumatic injury |
| Mechanism | BLAST, MVA, FALL, ASSAULT, OTHER |
| Severity | Overall injury severity |
| TBI Severity | Mild, Moderate, Severe |
| Care Phase | ACUTE, REHABILITATION, COMMUNITY_REINTEGRATION, LONG_TERM_FOLLOW_UP |

**Functional assessment:** FIM (Functional Independence Measure) scores for motor and cognitive domains. Treatment goals and progress tracking.

### Tab 3: Dashboard

Aggregate statistics, screening completion rates, registry enrollment, and follow-up compliance metrics.

---

## Transplant (`/transplant`)

Manages transplant waiting lists, donor matching, and post-transplant tracking.

![Transplant waiting list](screenshots/admin-transplant-waitlist.png)

### Patient Records

| Field | Description |
|---|---|
| Blood Type | ABO group and Rh factor |
| PRA | Panel Reactive Antibody percentage |
| HLA Typing | Human Leukocyte Antigen typing |
| Transplant History | Previous transplants with dates and outcomes |

### Waitlist

**Organ types:** KIDNEY, LIVER, HEART, LUNG, PANCREAS, INTESTINE

**Waitlist statuses:**

| Status | Description |
|---|---|
| **ACTIVE** | Actively waiting for organ |
| **INACTIVE_TEMPORARY** | Temporarily inactive (medical hold) |
| **TRANSPLANTED** | Received transplant |
| **REMOVED** | Removed from list |
| **DECEASED** | Deceased while waiting |

**Status transitions:** ACTIVE <-> INACTIVE_TEMPORARY, ACTIVE -> TRANSPLANTED / REMOVED

**Priority scoring** based on medical urgency, time on list, geographic proximity, and organ-specific criteria.

### Donors

| Field | Description |
|---|---|
| Donor Type | LIVING or DECEASED |
| Blood Type | ABO group and Rh factor |
| Organ Types | Available organs for donation |
| HLA Typing | For crossmatch compatibility |
| Crossmatch Results | Compatible / Incompatible |

### Post-Transplant Tracking

Monitor graft function, immunosuppression regimen, rejection episodes, and long-term outcomes.

---

## Related Pages

| Page | Route | Description |
|---|---|---|
| Oncology | `/oncology` | Tumor registry and cancer treatment tracking (see [Specialty Clinical](../clinician/specialty.md)) |
| Suicide Prevention | `/suicide-prevention` | High-risk roster and safety planning (see [Mental Health](../clinician/mental-health.md)) |
| Quality Management | `/quality-management` | Quality registries and incident tracking (see [Quality & Safety](quality-safety.md)) |
