# Nursing Triage (ESI) -- Human Test Script -- WPF UI

## Prerequisites

- **Login:** NURSE3 / Password: `smythVista1`
- **Patient:** 22
- **Pre-conditions:**
  1. The SiloHost, WebServer, and WPF Application (NewVistas.WpfClient) must all be running.
  2. In the Navigation Panel, select **Nursing Triage**.
  3. Enter Patient ID `22` in the Patient ID field in the toolbar and click **Load**.
  4. The Triage History TabItem loads. If no triages exist, the DataGrid is empty.

---

## Scenario 1: ESI Level 3 -- Chest Pain Workup (Happy Path)

### Steps

1. In the Navigation Panel, select **Nursing Triage**.
2. Enter Patient ID: `22` in the Patient ID field in the toolbar.
3. Click **Load**.
4. Click the **New Triage** TabItem.
5. Fill in all fields:
   - Chief Complaint TextBox: `Chest pain, substernal, onset 2 hours ago at rest`
   - Nurse ID TextBox: `NURSE3`
   - Nurse Name TextBox: `RODRIGUEZ,MARIA L`
   - Temp (F) TextBox: `98.4`
   - HR TextBox: `88`
   - RR TextBox: `20`
   - Sys BP TextBox: `148`
   - Dia BP TextBox: `92`
   - SpO2 TextBox: `96`
   - Pain (0-10) TextBox: `7`
   - ESI Level ComboBox: `ESI 3 -- Urgent`
   - Mode of Arrival ComboBox: `AMBULATORY`
   - Notes TextBox: `55 y/o male presents with substernal chest pain radiating to left arm, onset at rest 2 hours ago. Denies diaphoresis, nausea. History of HTN, hyperlipidemia. Takes lisinopril and atorvastatin. ECG ordered, troponin ordered.`
6. Click **Create Triage**.

### Expected Result

- A green notification appears in the status bar: "Triage created: TRIAGE-..."
- Click the **Triage History** TabItem. A new entry appears:
  - Date/Time: current date/time
  - Nurse: RODRIGUEZ,MARIA L
  - Chief Complaint: Chest pain, substernal, onset 2 hours ago at rest
  - ESI: ESI 3 status indicator (blue/info colored)
  - Pain: 7
  - Disposition: Pending (yellow/warning status indicator)
  - Status: Draft (yellow/warning status indicator)
- Click **View** on the row (or right-click and select **View**) to see the detail.
- The Triage Detail TabItem shows all entered values:
  - Chief Complaint: Chest pain, substernal, onset 2 hours ago at rest
  - Arrival: AMBULATORY
  - Acute Distress: No
  - LOC: -- (not set)
  - Triage Vital Signs:
    - Temp: 98.4 F
    - HR: 88 bpm
    - RR: 20
    - BP: 148/92
    - SpO2: 96%
    - Pain: 7/10

---

## Scenario 2: ESI Level 1 -- Resuscitation (Cardiac Arrest)

### Steps

1. Click the **New Triage** TabItem.
2. Fill in:
   - Chief Complaint TextBox: `Unresponsive, found on floor by family, no pulse detected by EMS`
   - Nurse ID TextBox: `NURSE3`
   - Nurse Name TextBox: `RODRIGUEZ,MARIA L`
   - Temp (F) TextBox: `96.0`
   - HR TextBox: `0`
   - RR TextBox: `0`
   - Sys BP TextBox: `0`
   - Dia BP TextBox: `0`
   - SpO2 TextBox: `0`
   - Pain (0-10) TextBox: `0`
   - ESI Level ComboBox: `ESI 1 -- Resuscitation`
   - Mode of Arrival ComboBox: `AMBULANCE`
   - Notes TextBox: `Cardiac arrest. EMS reports witnessed collapse at home 15 minutes ago. CPR in progress. 2 rounds of epinephrine given prehospital. Code team activated. Patient to Resuscitation Bay 1.`
3. Click **Create Triage**.

### Expected Result

- Triage created successfully.
- On the Triage History TabItem, the new entry shows:
  - ESI: ESI 1 status indicator (red/danger colored)
  - Pain: 0
  - Disposition: Pending
  - Status: Draft
