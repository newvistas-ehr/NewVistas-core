# Drug Utilization Review (DUR) -- Pharmacist Human Test Script

## Prerequisites

- **Login:** PHARM1 (WILLIAMS,ROBERT L -- Clinical Pharmacy) / Password: `smythVista1`
- **Patient:** 9
- **Pre-conditions:**
  1. The SiloHost, WebServer, and BlazorWeb projects must all be running.
  2. Navigate to `/drug-utilization-review` in the browser.
  3. Ensure Patient 9 has at least one active prescription. If not, create one via the Outpatient Pharmacy API:
     ```
     POST /api/outpatientpharmacy/demo/load?patientId=9
     ```
  4. Ensure Patient 9 has a documented allergy. Record one via:
     ```
     POST /api/patient/9/allergies
     { "allergen": "PENICILLIN", "severity": "SEVERE", "observedHistorical": "O", "reactions": ["ANAPHYLAXIS", "HIVES"] }
     ```

---

## Scenario 1: Happy Path -- All DUR Checks Pass

### Steps

1. Navigate to `/drug-utilization-review`.
2. Enter Patient ID: `9` in the Patient ID field and click **Load**.
3. Click **+ Perform DUR**.
4. Fill in the form:
   - Prescription ID: `RX-DUR-TEST-001`
   - Drug Name: `LISINOPRIL 10MG TAB`
   - Drug ID: `50-LISINOPRIL`
   - Drug Class (VA): `CV800`
   - Dosage: `10MG`
   - Route: `ORAL`
   - Schedule: `DAILY`
   - Days Supply: `30`
   - Quantity: `30`
   - Max Days Supply: `90`
   - Max Quantity: `90`
   - Controlled Substance: `No`
   - Ingredient IENs: `1898`
   - Max Daily Dose (mg): `80`
   - Performed By: `PHARM1`
5. Click **Run DUR**.

### Expected Result

- Success banner: "DUR assessment completed: DUR-XXXXXXXX"
- The view switches to the Detail tab automatically.
- Assessment status: **Passed** (green badge).
- Overall outcome: **Pass**.
- Check Results table shows all checks with outcome Pass or N/A:
  - Duplicate Drug: Pass
  - Drug-Allergy: Pass (no allergy to ACE inhibitors)
  - Days Supply: Pass (30 <= 90)
  - Max Dose: Pass (10mg <= 80mg)
  - Controlled Substance: N/A
  - Renal Adjustment: N/A or Pass
  - Hepatic Adjustment: N/A or Pass

---

## Scenario 2: Duplicate Drug Detected

### Steps

1. Ensure Patient 9 already has an active LISINOPRIL prescription (from demo data).
2. Click **+ Perform DUR**.
3. Fill in:
   - Prescription ID: `RX-DUR-TEST-002`
   - Drug Name: `LISINOPRIL 20MG TAB`
   - Drug ID: `50-LISINOPRIL`
   - Drug Class (VA): `CV800`
   - Dosage: `20MG`
   - Route: `ORAL`
   - Schedule: `DAILY`
   - Days Supply: `30`
   - Quantity: `30`
   - Max Days Supply: `90`
   - Controlled Substance: `No`
   - Ingredient IENs: `1898`
   - Performed By: `PHARM1`
4. Click **Run DUR**.

### Expected Result

- The Detail tab shows status: **Failed** (red badge) or **Pending** (yellow badge).
- Check Results table shows:
  - Duplicate Drug: **Fail** (red badge)
  - Message: references existing LISINOPRIL prescription
  - Conflict column: shows the conflicting drug name
- The Failed check count shows at least 1.
- Override button appears next to the Duplicate Drug row.

---

## Scenario 3: Drug-Allergy Contraindication

### Steps

1. Click **+ Perform DUR**.
2. Fill in:
   - Prescription ID: `RX-DUR-TEST-003`
   - Drug Name: `AMOXICILLIN 500MG CAP`
   - Drug ID: `50-AMOXICILLIN`
   - Drug Class (VA): `AM110`
   - Dosage: `500MG`
   - Route: `ORAL`
   - Schedule: `TID`
   - Days Supply: `10`
   - Quantity: `30`
   - Max Days Supply: `30`
   - Controlled Substance: `No`
   - Ingredient IENs: `383`
   - Performed By: `PHARM1`
