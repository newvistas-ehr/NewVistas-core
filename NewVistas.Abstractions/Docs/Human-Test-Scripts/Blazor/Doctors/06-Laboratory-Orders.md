# Laboratory Orders and Results -- Physician Human Test Script

## Prerequisites
- Login: DOCTOR1 / Password: smythVista1
- Patient: 4
- Pre-conditions: Demo data loaded. SiloHost, WebServer, and BlazorWeb running.

---

## Scenario 1: Order a CBC, Collect, Enter Results, and Verify (Happy Path)

### Part A: Place the Order

#### Steps
1. Log in as **DOCTOR1** (SMITH,JOHN A / Internal Medicine)
2. Navigate to `/labs`
3. The page shows three tabs: **Results**, **Current Summary**, **Order / Submit**
4. Click the **Order / Submit** tab
5. In the Patient ID field, enter: `4`
6. Under "Order New Lab Test", fill in:
   - Test Name: `CBC`
   - LOINC / Test Code: `58410-2`
   - Category: **HEMATOLOGY** (dropdown; options: HEMATOLOGY, CHEMISTRY, MICROBIOLOGY, COAGULATION, URINALYSIS, SEROLOGY, BLOOD BANK)
   - Specimen Type: **Blood** (dropdown; options: Blood, Serum, Urine, CSF, Tissue, Stool, Sputum, Swab)
   - Ordering Provider: `SMITH,JOHN A`
7. Click **Place Order**

#### Expected Result
- Green success bar: "Lab order placed: CBC"
- The Test Name and Test Code fields clear

### Part B: View the Order in Results Tab

#### Steps
1. Click the **Results** tab
2. Click **Load Results**

#### Expected Result
- Summary cards appear at top:
  - Total Orders: at least 1
  - Abnormal: 0
  - Pending: 1
- The CBC order appears in the table with:
  - Test: CBC
  - Result: -- (em dash)
  - Units: -- (em dash)
  - Flag: -- (em dash)
  - Status: badge "Ordered" (purple)
  - Collected: empty
  - Action button: **Collect**

### Part C: Collect the Specimen

#### Steps
1. Click the **Collect** button on the CBC row
2. The "Record Specimen Collection" action panel appears below the table
3. Fill in:
   - Collection Time: Current date/time (pre-filled)
   - Collection Sample: `LAVENDER`
   - Performing Lab: `Hematology Lab`
4. Click **Submit**

#### Expected Result
- Green success: "Specimen collection recorded."
- The Collect panel closes
- Results table reloads
- CBC status changes to "Collected" (blue badge)
- Collected column shows the date/time
- New action button appears: **Enter Result**

### Part D: Enter the Result

#### Steps
1. Click the **Enter Result** button on the CBC row
2. The "Enter Result -- CBC" action panel appears
3. Fill in:
   - Result Value: `7.5`
   - Units: `K/cmm`
   - Reference Low: `4.5`
   - Reference High: `11.0`
   - Abnormal Flag: **Normal** (dropdown; options: Normal, H -- High, L -- Low, CH -- Critical High, CL -- Critical Low, A -- Abnormal)
4. Click **Submit**

#### Expected Result
- Green success: "Lab result recorded."
- The Result panel closes
- Results reload
- CBC shows:
  - Result: 7.5
  - Units: K/cmm
  - Flag: -- (normal, no flag)
  - Status: "Completed" (green badge)
  - Action button: **Verify**

### Part E: Verify the Result

#### Steps
1. Click the **Verify** button on the CBC row
2. The "Verify Result" action panel appears
3. Fill in:
   - Verifying Provider ID: `DOCTOR1`
   - Verifying Provider Name: `SMITH,JOHN A`
4. Click **Verify**

#### Expected Result
- Green success: "Lab result verified."
- The Verify panel closes
- CBC status remains "Completed" (green badge)
- The Verify button disappears (provider name now set)

---

## Scenario 2: Order BMP with STAT Urgency

### Steps
1. Click the **Order / Submit** tab
2. Fill in:
   - Test Name: `Basic Metabolic Panel`
   - LOINC / Test Code: `51990-0`
   - Category: **CHEMISTRY**
   - Specimen Type: **Serum**
   - Ordering Provider: `SMITH,JOHN A`
3. Click **Place Order**
4. Switch to Results tab and Load Results

### Expected Result
- BMP order appears with Status: "Ordered"
- To make it STAT: Note that the Labs page Order form does not have an Urgency field (urgency is on the Orders page). For STAT lab urgency, use the Orders page (`/orders`) with Order Type: Lab.

---

## Scenario 3: Abnormal Result (High Potassium)

### Steps
1. Order a lab test: Test Name `Potassium`, LOINC `2823-3`, Category: CHEMISTRY, Specimen: Serum
2. Collect the specimen (same flow as Scenario 1C)
3. Enter the result:
   - Result Value: `6.2`
   - Units: `mEq/L`
   - Reference Low: `3.5`
   - Reference High: `5.0`
   - Abnormal Flag: **H -- High**
4. Submit

### Expected Result
- Result row has yellow background (row-abnormal class)
- Result value "6.2" appears in red bold font (flagged class)
- Flag column shows an orange "H" badge (badge-high)
- Summary cards update: Abnormal count increases by 1

---

## Scenario 4: Critical Result

### Steps
1. Order a lab test: Test Name `Potassium (Critical)`, LOINC `2823-3`, Category: CHEMISTRY, Specimen: Serum
2. Collect the specimen
3. Enter the result:
   - Result Value: `7.8`
   - Units: `mEq/L`
   - Reference Low: `3.5`
   - Reference High: `5.0`
   - Abnormal Flag: **CH -- Critical High**
