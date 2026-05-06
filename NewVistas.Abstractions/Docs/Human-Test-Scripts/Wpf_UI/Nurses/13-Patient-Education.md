# Patient Education -- Human Test Script -- WPF UI

## Prerequisites

- **Login:** NURSE1 / Password: `smythVista1`
- **Patient:** 4
- **Pre-conditions:**
  1. The SiloHost, WebServer, and WPF Application (NewVistas.WpfClient) must all be running.
  2. In the Navigation Panel, select **PCE** (Patient Care Encounters).
  3. Enter Patient ID `4` in the Patient ID field in the toolbar and click **Load Demo** to seed encounter data.
  4. After demo load succeeds (a green notification appears in the status bar), the Encounter List TabItem should display at least 3 visits.
  5. Select an OPEN encounter (e.g., the Telehealth visit) to use as the active encounter context for patient education documentation.

---

## Scenario 1: View Patient Education History

### Steps

1. In the Navigation Panel, select **PCE**.
2. Enter Patient ID: `4` in the Patient ID field in the toolbar.
3. Click **Load**.
4. Select an encounter from the **Encounter List** TabItem by clicking a row.
5. In the **Encounter Detail** TabItem, click the **Patient Education** section (or TabItem within the encounter detail).
6. Observe the Patient Education DataGrid.

### Expected Result

- The DataGrid displays columns: Topic, Date, Level of Understanding, Provider.
- If patient education topics have been documented for this encounter, each row shows:
  - Topic (e.g., "DIABETES MANAGEMENT")
  - Date in date/time format
  - Level of Understanding (e.g., "GOOD", "FAIR", "POOR")
  - Provider (name of the documenting clinician)
- If no education topics exist for this encounter, the DataGrid shows "No patient education documented for this encounter."
- The data is retrieved from the encounter's PatientEducation list via: `GET /api/pce/4/visits/{visitId}`.

---

## Scenario 2: Add Patient Education Topic

### Steps

1. With an OPEN encounter selected, navigate to the **Patient Education** section within the Encounter Detail.
2. Click **Add Education Topic**.
3. In the dialog window "Add Patient Education", fill in:
   - Topic ComboBox or Search TextBox: type `Diabetes` and select **DIABETES MANAGEMENT** from the list.
   - Level of Understanding ComboBox: `Good`
   - Provider ID TextBox: `NURSE1`
   - Provider Name TextBox: `JOHNSON,MARY R`
4. Click **Save**.

### Expected Result

- A green notification appears in the status bar: "Patient education recorded."
- The dialog window closes.
- The Patient Education DataGrid refreshes and shows a new row:
  - Topic: DIABETES MANAGEMENT
  - Date: current date/time
  - Level of Understanding: GOOD
  - Provider: JOHNSON,MARY R
- The education topic was added to the encounter via the PCE Visit grain's `AddPatientEducationAsync` method.

---

## Scenario 3: Record Level of Understanding

### Steps

1. With an OPEN encounter selected, click **Add Education Topic**.
2. In the dialog window, select:
   - Topic: **MEDICATION INSTRUCTIONS**
   - Level of Understanding ComboBox: `Poor`
   - Provider ID TextBox: `NURSE1`
   - Provider Name TextBox: `JOHNSON,MARY R`
3. Click **Save**.

### Expected Result

- A green notification appears in the status bar: "Patient education recorded."
- The Patient Education DataGrid shows the new row:
  - Topic: MEDICATION INSTRUCTIONS
  - Level of Understanding: POOR (displayed with orange or yellow foreground to indicate concern)
- The Level of Understanding values are:
  - `Good` -- patient demonstrates understanding
  - `Fair` -- patient has partial understanding, may need reinforcement
  - `Poor` -- patient does not understand, requires re-education
  - `Group` -- education provided in a group setting
  - `Refused` -- patient declined education

---

## Scenario 4: Record Education for Multiple Topics in One Encounter

### Steps

1. With an OPEN encounter selected, add the first education topic:
   - Click **Add Education Topic**.
   - Topic: **DIABETES MANAGEMENT**
   - Level of Understanding: `Good`
   - Provider: `JOHNSON,MARY R`
   - Click **Save**.
2. Add the second education topic:
   - Click **Add Education Topic**.
   - Topic: **DIET COUNSELING**
   - Level of Understanding: `Fair`
   - Provider: `JOHNSON,MARY R`
   - Click **Save**.
3. Add the third education topic:
   - Click **Add Education Topic**.
   - Topic: **MEDICATION INSTRUCTIONS**
   - Level of Understanding: `Good`
   - Provider: `JOHNSON,MARY R`
   - Click **Save**.

