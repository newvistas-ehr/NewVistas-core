# Radiology Orders -- Physician CharUI Human Test Script

## Prerequisites

- **Login:** DOCTOR1 / Password: `smythVista1`
- **Security Keys:** PROVIDER, ORES, TIU SIGN, GMRA ALLERGY, GMRV VITALS, GMPL PROBLEM
- **Patient:** Select a patient with demo data loaded.
- **Pre-conditions:** SiloHost and WebServer running. Login and select a patient per `01-Login-Patient-Selection.md`.

---

## Scenario 1: List Radiology Studies (Happy Path)

### Steps

1. At the Main Menu, type: `RD` and press Enter.
2. At the Radiology menu, type: `1` (List Radiology Studies).

### Expected Result

- A table displays with columns: #, Procedure, Type, Status, Date, Provider.
- Shows all radiology studies for the patient.

---

## Scenario 2: View Radiology Study Detail

### Steps

1. At the Radiology menu, type: `2` (View Study Detail).
2. A numbered list of radiology studies appears.
3. Select a study by number (e.g., `1`).

### Expected Result

- The terminal displays the full study detail:
  ```
  Procedure: CXR PA & Lateral
  Imaging Type: XR
  CPT Code: 71046
  Status: COMPLETED
  Exam Date: 03/31/2026 09:30
  Requesting Provider: SMITH,JOHN A
  Clinical History: Annual screening, former smoker
  Urgency: Routine
  ---
  Report:
  FINDINGS: Heart size is normal. Lungs are clear bilaterally.
  No pleural effusion. No pneumothorax. Mediastinal contours normal.

  Impression:
  Normal chest radiograph.
  ```
- Returns to the Radiology menu.

---

## Scenario 3: Order a Radiology Study -- X-Ray (Happy Path)

### Steps

1. At the Radiology menu, type: `3` (Order Radiology Study).
2. Enter the following field-by-field:

| Prompt | Value to Enter |
|--------|----------------|
| Procedure Name | `CXR PA and Lateral` |
| CPT Code (optional) | `71046` |
| Imaging Type (XR, CT, MRI, US, NM) | `XR` |
| Urgency (Routine, STAT, ASAP) | `Routine` |
| Clinical History | `Annual wellness exam, former smoker 20 pack-years, quit 5 years ago` |
| Reason for Study | `Lung cancer screening, baseline chest X-ray` |

3. At the confirmation prompt `Order this radiology study?`, type: `Y`.

### Expected Result

- The terminal displays: `Radiology study ordered: [study-ID]`
- Returns to the Radiology menu.
- Verify by listing studies (option 1) -- the new CXR study appears.

---

## Scenario 4: Order a CT Scan -- STAT

### Steps

1. At the Radiology menu, type: `3` (Order Radiology Study).
2. Enter:

| Prompt | Value to Enter |
|--------|----------------|
| Procedure Name | `CT Head without Contrast` |
| CPT Code (optional) | `70450` |
| Imaging Type | `CT` |
| Urgency | `STAT` |
| Clinical History | `72 yo M presents with acute onset right-sided weakness and slurred speech, onset 45 minutes ago` |
| Reason for Study | `Rule out acute CVA, stroke alert` |

3. Confirm: `Y`

### Expected Result

- Study ordered with Imaging Type = CT and Urgency = STAT.

---

## Scenario 5: Order an MRI

### Steps

1. At the Radiology menu, type: `3` (Order Radiology Study).
2. Enter:

| Prompt | Value to Enter |
|--------|----------------|
| Procedure Name | `MRI Brain with and without Contrast` |
| CPT Code (optional) | `70553` |
| Imaging Type | `MRI` |
| Urgency | `ASAP` |
| Clinical History | `New onset seizures, no prior history. Normal CT head.` |
| Reason for Study | `Evaluate for structural lesion, tumor, or vascular malformation` |

3. Confirm: `Y`

### Expected Result

- Study ordered with Imaging Type = MRI.

---

## Scenario 6: Order an Ultrasound

### Steps

1. At the Radiology menu, type: `3`.
2. Enter:

| Prompt | Value to Enter |
|--------|----------------|
| Procedure Name | `Abdominal Ultrasound Complete` |
| CPT Code (optional) | `76700` |
| Imaging Type | `US` |
| Urgency | `Routine` |
| Clinical History | `RUQ pain, elevated LFTs (ALT 85, AST 72)` |
| Reason for Study | `Evaluate for cholelithiasis, hepatic disease` |

3. Confirm: `Y`

### Expected Result

- Study ordered with Imaging Type = US.

---

## Scenario 7: Order a Nuclear Medicine Study

### Steps

1. At the Radiology menu, type: `3`.
2. Enter:

| Prompt | Value to Enter |
|--------|----------------|
| Procedure Name | `Myocardial Perfusion Imaging` |
| CPT Code (optional) | `78452` |
| Imaging Type | `NM` |
| Urgency | `Routine` |
| Clinical History | `Chest pain on exertion, history of HTN and DM. ECG nondiagnostic.` |
| Reason for Study | `Evaluate for coronary artery disease` |

3. Confirm: `Y`

### Expected Result

- Study ordered with Imaging Type = NM.

---

## Scenario 8: Order Study -- Minimal Fields

### Steps

1. At the Radiology menu, type: `3`.
2. Enter:

| Prompt | Value to Enter |
|--------|----------------|
| Procedure Name | `KUB` |
| CPT Code (optional) | (press Enter to skip) |
| Imaging Type | (press Enter for default: XR) |
| Urgency | (press Enter for default: Routine) |
| Clinical History | `Abdominal pain and constipation` |
| Reason for Study | `Evaluate bowel gas pattern` |

3. Confirm: `Y`

### Expected Result

- Study ordered with defaults: Type = XR, Urgency = Routine, no CPT code.

---

## Scenario 9: Cancel Ordering a Radiology Study

### Steps

1. At the Radiology menu, type: `3`.
2. Fill in fields with test data.
3. At the confirmation prompt `Order this radiology study?`, type: `N`.

### Expected Result

- The study is NOT ordered.
- Returns to the Radiology menu.

---

## Scenario 10: Return to Main Menu

### Steps

1. At the Radiology menu, type `Q` or `^` to return.

### Expected Result

- Returns to the Main Menu with patient context preserved.
