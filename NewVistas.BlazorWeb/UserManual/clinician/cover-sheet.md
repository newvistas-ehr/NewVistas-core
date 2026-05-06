# Cover Sheet

**Route:** `/cover-sheet`

The Cover Sheet is the CPRS-style patient overview dashboard in NewVistas. It provides a unified, at-a-glance view of a patient's clinical status, consolidating data from multiple clinical domains into a single page. The Cover Sheet maps to the VistA ORWCV.m routine and is the primary entry point for patient-centered clinical work.

![Full Cover Sheet view with all panels populated](screenshots/cover-sheet-full.png)

---

## Loading the Cover Sheet

1. Navigate to `/cover-sheet` from the sidebar or by clicking a patient row on the Provider Dashboard.
2. Enter the **Patient ID** in the lookup bar at the top of the page.
3. Click **Load** (or press **Enter**).

The system calls the `PatientWorkflowGrain.GetCoverSheetAsync()` method, which orchestrates data retrieval from all relevant domain grains and returns a unified `CoverSheetState` object. The Cover Sheet loads all panels simultaneously.

> **Tip:** If you navigated from the Provider Dashboard or Patient Lookup, the Patient ID may already be pre-populated via the patient context service. You can also pass the patient ID as a query parameter: `/cover-sheet?patientId=PATIENT-001`.

---

## Patient Banner

Once a patient is loaded, the Patient Banner appears at the top of the Cover Sheet. It displays the patient's key identifying and clinical information at a glance.

![Patient banner with CWAD flags and service-connected badge](screenshots/cover-sheet-patient-banner.png)

The banner includes:

- **Patient Name** -- displayed prominently in bold.
- **Sex** -- the patient's recorded sex.
- **Age** -- calculated age in years (shown if date of birth is available).
- **Admitted Status** -- if the patient is currently admitted as an inpatient, an amber **"Admitted"** badge appears along with the room and bed assignment (e.g., "Admitted -- 3B-12").
- **Service Connected (SC) Percentage** -- if the patient has a service-connected disability rating, a green **SC** badge appears showing the percentage (e.g., "SC 70%").
- **CWAD Flags** -- a red badge displaying the patient's active posting flags.

### CWAD Flags

The CWAD flags are critical clinical safety indicators that alert providers to important patient information. The letters in the badge indicate:

| Flag | Meaning | Description |
|---|---|---|
| **C** | Crisis | Patient has an active crisis note or behavioral flag |
| **W** | Warning | Clinical warning or caution on the patient record |
| **A** | Allergies | Patient has documented allergies (always check allergy list) |
| **D** | Advance Directive | Patient has an advance directive on file |

> **Warning:** Always review the CWAD flags before interacting with a patient. A "C" (Crisis) flag in particular may indicate an active behavioral emergency or safety concern that requires immediate attention.

The CWAD badge only appears if at least one flag is active. The specific letters displayed indicate which flags are set.

---

## Workflow Actions Bar

Directly below the Patient Banner, a Workflow Actions bar provides quick-launch buttons for common clinical tasks. These buttons navigate you to the appropriate module with the current patient context pre-loaded:

- **New Order** -- navigates to `/orders` with the New Order tab active.
- **New Medication** -- navigates to `/orders` with the New Order tab active and the Pharmacy order type pre-selected.
- **New Lab Order** -- navigates to `/labs` with the current patient loaded.
- **New Consult** -- navigates to `/consults` with the current patient loaded.

---

## Cover Sheet Panels (Grid Layout)

The Cover Sheet displays clinical data in a responsive grid of eight panels. Each panel shows a focused summary of one clinical domain. The grid uses a responsive layout (minimum panel width of 340 pixels) that adapts to your screen size.

### Active Problems

![Active Problems panel](screenshots/cover-sheet-active-problems.png)

Displays the patient's active problem list. Each row shows:

| Column | Description |
|---|---|
| **Diagnosis Code** | ICD-10 code (displayed in monospace font, e.g., `E11.9`) |
| **Diagnosis** | Diagnosis description text (e.g., "Type 2 Diabetes Mellitus") |
| **Onset Date** | Date of onset in MM/DD/YY format |

If the patient has no active problems, the panel displays "No active problems" in italic text.

### Allergies

![Allergies panel](screenshots/cover-sheet-allergies.png)

Displays the patient's documented allergies. Each row shows:

| Column | Description |
|---|---|
| **Allergen** | Name of the allergen (e.g., "Penicillin") |
| **Severity** | Severity level: MILD, MODERATE, or SEVERE (displayed in amber text) |
| **Reactions** | Comma-separated list of reactions (e.g., "Rash, Hives") |

If the patient has no documented allergies, the panel displays "No Known Allergies."

> **Warning:** An empty allergy list is clinically ambiguous -- it may mean the patient has no allergies or that allergies have not been assessed. If you see "No Known Allergies," confirm with the patient that their allergy status has been formally assessed and documented. Use the Allergies page (`/allergies`) to formally document NKA.

### Active Medications

Displays the patient's current active medication profile. Each row shows:

