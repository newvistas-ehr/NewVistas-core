# Patient Demographics / Registration -- Physician Human Test Script -- WPF UI

## Prerequisites
- Login: DOCTOR1 / Password: smythVista1
- Patient: 4
- Pre-conditions: Demo data loaded. SiloHost, WebServer, and WPF Application (NewVistas.WpfClient) are running.

---

## Scenario 1: View Patient Demographics on Cover Sheet

### Steps
1. Log in as **DOCTOR1** (SMITH,JOHN A / Internal Medicine)
2. In the Navigation Panel, select **Cover Sheet**
3. Enter Patient ID in the toolbar: `4`
4. Click **Load**

### Expected Result
- The **Patient Banner** displays:
  - Patient Name (bold, navy text)
  - Sex (M or F)
  - Age (if DOB set)
  - Admission status (gold status indicator if admitted, showing room/bed)
  - Service-Connected percentage (green status indicator if applicable)
  - CWAD flags (red status indicator if applicable)

---

## Scenario 2: View and Edit Patient Demographics

### Steps
1. In the Navigation Panel, select **Patient Edit**
2. Enter Patient ID in the toolbar: `4`
3. Click **Load Patient**

### Expected Result
- Patient banner appears with: Name, ID, Sex, DOB
- Five TabItems visible:
  - **Demographics** (default active)
  - **Address**
  - **Contact**
  - **Emergency Contact**
  - **Veteran / Military**

### Steps (continued -- Review Demographics TabItem)
4. On the Demographics TabItem, verify the following fields are present and populated (or empty):
   - Name (LAST,FIRST MI) * -- TextBox
   - Sex * -- ComboBox (Male/Female)
   - Date of Birth -- DatePicker
   - SSN -- TextBox
   - Marital Status -- ComboBox (Single, Married, Divorced, Widowed, Separated)

---

## Scenario 3: Update Patient Address

### Steps
1. On the Patient Edit view with patient 4 loaded
2. Click the **Address** TabItem
3. Review current address fields:
   - Street Address 1 -- TextBox
   - Street Address 2 -- TextBox
   - Street Address 3 -- TextBox
   - City -- TextBox
   - State -- TextBox (e.g., "VA")
   - Zip Code -- TextBox
4. Update the fields:
   - Street Address 1: `123 Veterans Memorial Drive`
   - Street Address 2: `Apt 4B`
   - Street Address 3: (leave empty)
   - City: `Richmond`
   - State: `VA`
   - Zip Code: `23219`
5. Click **Save Address** (or press Ctrl+S)

### Expected Result
- A green notification appears in the status bar (e.g., "Address updated." or similar)
- The fields retain the new values
- Reload the patient (click Load Patient again) to confirm persistence

---

## Scenario 4: Update Emergency Contact

### Steps
1. Click the **Emergency Contact** TabItem
2. Review fields:
   - Contact Name -- TextBox
   - Relationship -- TextBox (e.g., "SPOUSE")
   - Phone -- TextBox (telephone format)
3. Update the fields:
   - Contact Name: `JOHNSON,MARY L`
   - Relationship: `SPOUSE`
   - Phone: `804-555-1234`
4. Click **Save Emergency Contact** (or press Ctrl+S)

### Expected Result
- A green notification appears in the status bar
- Fields retain new values
- Reload patient to confirm persistence

---

## Scenario 5: Update Veteran Information / Service-Connected Percentage

### Steps
1. Click the **Veteran / Military** TabItem
2. Review fields:
   - Veteran: ComboBox (Yes/No)
   - SC %: number TextBox (0-100)
   - Eligibility Code: TextBox
   - Primary Eligibility Code: TextBox
3. Update:
   - Veteran: **Yes**
   - SC %: `30`
   - Eligibility Code: `SC LESS 50%`
   - Primary Eligibility Code: `SC LESS 50%`
4. Click **Save Veteran Info**

### Expected Result
- A green notification appears in the status bar
- SC % shows 30

### Steps (continued -- Military Service)
5. Scroll down to the **Military Service** section
6. Fill in:
   - Service Entry Date: `06/15/1990` using the DatePicker
   - Service Separation Date: `06/14/2010` using the DatePicker
   - Branch: **ARMY** (ComboBox; options: ARMY, NAVY, AIR FORCE, MARINES, COAST GUARD, SPACE FORCE)
   - Discharge Type: **HONORABLE** (ComboBox; options: HONORABLE, GENERAL, OTHER THAN HONORABLE, BAD CONDUCT, DISHONORABLE)
   - Prisoner of War: **No** (ComboBox; options: Yes, No)
7. Click **Save Military Service**

### Expected Result
- A green notification appears in the status bar
- Military service dates and branch saved
- Reload patient to confirm: go to Cover Sheet (select **Cover Sheet** in the Navigation Panel), and the SC status indicator should show "SC 30%"

---

## Scenario 6: Update Contact Information

### Steps
1. Click the **Contact** TabItem
2. Review fields:
   - Phone (Residence) -- TextBox (telephone format)
   - Phone (Work) -- TextBox (telephone format)
   - Email -- TextBox (email format)
3. Update:
   - Phone (Residence): `804-555-5678`
   - Phone (Work): `804-555-9999`
   - Email: `veteran.patient@example.com`
4. Click **Save Contact Info** (or press Ctrl+S)

### Expected Result
- A green notification appears in the status bar
- Contact info persisted

---

## Scenario 7: Update Demographics (Name, DOB, SSN)

### Steps
1. Click the **Demographics** TabItem
2. Note the current Name
3. Update:
   - Name: Keep existing or modify slightly (e.g., add middle initial)
   - Sex: Keep as is
   - Date of Birth: `04/15/1968` using the DatePicker
   - SSN: `123-45-6789`
   - Marital Status: **MARRIED**
4. Click **Save Demographics** (or press Ctrl+S)

### Expected Result
- A green notification appears in the status bar
- Patient banner updates to reflect any name change
- Reload to confirm DOB and other fields persisted

---

## Scenario 8: View Demographics from Cover Sheet Workflow

### Steps
1. In the Navigation Panel, select **Cover Sheet**
2. Load patient 4
3. Verify the Patient Banner reflects the updates from Scenarios 3-7:
   - Name matches
   - Age calculated from updated DOB
   - SC status indicator shows "SC 30%"
4. This confirms the demographics edits made on the Patient Edit view are reflected on the Cover Sheet view

### Expected Result
- All demographic data consistent between Patient Edit view and Cover Sheet view
