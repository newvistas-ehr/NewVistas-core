# Health Factors -- Human Test Script -- WPF UI

## Prerequisites

- **Login:** NURSE2 / Password: `smythVista1`
- **Patient:** 9
- **Pre-conditions:**
  1. The SiloHost, WebServer, and WPF Application (NewVistas.WpfClient) must all be running.
  2. In the Navigation Panel, select **Health Factors**.
  3. Enter Patient ID `9` in the Patient ID field in the toolbar and click **Load**.
  4. If no health factors exist yet, the Health Factors TabItem shows "No health factors recorded."

---

## Scenario 1: View Patient Health Factors

### Steps

1. In the Navigation Panel, select **Health Factors**.
2. Enter Patient ID: `9` in the Patient ID field in the toolbar.
3. Click **Load**.
4. Verify the **Health Factors** TabItem is active.
5. Observe the DataGrid columns.

### Expected Result

- The DataGrid displays columns: Factor Name, Category, Date, Level, Comment.
- If health factors exist, each row shows one health factor entry with:
  - Factor Name (e.g., "CURRENT SMOKER")
  - Category (e.g., "TOBACCO USE", "ALCOHOL USE", "SCREENING")
  - Date in date/time format
  - Level (e.g., "MODERATE", "HEAVY/SEVERE", or blank)
  - Comment (truncated if long, with tooltip showing full text)
- Rows are sorted by Date, newest first.
- If no health factors exist, a message reads "No health factors recorded."
- The API endpoint `GET /api/patient/9/health-factors` provides the data.

---

## Scenario 2: Add Health Factor (Search and Select)

### Steps

1. Click the **Add Health Factor** TabItem.
2. In the Search TextBox, type `tobacco` and press Enter (or click **Search**).
3. The search results DataGrid displays matching health factors from the catalog.
4. Select **CURRENT SMOKER** from the results list by clicking the row.
5. Fill in the detail fields:
   - Category: (auto-populated) `TOBACCO USE`
   - Level ComboBox: `MODERATE`
   - Event Date DatePicker: (leave as current date/time)
   - Location TextBox: `Primary Care Clinic`
   - Entered By TextBox: `THOMPSON,PATRICIA A`
   - Comments TextBox: `Patient reports smoking 1/2 pack per day for 15 years. Counseled on cessation options.`
6. Click **Record**.

### Expected Result

- A green notification appears in the status bar: "Health factor recorded."
- The form fields are cleared (reset to defaults).
- Switch to the **Health Factors** TabItem. The newest entry shows:
  - Factor Name: CURRENT SMOKER
  - Category: TOBACCO USE
  - Date: current date/time
  - Level: MODERATE
  - Comment: Patient reports smoking 1/2 pack per day for 15 years...
- The API endpoint `POST /api/patient/9/health-factors` was called with the entered data.

---

## Scenario 3: Search Health Factors (Type-Ahead)

### Steps

1. Click the **Add Health Factor** TabItem.
2. In the Search TextBox, type `alc`.
3. Observe the results as you type (type-ahead behavior after 3 characters).

### Expected Result

- The search results DataGrid dynamically filters to display health factors matching "alc":
  - ALCOHOL USE SCREENING
  - ALCOHOL - CURRENT NON-DRINKER
  - ALCOHOL - CURRENT LIGHT DRINKER
  - ALCOHOL - CURRENT MODERATE DRINKER
  - ALCOHOL - CURRENT HEAVY DRINKER
  - (and similar entries)
- Results update as the nurse continues typing (e.g., typing "alcohol" narrows the list further).
- Each result row displays: Factor Name, Category.

---

## Scenario 4: Browse Health Factor Categories (TreeView)

### Steps

1. Click the **Add Health Factor** TabItem.
2. Click the **Browse Categories** button (or expand the TreeView panel on the left side).
3. Expand the **TOBACCO USE** category node in the TreeView.

### Expected Result

- The TreeView displays a hierarchical list of health factor categories:
  - ALCOHOL USE
  - TOBACCO USE
  - DIET
  - EXERCISE
  - MENTAL HEALTH SCREENING
  - SOCIAL HISTORY
  - SUBSTANCE USE
  - WOMEN'S HEALTH
  - (and others)
- Expanding **TOBACCO USE** reveals child items:
  - CURRENT SMOKER
  - FORMER SMOKER
  - NEVER SMOKER
  - CURRENT SMOKELESS TOBACCO USER
  - HEAVY TOBACCO USE
- Clicking a leaf node (e.g., "CURRENT SMOKER") auto-populates the Factor Name and Category fields in the recording form.

---

## Scenario 5: Record Tobacco Use Health Factor

### Steps

1. Click the **Add Health Factor** TabItem.
2. From the TreeView, expand **TOBACCO USE** and select **CURRENT SMOKER**.
3. The Factor Name and Category fields are auto-populated.
4. Fill in the detail fields:
   - Level ComboBox: `HEAVY/SEVERE`
   - Event Date DatePicker: (leave as current date/time)
   - Location TextBox: `Primary Care Clinic`
   - Entered By TextBox: `THOMPSON,PATRICIA A`
   - Comments TextBox: `Patient reports smoking 2 packs/day for 30 years. Pack-years: 60. Referred to tobacco cessation program.`