### Expected Result

- Three green notifications appear sequentially in the status bar: "Patient education recorded."
- The Patient Education DataGrid now shows 3 rows, all linked to the current encounter:
  - DIABETES MANAGEMENT -- Good
  - DIET COUNSELING -- Fair
  - MEDICATION INSTRUCTIONS -- Good
- All three topics share the same encounter Visit ID.
- Verify via the API: `GET /api/pce/4/visits/{visitId}` -- the `patientEducation` array in the response contains all 3 entries.

---

## Scenario 5: Document Education Details (Method and Materials)

### Steps

1. With an OPEN encounter selected, click **Add Education Topic**.
2. In the dialog window, fill in:
   - Topic: **WOUND CARE**
   - Level of Understanding: `Good`
   - Provider ID TextBox: `NURSE1`
   - Provider Name TextBox: `JOHNSON,MARY R`
3. Click **Save**.
4. After the topic is recorded, select the **WOUND CARE** row in the DataGrid and click **Add Details** (or right-click and select **Add Details**).
5. In the detail dialog window, fill in:
   - Method ComboBox: `Demonstration`
   - Materials Given CheckBox: checked
   - Materials Description TextBox: `Written wound care instructions sheet, dressing change supply list`
   - Comments TextBox: `Demonstrated dressing change technique. Patient performed return demonstration successfully. Written instructions provided in English.`
6. Click **Save**.

### Expected Result

- A green notification appears in the status bar: "Education details saved."
- The WOUND CARE row in the DataGrid now shows an icon or indicator that detailed documentation exists.
- The education detail is stored as a comment on the encounter's patient education entry. Since the current API stores TopicName, LevelOfUnderstanding, and Provider fields, the method and materials are captured in the Comments field of the encounter or as a linked TIU note.

---

## Scenario 6: Education Linked to Encounter

### Steps

1. Record two education topics for the current OPEN encounter as described in Scenario 4.
2. Navigate back to the **Encounter List** TabItem.
3. Select the same encounter row.
4. In the **Encounter Detail** TabItem, verify the Patient Education section.
5. Now select a DIFFERENT encounter (e.g., a CHECKED OUT visit from 30 days ago).
6. Observe the Patient Education section for the older encounter.

### Expected Result

- The OPEN encounter shows the education topics recorded in this session (e.g., DIABETES MANAGEMENT, DIET COUNSELING, MEDICATION INSTRUCTIONS).
- The CHECKED OUT encounter from 30 days ago shows its own education topics (if any), or "No patient education documented for this encounter."
- Education topics are encounter-specific -- each set of topics is associated with exactly one Visit ID.
- The visit detail from the API (`GET /api/pce/4/visits/{visitId}`) confirms that `patientEducation` entries are scoped to the specific encounter.

---

## Reference: API Endpoints

| Action                     | Method | Endpoint                                           |
|----------------------------|--------|----------------------------------------------------|
| Get encounter detail       | GET    | `/api/pce/{patientId}/visits/{visitId}`             |
| Create encounter           | POST   | `/api/pce/{patientId}/visits`                       |
| Add patient education      | (via PCE Visit grain `AddPatientEducationAsync`)    |
| Load demo encounters       | POST   | `/api/pce/demo/load?patientId={patientId}`          |

## Reference: Education Topic Categories

| Topic Category           | Example Topics                                              |
|--------------------------|-------------------------------------------------------------|
| Disease Management       | DIABETES MANAGEMENT, HYPERTENSION MANAGEMENT, COPD MANAGEMENT|
| Nutrition                | DIET COUNSELING, LOW SODIUM DIET, DIABETIC DIET             |
| Medications              | MEDICATION INSTRUCTIONS, ANTICOAGULANT EDUCATION, INSULIN USE|
| Procedures               | WOUND CARE, POST-OPERATIVE INSTRUCTIONS, CAST CARE          |
| Preventive Health        | SMOKING CESSATION, EXERCISE COUNSELING, FALL PREVENTION     |
| Safety                   | HOME SAFETY, MEDICATION SAFETY, INFECTION CONTROL           |

## Reference: Level of Understanding Values

| Code | Level     | Description                                         |
|------|-----------|-----------------------------------------------------|
| G    | Good      | Patient demonstrates understanding                   |
| F    | Fair      | Partial understanding, may need reinforcement         |
| P    | Poor      | Does not understand, requires re-education            |
| GR   | Group     | Education provided in a group setting                 |
| R    | Refused   | Patient declined education                            |
| N/A  | N/A       | Not applicable                                        |
