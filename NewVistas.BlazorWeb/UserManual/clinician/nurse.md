# Nurse Guide

This guide is written for Registered Nurses (RNs), Licensed Practical Nurses (LPNs), Licensed Vocational Nurses (LVNs), and Certified Nursing Assistants (CNAs) who use the NewVistas clinical information system. It covers the core nursing workflows you will perform daily: nursing assessments, vital signs, BCMA medication administration, care planning, acuity scoring, nursing documentation, unit census management, and bed assignments.

![Nursing dashboard overview showing unit census and shift summary](screenshots/nursing-dashboard-overview.png)

---

## Role Description

As a nurse in NewVistas, your responsibilities include:

- **Performing nursing assessments** -- conducting initial, shift, focused, discharge, and PRN assessments across all body systems with validated scoring tools (Braden, Morse, Pain).
- **Recording vital signs** -- measuring and documenting temperature, pulse, respiration, blood pressure, weight, height, pain, and pulse oximetry at scheduled intervals and as needed.
- **Administering medications via BCMA** -- using the Barcode Medication Administration system to safely scan, verify, and administer medications with full five-rights verification.
- **Developing and evaluating care plans** -- creating individualized nursing care plans with nursing diagnoses, goals, interventions, expected outcomes, and shift-level evaluations.
- **Scoring patient acuity** -- assessing and updating patient acuity levels to support appropriate nurse-to-patient ratios and staffing decisions.
- **Documenting nursing notes** -- writing shift assessments, progress notes, and incident documentation using the TIU (Text Integration Utilities) framework.
- **Managing unit census** -- monitoring bed occupancy, patient assignments, and aggregate unit statistics.
- **Reviewing orders** -- reviewing and acknowledging physician orders, flagging orders that require nursing clarification, and carrying out nursing-specific orders.

---

## Daily Workflow Overview

A typical nursing shift in NewVistas follows this seven-step workflow:

1. **Sign in and review the unit census** (`/nursing`). The Unit Census tab shows all patients currently assigned to your unit, their acuity levels, nurse assignments, and key status indicators. Review your patient assignments, check for new admissions, discharges, and transfers since the last shift, and note any patients flagged for special precautions.

2. **Perform initial assessments.** For each assigned patient, complete a shift assessment on the Assessments tab. Conduct a head-to-toe systems review covering neurological, respiratory, cardiovascular, integumentary, pain, gastrointestinal, genitourinary, psychosocial, and mobility domains. Calculate Braden (skin integrity) and Morse (fall risk) scores.

3. **Review the Cover Sheet and orders** (`/cover-sheet`). Open each patient's Cover Sheet to review active problems, allergies, medications, recent lab results, recent vitals, and active orders. Pay special attention to new orders entered since the last shift and any clinical reminders that are due.

4. **Record vital signs** (`/vitals`). Take and document vital signs at the intervals specified by the patient's orders and unit policy. Record temperature, pulse, respiration, blood pressure, pulse oximetry, pain score, and weight/height as indicated.

5. **Administer medications via BCMA** (`/bcma`). Open the BCMA page to view the Medication Administration Record (MAR). Scan the patient's wristband barcode, scan each medication barcode, verify the five rights, administer the medication, and document the administration in real time.

6. **Document care and nursing notes** (`/notes`). Throughout the shift, document nursing interventions, patient responses, patient/family education, and any notable events. Use the TIU document framework to create nursing progress notes, assessment summaries, and incident reports as needed.

7. **Handoff and end-of-shift.** Before ending your shift, update all care plans on the Care Plan tab, finalize acuity scores on the Acuity tab, sign all draft assessments and notes, and provide a verbal and electronic handoff to the oncoming nurse. Verify that no unsigned documents or incomplete tasks remain for your patients.

---

## Nursing (/nursing)

**Route:** `/nursing`

The Nursing page is the primary workspace for nursing-specific clinical functions. It maps to VistA Files #210 (Nursing Site Parameters), #211 (Nursing Care Plan), and #212 (Nursing Assessment). The page is organized into four tabs: Assessments, Care Plan, Acuity, and Unit Census.

![Nursing page showing the four-tab layout with Assessments tab selected](screenshots/nursing-tabs-overview.png)

---

### Assessments Tab

The Assessments tab provides the interface for recording, reviewing, and signing nursing assessments. When you load a patient, the tab displays a list of all assessments on file for that patient, sorted by date and time in descending order (most recent first).

#### Loading Assessments

1. Enter the **Patient ID** in the lookup bar.
2. Click **Load** (or press **Enter**).

The system retrieves the patient's nursing assessments and displays them in the assessment list.

#### Assessment List

The assessment list table displays the following columns:

