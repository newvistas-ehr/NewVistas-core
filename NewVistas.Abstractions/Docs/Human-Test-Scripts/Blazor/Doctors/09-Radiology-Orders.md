# Radiology Orders and Interpretation -- Physician Human Test Script

## Prerequisites
- Login: DOCTOR3 / Password: smythVista1
- Patient: 22
- Pre-conditions: Demo data loaded. SiloHost, WebServer, and BlazorWeb running.

---

## Scenario 1: Order a Chest X-Ray and Complete with Impression (Happy Path)

### Part A: Order the Study

#### Steps
1. Log in as **DOCTOR3** (PATEL,ARUN K / Cardiology)
2. Navigate to `/radiology`
3. Enter Patient ID: `22`
4. Click **Load** (or press Enter)
5. Note any existing radiology studies in the Studies tab
6. Click the **+ Order Study** button (green)
7. The "Order Radiology Study" form appears with a 2-column grid
8. Fill in:
   - Procedure Name *: `Chest X-Ray PA and Lateral`
   - CPT Code: `71046`
   - Imaging Type: **GENERAL RADIOLOGY** (dropdown; options: GENERAL RADIOLOGY, CT SCAN, MRI, ULTRASOUND, NUCLEAR MEDICINE, MAMMOGRAPHY)
   - Requesting Provider: `PATEL,ARUN K`
   - Urgency: **ROUTINE** (dropdown; options: ROUTINE, URGENT, STAT)
   - Location: `RADIOLOGY DEPT`
   - Clinical History: `65yo male with new onset exertional dyspnea and bilateral lower extremity edema. History of HTN and diastolic dysfunction.`
   - Reason for Study: `Evaluate for cardiomegaly, pleural effusion, and pulmonary congestion`
9. Click **Order Study**

#### Expected Result
- Green success: "Study ordered."
- The form closes
- Studies list reloads
- New study appears in the table with:
  - Date: today (Exam Date)
  - Procedure: Chest X-Ray PA and Lateral
  - Type: GENERAL RADIOLOGY
  - Requesting: PATEL,ARUN K
  - Status: badge "PENDING" (blue)
  - Report: "--"
  - Action button: **Complete**

### Part B: Complete the Study with Report

#### Steps
1. Click the **Complete** button on the Chest X-Ray row

#### Expected Result
- Green success: "Study completed."
- Status changes to "COMPLETE" (green badge)
- Report column: "Yes" (the default report text "Study reviewed." is set)
- Complete button disappears

### Part C: View Study Detail

#### Steps
1. Click the study row to open the **Study Detail** tab
2. Review all detail fields

#### Expected Result
- Detail panel shows:
  - Procedure name header with COMPLETE badge
  - Radiology ID, Imaging Type: GENERAL RADIOLOGY
  - Status: COMPLETE, Urgency: ROUTINE
  - Requesting Provider: PATEL,ARUN K
  - Order Date, Exam Date
  - Clinical History text
  - Reason text
  - Report: "Study reviewed." (default from Complete)
  - Impression: "No acute findings." (default from Complete)

---

## Scenario 2: Order CT with Contrast

### Part A: Order the CT

#### Steps
1. Click **+ Order Study**
2. Fill in:
   - Procedure Name *: `CT Chest with Contrast`
   - CPT Code: `71260`
   - Imaging Type: **CT SCAN**
   - Requesting Provider: `PATEL,ARUN K`
   - Urgency: **ROUTINE**
   - Location: `CT SUITE`
   - Clinical History: `65yo male with exertional dyspnea. CXR showed cardiomegaly. Evaluate for pulmonary embolism and mediastinal pathology.`
   - Reason for Study: `R/O pulmonary embolism, evaluate mediastinal structures`
3. Click **Order Study**

### Part B: Record Contrast Administration

#### Steps
1. Complete the study (click Complete button on the row)
2. Click the study row to view detail
3. Scroll to the **Contrast** section
4. Since no contrast has been recorded, the entry form is visible
5. Fill in:
   - Contrast Agent *: `Omnipaque 300`
   - Route: **IV** (dropdown; options: IV, ORAL, RECTAL, INTRATHECAL)
   - Volume (mL): `100`
6. Click **Record Contrast**

#### Expected Result
- Green success: "Contrast recorded."
- The form is replaced by a read-only display:
  - Agent: Omnipaque 300
  - Route: IV
  - Volume: 100.0 mL
- A new form appears for recording a contrast reaction (if one occurred)

---

## Scenario 3: STAT Radiology Order for Acute Condition

### Steps
1. Click **+ Order Study**
2. Fill in:
   - Procedure Name *: `CT Head without Contrast`
   - CPT Code: `70450`
   - Imaging Type: **CT SCAN**
   - Requesting Provider: `PATEL,ARUN K`
   - Urgency: **STAT**
   - Location: `EMERGENCY CT`
   - Clinical History: `Patient presenting with sudden onset severe headache, worst of life, with neck stiffness. GCS 14.`
   - Reason for Study: `R/O subarachnoid hemorrhage, intracranial hemorrhage`
