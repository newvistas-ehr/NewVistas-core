# Shift Handoff (SBAR) -- Human Test Script

## Prerequisites

- **Login:** NURSE1 (outgoing nurse) / Password: `smythVista1`
- **Patient:** 9
- **Pre-conditions:**
  1. The SiloHost, WebServer, and BlazorWeb projects must all be running.
  2. Navigate to `/shift-handoff` in the browser.
  3. Enter Patient ID `9` in the Patient ID field and click **Load**.
  4. The Handoff History tab loads. If no handoffs exist, the table is empty.

---

## Scenario 1: Create SBAR Handoff Report (Happy Path -- Day to Evening)

### Steps

1. Navigate to `/shift-handoff`.
2. Enter Patient ID: `9`
3. Click **Load**.
4. Click the **New Handoff** tab.
5. Fill in all fields:
   - Shift: `Day`
   - Outgoing Nurse ID: `NURSE1`
   - Outgoing Nurse Name: `JOHNSON,MARY R`
   - Bed #: `301-A`
   - **S -- Situation:**
     ```
     Mr. Patient is a 62 y/o male admitted 2 days ago for community-acquired pneumonia. Currently on day 2 of IV Vancomycin and Ceftriaxone. SpO2 stable at 95% on 2L NC. Temperature trending down from 101.2F to 99.1F over the past 12 hours. Pain currently 3/10 at right chest wall.
     ```
   - **B -- Background:**
     ```
     PMH: HTN, DM type 2, COPD. Allergies: Penicillin (rash). Home meds: Metformin 1000mg BID, Lisinopril 20mg daily, Tiotropium inhaler. Chest X-ray today shows improving bilateral infiltrates. AM labs: WBC 12.4 (down from 18.2 on admission), Creatinine 1.1. Blood cultures from admission negative at 48 hours.
     ```
   - **A -- Assessment:**
     ```
     Patient is improving clinically. Fever resolving, WBC trending down. Respiratory status stable on low-flow O2. Braden score 18 (mild risk). Morse fall score 35 (moderate risk -- ambulates with walker). Appetite poor, ate 30% of meals today. I&O: Intake 1800mL, Output 1200mL. IV site left forearm clean, dry, intact, no erythema.
     ```
   - **R -- Recommendation:**
     ```
     1. Continue IV antibiotics per schedule (Vancomycin due at 2200, Ceftriaxone due at 0800).
     2. Q4H vital signs -- notify MD if temp > 101.5F or SpO2 < 92%.
     3. Encourage PO fluid intake -- patient prefers ice water and apple juice.
     4. Blood cultures to be drawn at 72 hours (tomorrow AM).
     5. PT/OT evaluation pending -- consult placed today.
     6. Fall precautions in place -- yellow wristband, bed alarm activated.
     7. Pain reassessment due at 1800 (last Tylenol given at 1400).
     ```
6. Click **Create Handoff**.

### Expected Result

- Green success banner: "Handoff created: HANDOFF-..."
- Switch to the **Handoff History** tab. The new entry shows:
  - Date: today's date
  - Shift: Day
  - Outgoing: JOHNSON,MARY R
  - Incoming: -- (not yet assigned)
  - Status: **Draft** (yellow/warning badge)
- Click **View** to see the full report on the **Report Detail** tab.

---

## Scenario 2: View Handoff Detail and SBAR Report

### Steps

1. On the **Handoff History** tab, click **View** on the handoff created in Scenario 1.
2. The **Report Detail** tab displays the full handoff.

### Expected Result

