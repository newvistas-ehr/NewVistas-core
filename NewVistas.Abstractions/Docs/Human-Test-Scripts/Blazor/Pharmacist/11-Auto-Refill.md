# Auto-Refill Enrollment -- Pharmacist Human Test Script

## Prerequisites

- **Login:** PHARM3 (MARTINEZ,CARLOS R -- Ambulatory Pharmacy) / Password: `smythVista1`
- **Patient:** 30
- **Pre-conditions:**
  1. The SiloHost, WebServer, and BlazorWeb projects must all be running.
  2. Navigate to `/auto-refill` in the browser.
  3. The AUTO_REFILL feature must be enabled. If the page shows a warning "The AUTO_REFILL feature is not enabled", navigate to `/site-parameters` and enable the feature, then return to `/auto-refill`.
  4. Ensure Patient 30 has active prescriptions. Load demo data if needed:
     ```
     POST /api/outpatientpharmacy/demo/load?patientId=30
     ```

---

## Scenario 1: Enroll Patient in Auto-Refill

### Steps

1. Navigate to `/auto-refill`.
2. The **Patient Enrollments** tab (tab 0) should be active.
3. Enroll a prescription in auto-refill via the API:
   ```
   POST /api/autorefill/enroll
   {
     "patientId": "30",
     "patientName": "DEMO,PATIENT THIRTY",
     "prescriptionId": "RX-AR-001",
     "drugName": "LISINOPRIL 10MG TAB",
     "drugId": "50-LISINOPRIL",
     "daysSupply": 90,
     "refillsRemaining": 5,
     "pharmacyName": "MAIN PHARMACY",
     "nextRefillDate": "2026-04-15"
   }
   ```
4. Enter Patient ID: `30` in the Patient Enrollments tab and click **Load**.

### Expected Result

- The enrollments table shows:
  - Drug: LISINOPRIL 10MG TAB (bold)
  - Status: **ACTIVE** (green styled badge)
  - Next Refill: 04/15/2026
  - Refills Left: 5
  - Pharmacy: MAIN PHARMACY
  - Auto-Refills: 0 (no refills generated yet)

---

## Scenario 2: View Enrollments

### Steps

1. On the **Patient Enrollments** tab, enter Patient ID: `30`.
2. Click **Load**.
3. Add a second enrollment via the API:
   ```
   POST /api/autorefill/enroll
   {
     "patientId": "30",
     "patientName": "DEMO,PATIENT THIRTY",
     "prescriptionId": "RX-AR-002",
     "drugName": "METFORMIN 500MG TAB",
     "drugId": "50-METFORMIN",
     "daysSupply": 30,
     "refillsRemaining": 11,
     "pharmacyName": "MAIN PHARMACY",
     "nextRefillDate": "2026-04-01"
   }
   ```
4. Click **Load** again.

### Expected Result

- The enrollments table now shows 2 entries.
- Columns displayed: Drug, Status, Next Refill, Refills Left, Pharmacy, Auto-Refills.
- Both entries show Status: ACTIVE.

---

## Scenario 3: Suspend Auto-Refill with Reason

### Steps

1. Suspend an enrollment via the API:
   ```
   POST /api/autorefill/{enrollmentId}/suspend
   {
     "reason": "Patient hospitalized - hold all auto-refills until discharge"
   }
   ```
2. Return to the Patient Enrollments tab and click **Load** for Patient 30.

### Expected Result

- The suspended enrollment shows Status: **SUSPENDED** (different colored badge, per the GetStatusStyle function).
- Other active enrollments remain unaffected.
- The status values supported are: ACTIVE, REFILL_PENDING, SUSPENDED, NO_REFILLS, DISENROLLED, EXPIRED.

---

## Scenario 4: Resume Suspended Enrollment

### Steps

1. Resume the suspended enrollment via the API:
   ```
   POST /api/autorefill/{enrollmentId}/resume
   ```
2. Reload the patient enrollments.

### Expected Result

- The enrollment status changes back to **ACTIVE**.
- The Next Refill date may be recalculated.

---

## Scenario 5: Disenroll with Reason

### Steps

1. Disenroll a prescription from auto-refill via the API:
   ```
   POST /api/autorefill/{enrollmentId}/disenroll
   {
     "reason": "Patient transferred care to outside provider"
   }
   ```
2. Reload the patient enrollments.

### Expected Result

- The enrollment status changes to **DISENROLLED**.
- The enrollment remains visible in the list but is no longer active.
- No future auto-refills will be generated for this enrollment.

---

## Scenario 6: View Auto-Refill Dashboard (Due Prescriptions)

### Steps

1. Click the **Due for Refill** tab (tab 1).
2. Click **Load Due Refills**.
3. This shows all enrollments across all patients that are currently due for refill.

### Expected Result

- If any enrollments have a NextRefillDate on or before today, they appear in the table.
- Columns: Patient (name + ID), Drug, Next Refill, Refills Left, Pharmacy.
- If no refills are currently due, the message "No refills currently due." appears.

### Dashboard Tab

4. Click the **Dashboard** tab (tab 2).
5. Use the Status dropdown to filter:
   - Select **ACTIVE** and click **Search**. Only active enrollments appear.
   - Select **SUSPENDED** and click **Search**. Only suspended enrollments appear.
   - Select **All** (empty) and click **Search**. All enrollments appear.

### Expected Result

- The dashboard table shows columns: Patient, Drug, Status, Next Refill, Refills Left, Pharmacy, Auto-Refills.
- Status badges are color-coded per enrollment status.
- Filtering by status correctly narrows the results.
- If no results match, "No enrollments match." message appears.