| Column | Description |
|---|---|
| **Date/Time** | Date and time the assessment was performed, in MM/DD/YYYY HH:MM format |
| **Type** | Assessment type: Initial, Shift, Focused, Discharge, or PRN |
| **Nurse** | Name of the nurse who performed the assessment |
| **Status** | Document status: Draft (editable) or Signed (finalized, read-only) |
| **Pain** | Pain score on the 0-10 numeric rating scale |
| **Morse** | Morse Fall Scale score (0-125) |
| **Braden** | Braden Scale score (6-23) for pressure injury risk |

If no assessments are recorded for the patient, the tab displays "No assessments on file."

#### Assessment Domains

Each assessment contains the following clinical domains. Every domain has specific fields that must be evaluated during a comprehensive assessment.

**Neurological:**

| Field | Options/Range | Description |
|---|---|---|
| **Level of Consciousness** | Alert, Voice responsive, Pain responsive, Unresponsive (AVPU) | Patient's responsiveness level using the AVPU scale |
| **Orientation** | Person, Place, Time, Situation (check all that apply) | Orientation domains the patient is oriented to |
| **Pupil Response** | Reactive, Sluggish, Fixed, Unequal | Pupillary light reflex |
| **GCS Score** | 3-15 | Glasgow Coma Scale score if applicable |

**Respiratory:**

| Field | Options/Range | Description |
|---|---|---|
| **Breath Sounds** | Clear, Diminished, Crackles, Wheezes, Rhonchi, Absent | Auscultated lung sounds |
| **O2 Therapy** | None, Nasal Cannula, Simple Mask, Non-Rebreather, CPAP, BiPAP, Ventilator | Current oxygen delivery method |
| **SpO2** | 0-100% | Pulse oximetry reading |
| **Respiratory Effort** | Normal, Labored, Shallow, Accessory muscle use | Observed work of breathing |

**Cardiovascular:**

| Field | Options/Range | Description |
|---|---|---|
| **Heart Rhythm** | Regular, Irregular, Irregularly Irregular | Cardiac rhythm on auscultation |
| **Heart Sounds** | Normal S1/S2, Murmur, Gallop, Rub | Auscultated heart sounds |
| **Edema** | None, 1+, 2+, 3+, 4+ | Peripheral edema grading |
| **Capillary Refill** | < 2 sec, 2-4 sec, > 4 sec | Peripheral perfusion indicator |
| **Peripheral Pulses** | Strong, Weak, Absent | Palpable pulse quality |

**Integumentary:**

| Field | Options/Range | Description |
|---|---|---|
| **Skin Integrity** | Intact, Impaired, Wound present, Pressure injury | Overall skin condition |
| **Skin Color** | Normal, Pale, Flushed, Cyanotic, Jaundiced, Mottled | Observed skin coloration |
| **Skin Turgor** | Normal, Tenting | Hydration assessment |
| **Braden Score** | 6-23 | Composite Braden Scale score for pressure injury risk |

The Braden Scale assesses six subscales (Sensory Perception, Moisture, Activity, Mobility, Nutrition, Friction/Shear) and produces a total score that indicates pressure injury risk:

| Score Range | Risk Level | Recommended Action |
|---|---|---|
| 19-23 | No risk | Standard care; reassess per unit protocol |
| 15-18 | Mild risk | Implement prevention protocol; reposition every 2 hours |
| 13-14 | Moderate risk | Add pressure redistribution surface; nutritional consult |
| 10-12 | High risk | All above plus wound care consult; increase turning frequency |
| 6-9 | Very high risk | All above plus specialty bed; consider wound care team |

> **Warning:** A Braden score of 14 or below triggers an automatic clinical decision support alert. The system will prompt you to initiate a skin care plan and notify the charge nurse.

**Pain:**

| Field | Options/Range | Description |
|---|---|---|
| **Pain Score** | 0-10 | Numeric Rating Scale (0 = no pain, 10 = worst possible pain) |
| **Pain Location** | Free text | Anatomic location of pain (e.g., "lower back", "right knee") |
| **Pain Quality** | Sharp, Dull, Aching, Burning, Throbbing, Stabbing, Radiating | Character of the pain |
| **Pain Onset** | Free text | When the pain started or changed |
| **Non-Verbal Pain Indicators** | Grimacing, Guarding, Restlessness, Moaning, Diaphoresis | For patients unable to self-report |

**Gastrointestinal:**

| Field | Options/Range | Description |
|---|---|---|
| **Bowel Sounds** | Present, Hypoactive, Hyperactive, Absent | Auscultated bowel sounds in all four quadrants |
| **Appetite** | Good, Fair, Poor, NPO | Patient's current appetite and oral intake status |
| **Last Bowel Movement** | Date/time | Date and time of last documented bowel movement |
| **Abdomen** | Soft, Firm, Distended, Tender | Abdominal assessment findings |

**Genitourinary:**