- Header shows:
  - Shift Handoff: Day [today's date]
  - Status: Draft (badge)
  - Outgoing: JOHNSON,MARY R
  - Incoming: Pending
  - Location: -- Bed: 301-A
- **SBAR Report** section displays 4 cards:
  - **S -- Situation** (blue heading): Full situation text as entered
  - **B -- Background** (teal heading): Full background text
  - **A -- Assessment** (green heading): Full assessment text
  - **R -- Recommendation** (yellow heading): Full recommendation text with numbered items
- **Clinical Snapshot** section (auto-populated if available):
  - Vitals: latest vitals summary
  - Pain: pain score/10
  - Acuity: acuity level
  - Active Dx: list of active nursing diagnoses
  - Pending Tasks: list of pending tasks
- **Safety Concerns** alert box (if any safety concerns exist).
- Action buttons visible:
  - **Complete Report** button (since Status is Draft)

---

## Scenario 3: Complete the Handoff Report

### Steps

1. On the **Report Detail** tab, with the Draft handoff open, click **Complete Report**.

### Expected Result

- The detail reloads.
- Status changes from **Draft** to **Completed** (blue/primary badge).
- The **Complete Report** button disappears.
- A new button appears: **Acknowledge (Incoming)**.
- On the Handoff History tab, the Status column shows: Completed.

---

## Scenario 4: Acknowledge Handoff as Incoming Nurse

### Steps

1. **Log out** as NURSE1 and **log in** as NURSE5 (DAVIS,ANGELA M -- Primary Care).
   - Login: `NURSE5` / Password: `smythVista1`
2. Navigate to `/shift-handoff`.
3. Enter Patient ID: `9`
4. Click **Load**.
5. On the **Handoff History** tab, locate the Completed handoff from Scenario 3.
6. Click **View** to open the Report Detail.
7. Verify the Status is **Completed** and the **Acknowledge (Incoming)** button is visible.
8. Click **Acknowledge (Incoming)**.

### Expected Result

- The detail reloads.
- Status changes from **Completed** to **Acknowledged** (green/success badge).
- The **Acknowledge (Incoming)** button disappears.
- The Incoming nurse field now shows: **Incoming Nurse** (the name passed in the acknowledgment call -- in the current implementation, hardcoded as "Incoming Nurse" and "RN-INCOMING" in the Blazor page).
- On the Handoff History tab:
  - Incoming: Incoming Nurse
  - Status: Acknowledged (green badge)
- The handoff lifecycle is complete: Draft -> Completed -> Acknowledged.

---

## Scenario 5: Create Evening to Night Shift Handoff

### Steps

1. Login as NURSE2 (THOMPSON,PATRICIA A -- ICU).
2. Navigate to `/shift-handoff`.
3. Enter Patient ID: `9`
4. Click **Load**.
5. Click the **New Handoff** tab.
6. Fill in:
   - Shift: `Evening`
   - Outgoing Nurse ID: `NURSE2`
   - Outgoing Nurse Name: `THOMPSON,PATRICIA A`
   - Bed #: `301-A`
   - **S -- Situation:**
     ```
     Patient remained stable during evening shift. Temperature 98.8F at 2000. SpO2 96% on 2L NC. Vancomycin infused at 2200 without incident. Pain improved to 2/10 after Tylenol 650mg at 1800.
     ```
   - **B -- Background:**
     ```
     Refer to Day shift handoff for full PMH and admission details. No new orders received. No code status changes. PT evaluation completed -- recommends continued ambulation with walker TID.
     ```
   - **A -- Assessment:**
     ```
     Clinically improving. Afebrile since 1600. Lung sounds clear bilaterally with diminished bases. Eating better at dinner -- consumed 60% of tray. I&O for evening: Intake 900mL, Output 600mL.
     ```
   - **R -- Recommendation:**
     ```
     1. Ceftriaxone due at 0800.
     2. Blood cultures to draw at 0600.
     3. Continue fall precautions and Q4H vitals.
     4. May discontinue O2 if SpO2 > 95% on room air overnight -- discuss with day MD.
     5. Encourage early morning ambulation with PT.
     ```
7. Click **Create Handoff**.

### Expected Result

- A second handoff entry appears in the Handoff History:
  - Shift: Evening
  - Outgoing: THOMPSON,PATRICIA A
  - Status: Draft
- Both the Day and Evening handoffs are now visible in the history, providing continuity of care documentation.

---

## Scenario 6: View Handoff History Across Multiple Shifts

### Steps

1. On the **Handoff History** tab, observe all handoff entries for Patient 9.

### Expected Result

- The table shows all handoffs in chronological order:
  - Day shift handoff (Acknowledged)
  - Evening shift handoff (Draft)
- Each row shows Date, Shift, Outgoing nurse, Incoming nurse (or --), and Status.
- Clicking View on any row loads that specific handoff's full SBAR report.

---

## Reference: Handoff Status Lifecycle

| Status        | Description                              | Badge Color    |
|---------------|------------------------------------------|----------------|
| Draft         | Report created, not yet finalized        | Yellow/Warning |
| Completed     | Outgoing nurse has finalized the report  | Blue/Primary   |
| Acknowledged  | Incoming nurse has read and accepted     | Green/Success  |

### Shift Values

| Shift   | Typical Hours    |
|---------|-----------------|
| Day     | 0700 -- 1500    |
| Evening | 1500 -- 2300    |
| Night   | 2300 -- 0700    |

### SBAR Framework

| Component      | Content                                    |
|---------------|--------------------------------------------|
| **S**ituation    | Current condition, reason for report       |
| **B**ackground   | Relevant history, meds, allergies, labs    |
| **A**ssessment   | Nursing assessment findings, trends        |
| **R**ecommendation | Pending items, watch parameters, plans  |
