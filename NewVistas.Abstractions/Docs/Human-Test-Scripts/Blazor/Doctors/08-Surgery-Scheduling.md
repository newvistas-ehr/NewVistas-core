# Surgery Scheduling and Management -- Physician Human Test Script

## Prerequisites
- Login: SURGEON1 / Password: smythVista1
- Patient: 16
- Pre-conditions: Demo data loaded. SiloHost, WebServer, and BlazorWeb running.

---

## Scenario 1: Schedule a Cholecystectomy and Complete with Operative Report (Happy Path)

### Part A: Schedule the Surgery

#### Steps
1. Log in as **SURGEON1** (MARTINEZ,CARLOS R / General Surgery)
2. Navigate to `/surgery`
3. Enter Patient ID: `16`
4. Click **Load** (or press Enter)
5. Note any existing surgeries in the Surgeries tab
6. Click the **+ Schedule Surgery** button (green)
7. The "Schedule Surgery" form appears with a 2-column grid
8. Fill in:
   - Principal Procedure *: `Laparoscopic Cholecystectomy`
   - CPT Code: `47562`
   - Date of Operation *: Pick a date 7 days from today, 07:30 AM
   - Surgeon Name: `MARTINEZ,CARLOS R`
   - Anesthesia: **GENERAL** (dropdown; options: GENERAL, SPINAL, REGIONAL, LOCAL, MAC)
   - Specialty: `General Surgery`
   - Pre-Op Diagnosis: `Acute cholecystitis (K81.0)`
   - Location: `OR SUITE 3`
   - Comments: `Patient NPO after midnight. Consent obtained. Preop labs and EKG reviewed.`
9. Click **Schedule**

#### Expected Result
- Green success: "Surgery scheduled."
- The form closes
- The surgeries list reloads
- The new surgery appears in the table with:
  - Date: the scheduled date
  - Procedure: Laparoscopic Cholecystectomy
  - CPT: 47562
  - Surgeon: MARTINEZ,CARLOS R
  - Specialty: General Surgery
  - Status: badge "SCHEDULED" (blue)
  - Action buttons: **Complete**, **Cancel**

### Part B: Record Pre-Op Assessment

#### Steps
1. Click on the surgery row to view the **Surgery Detail** tab
2. Detail shows: surgery ID, date, surgeon, specialty, CPT, anesthesia (GENERAL), pre-op dx, location, comments
3. Scroll to the **Pre-Op Assessment** section
4. Since no pre-op assessment exists yet, the assessment form is visible
5. Fill in:
   - ASA Classification (1-6) *: `2` (mild systemic disease)
   - Provider Name: `MARTINEZ,CARLOS R`
   - Provider ID: `SURGEON1`
   - Assessment Notes: `Patient is a 45yo male with well-controlled HTN and mild obesity. No prior anesthesia complications. Mallampati class II. No anticoagulants. Labs within normal limits. EKG: NSR. Cleared for general anesthesia.`
6. Click **Record Pre-Op Assessment**

#### Expected Result
- Green success: "Pre-op assessment recorded."
- The form is replaced by a read-only display showing:
  - ASA Class: 2
  - Date: today
  - Provider: MARTINEZ,CARLOS R
  - Notes: the assessment text

### Part C: Complete the Surgery

#### Steps
1. Return to the Surgeries list tab
2. Click the **Complete** button on the scheduled surgery row

#### Expected Result
- Green success: "Surgery completed."
- Status changes to "COMPLETED" (green badge)
- Complete and Cancel buttons disappear

### Part D: Record Intra-Op Details

#### Steps
1. Click the surgery row to return to detail
2. Scroll to the **Intra-Op Details** section
3. Fill in:
   - Estimated Blood Loss (mL): `50`
   - Sponge Count: **Correct** (dropdown; options: Correct, Incorrect)
   - Needle Count: **Correct**
   - Instrument Count: **Correct**
   - Disposition After Surgery: **PACU** (dropdown; options: PACU, ICU, WARD, HOME, MORGUE)
4. Click **Record Intra-Op Details**

#### Expected Result
- Green success: "Intra-op details recorded."
- Form replaced by read-only display:
  - EBL: 50 mL
  - Sponge Count: Correct
  - Needle Count: Correct
  - Instrument Count: Correct
  - Disposition: PACU

### Part E: Add Specimens

#### Steps
1. Scroll to the **Specimens** section
2. Fill in:
   - Specimen Type *: `TISSUE`
   - Body Site: `GALLBLADDER`
   - Accession Number: `SP-2026-0042`