| Field | Options/Range | Description |
|---|---|---|
| **Urine Output** | mL/hr or mL/shift | Measured urine output |
| **Urine Color** | Clear yellow, Dark amber, Cloudy, Hematuria | Observed urine characteristics |
| **Foley Catheter** | Yes/No | Whether an indwelling urinary catheter is present |
| **Foley Day** | Numeric | Number of days the catheter has been in place (for CAUTI tracking) |

**Psychosocial:**

| Field | Options/Range | Description |
|---|---|---|
| **Anxiety Level** | None, Mild, Moderate, Severe | Assessed anxiety level |
| **Mood** | Appropriate, Depressed, Agitated, Flat, Euphoric, Labile | Observed mood and affect |
| **Coping** | Effective, Ineffective | Assessment of patient coping mechanisms |
| **Support System** | Present, Absent, Limited | Availability of family/social support |

**Mobility / Fall Risk:**

| Field | Options/Range | Description |
|---|---|---|
| **Mobility Level** | Independent, Assist x1, Assist x2, Dependent, Bedrest | Current mobility status |
| **Assistive Devices** | None, Cane, Walker, Wheelchair, Crutches | Mobility aids in use |
| **Morse Score** | 0-125 | Morse Fall Scale composite score |
| **Fall Risk Level** | Low, Moderate, High | Derived risk category |

The Morse Fall Scale assesses six factors (History of Falling, Secondary Diagnosis, Ambulatory Aid, IV/Heparin Lock, Gait, Mental Status) and produces a composite score:

| Score Range | Risk Level | Recommended Action |
|---|---|---|
| 0-24 | Low risk | Standard fall precautions; good nursing practice |
| 25-50 | Moderate risk | Implement fall prevention protocol; yellow wristband; bed alarm |
| 51+ | High risk | All above plus 1:1 sitter consideration; high-risk signage; non-slip socks |

> **Warning:** A Morse score of 25 or above triggers an automatic clinical decision support alert. The system will prompt you to activate fall prevention interventions and update the care plan.

**Narrative Notes:**

| Field | Description |
|---|---|
| **Narrative** | Free-text field for additional observations, patient statements, and clinical findings not captured by structured fields. Use this field to document patient quotes, clinical context, and any assessment findings that require narrative explanation. |

#### Recording a New Assessment

Follow these steps to record a new nursing assessment:

1. **Enter the Patient ID** in the lookup bar and click **Load** to establish the patient context.

2. **Click "New Assessment"** to open the assessment entry form.

3. **Select the Assessment Type** from the dropdown: Initial (first assessment after admission), Shift (routine shift assessment), Focused (targeted reassessment of a specific concern), Discharge (assessment at time of discharge), or PRN (as-needed assessment triggered by a change in condition).

4. **Complete each assessment domain.** Work through the assessment systematically, documenting findings for each body system. Required fields are marked with an asterisk. At minimum, a shift assessment requires: Level of Consciousness, Breath Sounds, Heart Rhythm, Skin Integrity, Pain Score, Bowel Sounds, Urine Output, Mobility Level, Braden Score, and Morse Score.

5. **Enter the Braden and Morse scores.** Calculate each score using the subscale criteria. The system will automatically derive the risk level from the composite score and display it alongside the numeric value.

6. **Add narrative notes.** Document any additional observations, patient statements, or clinical context in the Narrative Notes field.

7. **Click "Save as Draft"** to save the assessment in Draft status. A draft assessment can be edited and updated until it is signed. The system displays a confirmation message and the new assessment appears at the top of the assessment list with a Draft status badge.

8. **Sign the assessment.** When you are satisfied that the assessment is complete and accurate, click **"Sign"** on the draft assessment. Enter your electronic signature code when prompted. Signing changes the status from Draft to Signed.

> **Warning:** Once an assessment is signed, it becomes a permanent part of the legal medical record and cannot be edited or deleted. If an error is discovered after signing, create an addendum assessment of type PRN with corrections documented in the narrative notes. Always verify all findings before signing.

> **Tip:** For efficiency, complete the assessment form while at the bedside. If you must step away before finishing, save as Draft and return to complete and sign the assessment before the end of your shift. Unit policy typically requires all shift assessments to be signed within two hours of the assessment time.

![Nursing assessment form showing body system domains and scoring tools](screenshots/nursing-assessment-form.png)

---

### Care Plan Tab

The Care Plan tab provides the interface for creating, viewing, and evaluating individualized nursing care plans. Care plans in NewVistas follow the nursing process framework: Assessment, Diagnosis, Planning, Implementation, and Evaluation.

#### Care Plan Components

Each care plan entry contains the following components:

| Component | Description |
|---|---|
| **Problem** | The nursing diagnosis or patient problem statement (e.g., "Risk for Impaired Skin Integrity", "Acute Pain", "Impaired Physical Mobility") |
| **Goal** | The measurable patient-centered goal to be achieved (e.g., "Patient will maintain intact skin throughout hospitalization", "Patient will report pain <= 4/10 within 30 minutes of intervention") |
| **Interventions** | The specific nursing actions to address the problem (e.g., "Reposition every 2 hours", "Assess pain using NRS before and after medication administration", "Assist with ambulation TID") |
| **Expected Outcome** | The anticipated result of the interventions (e.g., "No new pressure injuries", "Pain controlled at acceptable level", "Patient ambulates 200 feet by discharge") |
| **Evaluation** | The assessment of progress toward the goal: Met, Partially Met, or Not Met, with supporting narrative |

#### Care Plan Table

The care plan table displays all active and resolved care plans for the patient with the following columns:

| Column | Description |
|---|---|
| **Problem** | Nursing diagnosis or problem statement |
| **Goal** | Target patient outcome |
| **Status** | Active or Resolved |
| **Last Evaluated** | Date and time of the most recent evaluation |
| **Evaluation** | Most recent evaluation result: Met, Partially Met, or Not Met |

#### Creating a New Care Plan

1. **Click "New Care Plan"** on the Care Plan tab.

2. **Enter the Problem** (nursing diagnosis). Select from the standardized nursing diagnosis list or enter a custom problem statement. Common nursing diagnoses include: Risk for Falls, Impaired Skin Integrity, Acute Pain, Risk for Infection, Impaired Physical Mobility, Anxiety, Deficient Knowledge, Imbalanced Nutrition, Impaired Gas Exchange, and Risk for Bleeding.

3. **Define the Goal.** Write a specific, measurable, achievable, realistic, and time-bound (SMART) goal statement. Include the patient as the subject (e.g., "Patient will..." not "Nurse will...").

4. **Add Interventions.** Enter one or more nursing interventions. Each intervention should be specific and actionable. Click "Add Intervention" to add additional intervention rows.

5. **Set the Expected Outcome.** Describe the anticipated result that will indicate the goal has been achieved.

6. **Click "Save"** to create the care plan. The new care plan appears in the care plan table with Active status.

> **Note:** Care plans should be reviewed and evaluated at least once per shift. Update interventions as the patient's condition changes. When a goal is fully met and no longer relevant, mark the care plan as Resolved.

#### Evaluating a Care Plan

1. **Click "Evaluate"** on the care plan row you want to evaluate.

2. **Select the Evaluation result:** Met (goal achieved), Partially Met (progress made but goal not fully achieved), or Not Met (no progress or condition worsened).

3. **Enter evaluation notes** describing the patient's current status relative to the goal, any barriers to progress, and planned modifications to the interventions.

4. **Click "Save Evaluation"** to record the evaluation. The Last Evaluated timestamp and Evaluation result are updated in the care plan table.

> **Tip:** When an evaluation result is "Not Met" or "Partially Met", consider revising the interventions, adjusting the goal timeline, or escalating to the provider if the patient's condition is not improving as expected.

![Care plan tab showing active plans with interventions and evaluation status](screenshots/nursing-care-plan.png)

---

### Acuity Tab

The Acuity tab is used to assess and record patient acuity levels. Acuity scores drive staffing calculations, nurse-to-patient ratio adjustments, and resource allocation decisions.

#### Acuity Levels

| Level | Label | Description | Typical Nurse Ratio |
|---|---|---|---|
| **1** | Minimal | Stable, self-care capable, routine monitoring. Patients awaiting discharge or in observation status with no acute concerns. | 1:5-6 |
| **2** | Moderate | Requires intermittent nursing care, standard medication schedule, regular assessments. Most medical-surgical patients fall into this category. | 1:4-5 |
| **3** | Complex | Multiple comorbidities, complex medication regimen, frequent reassessments required, requires assistance with most ADLs. | 1:3-4 |
| **4** | Intensive | Unstable vital signs, continuous monitoring required, high-risk medications (drips, blood products), post-operative care, frequent neurological checks. | 1:2-3 |
| **5** | Critical | Life-threatening condition, ICU-level care, mechanical ventilation, vasoactive drips, continuous 1:1 or 1:2 nursing care required. | 1:1-2 |

#### Recording Acuity

1. **Select the patient** from the patient list on the Acuity tab.

2. **Select the Acuity Level** (1 through 5) from the dropdown.

3. **Enter justification notes** explaining the clinical basis for the assigned acuity level.

4. **Click "Save Acuity"** to record the score. The system timestamps the entry and associates it with the current user.

> **Note:** Acuity should be assessed and recorded at the beginning of each shift and updated whenever there is a significant change in patient condition. A change in acuity level of 2 or more points within a shift should be communicated to the charge nurse immediately.

> **Tip:** Use the assessment findings from the Assessments tab to support your acuity determination. For example, a patient with a Morse score of 51+ (high fall risk), Braden score of 10-12 (high pressure injury risk), and pain score of 8/10 would likely warrant an acuity level of 3 (Complex) or higher.

