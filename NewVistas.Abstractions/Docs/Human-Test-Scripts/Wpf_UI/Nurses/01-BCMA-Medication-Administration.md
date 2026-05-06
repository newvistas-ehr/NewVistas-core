# BCMA -- Bar Code Medication Administration -- Human Test Script -- WPF UI

## Prerequisites

- **Login:** NURSE2 / Password: `smythVista1`
- **Patient:** 4
- **Pre-conditions:**
  1. The SiloHost, WebServer, and WPF Application (NewVistas.WpfClient) must all be running.
  2. In the Navigation Panel, select **BCMA**.
  3. Enter Patient ID `4` in the Patient ID field in the toolbar and click **Load Demo** to seed MAR data.
  4. After demo load succeeds (a green notification appears in the status bar: "Data loaded from grains."), the MAR tab should display 4 medication entries:
     - Metoprolol Tartrate 25mg PO BID (ROUTINE)
     - Lisinopril 10mg PO DAILY (ROUTINE)
     - Vancomycin 1g IV Q12H (STAT)
     - Morphine Sulfate 4mg IV PRN Q4H (ROUTINE)
  5. The History tab should show 2 prior administrations (one GIVEN for Metoprolol, one NOT GIVEN for Morphine).

---

## Scenario 1: Administer a Scheduled Medication (Happy Path)

### Steps

1. In the Navigation Panel, select **BCMA**.
2. Enter Patient ID: `4` in the Patient ID field in the toolbar.
3. Click **Load**.
4. Verify the **MAR** TabItem is active and shows the medication list.
5. Locate **Lisinopril** (10mg PO DAILY) in the MAR DataGrid.
6. Confirm the Status column shows **DUE** (orange highlight on the row) or **ON SCHEDULE**.
7. Click the **Administer** button on the Lisinopril row (or right-click and select **Administer**).
8. In the dialog window "Administer: Lisinopril", fill in:
   - Action Status: `GIVEN`
   - Administration Time: (leave as current date/time)
   - Administered By: `THOMPSON,PATRICIA A`
   - Injection Site: (leave blank -- oral medication)
   - Comments: `Patient tolerated well, no adverse effects`
9. Click **Confirm**.

### Expected Result

- A green notification appears in the status bar: "Administration recorded."
- The dialog window closes.
- The MAR reloads. Lisinopril row now shows:
  - Last Given column: current date/time with "GIVEN by THOMPSON,PATRICIA A"
  - Count column: incremented by 1
- Switch to the **History** TabItem. The newest entry shows:
  - Drug: Lisinopril
  - Dosage: 10mg
  - Route: PO
  - Status: GIVEN (green status indicator)
  - Given By: THOMPSON,PATRICIA A

---

## Scenario 2: PRN Medication with Reason and Effectiveness Follow-Up

### Steps

1. On the **MAR** TabItem, locate **Morphine Sulfate** (4mg IV PRN Q4H).
2. Click the **Administer** button on the Morphine row (or right-click and select **Administer**).
3. In the dialog window, fill in:
   - Action Status: `GIVEN`
   - Administration Time: (leave as current date/time)
   - Administered By: `THOMPSON,PATRICIA A`
   - Injection Site: `Right Hand IV`
   - Comments: `PRN for post-operative pain, patient rates pain 7/10`
4. Click **Confirm**.
5. The administration is recorded. Note the BCMA ID from the History TabItem (click **History** TabItem, find the newest Morphine entry).
6. **PRN Reason:** The PRN reason must be recorded via the API since the WPF UI records it in the Comments field. For a full test, use the API endpoint:
   - `POST /api/bcma/4/history/{bcmaId}/prn-reason` with body: `{ "reason": "Post-operative pain, NRS 7/10" }`
7. **Effectiveness Follow-Up (30 minutes later):** Use the API endpoint:
   - `POST /api/bcma/4/history/{bcmaId}/effectiveness` with body: `{ "effectiveness": "Pain reduced from 7/10 to 3/10 within 30 minutes. Patient resting comfortably." }`

### Expected Result

- The GIVEN administration appears in History with:
  - Drug: Morphine Sulfate
  - Status: GIVEN (green status indicator)
  - Given By: THOMPSON,PATRICIA A
- The MAR row for Morphine shows updated Last Given date/time and count incremented.
- If PRN reason/effectiveness are recorded via API, the detail record (GET `/api/bcma/4/history/{bcmaId}`) includes:
  - PrnReason: "Post-operative pain, NRS 7/10"
  - PrnEffectiveness: "Pain reduced from 7/10 to 3/10 within 30 minutes. Patient resting comfortably."