5. Set the Value field:
   - Value TextBox: `2 packs/day`
   - Magnitude TextBox: `60` (pack-years)
6. Click **Record**.

### Expected Result

- A green notification appears in the status bar: "Health factor recorded."
- On the **Health Factors** TabItem, the newest entry shows:
  - Factor Name: CURRENT SMOKER
  - Category: TOBACCO USE
  - Level: HEAVY/SEVERE
- The health factor was recorded with value and magnitude via:
  - `POST /api/patient/9/health-factors` (initial record)
  - `POST /api/patient/9/health-factors/{healthFactorId}/value` with body: `{ "value": "2 packs/day", "magnitude": "60" }`

---

## Scenario 6: Record Alcohol Screening (AUDIT-C)

### Steps

1. Click the **Add Health Factor** TabItem.
2. In the Search TextBox, type `AUDIT-C` and press Enter.
3. Select **ALCOHOL USE SCREENING** from the results.
4. Fill in the detail fields:
   - Category: (auto-populated) `ALCOHOL USE`
   - Level ComboBox: `MODERATE`
   - Event Date DatePicker: (leave as current date/time)
   - Location TextBox: `Primary Care Clinic`
   - Entered By TextBox: `THOMPSON,PATRICIA A`
   - Comments TextBox: `AUDIT-C Score: 5. Positive screen (>=4 for men). Brief intervention provided: discussed risks of heavy drinking, recommended reducing consumption. Patient agreeable to follow-up in 3 months.`
5. Set the Value field:
   - Value TextBox: `AUDIT-C SCORE 5`
   - Magnitude TextBox: `5`
6. Click **Record**.

### Expected Result

- A green notification appears in the status bar: "Health factor recorded."
- On the **Health Factors** TabItem, the newest entry shows:
  - Factor Name: ALCOHOL USE SCREENING
  - Category: ALCOHOL USE
  - Level: MODERATE
  - Comment: AUDIT-C Score: 5...
- The positive screen result (score >= 4 for men, >= 3 for women) triggers a clinical reminder for brief intervention follow-up.

---

## Scenario 7: Remove Health Factor from Visit (Entered in Error)

### Steps

1. On the **Health Factors** TabItem, locate a health factor to remove (e.g., one with incorrect data).
2. Click the row to select it, then click the **Resolve** button (or right-click and select **Mark as Resolved**).
3. In the dialog window "Resolve Health Factor", fill in:
   - Resolved By TextBox: `THOMPSON,PATRICIA A`
   - Reason TextBox: `Entered in error - incorrect patient. Factor should be associated with patient 4, not patient 9.`
4. Click **Confirm**.

### Expected Result

- A green notification appears in the status bar: "Health factor resolved."
- The dialog window closes.
- The Health Factors DataGrid refreshes. The resolved entry now shows:
  - An "RESOLVED" status indicator (gray text or strikethrough)
  - The factor remains visible but is clearly marked as resolved.
- The API endpoint `POST /api/patient/9/health-factors/{healthFactorId}/resolve` was called with body: `{ "resolvedByName": "THOMPSON,PATRICIA A" }`.
- The resolved factor no longer appears in active clinical decision support queries.

---

## Reference: API Endpoints

| Action                | Method | Endpoint                                                         |
|-----------------------|--------|------------------------------------------------------------------|
| List health factors   | GET    | `/api/patient/{patientId}/health-factors`                        |
| Record health factor  | POST   | `/api/patient/{patientId}/health-factors`                        |
| Update severity       | POST   | `/api/patient/{patientId}/health-factors/{id}/severity`          |
| Set category          | POST   | `/api/patient/{patientId}/health-factors/{id}/category`          |
| Set value             | POST   | `/api/patient/{patientId}/health-factors/{id}/value`             |
| Resolve               | POST   | `/api/patient/{patientId}/health-factors/{id}/resolve`           |
| Reactivate            | POST   | `/api/patient/{patientId}/health-factors/{id}/reactivate`        |
| Add history entry     | POST   | `/api/patient/{patientId}/health-factors/{id}/history`           |
| Get history           | GET    | `/api/patient/{patientId}/health-factors/{id}/history`           |

## Reference: Common Health Factor Categories

| Category               | Example Factors                                                        |
|------------------------|------------------------------------------------------------------------|
| TOBACCO USE            | CURRENT SMOKER, FORMER SMOKER, NEVER SMOKER, SMOKELESS TOBACCO         |
| ALCOHOL USE            | CURRENT DRINKER, NON-DRINKER, ALCOHOL USE SCREENING                    |
| DIET                   | DIET EDUCATION, NUTRITION SCREENING                                    |
| EXERCISE               | EXERCISE SCREENING, PHYSICALLY ACTIVE, SEDENTARY                       |
| MENTAL HEALTH SCREENING| PHQ-2 SCREEN, PHQ-9 SCREEN, DEPRESSION SCREENING                      |
| SOCIAL HISTORY         | HOMELESS, EMPLOYED, LIVES ALONE                                        |
| SUBSTANCE USE          | SUBSTANCE USE SCREENING, ILLICIT DRUG USE                              |
| WOMEN'S HEALTH         | PREGNANCY SCREENING, MAMMOGRAM SCREENING                               |