![Acuity scoring interface showing patient list with current acuity levels](screenshots/nursing-acuity-levels.png)

---

### Unit Census Tab

The Unit Census tab provides a real-time overview of all patients on the nursing unit. This is the primary dashboard for charge nurses and serves as the starting point for shift handoffs.

#### Census Table

| Column | Description |
|---|---|
| **Bed** | Bed number and room assignment (e.g., "4A-201-1") |
| **Patient** | Patient name and ID |
| **Acuity** | Current acuity level (1-5) with color indicator (green for 1-2, yellow for 3, orange for 4, red for 5) |
| **Nurse** | Assigned nurse for the current shift |
| **Admission Date** | Date of admission |
| **LOS** | Length of stay in days |
| **Diet** | Current diet order |
| **Activity** | Current activity order (e.g., Bedrest, Up ad lib, Assist x1) |
| **Isolation** | Isolation precautions if any (Contact, Droplet, Airborne, Protective) |
| **Alerts** | Active clinical alerts (fall risk, allergy, code status, restraints) |

#### Aggregate Statistics

At the top of the Unit Census tab, summary cards display:

| Statistic | Description |
|---|---|
| **Total Beds** | Total number of beds on the unit |
| **Occupied** | Number of beds currently occupied |
| **Available** | Number of beds available for new admissions |
| **Occupancy Rate** | Percentage of beds occupied |
| **Average Acuity** | Mean acuity score across all patients on the unit |
| **Pending Admissions** | Number of patients with pending admission orders |
| **Pending Discharges** | Number of patients with pending discharge orders |

> **Tip:** Use the Unit Census tab during shift handoff to quickly review the entire unit. Filter by nurse assignment to see only your patients, or sort by acuity to prioritize your rounding order.

![Unit census board showing patient assignments, acuity, and bed status](screenshots/nursing-unit-census.png)

---

## BCMA (/bcma)

**Route:** `/bcma`

The BCMA (Barcode Medication Administration) page is the primary interface for safe medication administration. It maps to the VistA BCMA package and implements the electronic Medication Administration Record (MAR). The page provides three tabs: MAR, History, and Record.

![BCMA page showing the MAR tab with medication list and status indicators](screenshots/bcma-mar-overview.png)

---

### MAR Tab

The MAR (Medication Administration Record) tab displays all active medication orders for the patient, their administration schedules, and current status.

#### Loading the MAR

1. Enter the **Patient ID** in the lookup bar.
2. Click **Load** (or press **Enter**).

The system retrieves the patient's medication orders and displays them in the MAR table.

#### MAR Table

| Column | Description |
|---|---|
| **Drug** | Medication name and strength, displayed in bold. The patient's ward and bed number appear below the drug name in smaller text. |
| **Dose/Route** | Dose amount and route of administration (e.g., "500mg PO", "2mg IV", "0.4mg SL") |
| **Schedule** | Administration schedule (e.g., "BID", "Q8H", "QHS", "ONCE", "PRN") |
| **Priority** | Order priority, color-coded: ROUTINE (gray), ASAP (yellow), STAT (red), PRN (blue) |
| **Last Given** | Date and time of the most recent administration, or "Never" if not yet given |
| **Status** | Current order status: ACTIVE (green), HELD (yellow), INACTIVE (gray) |
| **Count** | Number of times the medication has been administered |
| **Action** | Action buttons (see below) |

#### Due Medication Highlighting

Medications that are currently due for administration are highlighted with a **yellow background**. A medication is considered due when the current time falls within a 60-minute window of the scheduled administration time (30 minutes before through 30 minutes after the scheduled time).

> **Tip:** Start your medication pass by sorting the MAR by schedule time and working through the due medications (highlighted in yellow) first. This helps ensure timely administration within the accepted window.

#### MAR Actions

| Action | Description |
|---|---|
| **Administer** | Records the medication as administered at the current timestamp. Opens the administration workflow (see below). |
| **Sync Orders** | Refreshes the MAR by re-synchronizing with the current active orders. Use this if you suspect the MAR is out of date (e.g., a new order was just entered by a provider). |
| **Deactivate** | Marks a medication order as INACTIVE on the MAR. Use this only when directed by a provider (e.g., medication discontinued or held). |

---

### Administering Medication

Follow these steps to administer a medication using the BCMA system:

1. **Scan the patient wristband.** Use the barcode scanner to scan the patient's identification wristband. The system verifies the patient identity and loads the patient's MAR. If the barcode cannot be scanned, manually enter the patient ID, but document the reason for manual entry.

2. **Review the MAR.** Verify the patient's active medication orders. Confirm that the medication you are about to administer appears on the MAR with ACTIVE status and is currently due (highlighted in yellow).

3. **Scan the medication barcode.** Scan the barcode on the medication package. The system cross-references the scanned medication against the order on the MAR and performs the following checks:
   - Drug name and strength match the order
   - Dose matches the ordered dose
   - Route matches the ordered route
   - Medication is not expired
   - No duplicate administration within the scheduled window