---

## Scenario 3: Witness Required for Controlled Substance

### Steps

1. Administer Morphine Sulfate as in Scenario 2 (Action Status: GIVEN).
2. After the administration is recorded, note the BCMA ID from History.
3. **Record Witness** via the API endpoint:
   - `POST /api/bcma/4/history/{bcmaId}/witness`
   - Body: `{ "witnessId": "NURSE1", "witnessName": "JOHNSON,MARY R" }`
4. Verify the witness was recorded by checking the detail:
   - `GET /api/bcma/4/history/{bcmaId}`

### Expected Result

- The detail record now includes:
  - WitnessId: `NURSE1`
  - WitnessName: `JOHNSON,MARY R`
- The witness fields confirm dual verification of the controlled substance administration.

---

## Scenario 4: Medication NOT GIVEN -- Patient Refused

### Steps

1. On the **MAR** TabItem, locate **Vancomycin** (1g IV Q12H STAT).
2. Click the **Administer** button (or right-click and select **Administer**).
3. In the dialog window, fill in:
   - Action Status: `REFUSED`
   - Administration Time: (leave as current date/time)
   - Administered By: `THOMPSON,PATRICIA A`
   - Injection Site: (leave blank)
   - Reason: `Patient refused IV access -- states IV site is painful. MD notified.`
   - Comments: `Will attempt again after IV site change per MD order`
4. Click **Confirm**.

### Expected Result

- A green notification appears in the status bar: "Administration recorded."
- The MAR reloads. Vancomycin row Last Given shows the current time with "REFUSED by THOMPSON,PATRICIA A."
- On the **History** TabItem, the newest entry shows:
  - Status: REFUSED (red status indicator)
  - Drug: Vancomycin
  - Given By: THOMPSON,PATRICIA A

---

## Scenario 5: Medication HELD

### Steps

1. On the **MAR** TabItem, locate **Metoprolol Tartrate** (25mg PO BID).
2. Click the **Administer** button (or right-click and select **Administer**).
3. In the dialog window, fill in:
   - Action Status: `HELD`
   - Administration Time: (leave as current date/time)
   - Administered By: `THOMPSON,PATRICIA A`
   - Reason: `HR 52 bpm -- below threshold of 60. Held per protocol, MD notified.`
   - Comments: `Will reassess vital signs in 1 hour`
4. Click **Confirm**.

### Expected Result

- A green notification appears in the status bar: "Administration recorded."
- The MAR row for Metoprolol shows Last Given with "HELD by THOMPSON,PATRICIA A."
- History TabItem entry shows Status: HELD (orange status indicator).

---

## Scenario 6: Record Standalone Administration (Off-Formulary / Outpatient)

### Steps

1. Click the **+ Record** TabItem.
2. Verify the heading reads "Record Standalone Administration" with hint text about medications not linked to an inpatient order.
3. Fill in:
   - Drug Name: `Ondansetron ODT 4mg`
   - Dosage: `4mg`
   - Route: `PO`
   - Action Status: `GIVEN`
   - Administration Time: (leave as current date/time)
   - Administered By: `THOMPSON,PATRICIA A`
   - Injection Site: (leave blank)
   - Prescription ID: (leave blank)
   - Comments: `PRN for nausea. Patient placed tablet on tongue, dissolved in < 1 minute.`
4. Click **Record**.

### Expected Result

- A green notification appears in the status bar: "Standalone administration recorded."
- The form fields are cleared (reset to defaults).
- Switch to **History** TabItem. The newest entry shows:
  - Drug: Ondansetron ODT 4mg
  - Dosage: 4mg
  - Route: PO
  - Status: GIVEN (green status indicator)
  - Given By: THOMPSON,PATRICIA A

---

## Scenario 7: Verify Drug Name Required for Standalone

### Steps

1. Click the **+ Record** TabItem.
2. Leave the Drug Name TextBox blank.
3. Fill in other fields (Dosage: `500mg`, Route: `PO`).
4. Click **Record**.

### Expected Result

- A red error notification appears in the status bar: "Drug name is required."
- No record is created.

---

## Scenario 8: Verify MAR Badge Counts

### Steps

1. On the **MAR** TabItem, observe the TabItem header.
2. Count the number of rows with a **DUE** status indicator (red status indicator in the Status column).

### Expected Result

- The MAR TabItem header shows a due count (e.g., "MAR 3 due") matching the count of rows that display the red DUE status indicator.
- Active medications show either DUE (red) or ON SCHEDULE (green).
- Inactive medications show INACTIVE (gray).
