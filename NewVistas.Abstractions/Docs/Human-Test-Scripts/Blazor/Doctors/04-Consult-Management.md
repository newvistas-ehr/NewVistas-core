# Consult Management -- Physician Human Test Script

## Prerequisites
- Login: DOCTOR1 / Password: smythVista1 (requesting physician)
- Login: DOCTOR3 / Password: smythVista1 (consulting cardiologist)
- Patient: 16
- Pre-conditions: Demo data loaded. SiloHost, WebServer, and BlazorWeb running.

---

## Scenario 1: Request and Complete a Cardiology Consult (Happy Path)

### Part A: Request the Consult (DOCTOR1)

#### Steps
1. Log in as **DOCTOR1** (SMITH,JOHN A / Internal Medicine)
2. Navigate to `/consults`
3. Enter Patient ID: `16`
4. Click **Load Consults** (or press Enter)
5. Click the **+ New Consult** button (green)
6. The "Request Consult" form appears
7. Fill in:
   - To Service *: `Cardiology`
   - From Service: `Internal Medicine`
   - Urgency: **ROUTINE** (dropdown; options: ROUTINE, URGENT, STAT)
   - Requesting Provider: `SMITH,JOHN A`
   - Attention: `PATEL,ARUN K`
   - Provisional Diagnosis: `Chest pain, unspecified (R07.9)`
   - Reason for Request:
     ```
     58yo male with new onset exertional chest pressure. Stress test
     equivocal. Request cardiology evaluation for possible cardiac
     catheterization. PMH: HTN, DM2, dyslipidemia. Current meds include
     Lisinopril 20mg, Metformin 1000mg BID, Atorvastatin 40mg.
     ```
8. Click **Submit Request**

#### Expected Result
- Green success: "Consult requested successfully."
- The form closes
- The consults list reloads
- The new consult appears in the table with:
  - Date: today
  - To Service: Cardiology
  - From: Internal Medicine
  - Urgency: badge "ROUTINE" (blue)
  - Status: badge "PENDING" (yellow)
  - Requesting: SMITH,JOHN A
  - Diagnosis: Chest pain, unspecified (R07.9)

### Part B: Accept the Consult (DOCTOR3)

