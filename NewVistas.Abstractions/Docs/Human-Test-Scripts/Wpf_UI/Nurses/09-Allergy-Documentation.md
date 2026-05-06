# Allergy Documentation -- Human Test Script -- WPF UI

## Prerequisites

- **Login:** NURSE4 / Password: `smythVista1`
- **Patient:** 35
- **Pre-conditions:**
  1. The SiloHost, WebServer, and WPF Application (NewVistas.WpfClient) must all be running.
  2. In the Navigation Panel, select **Allergies**.
  3. Enter Patient ID `35` in the Patient ID field in the toolbar and click **Load**.
  4. If no allergies exist, the Allergies TabItem shows the green "No Known Allergies" banner.

---

## Scenario 1: Record a Drug Allergy with Reactions and Severity (Happy Path)

### Steps

1. In the Navigation Panel, select **Allergies**.
2. Enter Patient ID: `35` in the Patient ID field in the toolbar.
3. Click **Load**.
4. Click the **Record Allergy** TabItem.
5. Fill in all fields:
   - Allergen TextBox: `Penicillin`
   - Allergen Type ComboBox: `Drug`
   - Reaction Type ComboBox: `ALLERGY`
   - Reactions TextBox: `Rash, Hives, Angioedema`
   - Severity ComboBox: `Severe`
   - Observed / Historical: select `Observed` RadioButton
   - Originator TextBox: `WILLIAMS,KAREN S`
   - Comments TextBox: `Patient reports anaphylactic reaction to Amoxicillin in 2019 requiring ER visit. Cross-reactivity with all penicillin-class antibiotics assumed. Allergy bracelet placed.`
6. Click **Record Allergy**.

### Expected Result

- A green notification appears in the status bar: "Allergy recorded successfully."
- The view switches to the **Allergies** TabItem.
- The "No Known Allergies" banner is replaced by the allergy DataGrid.
- The DataGrid shows one row:
  - Allergen: **Penicillin** (bold)
  - Type: Drug
  - Reactions: Rash, Hives, Angioedema
  - Severity: **Severe** (displayed with red foreground, bold)
  - Observed/Historical: Observed

---

## Scenario 2: Record a Food Allergy

### Steps

1. Click the **Record Allergy** TabItem.
2. Fill in:
   - Allergen TextBox: `Shellfish`
   - Allergen Type ComboBox: `Food`
   - Reaction Type ComboBox: `ALLERGY`
   - Reactions TextBox: `Throat swelling, Difficulty breathing, Urticaria`
   - Severity ComboBox: `Severe`
   - Observed / Historical: select `Observed` RadioButton
   - Originator TextBox: `WILLIAMS,KAREN S`
   - Comments TextBox: `Patient reports severe allergic reaction to shrimp and crab. Carries EpiPen. Dietary notified -- shellfish-free diet order placed.`
3. Click **Record Allergy**.

### Expected Result

- A green notification appears in the status bar: "Allergy recorded successfully."
- The Allergies TabItem now shows 2 rows:
  1. Penicillin (Drug, Severe, Observed)
  2. Shellfish (Food, Severe, Observed)

---

## Scenario 3: Record a Mild Historical Drug Allergy

### Steps

1. Click the **Record Allergy** TabItem.
2. Fill in:
   - Allergen TextBox: `Sulfa drugs`
   - Allergen Type ComboBox: `Drug`
   - Reaction Type ComboBox: `ADVERSE REACTION`
   - Reactions TextBox: `Nausea, Mild rash`
   - Severity ComboBox: `Mild`
   - Observed / Historical: select `Historical` RadioButton
   - Originator TextBox: `WILLIAMS,KAREN S`
   - Comments TextBox: `Patient reports history of mild GI upset and skin rash with Bactrim (trimethoprim-sulfamethoxazole) approximately 10 years ago. Reaction self-resolved. Not confirmed by medical records.`
3. Click **Record Allergy**.

### Expected Result

- Allergy recorded.
- The Allergies TabItem now shows 3 rows:
  1. Penicillin (Drug, Severe, Observed)
  2. Shellfish (Food, Severe, Observed)
  3. Sulfa drugs (Drug, Mild, Historical)
