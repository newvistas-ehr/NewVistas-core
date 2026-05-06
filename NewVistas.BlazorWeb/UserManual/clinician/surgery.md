# Surgery and Anesthesia

This guide covers surgical case management and anesthesia documentation in NewVistas. The **Surgery** module handles scheduling, completing, and tracking surgical cases. The **Anesthesia Tracking** module provides comprehensive peri-operative anesthesia documentation including pre-operative assessment, intraoperative monitoring, and post-anesthesia recovery scoring.

Both modules map to the VistA Surgery package (File #130) and support the full operative lifecycle from scheduling through post-operative care.

---

## Surgery

**Route:** `/surgery`
**VistA File:** #130 (Surgery)

The Surgery module manages the complete lifecycle of surgical cases, from scheduling through completion or cancellation. It maintains a comprehensive operative record including procedure details, surgical team, anesthesia information, and outcomes.

![Surgical case list showing scheduled and completed surgeries](screenshots/surgery-case-list.png)

### Tab 1: Surgeries

The Surgeries tab displays all surgical cases for the selected patient in reverse chronological order. Each row in the surgeries table shows:

| Column | Description |
|---|---|
| **Date** | Scheduled or actual date of the surgical procedure |
| **Procedure** | Name of the principal surgical procedure |
| **CPT** | Current Procedural Terminology code for the procedure |
| **Surgeon** | Name of the principal surgeon |
| **Specialty** | Surgical specialty (e.g., General Surgery, Orthopedics, Neurosurgery, Cardiothoracic) |
| **Status** | Current case status: SCHEDULED, COMPLETED, or CANCELLED |
| **Actions** | Context-sensitive action buttons based on current status |

#### Status Badges

- **SCHEDULED** -- The surgical case has been booked but not yet performed. Displayed with a blue badge.
- **COMPLETED** -- The surgery has been performed and the operative record has been filed. Displayed with a green badge.
- **CANCELLED** -- The surgical case was cancelled before being performed. Displayed with a gray badge.

#### Status Workflow

```
SCHEDULED  ──►  COMPLETED
     │
     └──────►  CANCELLED
```

A scheduled case can either be completed (surgery performed) or cancelled. There is no intermediate status. Once a case is marked COMPLETED or CANCELLED, its status cannot be changed.

---

### Scheduling a Surgical Case

To schedule a new surgical case:

1. Navigate to the Surgery module at `/surgery`.
2. Click the **Schedule Surgery** button on the Surgeries tab.
3. Complete the scheduling form:

| Field | Required | Description |
|---|---|---|
| **Principal Procedure** | Yes | The primary surgical procedure to be performed. Type to search the procedure catalog. |
| **CPT Code** | No | Auto-populated based on the selected procedure. Can be overridden. |
| **Date** | Yes | Scheduled date and time for the procedure. |
| **Surgeon** | No | The principal surgeon. Defaults to the currently signed-in provider if the user holds a surgeon role. |
| **Anesthesia Type** | No | Type of anesthesia planned. Options: GENERAL, SPINAL, REGIONAL, LOCAL, or MAC (Monitored Anesthesia Care). |
| **Specialty** | No | Surgical specialty category for the case. |
| **Pre-Op Diagnosis** | No | The pre-operative diagnosis or clinical indication for surgery. |
| **Location** | No | Operating room or surgical suite assignment. |
| **Comments** | No | Additional notes about the case, special equipment needs, or patient preparation instructions. |

4. Click **Schedule** to book the case.

![Schedule surgery form with procedure and date fields](screenshots/surgery-schedule-form.png)

> **Note:** Scheduling a surgical case does not constitute a surgical consent. Informed consent must be obtained and documented separately through the clinical notes system before the procedure.

---

### Completing a Surgical Case

When a surgery has been performed, the surgeon or authorized designee completes the case record with operative details and outcomes.

1. Navigate to the Surgery module at `/surgery` and locate the SCHEDULED case in the Surgeries list.
2. Click the **Complete** action button on the case row.
3. Enter the operative completion details:

| Field | Description |
|---|---|
| **Post-Op Diagnosis** | The diagnosis established after surgery, which may differ from the pre-operative diagnosis. |
| **Duration** | Total operative time in minutes, from incision to closure. |
| **EBL** | Estimated Blood Loss in milliliters. |
| **Complications** | Any intraoperative or immediate post-operative complications. Select from standard complication categories or enter free text. |
| **Outcome** | Summary of the surgical outcome and immediate post-operative status. |

4. Review all entered information for accuracy.
5. Click **Complete Surgery** to finalize the case and advance the status from SCHEDULED to COMPLETED.

> **Warning:** Completing a surgical case is a permanent action. Verify all operative details, especially the post-operative diagnosis and complication documentation, before clicking Complete Surgery. Once completed, the case status cannot be reverted to SCHEDULED.

---

### Cancelling a Surgical Case

When a scheduled surgery will not be performed, the case must be formally cancelled with a documented reason.

1. Navigate to the Surgery module at `/surgery` and locate the SCHEDULED case in the Surgeries list.
2. Click the **Cancel** action button on the case row.
3. Select or enter the **Reason for Cancellation** (e.g., patient request, medical contraindication, scheduling conflict, insurance issue).
4. Click **Confirm Cancellation** to cancel the case and advance the status from SCHEDULED to CANCELLED.

> **Note:** Cancelled cases remain in the patient's surgical history for documentation purposes. They are not deleted from the record.

---

### Tab 2: Surgery Detail

Selecting a surgery from the Surgeries tab opens the Surgery Detail view, which provides the complete operative record organized into four sections.

#### Case Information

| Field | Description |
|---|---|
| **Principal Procedure** | Name of the primary surgical procedure |
| **CPT Code** | Procedure code |
| **Case Status** | Current status (SCHEDULED, COMPLETED, CANCELLED) |
| **Scheduled Date** | Originally scheduled date and time |
| **Actual Date** | Date and time the surgery was actually performed (if completed) |
| **Pre-Op Diagnosis** | Diagnosis prior to surgery |
| **Post-Op Diagnosis** | Diagnosis after surgery (if completed) |
| **Duration** | Total operative time (if completed) |
| **EBL** | Estimated blood loss (if completed) |
| **Complications** | Documented complications (if any) |
| **Outcome** | Operative outcome (if completed) |

#### Surgical Team

| Field | Description |
|---|---|
| **Principal Surgeon** | Lead surgeon for the case |
| **First Assistant** | Surgical first assistant |
| **Second Assistant** | Additional surgical assistant (if applicable) |
| **Scrub Nurse** | Scrub nurse or surgical technologist |
| **Circulating Nurse** | Circulating nurse |

#### Anesthesia Information

| Field | Description |
|---|---|
| **Anesthesia Type** | Type of anesthesia administered (GENERAL, SPINAL, REGIONAL, LOCAL, MAC) |
| **Anesthesiologist** | Attending anesthesiologist |
| **CRNA** | Certified Registered Nurse Anesthetist (if applicable) |
| **ASA Classification** | American Society of Anesthesiologists physical status classification |

#### Clinical Details

| Field | Description |
|---|---|
| **Specialty** | Surgical specialty |
| **Location** | Operating room or suite |
| **Comments** | Additional case notes |
| **Cancellation Reason** | Reason for cancellation (if cancelled) |

---

## Anesthesia Tracking

**Route:** `/anesthesia-tracking`

The Anesthesia Tracking module provides comprehensive peri-operative anesthesia documentation covering the full spectrum from pre-operative assessment through post-anesthesia recovery. This module requires a feature flag to be enabled.

> **Note:** The Anesthesia Tracking module requires the **ANESTHESIA_TRACKING** feature flag to be enabled in Site Parameters. If you do not see this module in your navigation menu, contact your system administrator to enable it.

### Tab 1: Patient Records

The Patient Records tab displays all anesthesia records for the selected patient. Each row shows:

| Column | Description |
|---|---|
| **Date** | Date of the anesthetic |
| **Procedure** | Associated surgical procedure |
| **Type** | Anesthesia type (GENERAL, SPINAL, REGIONAL, LOCAL, MAC) |
| **ASA Classification** | ASA physical status classification (I through VI, with optional E suffix) |
| **Anesthesiologist** | Attending anesthesiologist |
| **Agents** | Number of anesthetic agents administered |
| **Status** | Record lifecycle status: DRAFT, IN_PROGRESS, FINALIZED, or ADDENDED |

#### Record Lifecycle

Anesthesia records progress through a defined lifecycle:

```
DRAFT  ──►  IN_PROGRESS  ──►  FINALIZED  ──►  ADDENDED
                                    │
                                    └── (optional addendum)
```

- **DRAFT** -- The record has been created but documentation has not yet begun. Displayed with a gray badge.
- **IN_PROGRESS** -- Active documentation is underway (during the procedure). Displayed with a blue badge.
- **FINALIZED** -- The record has been completed and signed by the anesthesiologist. Displayed with a green badge.
- **ADDENDED** -- A signed addendum has been appended to a finalized record to correct or supplement information. Displayed with a purple badge.

> **Note:** Once a record is FINALIZED, the original content cannot be modified. Corrections or additions must be made through a formal addendum, which preserves the original record and documents the change with a timestamp and author.

### Tab 2: Dashboard

The Dashboard tab provides a facility-wide view of anesthesia activity. This view is intended for anesthesia department leadership and quality assurance purposes.

#### Dashboard Filters

- **Status** -- Filter by record lifecycle status (DRAFT, IN_PROGRESS, FINALIZED, ADDENDED)
- **Anesthesia Type** -- Filter by type (GENERAL, SPINAL, REGIONAL, LOCAL, MAC)
- **Date Range** -- Restrict the view to a specific time period
- **Anesthesiologist** -- Filter by specific provider

The dashboard displays matching records from across all patients at the facility, enabling department-wide oversight of anesthesia activity.

---

### Pre-Operative Assessment

The pre-operative assessment section of the anesthesia record documents the patient's fitness for anesthesia. The centerpiece of this assessment is the ASA Physical Status Classification.

![ASA classification selection on the anesthesia pre-operative assessment](screenshots/anesthesia-asa-classification.png)

#### ASA Physical Status Classifications

| Class | Description | Examples |
|---|---|---|
| **ASA I** | A normal healthy patient | Healthy, non-smoking, no or minimal alcohol use |
| **ASA II** | A patient with mild systemic disease | Well-controlled diabetes, mild lung disease, social drinker, BMI 30-40, controlled hypertension |
| **ASA III** | A patient with severe systemic disease | Poorly controlled diabetes, COPD, morbid obesity (BMI >=40), active hepatitis, alcohol dependence, pacemaker, moderate reduction of ejection fraction, ESRD on dialysis, history of MI/CVA/TIA/CAD >3 months |
| **ASA IV** | A patient with severe systemic disease that is a constant threat to life | Recent MI/CVA/TIA (<3 months), ongoing cardiac ischemia, severe valve dysfunction, sepsis, DIC, ARDS, ESRD not on dialysis |
| **ASA V** | A moribund patient who is not expected to survive without the operation | Ruptured aortic aneurysm, massive trauma, intracranial bleed with mass effect, ischemic bowel with significant cardiac pathology, multiorgan dysfunction |
| **ASA VI** | A declared brain-dead patient whose organs are being removed for donor purposes | Brain death declared, organ procurement |

The **E suffix** (e.g., ASA IIIE) is appended to any classification when the surgical procedure is an emergency. An emergency is defined as existing when delay in treatment would lead to a significant increase in threat to life or body part.

#### Additional Pre-Operative Assessment Fields

The pre-operative assessment also includes:

- **Airway Assessment** -- Mallampati score, thyromental distance, neck mobility, dentition status, previous intubation history
- **NPO Status** -- Last oral intake of solids and clear liquids
- **Allergies** -- Verified medication and latex allergies
- **Current Medications** -- Medications taken and held prior to surgery
- **Relevant History** -- Previous anesthetic complications, family history of malignant hyperthermia, obstructive sleep apnea, cardiac/pulmonary/renal/hepatic history
- **Anesthesia Plan** -- Planned anesthesia type, airway management strategy, monitoring plan, post-operative pain management plan

---

### Intraoperative Documentation

The intraoperative section of the anesthesia record captures real-time documentation during the surgical procedure. This section includes three categories of data: anesthetic agents, vital signs, and significant events.

#### Anesthetic Agents

Each anesthetic agent administered is documented with the following fields:

| Field | Description |
|---|---|
| **Drug Name** | Name of the anesthetic agent or adjunct medication |
| **Dose** | Amount administered with units (e.g., "100 mg", "2 mcg/kg") |
| **Route** | Route of administration: IV, INHALATION, EPIDURAL, or INTRATHECAL |
| **Time** | Time the agent was administered |

Common agents include induction agents (propofol, etomidate, ketamine), maintenance agents (sevoflurane, desflurane, isoflurane), opioids (fentanyl, hydromorphone, remifentanil), muscle relaxants (succinylcholine, rocuronium, cisatracurium), and reversal agents (sugammadex, neostigmine/glycopyrrolate).

#### Vital Signs

Vital signs are documented at regular intervals throughout the procedure, typically every 5 minutes. Each vital sign entry includes:

| Parameter | Unit | Description |
|---|---|---|
| **Blood Pressure** | mmHg | Systolic/Diastolic (non-invasive or arterial line) |
| **Heart Rate** | bpm | Beats per minute |
| **SpO2** | % | Pulse oximetry oxygen saturation |
| **EtCO2** | mmHg | End-tidal carbon dioxide |
| **Temperature** | degrees C or F | Core or peripheral temperature |
| **Respiratory Rate** | breaths/min | Ventilator rate or spontaneous rate |
| **Tidal Volume** | mL | Ventilator tidal volume |
| **FiO2** | % | Fraction of inspired oxygen |

![Anesthesia record with vitals timeline showing intraoperative trends](screenshots/anesthesia-vitals-timeline.png)

> **Tip:** Vital signs are displayed both as a data table and as a graphical timeline. The timeline view makes it easy to spot trends, particularly hemodynamic responses to surgical events or medication administration.

#### Significant Events

Key events during the procedure are documented with timestamps. The following event types are tracked:

| Event Type | Description |
|---|---|
| **Intubation** | Airway placement -- includes device type (ETT, LMA, etc.), size, grade of view, number of attempts |
| **Incision** | Surgical incision time -- marks the start of the surgical procedure |
| **Tourniquet** | Tourniquet application and release times -- includes location and pressure |
| **Position Change** | Patient repositioning events -- includes new position (supine, prone, lateral, lithotomy, etc.) |
| **Blood Products** | Administration of blood products -- includes product type (PRBC, FFP, platelets, cryoprecipitate), volume, and unit number |
| **Critical Event** | Any critical or unexpected event requiring immediate documentation -- includes description and interventions |
| **Emergence** | Emergence from anesthesia -- includes time of extubation or LMA removal, patient responsiveness |

---

### Recovery (PACU)

The post-anesthesia recovery section documents the patient's recovery in the Post-Anesthesia Care Unit (PACU). The primary assessment tool is the Aldrete Score.

#### Aldrete Score

The Modified Aldrete Score is used to assess readiness for PACU discharge. The score ranges from 0 to 10, with each of five categories scored from 0 to 2:

| Category | Score 2 | Score 1 | Score 0 |
|---|---|---|---|
| **Activity** | Moves all 4 extremities voluntarily or on command | Moves 2 extremities voluntarily or on command | Unable to move extremities |
| **Respiration** | Able to breathe deeply and cough freely | Dyspnea, shallow or limited breathing | Apneic |
| **Circulation** | BP within 20% of pre-anesthetic level | BP within 20-50% of pre-anesthetic level | BP more than 50% from pre-anesthetic level |
| **Consciousness** | Fully awake | Arousable on calling | Not responding |
| **SpO2** | SpO2 > 92% on room air | Needs supplemental O2 to maintain SpO2 > 90% | SpO2 < 90% even with supplemental O2 |

**Total Score: 0 to 10**

A score of **9 or greater** indicates the patient is ready for discharge from the PACU.

![PACU handoff documentation with Aldrete scoring](screenshots/anesthesia-pacu-handoff.png)

> **Note:** Aldrete scores are typically assessed on arrival to PACU and then at regular intervals (every 15 minutes) until the patient meets discharge criteria.

#### Additional PACU Documentation

| Field | Description |
|---|---|
| **Complications** | Any post-anesthesia complications (nausea/vomiting, pain, shivering, respiratory depression, emergence delirium, etc.) |
| **Handoff Time** | Time of formal handoff from anesthesia to PACU nursing |
| **Receiving Nurse** | Name of the PACU nurse accepting care of the patient |

The PACU handoff constitutes a formal transfer of care. The anesthesiologist provides a verbal report to the receiving nurse covering the procedure, anesthetic technique, intraoperative course, current medications, and anticipated recovery issues.

---

### Operative Note Template

The operative note is a formal surgical document that summarizes the entire operative encounter. NewVistas provides a structured template with the following standard sections:

| Section | Content |
|---|---|
| **Pre-Operative Diagnosis** | The diagnosis that led to the surgical decision |
| **Post-Operative Diagnosis** | The diagnosis confirmed or established during surgery |
| **Procedure** | Formal name(s) of the surgical procedure(s) performed |
| **Surgeon** | Principal surgeon |
| **Assistants** | Surgical assistants |
| **Anesthesia** | Type of anesthesia and anesthesiologist |
| **Indications** | Clinical rationale for the procedure |
| **Findings** | Intraoperative findings, including gross pathological descriptions |
| **Procedure Description** | Step-by-step narrative of the surgical technique |
| **EBL** | Estimated blood loss in milliliters |
| **Specimens** | Specimens sent to pathology, including laterality and type |
| **Drains** | Drains placed, including type, size, and location |
| **Complications** | Any intraoperative complications, or "None" if uncomplicated |
| **Disposition** | Patient condition and destination at end of case (e.g., "To PACU in stable condition") |

![Operative note template with structured sections](screenshots/surgery-operative-note.png)

> **Tip:** The operative note should be dictated or entered as soon as possible after the procedure, ideally within 24 hours per Joint Commission requirements.

---

## Pre-operative and Post-operative Orders

Surgical cases typically require standardized pre-operative and post-operative order sets. These are placed through the CPOE Orders module but are closely associated with the surgical case.

### Pre-operative Orders

Pre-operative orders prepare the patient for surgery. Common pre-operative order categories include:

| Category | Examples |
|---|---|
| **Medications** | Pre-operative antibiotics (e.g., cefazolin 2g IV within 60 minutes of incision), anxiolytics (e.g., midazolam 2mg IV), DVT prophylaxis (e.g., enoxaparin), medications to hold (anticoagulants, oral hypoglycemics) |
| **Lab** | CBC, BMP, coagulation studies (PT/INR, PTT), type and screen, type and crossmatch, urinalysis, pregnancy test |
| **Diet** | NPO after midnight, clear liquids until 2 hours before procedure |
| **Nursing** | Pre-operative checklist, consent verification, surgical site marking, skin preparation, compression stockings, pre-operative vital signs |

### Post-operative Orders

Post-operative orders manage the patient's recovery after surgery. Common post-operative order categories include:

| Category | Examples |
|---|---|
| **Medications** | Pain management (PCA, scheduled analgesics, PRN breakthrough), antiemetics (ondansetron), antibiotics (continuation or discontinuation timing), DVT prophylaxis, stress ulcer prophylaxis |
| **Activity** | Bed rest, ambulate with assistance, weight-bearing status, physical therapy consultation, activity restrictions |
| **Diet** | NPO, clear liquids, advance as tolerated, specific dietary restrictions |
| **Monitoring** | Vital signs frequency (e.g., every 15 min x4, then every 30 min x4, then every 4 hours), neurovascular checks, intake/output monitoring, pulse oximetry, cardiac telemetry |
| **Lab** | Post-operative CBC, BMP, post-transfusion hemoglobin, drain output measurement |

> **Tip:** Use standardized pre-operative and post-operative order sets when available. Order sets ensure that all required elements are addressed and reduce the risk of omissions.

---

## Common Workflows

### Scheduling and Completing a Surgical Case

1. Navigate to `/surgery` and click **Schedule Surgery**.
2. Enter the principal procedure, date, surgeon, anesthesia type, and other case details.
3. Place pre-operative orders through the CPOE module.
4. On the day of surgery, the anesthesiologist creates an anesthesia record in the Anesthesia Tracking module.
5. After the procedure, the surgeon completes the case by entering the post-operative diagnosis, duration, EBL, complications, and outcome.
6. The surgeon enters the operative note.
7. Post-operative orders are placed through the CPOE module.

### Emergency Surgical Case

1. Navigate to `/surgery` and click **Schedule Surgery**.
2. Enter the procedure and set the date to the current date/time. Mark the urgency as emergent in the comments.
3. The anesthesiologist creates an anesthesia record and appends the E suffix to the ASA classification.
4. Proceed with standard case completion and operative note documentation.

---

## Related Modules

- **[Orders (CPOE)](orders.md)** -- Pre-operative and post-operative order sets are placed through the Orders module.
- **[Clinical Notes (TIU)](notes.md)** -- Operative notes, consent forms, and H&P documents are managed through the Notes module.
- **[Radiology](radiology.md)** -- Pre-operative imaging studies are tracked in the Radiology module.
- **[Laboratory](labs.md)** -- Pre-operative and post-operative laboratory results are reviewed in the Labs module.
