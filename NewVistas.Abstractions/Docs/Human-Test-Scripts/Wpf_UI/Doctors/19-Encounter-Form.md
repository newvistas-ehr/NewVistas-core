# Encounter Form (PCE Visit Documentation) -- Physician Human Test Script -- WPF UI

## Prerequisites
- Login: DOCTOR1 / Password: smythVista1
- Patient: 9
- Pre-conditions: Demo data loaded. PCE demo data loaded (Navigation Panel > **PCE**, load patient 9, click **Load Demo** if needed). Lexicon self-seeded (available on first activation). SiloHost, WebServer, and WPF Application (NewVistas.WpfClient) are running.

---

## Scenario 1: Open Encounter Form from Notes View

### Steps
1. Log in as **DOCTOR1** (SMITH,JOHN A / Internal Medicine)
2. In the Navigation Panel, select **Notes**
3. Enter Patient ID in the toolbar: `9`
4. Click **Load Notes**
5. Click the **Encounter** button in the view toolbar (or from the menu bar: **Tools** > **Encounter Form**)

### Expected Result
- The **Encounter Form** opens as a dialog window (or dedicated view)
- The window title shows: "Encounter Form -- [Patient Name]"
- A **TabControl** at the top with tabs:
  - **Visit** (selected by default)
  - **Diagnoses**
  - **Procedures**
  - **Providers**
  - **Exams**
  - **Health Factors**
  - **Patient Education**
  - **Immunizations**
  - **Skin Tests**
- The Visit tab is displayed initially
- A status bar at the bottom shows: "Encounter: [status]" (NEW or linked to an existing visit)

---

## Scenario 2: Select Visit Type and Create New Visit

### Steps
1. On the **Visit** tab of the Encounter Form
2. The tab displays three sections:
   - **New Visit** RadioButton (selected by default)
   - **Link to Existing Visit** RadioButton (shows a list of recent visits)
   - **Link to Admission** RadioButton (shows active admissions)
3. With **New Visit** selected, fill in:
   - Visit Date/Time: DatePicker + time -- select today's date, 10:00
   - Service Category: ComboBox with options:
     - A = Ambulatory
     - T = Telecommunications (Telehealth)
     - H = Hospitalization
     - C = Chart Review
     - E = Event (Historical)
     - O = Observation
   - Select **A** (Ambulatory)
   - Location: ComboBox (click **Load Clinics** if needed) -- select `PRIMARY CARE CLINIC A`
4. Click **Create Visit** (or the visit is created automatically when navigating to another tab)

### Expected Result
- A green notification appears in the status bar: "Visit created."
- The Visit tab shows the new visit details: date, location, service category
- The status bar updates: "Encounter: OPEN -- [Visit ID]"
- API call: `POST /api/pce/9/visits`

---

## Scenario 3: Add Diagnoses to Encounter (ICD-10 Lookup via Lexicon)

### Steps
1. Click the **Diagnoses** tab in the Encounter Form
2. The tab shows:
   - A search TextBox labeled "Search ICD-10 Diagnosis"
   - A results DataGrid (initially empty)
   - A "Selected Diagnoses" DataGrid at the bottom showing currently added diagnoses
3. In the search TextBox, type: `hypertension`
4. Click **Search** (or press Enter)

### Expected Result
- The results DataGrid populates with matching ICD-10 codes from the Lexicon:
  - Columns: Code, Description, System (ICD10)
  - Results include: I10 "Essential (primary) hypertension", I11.9 "Hypertensive heart disease without heart failure", etc.
- API call: `GET /api/lexicon/search?term=hypertension&system=ICD10`

### Steps (continued)
5. Click the row for **I10 -- Essential (primary) hypertension**
6. Click the **Add** button (or double-click the row)
7. A dialog appears asking: "Mark as primary diagnosis?" with **Yes** / **No** buttons
8. Click **Yes**

### Expected Result
- I10 appears in the Selected Diagnoses DataGrid with columns: Code, Description, Primary (CheckBox checked)
- The primary diagnosis is indicated with bold text and a star icon

### Steps (continued)
9. Search for `diabetes type 2`
10. Add **E11.9 -- Type 2 diabetes mellitus without complications**
11. When prompted "Mark as primary diagnosis?" click **No**

