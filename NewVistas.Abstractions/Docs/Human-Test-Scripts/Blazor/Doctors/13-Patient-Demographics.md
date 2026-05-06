# Patient Demographics / Registration -- Physician Human Test Script

## Prerequisites
- Login: DOCTOR1 / Password: smythVista1
- Patient: 4
- Pre-conditions: Demo data loaded. SiloHost, WebServer, and BlazorWeb running.

---

## Scenario 1: View Patient Demographics on Cover Sheet

### Steps
1. Log in as **DOCTOR1** (SMITH,JOHN A / Internal Medicine)
2. Navigate to `/cover-sheet`
3. Enter Patient ID: `4`
4. Click **Load**

### Expected Result
- The **Patient Banner** displays:
  - Patient Name (bold, navy text)
  - Sex (M or F)
  - Age (if DOB set)
  - Admission status (gold badge if admitted, showing room/bed)
  - Service-Connected percentage (green badge if applicable)
  - CWAD flags (red badge if applicable)

---

## Scenario 2: View and Edit Patient Demographics

### Steps
1. Navigate to `/patient-edit`
2. Enter Patient ID: `4`
3. Click **Load Patient**

### Expected Result
- Patient banner appears with: Name, ID, Sex, DOB
- Five tabs visible:
  - **Demographics** (default active)
  - **Address**
  - **Contact**
  - **Emergency Contact**
  - **Veteran / Military**

### Steps (continued -- Review Demographics tab)
4. On the Demographics tab, verify the following fields are present and populated (or empty):
   - Name (LAST,FIRST MI) * -- text input
   - Sex * -- dropdown (Male/Female)
   - Date of Birth -- date picker
   - SSN -- text input
   - Marital Status -- dropdown (Single, Married, Divorced, Widowed, Separated)

---

## Scenario 3: Update Patient Address

### Steps
1. On the Patient Edit page with patient 4 loaded
2. Click the **Address** tab
3. Review current address fields:
   - Street Address 1 -- text input
   - Street Address 2 -- text input
   - Street Address 3 -- text input
   - City -- text input
   - State -- text input (e.g., "VA")
   - Zip Code -- text input
4. Update the fields:
   - Street Address 1: `123 Veterans Memorial Drive`
   - Street Address 2: `Apt 4B`
   - Street Address 3: (leave empty)
   - City: `Richmond`
   - State: `VA`
   - Zip Code: `23219`
5. Click **Save Address**

### Expected Result
- Green success message appears (e.g., "Address updated." or similar)
- The fields retain the new values
- Reload the patient (click Load Patient again) to confirm persistence

---

## Scenario 4: Update Emergency Contact

### Steps
1. Click the **Emergency Contact** tab
2. Review fields:
   - Contact Name -- text input
   - Relationship -- text input (e.g., "SPOUSE")
   - Phone -- telephone input
3. Update the fields:
   - Contact Name: `JOHNSON,MARY L`
   - Relationship: `SPOUSE`
   - Phone: `804-555-1234`
4. Click **Save Emergency Contact**

### Expected Result
- Green success message
- Fields retain new values
- Reload patient to confirm persistence

---

## Scenario 5: Update Veteran Information / Service-Connected Percentage

### Steps
1. Click the **Veteran / Military** tab
2. Review fields:
   - Veteran: dropdown (Yes/No)
   - SC %: number input (0-100)
   - Eligibility Code: text input
   - Primary Eligibility Code: text input
3. Update:
   - Veteran: **Yes**
   - SC %: `30`
   - Eligibility Code: `SC LESS 50%`
   - Primary Eligibility Code: `SC LESS 50%`
4. Click **Save Veteran Info**

### Expected Result
- Green success message
- SC % shows 30

### Steps (continued -- Military Service)
5. Scroll down to the **Military Service** section
6. Fill in:
   - Service Entry Date: `06/15/1990`
   - Service Separation Date: `06/14/2010`
   - Branch: **ARMY** (dropdown; options: ARMY, NAVY, AIR FORCE, MARINES, COAST GUARD, SPACE FORCE)
   - Discharge Type: **HONORABLE** (dropdown; options: HONORABLE, GENERAL, OTHER THAN HONORABLE, BAD CONDUCT, DISHONORABLE)
   - Prisoner of War: **No** (dropdown; options: Yes, No)
7. Click **Save Military Service**

### Expected Result
- Green success message
- Military service dates and branch saved
- Reload patient to confirm: go to Cover Sheet, and the SC badge should show "SC 30%"

---

## Scenario 6: Update Contact Information

### Steps
1. Click the **Contact** tab
2. Review fields:
   - Phone (Residence) -- tel input
   - Phone (Work) -- tel input
   - Email -- email input
3. Update:
   - Phone (Residence): `804-555-5678`
   - Phone (Work): `804-555-9999`
   - Email: `veteran.patient@example.com`
4. Click **Save Contact Info**

### Expected Result
- Green success message
- Contact info persisted

---

## Scenario 7: Update Demographics (Name, DOB, SSN)

### Steps
1. Click the **Demographics** tab
2. Note the current Name
3. Update:
   - Name: Keep existing or modify slightly (e.g., add middle initial)
   - Sex: Keep as is
   - Date of Birth: `04/15/1968`
   - SSN: `123-45-6789`
   - Marital Status: **MARRIED**
4. Click **Save Demographics**

### Expected Result
- Green success message
- Patient banner updates to reflect any name change
- Reload to confirm DOB and other fields persisted

---

## Scenario 8: View Demographics from Cover Sheet Workflow

### Steps
1. Navigate to `/cover-sheet`
2. Load patient 4
3. Verify the Patient Banner reflects the updates from Scenarios 3-7:
   - Name matches
   - Age calculated from updated DOB
   - SC badge shows "SC 30%"
4. This confirms the demographics edits made on `/patient-edit` are reflected on `/cover-sheet`

### Expected Result
- All demographic data consistent between Patient Edit page and Cover Sheet
