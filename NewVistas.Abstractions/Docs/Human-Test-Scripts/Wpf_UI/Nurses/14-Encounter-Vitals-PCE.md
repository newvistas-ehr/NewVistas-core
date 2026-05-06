# Encounter Vitals (PCE) -- Human Test Script -- WPF UI

## Prerequisites

- **Login:** NURSE2 / Password: `smythVista1`
- **Patient:** 4
- **Pre-conditions:**
  1. The SiloHost, WebServer, and WPF Application (NewVistas.WpfClient) must all be running.
  2. In the Navigation Panel, select **PCE** (Patient Care Encounters).
  3. Enter Patient ID `4` in the Patient ID field in the toolbar and click **Load Demo** to seed encounter data.
  4. After demo load succeeds (a green notification appears in the status bar), the Encounter List TabItem should display at least 3 visits.
  5. Select an OPEN encounter (e.g., the Telehealth visit) to use as the active encounter context for vitals documentation.
  6. Optionally, record some vitals via the standalone **Vital Signs** module first (see Script 02-Vital-Signs-Recording.md) so that prior vitals exist for comparison.

---

## Scenario 1: Open Encounter Vitals

### Steps

1. In the Navigation Panel, select **PCE**.
2. Enter Patient ID: `4` in the Patient ID field in the toolbar.
3. Click **Load**.
4. On the **Encounter List** TabItem, select an OPEN encounter row.
5. In the **Encounter Detail** TabItem, click the **Vitals** section (or TabItem within the encounter detail).

### Expected Result

- The Vitals section displays within the encounter context.
- The header shows the encounter date, location, and primary provider.
- If vitals have already been recorded for this encounter, they appear in a summary DataGrid.
- If no vitals exist for this encounter, the section shows "No vitals recorded for this encounter" with a **Record Vitals** button.
- The encounter's Visit ID is displayed or indicated so that recorded vitals are linked to this specific visit.

---

## Scenario 2: Record Vitals Within Encounter (Happy Path -- All 8 Types)

### Steps

1. With an OPEN encounter selected, click the **Record Vitals** button within the Encounter Vitals section.
2. Fill in the header fields:
   - Date/Time Taken: (leave as current date/time)
   - Location: (auto-populated from encounter location, e.g., `Primary Care Clinic`)
   - Entered By: `THOMPSON,PATRICIA A`
3. Fill in the Vital Measurements grid:
   - Blood Pressure (mmHg) TextBox: `128/82`
   - Pulse (bpm) TextBox: `76`
   - Respiration (breaths/min) TextBox: `16`
   - Temperature (F) TextBox: `98.4`
   - Height (in) TextBox: `68`
   - Weight (lbs) TextBox: `185`
   - Pain (0-10) TextBox: `2`
   - Pulse Oximetry (%) TextBox: `97`
4. Click **Record Vitals**.

### Expected Result

- A green notification appears in the status bar: "Vitals recorded successfully."
- The Encounter Vitals DataGrid now shows 8 rows, one for each vital type:
  - BLOOD PRESSURE: 128/82 (no flag)
  - PULSE: 76 (no flag)
  - RESPIRATION: 16 (no flag)
  - TEMPERATURE: 98.4 (no flag)
  - HEIGHT: 68 (no flag)
  - WEIGHT: 185 (no flag)
  - PAIN: 2 (no flag)
  - PULSE OXIMETRY: 97 (no flag)
- All entries show the current date/time in the Date/Time column.
- The vitals are recorded via `POST /api/patient/4/vitals` with the vitals dictionary and are associated with the patient record.
- The encounter context ensures these vitals can be correlated with the specific visit.

---

## Scenario 3: View Previous Encounter Vitals (Comparison)

### Steps

1. With vitals recorded in the current encounter (Scenario 2), navigate to the **Encounter List** TabItem.
2. Select a CHECKED OUT encounter from a prior date (e.g., the Primary Care visit from 30 days ago).
3. In the **Encounter Detail** TabItem, click the **Vitals** section.
4. Observe the vitals displayed for the prior encounter.
5. Click the **Compare with Current** button (or toggle the **Show Previous** CheckBox).

### Expected Result

- The prior encounter's vitals section displays vitals recorded at or near that encounter's date/time.
- If the **Compare with Current** view is active, a side-by-side DataGrid shows:
  - Left column: Prior Encounter Vitals (date, values)
  - Right column: Current Encounter Vitals (date, values)
  - A Delta column showing the change (e.g., BP went from 132/84 to 128/82, Weight went from 182 to 185)