- The Severity column for Sulfa drugs does NOT have the red/bold styling (only "Severe" and "Moderate" get special styling).

---

## Scenario 4: Record an "Other" Type Allergy (Latex)

### Steps

1. Click the **Record Allergy** TabItem.
2. Fill in:
   - Allergen TextBox: `Latex`
   - Allergen Type ComboBox: `Other`
   - Reaction Type ComboBox: `ALLERGY`
   - Reactions TextBox: `Contact dermatitis, Itching, Redness`
   - Severity ComboBox: `Moderate`
   - Observed / Historical: select `Observed` RadioButton
   - Originator TextBox: `WILLIAMS,KAREN S`
   - Comments TextBox: `Patient develops contact dermatitis when exposed to latex gloves. Non-latex gloves to be used for all procedures. Latex allergy sign posted on door.`
3. Click **Record Allergy**.

### Expected Result

- The Allergies TabItem now shows 4 rows.
- The Latex entry shows:
  - Type: Other
  - Severity: **Moderate** (displayed with orange/amber foreground)

---

## Scenario 5: Verify NKA Display on Patient with No Allergies

### Steps

1. In the Navigation Panel, select **Allergies**.
2. Enter a Patient ID with no allergies, e.g., `48`
3. Click **Load**.

### Expected Result

- The **Allergies** TabItem displays the green NKA banner: "No Known Allergies"
- No DataGrid is shown.

---

## Scenario 6: Verify Allergen is Required

### Steps

1. On Patient 35, click the **Record Allergy** TabItem.
2. Leave the **Allergen** TextBox blank.
3. Fill in other fields:
   - Allergen Type ComboBox: `Drug`
   - Reactions TextBox: `Rash`
   - Severity ComboBox: `Mild`
4. Click **Record Allergy**.

### Expected Result

- A red error notification appears in the status bar: "Allergen is required."
- No allergy is saved.
- The view remains on the Record Allergy TabItem.

---

## Scenario 7: Verify an Existing Allergy (via API)

The WPF view displays allergies but the verify action is available through the API.

### Steps

1. First, retrieve the allergy IDs for Patient 35:
   - Use the workflow grain method `GetAllergiesAsync` or the API `GET /api/patient/35/allergies`.
2. Note the Allergy ID for the Penicillin allergy.
3. Call the API to verify:
   - The `IAllergyGrain.VerifyAllergyAsync()` method can be invoked via the grain interface.
   - Currently the verify action is grain-level; there is no dedicated verify button on the Allergies WPF view.
4. Reload the allergies view to confirm the allergy is still listed (verification does not change the display).

### Expected Result

- The allergy grain's internal verification flag is set.
- The allergy continues to appear in the DataGrid unchanged.

---

## Scenario 8: Mark Allergy as Entered in Error (via API)

### Steps

1. Identify the allergy ID for the Sulfa drugs allergy on Patient 35.
2. Call the grain method `MarkAsErrorAsync()` on the allergy grain.
3. Reload the Allergies view for Patient 35.

### Expected Result

- The Sulfa drugs allergy is no longer displayed in the allergy list (or shows an error indicator if the grain filters on error status).
- The remaining 3 allergies (Penicillin, Shellfish, Latex) continue to display.

---

## Reference: Allergy Form Fields

| Field              | Type     | Required | Options / Format                        |
|--------------------|----------|----------|-----------------------------------------|
| Allergen           | TextBox  | Yes      | Free text (e.g., "Penicillin")          |
| Allergen Type      | ComboBox | Yes      | Drug, Food, Other                       |
| Reaction Type      | ComboBox | No       | ALLERGY, ADVERSE REACTION, PHARMACOLOGIC |
| Reactions          | TextBox  | No       | Comma-separated (e.g., "Rash, Hives")  |
| Severity           | ComboBox | No       | Mild, Moderate, Severe                  |
| Observed/Historical| RadioButton | No    | O = Observed, H = Historical            |
| Originator         | TextBox  | No       | Name of entering clinician              |
| Comments           | TextBox  | No       | Free text (multiline)                   |

### Severity Display Styling

| Severity | Visual                         |
|----------|--------------------------------|
| Severe   | Displayed with red foreground, bold |
| Moderate | Displayed with amber/orange foreground |
| Mild     | Normal text                    |