4. **Verify the five rights.** Before administering, confirm all five rights on screen:
   - **Right Patient** -- patient wristband matches the MAR
   - **Right Drug** -- scanned medication matches the ordered medication
   - **Right Dose** -- dose to be given matches the ordered dose
   - **Right Route** -- planned route matches the ordered route
   - **Right Time** -- current time is within the acceptable administration window

5. **Administer the medication** to the patient using the ordered route and technique.

6. **Click "Administer"** to document the administration. The system records the current timestamp, the administering nurse, and updates the MAR. A confirmation message appears showing the drug name, dose, route, and administration time.

> **Warning:** Always scan both the patient wristband and the medication barcode. Never bypass the barcode scanning process. Manual overrides are logged and subject to audit. The barcode verification is a critical patient safety check that prevents wrong-patient and wrong-drug errors.

> **Warning:** For controlled substances (Schedule II-V), the system requires a **witness co-signature**. After you click "Administer", the witness must enter their credentials to verify the administration. The witness name is recorded in the administration history. Controlled substance counts must be reconciled at every shift change.

> **Note:** If the barcode scan returns a mismatch (the scanned drug does not match the ordered drug), the system displays a red alert banner and blocks the administration. Do not override this alert. Verify you have the correct medication and re-scan. If the mismatch persists, contact the pharmacy for clarification.

![BCMA MAR showing due medications highlighted in yellow with action buttons](screenshots/bcma-mar-due-medications.png)

---

### History Tab

The History tab displays a complete chronological record of all medication administrations for the patient. This log serves as the legal record of medication administration.

#### History Table

| Column | Description |
|---|---|
| **Drug** | Medication name and strength |
| **Dose/Route** | Dose and route administered |
| **Administration Time** | Date and time the medication was administered, in MM/DD/YYYY HH:MM format |
| **Administered By** | Name of the nurse who administered the medication |
| **Witness** | Name of the witness (for controlled substances); blank for non-controlled medications |
| **Status** | Administration status: GIVEN, HELD, REFUSED, or NOT_GIVEN |
| **PRN Reason** | For PRN medications, the documented reason for administration (e.g., "Pain 7/10", "Nausea", "Anxiety") |
| **PRN Effectiveness** | For PRN medications, the documented follow-up effectiveness assessment (e.g., "Pain reduced to 3/10 at 30 minutes") |

#### Administration Statuses

| Status | Description |
|---|---|
| **GIVEN** | Medication was administered to the patient as ordered |
| **HELD** | Medication was held (not given) due to a clinical reason (e.g., low blood pressure, low heart rate, NPO status). The reason for holding must be documented. |
| **REFUSED** | Patient refused the medication. The reason for refusal and patient education provided must be documented. The provider must be notified. |
| **NOT_GIVEN** | Medication was not given for an administrative reason (e.g., medication unavailable from pharmacy, patient off the unit for a procedure). The reason must be documented. |

> **Note:** When a medication is documented as HELD, REFUSED, or NOT_GIVEN, the system requires a mandatory reason field. The provider is automatically notified for REFUSED medications. Always document the clinical rationale for holding medications and the patient education provided for refused medications.

---

### Record Tab

The Record tab provides a quick-entry form for documenting a medication administration without going through the full barcode scanning workflow. This is used in situations where the standard BCMA workflow is not feasible (e.g., system downtime, bedside emergencies).

> **Warning:** The Record tab bypasses barcode verification. Use this tab only when the standard BCMA scanning workflow is unavailable. All administrations entered through the Record tab are flagged as "Manual Entry" in the audit log and are subject to additional review by nursing leadership and pharmacy.

---

## Clinical Decision Support Triggers

The nursing assessment and vital signs systems in NewVistas include automated clinical decision support (CDS) triggers. When an assessment finding or vital sign measurement meets a trigger threshold, the system generates an alert and recommends a specific nursing action.

| Trigger Condition | Threshold | Automated Action |
|---|---|---|
| **Braden Score** | <= 14 (Moderate risk or higher) | Alert: Initiate skin care prevention protocol. Prompt to create a care plan for "Risk for Impaired Skin Integrity." Notify charge nurse. |
| **Morse Score** | >= 25 (Moderate risk or higher) | Alert: Activate fall prevention protocol. Prompt to create a care plan for "Risk for Falls." Apply yellow wristband and bed alarm. |
| **Pain Score** | >= 7 (Severe pain) | Alert: Reassessment required within 30 minutes of intervention. Prompt to administer PRN pain medication if ordered. Notify provider if no PRN orders available. |
| **SpO2** | < 92% | Alert: Assess respiratory status immediately. Verify oxygen delivery device and flow rate. Notify provider. Prepare for potential escalation. |
| **Edema** | 3+ or 4+ | Alert: Assess cardiovascular and renal status. Check daily weight trend. Notify provider for possible diuretic adjustment. Elevate extremities. |
| **Bowel Sounds** | Absent | Alert: Assess for abdominal distension and pain. Maintain NPO status until evaluated. Notify provider for possible ileus workup. |
| **Level of Consciousness** | Not Alert (Voice, Pain, or Unresponsive) | Alert: Perform full neurological assessment. Assess airway patency. Notify provider immediately. Consider rapid response team activation. |