### Expected Result
- E11.9 appears in the Selected Diagnoses list with Primary unchecked
- I10 remains the primary diagnosis
- API call: `POST /api/pce/9/visits/{visitId}/diagnosis`

---

## Scenario 4: Add Procedures to Encounter (CPT Code Lookup)

### Steps
1. Click the **Procedures** tab in the Encounter Form
2. The tab shows:
   - A search TextBox labeled "Search CPT Procedure"
   - A results DataGrid
   - A "Selected Procedures" DataGrid at the bottom
3. Type: `office visit established` in the search TextBox
4. Click **Search**

### Expected Result
- The results DataGrid shows matching CPT codes:
  - 99213 "Office/outpatient visit, est patient, low complexity"
  - 99214 "Office/outpatient visit, est patient, moderate complexity"
  - 99215 "Office/outpatient visit, est patient, high complexity"
- API call: `GET /api/lexicon/search?term=office+visit+established&system=CPT`

### Steps (continued)
5. Select **99214** and click **Add**
6. A quantity/modifier dialog appears:
   - Quantity: numeric spinner (default 1)
   - Modifier (optional): TextBox
7. Leave defaults and click **OK**

### Expected Result
- 99214 appears in the Selected Procedures DataGrid with columns: Code, Description, Quantity
- API call: `POST /api/pce/9/visits/{visitId}/procedure`

---

## Scenario 5: Add Encounter Provider

### Steps
1. Click the **Providers** tab in the Encounter Form
2. The tab shows:
   - A provider search TextBox
   - A results list
   - A "Selected Providers" DataGrid with columns: Provider Name, Role (Primary, Attending, Consulting), Primary (CheckBox)
3. Type: `SMITH` in the search TextBox and click **Search**
4. Select **SMITH,JOHN A** from the results
5. Click **Add**
6. A role selection dialog appears:
   - Role: ComboBox (Primary, Attending, Consulting)
   - Primary Provider: CheckBox
7. Select Role: **Primary**, check **Primary Provider**
8. Click **OK**

### Expected Result
- SMITH,JOHN A appears in the Selected Providers DataGrid with Role: Primary and Primary checked
- Only one provider can be marked as Primary at a time

### Steps (continued)
9. Search for `CHEN`, select **CHEN,MICHAEL L**, add with Role: **Attending**, Primary unchecked

### Expected Result
- Two providers listed: SMITH,JOHN A (Primary) and CHEN,MICHAEL L (Attending)

---

## Scenario 6: Add Health Factors to Encounter

### Steps
1. Click the **Health Factors** tab in the Encounter Form
2. The tab shows:
   - A category TreeView on the left with health factor categories:
     - Tobacco Use
     - Alcohol Use
     - Exercise
     - Nutrition
     - Social History
     - Screening Results
   - A details panel on the right
3. Expand **Tobacco Use** in the TreeView
4. Select **CURRENT SMOKER**
5. Click **Add** (or double-click)
6. A details dialog appears:
   - Factor: CURRENT SMOKER (read-only)
   - Level/Severity (optional): ComboBox (Minimal, Moderate, Heavy)
   - Comment: TextBox
7. Select Level: **Moderate**
8. Enter Comment: `1/2 PPD x 20 years, interested in cessation`
9. Click **OK**

### Expected Result
- "CURRENT SMOKER" appears in the Selected Health Factors list below the TreeView
- Columns: Factor, Category, Level, Comment

### Steps (continued)
10. Expand **Alcohol Use**, select **AUDIT-C POSITIVE**, add with Comment: `Score 5/12`

### Expected Result
- Two health factors listed for the encounter
- API calls to the PCE visit grain record the health factors

---

## Scenario 7: Add Immunizations to Encounter

### Steps
1. Click the **Immunizations** tab in the Encounter Form
2. The tab shows:
   - A search/selection panel for immunization types
   - A "Selected Immunizations" DataGrid
