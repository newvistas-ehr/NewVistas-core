# Vital Signs Recording -- Human Test Script -- WPF UI

## Prerequisites

- **Login:** NURSE1 / Password: `smythVista1`
- **Patient:** 9
- **Pre-conditions:**
  1. The SiloHost, WebServer, and WPF Application (NewVistas.WpfClient) must all be running.
  2. In the Navigation Panel, select **Vital Signs**.
  3. Enter Patient ID `9` in the Patient ID field in the toolbar and click **Load**.
  4. If no vitals exist yet, the View Vitals TabItem shows "No vitals recorded."

---

## Scenario 1: Record All 8 Vital Types (Happy Path -- Normal Values)

### Steps

1. In the Navigation Panel, select **Vital Signs**.
2. Enter Patient ID: `9` in the Patient ID field in the toolbar.
3. Click **Load**.
4. Click the **Record Vitals** TabItem.
5. Fill in the header fields:
   - Date/Time Taken: (leave as current date/time)
   - Location: `Ward 3A`
   - Entered By: `JOHNSON,MARY R`
6. Fill in the Vital Measurements grid:
   - Temperature (F): `98.6`
   - Pulse (bpm): `72`
   - Respiration (breaths/min): `16`
   - Blood Pressure (mmHg): `120/78`
   - Weight (lbs): `170`
   - Height (in): `70`
   - Pain (0-10): `0`
   - Pulse Oximetry (%): `98`
7. Click **Record Vitals**.

### Expected Result

- A green notification appears in the status bar: "Vitals recorded successfully."
- The view automatically switches to the **View Vitals** TabItem.
- The DataGrid shows 8 rows, one for each vital type:
  - TEMPERATURE: 98.6 (no flag)
  - PULSE: 72 (no flag)
  - RESPIRATION: 16 (no flag)
  - BLOOD PRESSURE: 120/78 (no flag)
  - WEIGHT: 170 (no flag)
  - HEIGHT: 70 (no flag)
  - PAIN: 0 (no flag)
  - PULSE OXIMETRY: 98 (no flag)
- All entries show the current date/time in the Date/Time column.

---

## Scenario 2: Record Abnormal Vital Signs (Hypertension)

### Steps

1. Click the **Record Vitals** TabItem.
2. Fill in the header fields:
   - Date/Time Taken: (leave as current date/time)
   - Location: `Ward 3A`
   - Entered By: `JOHNSON,MARY R`
3. Fill in only the following vitals (leave others blank):
   - Temperature (F): `99.1`
   - Pulse (bpm): `92`
   - Respiration (breaths/min): `20`
   - Blood Pressure (mmHg): `180/110`
   - Pulse Oximetry (%): `94`
4. Click **Record Vitals**.

### Expected Result

- A green notification appears in the status bar: "Vitals recorded successfully."
- Switches to **View Vitals** TabItem.
- The BLOOD PRESSURE row shows value `180/110` with a red Flag value (displayed with red foreground).
- The PULSE OXIMETRY row may show `94` with or without a flag depending on system thresholds.
- The previous normal vitals from Scenario 1 are replaced by the latest readings for each type.

---

## Scenario 3: Record Critical Vital Sign (High Fever)

### Steps

1. Click the **Record Vitals** TabItem.
2. Fill in:
   - Date/Time Taken: (leave as current date/time)
   - Location: `Ward 3A`
   - Entered By: `JOHNSON,MARY R`
   - Temperature (F): `104.2`
   - Pulse (bpm): `118`
   - Respiration (breaths/min): `26`
   - Blood Pressure (mmHg): `88/52`
   - Pain (0-10): `8`
   - Pulse Oximetry (%): `89`
3. Click **Record Vitals**.

### Expected Result

- A green notification appears in the status bar: "Vitals recorded successfully."
- On the View Vitals TabItem:
  - TEMPERATURE: `104.2` -- displayed with red foreground (flagged)
  - PULSE: `118` -- displayed with red foreground (tachycardia)
  - RESPIRATION: `26` -- displayed with red foreground (tachypnea)
  - BLOOD PRESSURE: `88/52` -- displayed with red foreground (hypotension)
  - PAIN: `8` -- displayed with red foreground
  - PULSE OXIMETRY: `89` -- displayed with red foreground (hypoxia)
- Multiple flagged values should appear highlighted in bold red.

---

## Scenario 4: Pain Score Greater Than 0 Requires Follow-Up

### Steps

1. Click the **Record Vitals** TabItem.
2. Fill in:
   - Date/Time Taken: (leave as current date/time)
   - Location: `Ward 3A`
   - Entered By: `JOHNSON,MARY R`
   - Pain (0-10): `6`
   - (leave all other vitals blank)
3. Click **Record Vitals**.

### Expected Result

- A green notification appears in the status bar: "Vitals recorded successfully."
- The PAIN vital shows `6` on the View Vitals TabItem.
- **Follow-up required:** The tester should now select **Pain Assessment** in the Navigation Panel to record a structured pain assessment (see Script 07-Pain-Assessment.md). When Pain > 0 is recorded, nursing protocol requires a formal pain assessment within 1 hour.

---

## Scenario 5: Attempt to Record with No Vitals Entered

### Steps

1. Click the **Record Vitals** TabItem.
2. Fill in only the header fields:
   - Date/Time Taken: (leave as current date/time)
   - Location: `Ward 3A`
   - Entered By: `JOHNSON,MARY R`
3. Leave ALL 8 vital measurement TextBox fields blank.
4. Click **Record Vitals**.

### Expected Result

- A red error notification appears in the status bar: "Enter at least one vital measurement."
- No vitals are saved.
- The view remains on the Record Vitals TabItem with the header fields still populated.

---

## Scenario 6: Record Partial Vital Signs (Only BP and SpO2)

### Steps

1. Click the **Record Vitals** TabItem.
2. Fill in:
   - Date/Time Taken: (leave as current date/time)
   - Location: `Ambulatory Clinic 2`
   - Entered By: `JOHNSON,MARY R`
   - Blood Pressure (mmHg): `132/84`
   - Pulse Oximetry (%): `97`
   - (leave all other vitals blank)
3. Click **Record Vitals**.

### Expected Result

- A green notification appears in the status bar: "Vitals recorded successfully."
- On the View Vitals TabItem, the latest BLOOD PRESSURE shows `132/84` and PULSE OXIMETRY shows `97`.
- Other vitals retain their previous values from earlier recordings.

---

## Scenario 7: Vital History TabItem -- Search by Date Range

### Steps

1. Click the **Vital History** TabItem.
2. Set filters:
   - From DatePicker: (30 days ago)
   - To DatePicker: (today)
   - Vital Type ComboBox: `ALL`
   - Max Results: `50`
3. Click **Search**.

### Expected Result

- The results DataGrid shows all vitals recorded in the date range.
- Each row shows: Vital type, Value, Units, Flag, Date/Time.
- The result count is displayed above the DataGrid (e.g., "12 result(s)").

---

## Scenario 8: Vital History -- Filter by Specific Vital Type

### Steps

1. Click the **Vital History** TabItem.
2. Set filters:
   - From DatePicker: (30 days ago)
   - To DatePicker: (today)
   - Vital Type ComboBox: `BLOOD PRESSURE`
   - Max Results: `50`
3. Click **Search**.

### Expected Result

- Only BLOOD PRESSURE entries are shown in the results DataGrid.
- All entries have "BLOOD PRESSURE" in the Vital column.
- Entries are sorted by date/time, newest first.

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