3. Click **Run DUR**.

### Expected Result

- Status: **Failed** (red badge).
- Check Results table shows:
  - Drug-Allergy Contraindication: **Fail** (red badge)
  - Severity: HIGH or CRITICAL
  - Message: references PENICILLIN allergy (penicillin cross-reactivity with amoxicillin)
  - Conflict column: PENICILLIN
- This is a critical safety check. The pharmacist should contact the provider before overriding.

---

## Scenario 4: Days Supply Exceeded

### Steps

1. Click **+ Perform DUR**.
2. Fill in:
   - Prescription ID: `RX-DUR-TEST-004`
   - Drug Name: `METFORMIN 500MG TAB`
   - Drug ID: `50-METFORMIN`
   - Drug Class (VA): `HS502`
   - Dosage: `500MG`
   - Route: `ORAL`
   - Schedule: `BID`
   - Days Supply: `180`
   - Quantity: `360`
   - Max Days Supply: `90`
   - Max Quantity: `180`
   - Controlled Substance: `No`
   - Ingredient IENs: `6809`
   - Performed By: `PHARM1`
3. Click **Run DUR**.

### Expected Result

- Status: **Failed** or **Pending**.
- Check Results table shows:
  - Days Supply Exceeded: **Fail** (red badge)
  - Message: "Days supply 180 exceeds maximum of 90" (or similar)
- Override button available on the DaysSupplyExceeded row.

---

## Scenario 5: Controlled Substance Warning (DEA Schedule IV)

### Steps

1. Click **+ Perform DUR**.
2. Fill in:
   - Prescription ID: `RX-DUR-TEST-005`
   - Drug Name: `ALPRAZOLAM 0.5MG TAB`
   - Drug ID: `50-ALPRAZOLAM`
   - Drug Class (VA): `CN302`
   - Dosage: `0.5MG`
   - Route: `ORAL`
   - Schedule: `TID PRN`
   - Days Supply: `30`
   - Quantity: `90`
   - Max Days Supply: `30`
   - Controlled Substance: **Yes**
   - DEA Schedule: **Schedule IV**
   - Ingredient IENs: `205`
   - Performed By: `PHARM1`
3. Click **Run DUR**.

### Expected Result

- Check Results table shows:
  - Controlled Substance: **Warning** (yellow badge)
  - Message: references Schedule IV requirements
  - Severity: MODERATE
- Overall outcome may be **Warning** (not a hard fail, but requires acknowledgment).

---

## Scenario 6: Max Dose Exceeded

### Steps

1. Click **+ Perform DUR**.
2. Fill in:
   - Prescription ID: `RX-DUR-TEST-006`
   - Drug Name: `LISINOPRIL 80MG TAB`
   - Drug ID: `50-LISINOPRIL`
   - Drug Class (VA): `CV800`
   - Dosage: `80MG`
   - Route: `ORAL`
   - Schedule: `BID`
   - Days Supply: `30`
   - Quantity: `60`
   - Max Days Supply: `90`
   - Controlled Substance: `No`
   - Ingredient IENs: `1898`
   - Max Daily Dose (mg): `40`
   - Performed By: `PHARM1`
3. Click **Run DUR**.

### Expected Result

- Check Results table shows:
  - Max Dose Exceeded: **Fail** (red badge)
  - Message: "Dose exceeds maximum daily dose of 40mg" (or similar)
- Override button available.

---

## Scenario 7: Renal Adjustment Warning (eGFR < 60)

### Steps

1. First, seed a low eGFR lab result for Patient 9 via the Lab API:
   ```
   POST /api/lab/9/results/ingest
   {
     "testName": "eGFR",
     "loincCode": "48642-3",
     "value": "45",
     "units": "mL/min/1.73m2",
     "referenceRange": "60-120",
     "abnormalFlag": "Low"
   }
   ```