| Column | Description |
|---|---|
| **Drug Name** | Name of the medication (e.g., "METFORMIN 500MG TAB") |
| **Sig** | Directions for use (e.g., "TAKE ONE TABLET BY MOUTH TWICE DAILY") |
| **Status** | Current status (Active, Hold, etc.) |

If the patient has no active medications, the panel displays "No active medications."

### Clinical Reminders

Displays clinical reminders that are due or recently resolved for the patient. Each row shows:

| Column | Description |
|---|---|
| **Reminder Name** | Name of the clinical reminder (e.g., "Influenza Vaccine") |
| **Status** | Current status: DUE, DONE, or N/A |
| **Due Date** | Date the reminder is due in MM/DD/YY format |

If the patient has no reminders, the panel displays "No reminders due."

### Recent Labs

![Recent Labs panel with abnormal flags](screenshots/cover-sheet-recent-labs.png)

Displays the patient's most recent laboratory results. Each row shows:

| Column | Description |
|---|---|
| **Test Name** | Name of the lab test (e.g., "CBC", "BMP", "Creatinine") |
| **Result/Units** | Result value and units (e.g., "7.5 K/cmm"); flagged results appear in bold red |
| **Flag** | Abnormality flag: H (High), L (Low), HH (Critical High), LL (Critical Low) |
| **Collection Date** | Date the specimen was collected in MM/DD/YY format |

If the patient has no recent labs, the panel displays "No recent labs."

> **Warning:** Results with **HH** (Critical High) or **LL** (Critical Low) flags require immediate clinical attention. These critical values indicate potentially life-threatening conditions.

### Recent Vitals

Displays the patient's most recent vital sign measurements. Each row shows:

| Column | Description |
|---|---|
| **Vital Type** | Type of vital sign (e.g., "TEMPERATURE", "BLOOD PRESSURE") |
| **Value/Units** | Measured value and units; abnormal values appear in bold red |
| **Date/Time** | Date and time the measurement was taken in MM/DD/YY HH:MM format |

If the patient has no recent vitals, the panel displays "No recent vitals."

### Appointments

![Appointments panel with Follow-Up booking form](screenshots/cover-sheet-appointments.png)

Displays the patient's recent and upcoming appointments. Each row shows:

| Column | Description |
|---|---|
| **Clinic Name** | Name of the clinic (e.g., "Primary Care Clinic 3B") |
| **Date/Time** | Appointment date and time in MM/DD/YY HH:MM format |
| **Status** | Appointment status (e.g., Scheduled, Checked In, Completed, No Show) |

If the patient has no appointments, the panel displays "No appointments."

#### Quick Follow-Up Booking

The Appointments panel includes a **+ Follow-Up** button in the panel header. Clicking this button opens an inline form to quickly schedule a follow-up appointment:

1. Click **+ Follow-Up** in the Appointments panel header.
2. Click **Load Clinics** to populate the clinic dropdown with active clinics.
3. Select a **Clinic** from the dropdown.
4. Set the **Date/Time** for the follow-up (defaults to 7 days from today at 9:00 AM).
5. Select the **Duration** (15, 20, 30, 45, or 60 minutes; defaults to 30).
6. Optionally enter a **Provider** name.
7. Optionally enter a **Purpose** (e.g., "Blood pressure recheck").
8. Click **Book** to schedule the appointment.

The system checks for scheduling conflicts. If a conflict is detected, an error message will indicate the conflict and direct you to the Scheduling page for override options.

> **Tip:** The Follow-Up booking feature is designed for quick scheduling of simple follow-up appointments. For complex scheduling needs (e.g., multi-provider appointments, recurring series, or specific resource requirements), use the full Scheduling module.

### Active Orders

Displays the patient's currently active orders. Each row shows:

| Column | Description |
|---|---|
| **Order Text** | Description of the order (e.g., "CBC WITH DIFFERENTIAL") |
| **Status** | Current order status (Active, Pending, Hold, etc.) |
| **Start Date** | Order start date in MM/DD/YY format |

If the patient has no active orders, the panel displays "No active orders."

---

## Display Configuration

The number of items shown in certain Cover Sheet panels is configurable through Site Parameters. The default display counts are:

| Panel | Default Count |
|---|---|
| Recent Vitals | 10 |
| Active Orders | 5 |
| Recent Notes | 10 |

Your site administrator can adjust these defaults through the Site Parameters configuration. See the [Administrator Guide](../admin/index.md) for details.

---

## Navigating from the Cover Sheet

The Cover Sheet is designed as a launching point for deeper clinical work. You can navigate to detailed views for any domain by:

- Clicking the **Workflow Actions** buttons (New Order, New Medication, New Lab Order, New Consult) in the actions bar.
- Using the sidebar navigation to go directly to any clinical module (Orders, Notes, Problems, Labs, Vitals, etc.).
- Using the Patient Lookup (`/patient-lookup`) to switch to a different patient.

The current patient context is maintained as you navigate between modules, so you do not need to re-enter the Patient ID on each page.