3. From the immunization ComboBox, select **Influenza, Injectable (IIV4)**
4. Fill in:
   - Lot Number: TextBox -- enter `FLU2026-A123`
   - Manufacturer: ComboBox -- select `Sanofi Pasteur`
   - Administration Site: ComboBox (Left Deltoid, Right Deltoid, Left Thigh, Right Thigh) -- select **Left Deltoid**
   - Route: ComboBox (Intramuscular, Subcutaneous, Intradermal) -- select **Intramuscular**
   - Dose: TextBox -- enter `0.5 mL`
   - Reaction: ComboBox (None, Local Redness, Fever, Other) -- select **None**
   - VIS Date (Vaccine Information Statement): DatePicker -- select today's date
5. Click **Add Immunization**

### Expected Result
- The immunization appears in the Selected Immunizations DataGrid:
  - Columns: Immunization, Lot #, Site, Route, Dose, Reaction, VIS Date
- A green notification appears in the status bar: "Immunization recorded."

---

## Scenario 8: Add Patient Education Topics

### Steps
1. Click the **Patient Education** tab in the Encounter Form
2. The tab shows:
   - A topic search TextBox
   - A results list
   - A "Selected Topics" DataGrid
3. Type: `diabetes` in the search TextBox and click **Search**
4. Select **Diabetes Self-Management** from the results
5. Fill in:
   - Level of Understanding: ComboBox (Good, Fair, Poor, Refused) -- select **Good**
   - Comment: TextBox -- enter `Reviewed blood glucose monitoring and diet modifications`
6. Click **Add**

### Expected Result
- The topic appears in the Selected Topics DataGrid:
  - Columns: Topic, Level of Understanding, Comment
- API calls record the patient education entry for the encounter

### Steps (continued)
7. Search for `hypertension`, select **Hypertension Management**
8. Level of Understanding: **Fair**
9. Comment: `Discussed sodium restriction and medication adherence`
10. Click **Add**

### Expected Result
- Two education topics listed for the encounter

---

## Scenario 9: Add Exam Findings

### Steps
1. Click the **Exams** tab in the Encounter Form
2. The tab shows:
   - An exam type ComboBox with options: General Exam, Cardiovascular, Respiratory, Neurological, Musculoskeletal, Mental Status, Skin, HEENT, GI/Abdominal, GU
   - Result RadioButton group: **Normal**, **Abnormal**
   - Comment: TextBox
3. Select Exam Type: **Cardiovascular**
4. Select Result: **Normal**
5. Enter Comment: `RRR, no murmurs, rubs, or gallops. No peripheral edema.`
6. Click **Add Exam**

### Expected Result
- The exam appears in the Selected Exams DataGrid:
  - Columns: Exam Type, Result (green "Normal" or red "Abnormal" status indicator), Comment

### Steps (continued)
7. Select Exam Type: **Respiratory**
8. Select Result: **Abnormal**
9. Enter Comment: `Decreased breath sounds at right base. Dullness to percussion RLL.`
10. Click **Add Exam**

### Expected Result
- Two exams listed: Cardiovascular (Normal, green) and Respiratory (Abnormal, red)

---

## Scenario 10: Record GAF Score (Global Assessment of Functioning)

### Steps
1. In the Encounter Form, locate the **GAF** section (either on the Visit tab or as a separate small panel)
2. The GAF section shows:
   - Current GAF Score: TextBox (numeric, 1-100)
   - Date: DatePicker (defaults to today)
3. Enter GAF Score: `65`
4. Click **Save GAF** (or the score saves when leaving the field)

### Expected Result
- A green notification appears in the status bar: "GAF score recorded: 65"
- The GAF score interpretation appears: "65 -- Some mild symptoms OR some difficulty in social, occupational, or school functioning, but generally functioning pretty well"
- GAF scale reference (displayed on hover or in a tooltip):
  - 91-100: Superior functioning
  - 81-90: Absent or minimal symptoms
  - 71-80: Transient, expectable reactions
  - 61-70: Some mild symptoms
  - 51-60: Moderate symptoms
  - 41-50: Serious symptoms
  - 31-40: Some impairment in reality testing
  - 21-30: Behavior considerably influenced by delusions
  - 11-20: Some danger of hurting self or others
  - 1-10: Persistent danger of severely hurting self or others

### Steps (continued)
5. Try entering `0` in the GAF Score field

### Expected Result
- A red error notification appears in the status bar: "GAF score must be between 1 and 100."
- The invalid value is not saved