#### Steps
1. Log out and log in as **DOCTOR3** (PATEL,ARUN K / Cardiology)
2. Navigate to `/consults`
3. Enter Patient ID: `16`
4. Click **Load Consults**
5. Click the PENDING Cardiology consult row to open the detail view
6. The detail shows: ROUTINE badge, PENDING badge, "Cardiology" header
7. Verify the detail fields:
   - From: Internal Medicine
   - Requested: [today's date]
   - Requesting: SMITH,JOHN A
   - Attention: PATEL,ARUN K
   - Dx: Chest pain, unspecified (R07.9)
   - Reason text visible in a pre-formatted block
8. Click the **Accept** button (green)
9. The "Accept Consult with Details" form appears
10. Fill in:
    - Accepted By ID: `DOCTOR3`
    - Accepted By Name *: `PATEL,ARUN K`
11. Click **Accept**

#### Expected Result
- Green success: "Consult accepted."
- The Accept form closes
- Consult status changes to **ACTIVE** (blue badge)
- New buttons appear: **Schedule**, **Complete**
- Detail shows: "Accepted: [date] by PATEL,ARUN K"

### Part C: Schedule the Consult

#### Steps
1. Click the **Schedule** button (blue)
2. The "Schedule Consult with Details" form appears
3. Fill in:
   - Scheduled Date/Time *: Pick a date 5 days from today, 14:00
   - Clinic ID: `CARDIOLOGY-CLINIC`
   - Clinic Name: `Cardiology Clinic`
4. Click **Schedule**

#### Expected Result
- Green success: "Consult scheduled."
- Status changes to **SCHEDULED** (purple badge)
- Detail shows: "Scheduled: [date] Clinic: Cardiology Clinic"
- The **Complete** button remains visible

### Part D: Complete the Consult with Result Note

#### Steps
1. Click the **Complete** button (purple)
2. The "Complete Consult" form appears
3. Fill in:
   - Author: `PATEL,ARUN K`
   - Result Note:
     ```
     CARDIOLOGY CONSULT NOTE

     REASON FOR CONSULT: Evaluation of exertional chest pain

     HISTORY: 58yo male with HTN, DM2, dyslipidemia presenting with
     3-week history of exertional substernal chest pressure. Equivocal
     stress test per PCP.

     EXAMINATION:
     BP: 138/82  HR: 74  RR: 16
     Cardiac: RRR, S1/S2 normal, no murmurs or gallops
     Lungs: Clear bilaterally
     Peripheral pulses: 2+ bilateral

     EKG: NSR, no ST changes, normal axis

     ASSESSMENT:
     Atypical chest pain with equivocal stress test in setting of
     multiple cardiac risk factors. Intermediate pre-test probability
     for obstructive CAD.

     RECOMMENDATIONS:
     1. Proceed with coronary CT angiography
     2. If CTA positive, cardiac catheterization
     3. Optimize medical therapy: increase Atorvastatin to 80mg
     4. Start Aspirin 81mg daily
     5. Follow-up in Cardiology Clinic in 2 weeks

     PATEL,ARUN K, MD
     Cardiology
     ```
4. Click **Complete Consult**

#### Expected Result
- Green success: "Consult completed."
- The detail view closes
- Consult status changes to **COMPLETE** (green badge)
- In the consults list, the Diagnosis column shows "[Note]" indicator

---

## Scenario 2: Cancel a Pending Consult

### Steps
1. As DOCTOR1, navigate to `/consults`, load patient 16
2. Click **+ New Consult**
3. Fill in:
   - To Service *: `Orthopedics`
   - Urgency: **ROUTINE**
   - Requesting Provider: `SMITH,JOHN A`
   - Provisional Diagnosis: `Knee pain (M25.561)`
   - Reason for Request: `Right knee pain x 2 months, conservative treatment failed`
4. Click **Submit Request**
5. Click the new PENDING Orthopedics consult row
6. In the detail view, click the **Cancel** button (red)

### Expected Result
- Green success: "Consult cancelled."
- The detail view closes
- Consult status changes to **CANCELLED** (red badge)
- No more action buttons for this consult

---

## Scenario 3: STAT Consult for Acute Condition

### Steps
1. Click **+ New Consult**
2. Fill in:
   - To Service *: `Pulmonology`
   - From Service: `Internal Medicine`
   - Urgency: **STAT**
   - Requesting Provider: `SMITH,JOHN A`
   - Provisional Diagnosis: `Acute pulmonary embolism (I26.99)`
   - Reason for Request:
     ```
     STAT consult needed. Patient with acute onset dyspnea, tachycardia,
     and pleuritic chest pain. CT angiogram shows bilateral pulmonary
     emboli. Patient on heparin drip. D-dimer > 5000. Request urgent
     pulmonology evaluation for possible catheter-directed thrombolysis.
     ```
3. Click **Submit Request**

### Expected Result
- Consult created with Urgency: **STAT** (red badge)
- Status: PENDING

---

## Scenario 4: Add Tracking Comments During Consult Lifecycle

### Steps
1. Click an ACTIVE or SCHEDULED consult to view its detail
2. Scroll to the **Tracking Comments** section
3. Fill in:
   - Author ID: `DOCTOR1`
   - Author Name *: `SMITH,JOHN A`
   - Action Taken: Select **UPDATED** (dropdown options: FORWARDED, UPDATED, ACCEPTED, SCHEDULED, REVIEWED)
   - Comment *: `Spoke with patient. Will coordinate with cardiology for appointment next week.`
4. Click **Add Comment**

### Expected Result
- Green success: "Tracking comment added."
- The comment text field clears
- The tracking comments list updates showing:
  - Date/time
  - Author name (bold)
  - Action badge: "UPDATED" (blue)
  - Comment text below

### Steps (continued -- add another comment)
5. Fill in:
   - Author Name *: `PATEL,ARUN K`
   - Action Taken: **REVIEWED**
   - Comment *: `Chart reviewed. Will see patient during scheduled appointment.`
6. Click **Add Comment**

### Expected Result
- Tracking Comments count increments (e.g., "Tracking Comments (2)")
- Both comments displayed chronologically

---

## Scenario 5: Set Consult Type and Clinical Details

### Steps
1. View an active consult detail
2. Under **Consult Type** section:
   - Select: **CONSULT** (options: CONSULT, PROCEDURE, INTERFACILITY, COMMUNITY_CARE)
   - Click **Set Type**
3. Under **Clinical History** section:
   - Enter: `Patient is a 58yo male with HTN x 10 years, DM2 x 5 years, dyslipidemia. Family history: father MI at age 52. Current smoker.`
   - Click **Set Clinical History**
4. Under **Follow-Up Recommendation** section:
   - Enter: `Follow up with cardiology in 2 weeks. Repeat stress test in 6 months if symptoms persist.`
   - Click **Set Follow-Up**
5. Under **Consulting Provider** section:
   - Provider ID: `DOCTOR3`
   - Provider Name: `PATEL,ARUN K`
   - Click **Set Provider**

### Expected Result
- Each action shows a green success message
- The detail refreshes after each save showing updated values
- Consult Type badge appears in the detail header
- Clinical History and Follow-Up text displayed in their sections

---

## Scenario 6: Interfacility Consult

### Steps
1. Create a new consult (any service, ROUTINE)
2. After it is created, click it to view detail
3. Scroll to the **Interfacility** section
4. Fill in:
   - External Facility ID: `EXT-VA-RICHMOND`
   - External Facility Name: `VA Richmond Medical Center`
5. Click **Mark Interfacility**

### Expected Result
- Green success: "Marked as interfacility."
- Detail refreshes showing:
  - An orange "INTERFACILITY" badge
  - "External: VA Richmond Medical Center"

---

## Scenario 7: Validation -- Missing Required Fields

### Steps
1. Click **+ New Consult**
2. Leave "To Service" empty
3. Click **Submit Request**

### Expected Result
- Red error: "To Service is required."
- The consult is not created

### Steps (continued)
4. For the Accept form, leave "Accepted By Name" empty and click Accept

### Expected Result
- Red error: "Name is required."