4. Submit

### Expected Result
- Result row has red background (row-critical class)
- Result value "7.8" appears in red bold font
- Flag column shows a red "CH" badge (badge-critical)
- Summary cards: Abnormal count increases

---

## Scenario 5: Review Lab Summary and Trends

### Steps
1. Click the **Current Summary** tab
2. Enter Patient ID: `4`
3. Click **Load Summary**

### Expected Result
- If ingested results exist (via demo data or Ingest form), a summary table appears with columns:
  - Test, LOINC, Value, Units, Ref Range, Flag, Date, Trend (last 3), Facility
- If abnormal results exist, a yellow **Abnormal Results** banner appears at the top listing flagged values
- The Trend column shows arrows between recent values (e.g., "7.5 -> 7.2 -> 6.8")
- Values with abnormal flags show "!" indicator in the trend

---

## Scenario 6: Load Demo Data

### Steps
1. On the **Results** tab, enter Patient ID: `4`
2. Click the **Load Demo** button (dark gray)

### Expected Result
- Green success: "Demo data loaded successfully."
- Results load showing at least 2 orders:
  - CBC (LOINC 58410-2) -- Status: Ordered
  - Basic Metabolic Panel (LOINC 51990-0) -- Status: Ordered
- Summary cards show the total count

---

## Scenario 7: Ingest a Result Directly (HL7-style)

### Steps
1. Click the **Order / Submit** tab
2. Under "Ingest Result (HL7-style)", fill in:
   - LOINC Code: `2160-0`
   - Test Name: `Creatinine`
   - Value: `1.4`
   - Units: `mg/dL`
   - Reference Range: `0.7-1.3`
   - Abnormal Flag: **High** (dropdown; options: Normal, High, Low, CriticalHigh, CriticalLow, Abnormal)
   - Facility Code: `688`
   - Panel Name: `BMP`
3. Click **Ingest Result**

### Expected Result
- Green success: "Result ingested: Creatinine = 1.4 mg/dL"
- LOINC, Test Name, Value, and Units fields clear
- The result is now visible in the Current Summary tab

---

## Scenario 8: Validation -- Missing Fields

### Steps
1. On the Order / Submit tab, leave Test Name empty
2. Click **Place Order**

### Expected Result
- Red error: "Test name required."

### Steps (continued)
3. On the Ingest form, leave LOINC Code empty
4. Click **Ingest Result**

### Expected Result
- Red error: "LOINC code and value required."

---

## Reference: Common Lab Tests with Normal Ranges

| Test | LOINC | Normal Range | Units | Category |
|------|-------|-------------|-------|----------|
| WBC | 6690-2 | 4.5-11.0 | K/cmm | HEMATOLOGY |
| Hemoglobin | 718-7 | 12.0-17.5 | g/dL | HEMATOLOGY |
| Hematocrit | 4544-3 | 36-52 | % | HEMATOLOGY |
| Platelets | 777-3 | 150-400 | K/cmm | HEMATOLOGY |
| Sodium | 2951-2 | 136-145 | mEq/L | CHEMISTRY |
| Potassium | 2823-3 | 3.5-5.0 | mEq/L | CHEMISTRY |
| Creatinine | 2160-0 | 0.7-1.3 | mg/dL | CHEMISTRY |
| Glucose | 2345-7 | 70-100 | mg/dL | CHEMISTRY |
| BUN | 3094-0 | 7-20 | mg/dL | CHEMISTRY |
| AST | 1920-8 | 10-40 | U/L | CHEMISTRY |
| ALT | 1742-6 | 7-56 | U/L | CHEMISTRY |
| Troponin I | 10839-9 | 0.00-0.04 | ng/mL | CHEMISTRY |

---

## Appendix: Clinical Event Sourcing Verification

**Added 2026-04-27** -- Lab orders now emit clinical events to the per-patient
event stream and flow to the federation outbox when enabled.

### Steps

1. Before placing the lab order, capture the patient's current event-stream version:
   ```powershell
   $login = Invoke-RestMethod -Method Post -Uri https://localhost:7127/api/auth/login `
     -Body (@{ username = "DOCTOR1"; password = "smythVista1" } | ConvertTo-Json) `
     -ContentType "application/json"
   $before = Invoke-RestMethod -Method Get `
     -Uri "https://localhost:7127/api/patient/{patientId}/clinical-events?domain=Lab&max=1" `
     -Headers @{ Authorization = "Bearer $($login.token)" }
   $beforeVersion = if ($before) { $before[0].version } else { 0 }
   ```
2. Place a lab order via the UI (Scenario 1).
3. Re-query, filtered to the Lab domain:
   ```powershell
   Invoke-RestMethod -Method Get `
     -Uri "https://localhost:7127/api/patient/{patientId}/clinical-events?domain=Lab&max=5" `
     -Headers @{ Authorization = "Bearer $($login.token)" }
   ```

### Expected Result

- One new event with `domain = Lab` and `version > beforeVersion`.
- Hash chain remains valid (re-run [Admin/08](../Admin/08-Clinical-Event-Sourcing.md) Scenario 7 if in doubt).

### Verification Checklist (Event Sourcing)

- [ ] New `Lab` event appears after order placement
- [ ] Event hash chain still verifies as valid
- [ ] Federation outbox row inserted (if outbox enabled)

Cross-ref: `ClinicalEventSourcingTests.Append_AssignsHashChainAndIncrementsVersion`. See [Blazor/Admin/08-Clinical-Event-Sourcing.md](../Admin/08-Clinical-Event-Sourcing.md).