> **Note:** CDS alerts appear as banner notifications at the top of the Nursing and Vitals pages. Each alert must be acknowledged by the nurse. Acknowledging an alert does not dismiss the underlying clinical concern -- it records that the nurse has reviewed the alert and is taking appropriate action.

---

## Bed Management (/bed-management)

**Route:** `/bed-management`

The Bed Management page provides a visual board view of all beds on the nursing unit. Charge nurses use this page to manage bed assignments, track occupancy, and coordinate admissions, discharges, and transfers.

![Bed management board showing color-coded bed status grid](screenshots/bed-management-board.png)

### Bed Status Colors

The bed management board uses color coding to indicate bed status at a glance:

| Color | Status | Description |
|---|---|---|
| **Green** | Available | Bed is clean, ready for a new patient admission |
| **Blue** | Occupied | Bed is currently assigned to a patient |
| **Yellow** | Reserved | Bed is reserved for an incoming admission or transfer |
| **Red** | Blocked | Bed is temporarily unavailable (e.g., equipment malfunction, isolation cleaning required) |
| **Gray** | Maintenance | Bed is out of service for maintenance or repair |

### Bed Actions

#### Assigning a Bed

1. **Click on a green (Available) bed** on the board.
2. **Enter the Patient ID** or select from the pending admissions list.
3. **Confirm the assignment.** The bed status changes from Available (green) to Occupied (blue) and the patient's name appears on the bed tile.

#### Discharging a Patient

1. **Click on a blue (Occupied) bed** on the board.
2. **Click "Discharge"** to initiate the discharge process.
3. **Confirm the discharge.** The bed status changes to Available (green) after environmental services confirms the bed has been cleaned. Until cleaning is confirmed, the bed remains in a "Pending Clean" intermediate state.

#### Reserving a Bed

1. **Click on a green (Available) bed** on the board.
2. **Click "Reserve"** and enter the expected admission details (patient name, expected arrival time, admitting provider).
3. The bed status changes to Reserved (yellow) and displays the reservation details.

#### Blocking a Bed

1. **Click on a green (Available) bed** on the board.
2. **Click "Block"** and enter the reason for blocking (e.g., "Isolation cleaning in progress", "Equipment repair needed").
3. The bed status changes to Blocked (red).

### Occupancy Statistics

The bed management board displays aggregate statistics at the top:

| Statistic | Description |
|---|---|
| **Total Beds** | Total beds on the unit |
| **Occupied** | Number of beds with patients |
| **Available** | Number of beds ready for admission |
| **Reserved** | Number of beds reserved for incoming patients |
| **Blocked** | Number of beds temporarily unavailable |
| **Occupancy Rate** | Percentage of beds occupied (Occupied / Total) |

---

## Ward Stock (/ward-stock)

**Route:** `/ward-stock`

The Ward Stock page provides inventory management for medications and supplies stored on the nursing unit. Nurses use this page to view current stock levels, identify items that need replenishment, request restocking, and document medication usage.

### Ward Stock Functions

#### View Inventory

The inventory table displays all ward stock items with current quantities, par levels, and reorder status. Items below par level are highlighted in yellow. Items at zero quantity are highlighted in red.

| Column | Description |
|---|---|
| **Item** | Medication or supply name |
| **Category** | Category (e.g., IV Fluids, Oral Medications, Controlled Substances, Supplies) |
| **Quantity** | Current quantity on hand |
| **Par Level** | Target stocking level |
| **Reorder Status** | At par, Below par, or Out of stock |
| **Last Restocked** | Date of most recent restocking |

#### Requesting Stock

1. **Identify items** that are below par level or out of stock.
2. **Click "Request Restock"** on the item row.
3. **Enter the quantity requested** and any priority notes.
4. **Click "Submit"** to send the request to the pharmacy or central supply.

#### Documenting Usage

1. **Click "Record Usage"** on the item row.
2. **Enter the quantity used**, the patient ID (if applicable), and the purpose.
3. **Click "Save"** to update the inventory count.

#### Controlled Substance Tracking

> **Warning:** Controlled substance ward stock requires a double-count verification at every shift change. Both the outgoing and incoming nurses must independently count all controlled substances and reconcile the counts. Any discrepancy must be reported to the charge nurse and pharmacy immediately. The system logs all controlled substance transactions with timestamps and user identification.

---

