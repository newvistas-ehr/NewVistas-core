# Clinical Reminders -- Physician Human Test Script

## Prerequisites
- Login: DOCTOR2 / Password: smythVista1
- Patient: 30
- Pre-conditions: Demo data loaded. SiloHost, WebServer, and BlazorWeb running.

---

## Scenario 1: View Due Reminders and Complete a Reminder (Happy Path)

### Steps
1. Log in as **DOCTOR2** (CHEN,MICHAEL L / Family Medicine)
2. Navigate to `/reminders`
3. Enter Patient ID: `30`
4. Click **Load** (or press Enter)
5. The reminders table loads

### Expected Result
- If reminders exist, a table shows with columns:
  - Reminder, Category, Due Date, Status, Actions
- Reminders with Status "DUE" show a yellow badge
- Reminders that are completed show a green badge
- DUE reminders have a green checkmark button: **Done**
- If no reminders exist, shows: "No reminders found."

### Steps (continued -- Complete a Reminder)
6. If a DUE reminder exists, click the **Done** button on that row
7. If no DUE reminders exist, first create one (see Scenario 2), then complete it

### Expected Result
- Green success: "Reminder completed."
- The reminder list reloads
- The completed reminder's Status changes to a green badge (no longer "DUE")
- The Done button disappears for that reminder

---

## Scenario 2: Create a Custom Reminder

### Steps
1. Click the **+ Create Reminder** button (green)
2. The "Create Reminder" form appears
3. Fill in:
   - Reminder Name *: `Annual Diabetic Eye Exam`
   - Category: **CHRONIC DISEASE** (dropdown; options: IMMUNIZATION, PREVENTIVE, SAFETY, SCREENING, CHRONIC DISEASE)
   - Priority: **NORMAL** (dropdown; options: NORMAL, HIGH, LOW)
   - Frequency: `1Y` (free-text, e.g., "1Y" for yearly, "6M" for every 6 months)
   - Due Date: Today's date (use date picker)
4. Click **Create**

### Expected Result
- Green success: "Reminder created."
- The form closes
- The reminders list reloads
- New reminder appears:
  - Reminder: Annual Diabetic Eye Exam
  - Category: CHRONIC DISEASE
  - Due Date: today
  - Status: "DUE" (yellow badge)
  - Actions: Done button visible

---

## Scenario 3: Immunization Reminder (Flu Shot)

### Steps
1. Click **+ Create Reminder**
2. Fill in:
   - Reminder Name *: `Influenza Vaccine (Annual)`
   - Category: **IMMUNIZATION**
   - Priority: **NORMAL**
   - Frequency: `1Y`
   - Due Date: October 1 of the current year (or next flu season)
3. Click **Create**

### Expected Result
- Reminder created with Category: IMMUNIZATION
- Due date set for flu season

---

## Scenario 4: Preventive Care Reminder (Colonoscopy)

### Steps
1. Click **+ Create Reminder**
2. Fill in:
   - Reminder Name *: `Screening Colonoscopy`
   - Category: **SCREENING**
   - Priority: **NORMAL**
   - Frequency: `10Y`
   - Due Date: A date calculated based on patient age (e.g., if patient is 50, due now; if 45, due in 5 years)
3. Click **Create**

### Expected Result
- Reminder created with Category: SCREENING

---

## Scenario 5: High Priority Safety Reminder

### Steps
1. Click **+ Create Reminder**
2. Fill in:
   - Reminder Name *: `Fall Risk Assessment`
   - Category: **SAFETY**
   - Priority: **HIGH**
   - Frequency: `3M`
   - Due Date: Today's date
3. Click **Create**

### Expected Result
- Reminder created with Priority: HIGH
- Appears in the reminders list

---

## Scenario 6: Create Multiple Preventive Reminders

### Steps
1. Create the following reminders in sequence:

   **Reminder A:**
   - Name: `Hemoglobin A1c`
   - Category: CHRONIC DISEASE
   - Priority: NORMAL
   - Frequency: `3M`
   - Due Date: Today

   **Reminder B:**
   - Name: `Lipid Panel`
   - Category: SCREENING
   - Priority: NORMAL
   - Frequency: `1Y`
   - Due Date: 6 months from today

   **Reminder C:**
   - Name: `Pneumococcal Vaccine (PCV20)`
   - Category: IMMUNIZATION
   - Priority: HIGH
   - Frequency: (leave empty -- one-time)
   - Due Date: Today

2. After creating all three, click **Load** to refresh

### Expected Result
- All three reminders appear in the list
- DUE reminders (today's date or past) show yellow badge with Done button
- Future reminders may show different status

---

## Scenario 7: Complete Multiple Reminders

### Steps
1. Click **Done** on "Hemoglobin A1c" reminder

### Expected Result
- Success: "Reminder completed." -- status changes to completed

### Steps (continued)
2. Click **Done** on "Fall Risk Assessment" reminder

### Expected Result
- Success: "Reminder completed."

### Steps (continued)
3. Verify remaining DUE reminders still have Done buttons
4. Verify completed reminders no longer have Done buttons

---

## Scenario 8: Validation -- Missing Reminder Name

### Steps
1. Click **+ Create Reminder**
2. Leave Reminder Name empty
3. Fill in Category: IMMUNIZATION, Due Date: today
4. Click **Create**

### Expected Result
- Red error: "Reminder name is required."
- Reminder is not created