- On Triage Detail:
  - Temp: 96.0 F
  - HR: 0 bpm
  - BP: 0/0
  - SpO2: 0%
  - Arrival: AMBULANCE (Ambulance)
  - The Arrived by Ambulance flag is set to Yes since mode is AMBULANCE.

---

## Scenario 3: ESI Level 5 -- Non-Urgent (Medication Refill)

### Steps

1. Click the **New Triage** TabItem.
2. Fill in:
   - Chief Complaint TextBox: `Needs blood pressure medication refill, ran out 3 days ago`
   - Nurse ID TextBox: `NURSE3`
   - Nurse Name TextBox: `RODRIGUEZ,MARIA L`
   - Temp (F) TextBox: `98.2`
   - HR TextBox: `76`
   - RR TextBox: `14`
   - Sys BP TextBox: `134`
   - Dia BP TextBox: `86`
   - SpO2 TextBox: `99`
   - Pain (0-10) TextBox: `0`
   - ESI Level ComboBox: `ESI 5 -- Non-Urgent`
   - Mode of Arrival ComboBox: `AMBULATORY`
   - Notes TextBox: `Patient presents requesting refill on amlodipine 5mg. Ran out 3 days ago. No acute symptoms. VS within normal limits. Will redirect to primary care or urgent care for prescription refill.`
3. Click **Create Triage**.

### Expected Result

- Triage created successfully.
- On Triage History:
  - ESI: ESI 5 status indicator (green/success colored)
  - Pain: 0
  - Status: Draft

---

## Scenario 4: Sign a Triage Assessment

### Steps

1. On the **Triage History** TabItem, click **View** on the ESI Level 3 chest pain triage (Scenario 1) (or right-click and select **View**).
2. The **Triage Detail** TabItem shows the full assessment.
3. Verify the Status shows **Draft**.
4. Click the **Sign** button.

### Expected Result

- The detail reloads.
- Status changes from Draft (yellow) to **Signed** (green).
- The **Sign** button is no longer visible.
- On the Triage History TabItem, the row now shows Status: Signed.

---

## Scenario 5: Assign Disposition -- Admit

### Steps

1. On the **Triage Detail** TabItem for the ESI Level 3 chest pain triage, verify the Disposition is **Pending**.
2. Click the **Admit** button.

### Expected Result

- The disposition status indicator changes from Pending (yellow) to **Admit** (blue/primary).
- The Admit, Observation, and Discharge buttons disappear (disposition is now set).
- On the Triage History TabItem, the Disposition column shows: Admit.

---

## Scenario 6: Assign Disposition -- Observation

### Steps

1. View the Triage Detail for the ESI Level 5 medication refill triage.
2. Verify the Disposition is **Pending**.
3. Click the **Observation** button.

### Expected Result

- Disposition changes to **Observation** (info/teal status indicator).

---

## Scenario 7: Assign Disposition -- Discharge

### Steps

1. View the Triage Detail for the ESI Level 5 medication refill triage (if still Pending) or create a new ESI 5 triage.
2. Click the **Discharge** button.

### Expected Result

- Disposition changes to **Discharge** (green status indicator).

---

## Reference: ESI Triage Levels

| ESI Level | Name           | Description                                      | Color Indicator |
|-----------|----------------|--------------------------------------------------|-----------------|
| 1         | Resuscitation  | Immediate life-saving intervention required       | Red/Danger      |
| 2         | Emergent       | High risk, confused/lethargic, severe pain/distress | Yellow/Warning |
| 3         | Urgent         | Two or more resources needed (labs, ECG, imaging) | Blue/Info       |
| 4         | Less Urgent    | One resource needed (simple lab, X-ray)           | Primary         |
| 5         | Non-Urgent     | No resources needed (Rx refill, suture removal)   | Green           |

### Disposition Values

| Disposition  | Description                            |
|-------------|----------------------------------------|
| Pending     | Not yet determined                      |
| Admit       | Admission to inpatient unit            |
| Observation | Observation status (< 24 hours)        |
| Discharge   | Discharge home                         |
| Transfer    | Transfer to another facility           |

### Mode of Arrival Options

- AMBULATORY
- WHEELCHAIR
- STRETCHER
- AMBULANCE
