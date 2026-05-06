# Cover Sheet Review -- Physician Human Test Script

## Prerequisites
- Login: DOCTOR1 / Password: smythVista1
- Patient: 4
- Pre-conditions: Demo data loaded (patients 1-50 from Fifty dataset). Ensure the SiloHost, WebServer, and BlazorWeb are all running.

---

## Scenario 1: Load Cover Sheet and Review All Sections (Happy Path)

### Steps
1. Log in as **DOCTOR1** (SMITH,JOHN A / Internal Medicine)
2. Navigate to `/cover-sheet`
3. In the **Patient ID** field, type: `4`
4. Click the **Load** button (or press Enter)

### Expected Result
- The page title shows "Cover Sheet"
- A **Patient Banner** appears at the top with:
  - Patient name (bold, navy)
  - Sex (M or F)
  - Age (if DOB is set)
  - If patient is admitted: gold badge showing "Admitted -- [Room/Bed]"
  - If service-connected: green badge showing "SC [percent]%"
  - If CWAD flags exist: red badge with CWAD letters (e.g., "C W A D")
- Below the banner, a **Workflows** action bar with four buttons:
  - "New Order" -- links to `/orders?patientId=4&action=new`
  - "New Medication" -- links to `/orders?patientId=4&action=new&type=Pharmacy`
  - "New Lab Order" -- links to `/labs?patientId=4`
  - "New Consult" -- links to `/consults?patientId=4`
- A grid of 8 panels, each in a bordered card:
  1. **Active Problems** -- table with columns: Code, Diagnosis, Onset Date
  2. **Allergies** -- table with columns: Allergen, Severity, Reactions
  3. **Active Medications** -- table with columns: DrugName, Sig, Status
  4. **Clinical Reminders** -- table with columns: ReminderName, Status, DueDate
  5. **Recent Labs** -- table with columns: TestName, ResultValue+Units, Flag, CollectionDate (flagged values appear in red)
  6. **Recent Vitals** -- table with columns: VitalType, Value+Units, DateTimeTaken (abnormal flagged in red)
  7. **Appointments** -- table with columns: ClinicName, AppointmentDateTime, Status; includes a "+ Follow-Up" button
  8. **Active Orders** -- table with columns: OrderText, Status, StartDate
- Panels with no data show italic "No active problems", "No Known Allergies", etc.

---

## Scenario 2: Patient with CWAD Flags

### Steps
1. Remain on `/cover-sheet`
2. Before this test, ensure patient 4 has at least one of: Crisis note, Warning, documented Allergy, or Advance Directive
3. If patient 4 has no CWAD data, first:
   - Navigate to `/allergies?patientId=4`
   - Record an allergy (e.g., Penicillin, Severe) -- this sets the "A" in CWAD
   - Navigate to `/notes?patientId=4`
   - Create a new note with Document Type: **CRISIS NOTE** -- this sets the "C" in CWAD
   - Create a new note with Document Type: **ADVANCE DIRECTIVE** -- this sets the "D" in CWAD
4. Return to `/cover-sheet` and reload patient 4

### Expected Result
- The Patient Banner shows a red **CWAD badge** containing one or more letters:
  - **C** = Crisis note exists
  - **W** = Warning flag exists
  - **A** = Allergy documented
  - **D** = Advance Directive exists
- The badge has red background with red text and letter-spacing of 2px

---

## Scenario 3: Navigate from Cover Sheet to Specific Section

### Steps
1. On the cover sheet for patient 4, locate the **Active Problems** panel
2. Note the first problem listed (e.g., "Essential Hypertension" I10)
3. Click the **New Order** button in the Workflows action bar

### Expected Result
- Browser navigates to `/orders?patientId=4&action=new`
- The Orders page loads with patient 4 pre-filled
- The "New Order" tab (tab 1) is automatically selected

### Steps (continued)
4. Press the browser Back button to return to the cover sheet
5. Click the **New Lab Order** button

### Expected Result
- Browser navigates to `/labs?patientId=4`
- The Labs page loads with patient 4 in the Patient ID field

### Steps (continued)
6. Press the browser Back button to return to the cover sheet
7. Click the **New Consult** button

### Expected Result
- Browser navigates to `/consults?patientId=4`
- The Consults page loads with patient 4 in the Patient ID field

---

## Scenario 4: Book a Follow-Up Appointment from Cover Sheet

### Steps
1. On the cover sheet for patient 4, locate the **Appointments** panel
2. Click the **+ Follow-Up** button (top-right of the Appointments panel header)
3. The Follow-Up booking form expands inside the panel
4. Click **Load Clinics** button (if clinics are not already loaded)
5. Wait for the clinic dropdown to populate with ACTIVE clinics
6. Fill in the form:
   - Clinic: Select the first active clinic from the dropdown
   - Date / Time: Pick a date 7 days from today, 09:00
   - Duration: Select **30 min** from dropdown (options: 15, 20, 30, 45, 60)
   - Provider: `SMITH,JOHN A` (optional)
   - Purpose: `Follow-up hypertension management`
7. Click **Book**

### Expected Result
- A green success message appears: "Follow-up booked at [ClinicName] on [date]."
- The Follow-Up form closes
- The cover sheet reloads automatically
- The new appointment appears in the Appointments panel with status "Scheduled"

---

## Scenario 5: Follow-Up Booking Conflict

### Steps
1. On the cover sheet for patient 4, click **+ Follow-Up** again
2. Enter the exact same clinic and date/time as Scenario 4
3. Click **Book**

### Expected Result
- A red error message appears: "Conflict: [message]. Use the Scheduling page to override."
- The booking form remains open so the user can correct the date/time

---

## Scenario 6: Empty Patient (No Data)

### Steps
1. Navigate to `/cover-sheet`
2. Enter a patient ID that has no data loaded: `99`
3. Click **Load**

### Expected Result
- The Patient Banner appears (may show minimal info if patient record is sparse)
- All panels show their empty-state messages:
  - "No active problems"
  - "No Known Allergies"
  - "No active medications"
  - "No reminders due"
  - "No recent labs"
  - "No recent vitals"
  - "No appointments"
  - "No active orders"

---

## Scenario 7: Invalid Patient ID

### Steps
1. Navigate to `/cover-sheet`
2. Leave the Patient ID field empty
3. Observe the Load button

### Expected Result
- The **Load** button is disabled (grayed out) when the field is empty
- No error appears; the button simply cannot be clicked

### Steps (continued)
4. Type a single space in the field

### Expected Result
- The Load button remains disabled (whitespace-only is treated as empty)
