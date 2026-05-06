# Problem List Management -- Physician Human Test Script -- WPF UI

## Prerequisites
- Login: DOCTOR2 / Password: smythVista1
- Patient: 22
- Pre-conditions: Demo data loaded. SiloHost, WebServer, and WPF Application (NewVistas.WpfClient) are running.

---

## Scenario 1: Add a New Problem (Happy Path)

### Steps
1. Log in as **DOCTOR2** (CHEN,MICHAEL L / Family Medicine)
2. In the Navigation Panel, select **Problems**
3. Enter Patient ID in the toolbar: `22`
4. Click **Load** (or press Enter)
5. The Problem List TabItem displays (default tab)
6. Note any existing problems
7. Click the **Add Problem** TabItem
8. Fill in the form:
   - Diagnosis *: `Essential Hypertension`
   - ICD-10 Code: `I10`
   - Condition: **CHRONIC** (ComboBox; options: ACUTE, CHRONIC, PERMANENT, TRANSCRIBED)
   - Priority: **CHRONIC** (ComboBox; options: ACUTE, CHRONIC)
   - Onset Date: `01/15/2024` (use the DatePicker)
   - Provider: `CHEN,MICHAEL L`
   - Clinic: `FAMILY MEDICINE CLINIC`
   - Service Connected: Leave CheckBox unchecked
   - Comments: `Diagnosed during annual physical. Started on Lisinopril 10mg.`
9. Click **Add Problem**

### Expected Result
- A green notification appears in the status bar: "Problem added successfully."
- The form clears and resets
- View switches to the Problem List TabItem
- "Active only" CheckBox is checked (default)
- The new problem appears in the DataGrid with:
  - Diagnosis: Essential Hypertension
  - Code: I10 (monospace font)
  - Status: ACTIVE (green text)
  - Onset: 01/15/2024
  - Condition: CHRONIC
  - SC: (empty -- not service connected)

---

## Scenario 2: Add a Service-Connected Problem

### Steps
1. Click the **Add Problem** TabItem
2. Fill in:
   - Diagnosis *: `Post-Traumatic Stress Disorder`
   - ICD-10 Code: `F43.10`
   - Condition: **CHRONIC**
   - Priority: **CHRONIC**
   - Onset Date: `06/20/2018`
   - Provider: `CHEN,MICHAEL L`
   - Clinic: `MENTAL HEALTH CLINIC`
   - Service Connected: **Check the CheckBox** (Yes)
   - Comments: `Service-connected PTSD. Combat-related. Currently in therapy.`
3. Click **Add Problem**

### Expected Result
- A green notification appears in the status bar: "Problem added successfully."
- In the Problem List, the new problem shows:
  - SC column: "Yes"
  - Status: ACTIVE
  - Condition: CHRONIC

---

## Scenario 3: Add an Acute Problem

### Steps
1. Click the **Add Problem** TabItem
2. Fill in:
   - Diagnosis *: `Acute Upper Respiratory Infection`
   - ICD-10 Code: `J06.9`
   - Condition: **ACUTE**
   - Priority: **ACUTE**
   - Onset Date: Today's date
   - Provider: `CHEN,MICHAEL L`
   - Clinic: `FAMILY MEDICINE CLINIC`
   - Service Connected: Unchecked
   - Comments: `URI with cough x 3 days. No fever.`
3. Click **Add Problem**

### Expected Result
- Problem added with Condition: ACUTE
- Appears in Active problems DataGrid

---

## Scenario 4: View All Problems (Including Inactive)

### Steps
1. On the Problem List TabItem, the "Active only" CheckBox is checked by default
2. Verify only ACTIVE problems are shown
3. Uncheck the **Active only** CheckBox

### Expected Result
- The DataGrid reloads automatically
- Both ACTIVE and INACTIVE problems appear
- ACTIVE problems show green status text
- INACTIVE problems show gray status text

---

## Scenario 5: Add Multiple Problems with Priority Ordering

### Steps
1. Add the following problems in sequence:

   **Problem A:**
   - Diagnosis: `Type 2 Diabetes Mellitus`
   - ICD-10 Code: `E11.9`
   - Condition: CHRONIC
   - Priority: CHRONIC
   - Onset Date: `03/10/2020`

   **Problem B:**
   - Diagnosis: `Acute Bronchitis`
   - ICD-10 Code: `J20.9`
   - Condition: ACUTE
   - Priority: ACUTE
   - Onset Date: Today's date

   **Problem C:**
   - Diagnosis: `Chronic Kidney Disease, Stage 3`
   - ICD-10 Code: `N18.3`
   - Condition: CHRONIC
   - Priority: CHRONIC
   - Onset Date: `11/05/2022`

### Expected Result
- All three problems appear in the Active problems DataGrid
- Each shows the appropriate Condition and Priority values
- Problems display with their respective ICD-10 codes in monospace font

---

## Scenario 6: Validation -- Missing Required Field

### Steps
1. Click the **Add Problem** TabItem
2. Leave the Diagnosis field empty
3. Fill in ICD-10 Code: `Z00.00`
4. Click **Add Problem**

### Expected Result
- A red error notification appears in the status bar: "Diagnosis is required."
- The problem is not saved

---

## Scenario 7: Problem with Detailed Comments

### Steps
1. Click the **Add Problem** TabItem
2. Fill in:
   - Diagnosis *: `Obesity, BMI 35.0-39.9`
   - ICD-10 Code: `E66.01`
   - Condition: **CHRONIC**
   - Priority: **CHRONIC**
   - Onset Date: `07/01/2019`
   - Provider: `CHEN,MICHAEL L`
   - Clinic: `FAMILY MEDICINE CLINIC`
   - Service Connected: Unchecked
   - Comments:
     ```
     BMI 37.2 at last visit. Patient counseled on diet and exercise.
     Referred to MOVE! weight management program. Considering GLP-1
     agonist therapy if no improvement in 3 months. Co-morbidities
     include HTN and DM2.
     ```
3. Click **Add Problem**

### Expected Result
- Problem added successfully
- Comments are stored (visible if detail view is available)