- Values that changed significantly are highlighted (e.g., weight gain > 5 lbs in bold).
- The vitals history is retrieved via `GET /api/patient/4/vitals` which returns the latest vitals per type.

---

## Scenario 4: Validate Vital Ranges Within Encounter

### Steps

1. With an OPEN encounter selected, click **Record Vitals** within the Encounter Vitals section.
2. Fill in abnormal values:
   - Blood Pressure (mmHg): `182/114`
   - Pulse (bpm): `110`
   - Respiration (breaths/min): `24`
   - Temperature (F): `101.8`
   - Pain (0-10): `7`
   - Pulse Oximetry (%): `91`
   - (leave Height and Weight blank)
3. Click **Record Vitals**.

### Expected Result

- A green notification appears in the status bar: "Vitals recorded successfully."
- The Encounter Vitals DataGrid displays the recorded values with abnormal highlighting:
  - BLOOD PRESSURE: `182/114` -- displayed with red foreground (hypertension)
  - PULSE: `110` -- displayed with red foreground (tachycardia)
  - RESPIRATION: `24` -- displayed with red foreground (tachypnea)
  - TEMPERATURE: `101.8` -- displayed with red foreground (fever)
  - PAIN: `7` -- displayed with red foreground (severe pain)
  - PULSE OXIMETRY: `91` -- displayed with red foreground (hypoxemia)
- Out-of-range values are visually distinct (bold red text or red background row highlight).
- A summary alert may appear at the top of the vitals section: "6 abnormal vitals detected."

---

## Scenario 5: Record Supplemental O2

### Steps

1. With an OPEN encounter selected, click **Record Vitals** within the Encounter Vitals section.
2. Fill in the Pulse Oximetry field:
   - Pulse Oximetry (%) TextBox: `94`
3. Check the **On Supplemental O2** CheckBox.
4. A supplemental oxygen detail section expands. Fill in:
   - Flow Rate (L/min) TextBox: `2`
   - Concentration (%) TextBox: `28`
   - Delivery Method ComboBox: `Nasal Cannula`
5. Also fill in remaining vitals:
   - Blood Pressure (mmHg): `124/78`
   - Pulse (bpm): `88`
   - Respiration (breaths/min): `20`
6. Click **Record Vitals**.

### Expected Result

- A green notification appears in the status bar: "Vitals recorded successfully."
- The Encounter Vitals DataGrid shows:
  - PULSE OXIMETRY: `94` with qualifier text "on 2 L/min O2 via Nasal Cannula"
  - BLOOD PRESSURE: `124/78` (no flag)
  - PULSE: `88` (no flag)
  - RESPIRATION: `20` (no flag)
- The supplemental O2 data is stored as qualifiers on the Pulse Oximetry vital. The API call `POST /api/patient/4/vitals` includes qualifiers: `{ "PULSE OXIMETRY": ["SUPPLEMENTAL O2: 2 L/min", "DELIVERY: Nasal Cannula"] }`.
- The Pulse Oximetry value on supplemental O2 is interpreted differently from room air readings for clinical decision support.

---

## Scenario 6: Record Qualifiers for Vitals

### Steps

1. With an OPEN encounter selected, click **Record Vitals** within the Encounter Vitals section.
2. Fill in vitals with qualifiers:
   - Blood Pressure (mmHg): `138/86`
   - BP Position ComboBox: `SITTING`
   - BP Location ComboBox: `LEFT ARM`
   - Temperature (F): `99.2`
   - Temp Method ComboBox: `TYMPANIC`
   - Pulse (bpm): `82`
   - Pulse Site ComboBox: `RADIAL`
   - Pulse Method ComboBox: `PALPATED`
3. Click **Record Vitals**.

### Expected Result

- A green notification appears in the status bar: "Vitals recorded successfully."
- The Encounter Vitals DataGrid shows qualifiers alongside values:
  - BLOOD PRESSURE: `138/86` with qualifier text "Sitting, Left Arm"
  - TEMPERATURE: `99.2` with qualifier text "Tympanic"
  - PULSE: `82` with qualifier text "Radial, Palpated"
- The qualifiers are passed to the API via the qualifiers dictionary parameter:
  - `{ "BLOOD PRESSURE": ["SITTING", "LEFT ARM"], "TEMPERATURE": ["TYMPANIC"], "PULSE": ["RADIAL", "PALPATED"] }`