3. Click **Add Specimen**

#### Expected Result
- Green success: "Specimen recorded."
- Specimen appears in the table: Type: TISSUE, Body Site: GALLBLADDER, Accession #: SP-2026-0042

---

## Scenario 2: Cancel a Scheduled Surgery

### Steps
1. Schedule a new surgery:
   - Procedure: `Right Inguinal Hernia Repair`
   - CPT Code: `49505`
   - Date: 14 days from today
   - Surgeon: `MARTINEZ,CARLOS R`
   - Anesthesia: **GENERAL**
   - Specialty: `General Surgery`
   - Pre-Op Diagnosis: `Right inguinal hernia (K40.90)`
2. Click **Schedule**
3. In the surgeries list, click the **Cancel** button on the new surgery

### Expected Result
- Green success: "Surgery cancelled."
- Status changes to "CANCELLED" (red badge)
- No more action buttons for this surgery

---

## Scenario 3: Different Anesthesia Types

### Steps
1. Schedule surgeries with each anesthesia type to verify they are accepted:

   **Surgery A -- Spinal Anesthesia:**
   - Procedure: `Total Hip Replacement`
   - Anesthesia: **SPINAL**
   - Specialty: `Orthopedics`

   **Surgery B -- Regional Anesthesia:**
   - Procedure: `Carpal Tunnel Release`
   - Anesthesia: **REGIONAL**
   - Specialty: `Hand Surgery`

   **Surgery C -- Local Anesthesia:**
   - Procedure: `Excision of Lipoma`
   - Anesthesia: **LOCAL**
   - Specialty: `General Surgery`

   **Surgery D -- MAC (Monitored Anesthesia Care):**
   - Procedure: `Upper Endoscopy`
   - Anesthesia: **MAC**
   - Specialty: `Gastroenterology`

### Expected Result
- Each surgery is created with the specified anesthesia type
- Detail view shows the correct anesthesia for each

---

## Scenario 4: Add Pre-Op and Post-Op Diagnosis

### Steps
1. Schedule a surgery with Pre-Op Diagnosis: `Acute appendicitis (K35.80)`
2. Complete the surgery
3. View the detail -- Pre-Op Dx shows: "Acute appendicitis (K35.80)"
4. Post-Op Dx field shows "--" (empty by default after completion)
5. Note: Post-op diagnosis is set via the `CompleteSurgeryAsync` workflow method. The current Complete button uses a default report text. A future UI enhancement would allow entering post-op diagnosis.

### Expected Result
- Pre-Op Diagnosis visible in the detail grid
- Post-Op Diagnosis shows "--" unless set through the API directly

---

## Scenario 5: Record Surgical Complications

### Steps
1. View a completed surgery detail
2. Scroll to the **Complications** section
3. Fill in:
   - Complication Code *: `WOUND_INFECTION`
   - Description *: `Superficial surgical site infection`
   - Severity: **MINOR** (dropdown; options: MINOR, MAJOR, DEATH)
   - Occurrence Date: 3 days after surgery date
   - Treatment Action: `Started oral Cephalexin 500mg QID x 7 days. Wound care with daily dressing changes.`
4. Click **Add Complication**

### Expected Result
- Green success: "Complication recorded."
- Complication appears in the table with:
  - Code: WOUND_INFECTION
  - Description: Superficial surgical site infection
  - Severity: MINOR badge (blue)
  - Date
  - Treatment: the treatment text

---

## Scenario 6: Record Surgical Implants

### Steps
1. View a completed surgery detail (e.g., Total Hip Replacement)
2. Scroll to the **Implants** section
3. Fill in:
   - Device Name *: `DePuy Attune Total Knee System`
   - Manufacturer: `DePuy Synthes`
   - Serial Number: `ATN-2026-78432`
   - Lot Number: `LOT-DPS-44291`
   - Body Site: `LEFT KNEE`
4. Click **Add Implant**

### Expected Result
- Green success: "Implant recorded."
- Implant appears in the table: Device, Manufacturer, Serial #, Lot #, Body Site

---

## Scenario 7: Validation -- Missing Required Fields

### Steps
1. Click **+ Schedule Surgery**
2. Leave Principal Procedure empty
3. Click **Schedule**

### Expected Result
- Red error: "Procedure is required."
- Surgery is not created

### Steps (continued)
4. In the Complications form, leave Code and Description empty
5. Click **Add Complication**

### Expected Result
- Red error: "Code and description required."