3. Click **Order Study**

### Expected Result
- Study ordered with Urgency: STAT
- Appears in the studies list with STAT urgency indicator

---

## Scenario 4: Different Imaging Types

### Steps
1. Order studies with each imaging type:

   **Study A -- MRI:**
   - Procedure: `MRI Brain with and without Contrast`
   - CPT Code: `70553`
   - Imaging Type: **MRI**
   - Reason: `Evaluate for intracranial mass`

   **Study B -- Ultrasound:**
   - Procedure: `Echocardiogram Transthoracic`
   - CPT Code: `93306`
   - Imaging Type: **ULTRASOUND**
   - Reason: `Evaluate LV function, valvular disease`

   **Study C -- Nuclear Medicine:**
   - Procedure: `Myocardial Perfusion Imaging`
   - CPT Code: `78452`
   - Imaging Type: **NUCLEAR MEDICINE**
   - Reason: `Evaluate for coronary artery disease`

### Expected Result
- Each study appears with the correct Imaging Type in the studies list
- Detail views show the appropriate type

---

## Scenario 5: Record Radiation Dose

### Steps
1. View a completed CT study detail
2. Scroll to the **Radiation Dose** section
3. Since no dose has been recorded, the entry form is visible
4. Fill in:
   - Dose (mSv): `7.50`
   - CTDIvol (mGy): `15.20`
   - DLP (mGy*cm): `520.00`
5. Click **Record Radiation Dose**

### Expected Result
- Green success: "Radiation dose recorded."
- Form replaced by read-only display:
  - Dose (mSv): 7.50
  - CTDIvol (mGy): 15.20
  - DLP (mGy*cm): 520.00

---

## Scenario 6: Flag and Manage Critical Result

### Steps
1. View a completed study detail
2. Scroll to the **Critical Results** section
3. Click **Flag as Critical Result**

### Expected Result
- Green success: "Flagged as critical."
- Detail refreshes showing:
  - Red "CRITICAL" badge in the header
  - Critical section shows: "YES" badge
  - A notification form appears with "Notified To" field

### Steps (continued -- Record Notification)
4. Fill in:
   - Notified To *: `PATEL,ARUN K`
5. Click **Record Notification**

### Expected Result
- Green success: "Notification recorded."
- Shows: Notified To: PATEL,ARUN K, Notified At: [timestamp]
- An acknowledgment form appears

### Steps (continued -- Acknowledge)
6. Fill in:
   - Acknowledged By *: `PATEL,ARUN K`
7. Click **Acknowledge**

### Expected Result
- Green success: "Critical result acknowledged."
- Shows: Acknowledged By: PATEL,ARUN K

---

## Scenario 7: Sign and Amend a Radiology Report

### Part A: Sign the Report

#### Steps
1. View a completed study that has a report but has not been signed
2. Scroll to the **Report Actions** section
3. Fill in:
   - Radiologist Name *: `PATEL,ARUN K`
   - Radiologist ID: `DOCTOR3`
4. Click **Sign Report**

#### Expected Result
- Green success: "Report signed."
- Detail refreshes showing:
  - Signed By: PATEL,ARUN K
  - Signed At: [timestamp]
  - Report Status (if set)

### Part B: Amend the Report

#### Steps
1. In the Report Actions section, the Amendment form is always visible
2. Fill in:
   - Amendment Text:
     ```
     AMENDMENT: Upon further review, there is a small left-sided pleural
     effusion measuring approximately 1.5cm on the lateral view, not
     initially described. Clinical correlation recommended. This does not
     change the overall impression of cardiomegaly.
     ```
3. Click **Amend Report**

#### Expected Result
- Green success: "Report amended."
- Detail refreshes showing the Amendment text and Amendment date
- The amendment text field clears

---

## Scenario 8: Record Contrast Reaction

### Steps
1. View a study detail that has contrast recorded (from Scenario 2)
2. In the Contrast section, the reaction form is visible
3. Fill in:
   - Reaction Details: `Mild urticarial reaction (hives) noted 10 minutes post-injection. Treated with Diphenhydramine 50mg IV. Resolved within 30 minutes. Patient monitored for additional 2 hours.`
4. Click **Record Reaction**

### Expected Result
- Green success: "Contrast reaction recorded."
- Detail refreshes showing:
  - Reaction: "YES" red badge
  - Reaction details text

---

## Scenario 9: Validation -- Missing Required Fields

### Steps
1. Click **+ Order Study**
2. Leave Procedure Name empty
3. Click **Order Study**

### Expected Result
- Red error: "Procedure name is required."