- Qualifiers are important for clinical accuracy -- a BP of 138/86 sitting may be interpreted differently from 138/86 standing (orthostatic assessment).

---

## Scenario 7: BMI Auto-Calculation from Height and Weight

### Steps

1. With an OPEN encounter selected, click **Record Vitals** within the Encounter Vitals section.
2. Fill in:
   - Height (in) TextBox: `70`
   - Weight (lbs) TextBox: `210`
   - (leave all other vitals blank)
3. Observe the BMI field as you enter height and weight.
4. Click **Record Vitals**.

### Expected Result

- As soon as both Height and Weight are populated, the BMI field auto-calculates and displays: `30.1` (formula: (weight in lbs / (height in inches)^2) x 703).
- The BMI value is color-coded:
  - Green: 18.5 - 24.9 (Normal)
  - Yellow: 25.0 - 29.9 (Overweight)
  - Red: >= 30.0 (Obese) or < 18.5 (Underweight)
- In this case, BMI 30.1 is displayed with red foreground (Obese category).
- A green notification appears in the status bar: "Vitals recorded successfully."
- The Encounter Vitals DataGrid shows:
  - HEIGHT: 70
  - WEIGHT: 210
  - BMI: 30.1 (calculated, displayed with red foreground and label "Obese")
- The BMI is stored as a derived vital or displayed as a calculated field. If the patient's height is already on file from a prior recording, entering only weight will still trigger BMI calculation using the most recent height.

---

## Reference: Normal vs. Abnormal Vital Sign Ranges

| Vital Type      | Normal Range      | Abnormal Example   | Critical Example   |
|-----------------|-------------------|--------------------|--------------------|
| Temperature     | 97.0 - 99.5 F    | 100.4 F (fever)    | 104.2 F            |
| Pulse           | 60 - 100 bpm      | 110 bpm (tachy)    | 40 bpm (brady)     |
| Respiration     | 12 - 20 br/min    | 24 br/min          | 8 br/min           |
| Blood Pressure  | 90/60 - 140/90    | 180/110 (HTN)      | 80/40 (shock)      |
| Weight          | varies            | N/A                | N/A                |
| Height          | varies            | N/A                | N/A                |
| Pain            | 0                 | 4-6 (moderate)     | 7-10 (severe)      |
| Pulse Oximetry  | 95 - 100%         | 92% (hypoxia)      | 85% (critical)     |
| BMI             | 18.5 - 24.9       | 30.1 (obese)       | 15.0 (severe UW)   |

## Reference: Supplemental O2 Delivery Methods

| Method              | Typical Flow Rate | Typical FiO2 Range |
|---------------------|-------------------|---------------------|
| Nasal Cannula       | 1-6 L/min         | 24-44%              |
| Simple Face Mask    | 6-10 L/min        | 35-55%              |
| Venturi Mask        | 4-12 L/min        | 24-50% (precise)    |
| Non-Rebreather Mask | 10-15 L/min       | 60-100%             |
| High-Flow Nasal     | 10-60 L/min       | 21-100%             |

## Reference: Vital Sign Qualifiers

| Vital Type      | Qualifier Category | Options                                          |
|-----------------|--------------------|--------------------------------------------------|
| Blood Pressure  | Position           | SITTING, STANDING, LYING, L LATERAL RECUMBENT    |
| Blood Pressure  | Location           | LEFT ARM, RIGHT ARM, LEFT THIGH, RIGHT THIGH     |
| Temperature     | Method             | ORAL, TYMPANIC, RECTAL, AXILLARY, TEMPORAL       |
| Pulse           | Site               | RADIAL, APICAL, PERIPHERAL, CAROTID, FEMORAL     |
| Pulse           | Method             | PALPATED, AUSCULTATED, ELECTRONIC, DOPPLER       |
| Pulse Oximetry  | Supplement         | ROOM AIR, SUPPLEMENTAL O2                        |

## Reference: API Endpoints

| Action                | Method | Endpoint                                       |
|-----------------------|--------|-------------------------------------------------|
| Record vitals         | POST   | `/api/patient/{patientId}/vitals`               |
| Get latest vitals     | GET    | `/api/patient/{patientId}/vitals`               |
| Get encounter detail  | GET    | `/api/pce/{patientId}/visits/{visitId}`         |
| Create encounter      | POST   | `/api/pce/{patientId}/visits`                   |
| Load demo encounters  | POST   | `/api/pce/demo/load?patientId={patientId}`      |