## ADT (/adt)

**Route:** `/adt`

The ADT (Admission, Discharge, Transfer) page manages patient movements within the facility. Nurses use this page to prepare for admissions, process discharges, and coordinate transfers between units.

### Tabs

#### Patient Movements Tab

The Patient Movements tab displays a chronological log of all admissions, discharges, and transfers for the unit.

| Column | Description |
|---|---|
| **Date/Time** | Date and time of the movement |
| **Patient** | Patient name and ID |
| **Type** | Movement type: Admission, Discharge, or Transfer |
| **From** | Originating location (for transfers and discharges) |
| **To** | Destination location (for admissions and transfers) |
| **Provider** | Attending provider |
| **Status** | Pending, In Progress, or Completed |

#### Ward Census Tab

The Ward Census tab provides a summary count of patients on each ward, with drill-down capability to view individual patient details. This tab mirrors the Unit Census tab on the Nursing page but spans all wards visible to the current user.

#### Ward Directory Tab

The Ward Directory tab lists all wards and units in the facility with their current census counts, bed capacity, and specialty designation (e.g., Medical, Surgical, ICU, Pediatrics, Psychiatry).

### Preparing for Admissions

1. **Review pending admissions** on the Patient Movements tab (filtered to Type = Admission, Status = Pending).
2. **Assign a bed** using the Bed Management page.
3. **Prepare the room** with necessary equipment and supplies based on the admitting diagnosis and provider orders.
4. **Review admission orders** on the Orders page once the patient arrives.
5. **Complete the initial nursing assessment** on the Nursing page (Assessment Type = Initial).

### Processing Discharges

1. **Review the discharge order** and discharge instructions on the Orders page.
2. **Complete a discharge assessment** on the Nursing page (Assessment Type = Discharge).
3. **Provide patient education** and discharge instructions. Document education provided in a nursing note.
4. **Reconcile medications** and ensure the patient has discharge prescriptions.
5. **Update the ADT record** by clicking "Complete Discharge" on the Patient Movements tab.
6. **Release the bed** on the Bed Management page and notify environmental services for cleaning.

### Coordinating Transfers

1. **Review the transfer order** on the Orders page.
2. **Contact the receiving unit** to confirm bed availability and readiness.
3. **Prepare a transfer summary** including current assessment, medications, IV access, and pending orders.
4. **Update the ADT record** by clicking "Complete Transfer" on the Patient Movements tab.
5. **Provide a verbal handoff** to the receiving nurse using the SBAR (Situation, Background, Assessment, Recommendation) format.

---

## Additional Pages

The following additional pages are relevant to nursing practice. Each page has its own dedicated guide in the clinician documentation section.

| Page | Route | Nursing Relevance |
|---|---|---|
| **Immunizations** | `/immunizations` | Administer and document vaccinations. Record lot numbers, sites, routes, and patient consent. Monitor for adverse reactions per protocol. See [Immunizations Guide](immunizations.md). |
| **Clinical Reminders** | `/reminders` | Review and resolve clinical reminders for preventive care, screenings, and follow-up actions assigned to nursing. Common nursing reminders include fall risk reassessment, skin integrity checks, pain reassessment, and Foley catheter day tracking. See [Reminders Guide](reminders.md). |
| **Infection Control** | `/infection-control` | Document isolation precautions, hand hygiene compliance, and infection surveillance data. Track healthcare-associated infections (HAIs) including CAUTI, CLABSI, SSI, and VAP. |
| **Suicide Prevention** | `/suicide-prevention` | Perform suicide risk screening (Columbia Suicide Severity Rating Scale), document safety plans, initiate and monitor suicide precautions, and escalate to the mental health team as indicated. See [Mental Health Guide](mental-health.md). |
| **Emergency Department** | `/emergency` | Triage patients using the Emergency Severity Index (ESI), document triage assessments, manage ED patient tracking board, and coordinate admissions from the ED to inpatient units. |
| **Home Telehealth** | `/home-telehealth` | Review home telehealth vitals and patient-reported data for patients enrolled in remote monitoring programs. Flag abnormal readings for provider review and conduct follow-up telephone assessments. |

---

## Screenshots Reference

The following screenshots illustrate key nursing workflows in NewVistas:

- ![Nursing assessment form with body system domains and Braden/Morse scoring](screenshots/nursing-assessment-form.png)
- ![Care plan tab showing active plans with interventions and evaluation history](screenshots/nursing-care-plan.png)
- ![BCMA MAR with due medications highlighted in yellow and action buttons](screenshots/bcma-mar-due-medications.png)
- ![Acuity scoring interface with patient list and color-coded levels](screenshots/nursing-acuity-levels.png)
- ![Unit census board showing patient assignments and aggregate statistics](screenshots/nursing-unit-census.png)
- ![Bed management board with color-coded status grid and occupancy statistics](screenshots/bed-management-board.png)