2. Click **+ Perform DUR**.
3. Fill in:
   - Prescription ID: `RX-DUR-TEST-007`
   - Drug Name: `METFORMIN 1000MG TAB`
   - Drug ID: `50-METFORMIN`
   - Drug Class (VA): `HS502`
   - Dosage: `1000MG`
   - Route: `ORAL`
   - Schedule: `BID`
   - Days Supply: `30`
   - Quantity: `60`
   - Max Days Supply: `90`
   - Controlled Substance: `No`
   - Ingredient IENs: `6809`
   - Max Daily Dose (mg): `2000`
   - Performed By: `PHARM1`
4. Click **Run DUR**.

### Expected Result

- Check Results table shows:
  - Renal Adjustment: **Warning** or **Fail**
  - Message: references eGFR < 60 and need for dose adjustment
  - Severity: HIGH
- Clinical guidance: Metformin requires dose reduction or discontinuation when eGFR < 30; caution when 30-60.

---

## Scenario 8: Hepatic Adjustment Warning (Elevated ALT)

### Steps

1. Seed an elevated ALT lab result for Patient 9 via the Lab API:
   ```
   POST /api/lab/9/results/ingest
   {
     "testName": "ALT",
     "loincCode": "1742-6",
     "value": "185",
     "units": "U/L",
     "referenceRange": "7-56",
     "abnormalFlag": "High"
   }
   ```
2. Click **+ Perform DUR**.
3. Fill in:
   - Prescription ID: `RX-DUR-TEST-008`
   - Drug Name: `ATORVASTATIN 40MG TAB`
   - Drug ID: `50-ATORVASTATIN`
   - Drug Class (VA): `CV350`
   - Dosage: `40MG`
   - Route: `ORAL`
   - Schedule: `QHS`
   - Days Supply: `30`
   - Quantity: `30`
   - Max Days Supply: `90`
   - Controlled Substance: `No`
   - Ingredient IENs: `39367`
   - Max Daily Dose (mg): `80`
   - Performed By: `PHARM1`
4. Click **Run DUR**.

### Expected Result

- Check Results table shows:
  - Hepatic Adjustment: **Warning** or **Fail**
  - Message: references elevated ALT (>3x ULN) and hepatotoxicity risk
  - Severity: HIGH
- Clinical guidance: Statins should be held or discontinued if ALT > 3x upper limit of normal.

---

## Scenario 9: Override a Failed DUR Check with Clinical Reason

### Steps

1. Load a previously failed DUR assessment from the All or Pending tab.
2. Click **View** on a Failed assessment (e.g., the Duplicate Drug from Scenario 2).
3. The Detail tab opens showing the failed check.
4. Locate the failed check row (e.g., Duplicate Drug with Fail outcome).
5. Click the **Override** button on that row.
6. The Override form appears with fields:
   - Pharmacist ID: enter `PHARM1`
   - Clinical Reason: enter `Provider aware of duplicate therapy. Dose titration in progress - increasing from 10mg to 20mg. Old prescription to be discontinued.`
7. Click **Submit Override**.

### Expected Result

- Success banner: "Override submitted for Duplicate Drug."
- The check row now shows:
  - Outcome: **Override** (gold/brown badge)
  - Override column: "Overridden by PHARM1"
  - Hovering shows the clinical reason
- If all failed checks are now overridden, the assessment status changes to **OverriddenByPharmacist**.

---

## Scenario 10: Acknowledge DUR Assessment

### Steps

1. On the Detail tab of a DUR assessment (either Passed or Overridden), locate the Acknowledge section at the bottom.
2. Enter:
   - Pharmacist ID: `PHARM1`
   - Notes: `Reviewed all DUR checks. No additional clinical concerns.`
3. Click **Acknowledge**.

### Expected Result

- Success banner: "Assessment acknowledged."
- The assessment status changes to **Acknowledged** (blue badge).
- The Acknowledged section in detail shows: date/time and PHARM1.
- The Acknowledge button disappears (already acknowledged).
- On the All tab, the assessment row now shows Status: Acknowledged.
- On the Pending tab, this assessment no longer appears.
