# Allergy Documentation -- Human Test Script

## Prerequisites

- **Login:** NURSE4 / Password: `smythVista1`
- **Patient:** 35
- **Pre-conditions:**
  1. The SiloHost, WebServer, and BlazorWeb projects must all be running.
  2. Navigate to `/allergies` in the browser.
  3. Enter Patient ID `35` in the Patient ID field and click **Load**.
  4. If no allergies exist, the Allergies tab shows the green "No Known Allergies" banner.

---

## Scenario 1: Record a Drug Allergy with Reactions and Severity (Happy Path)

### Steps

1. Navigate to `/allergies`.
2. Enter Patient ID: `35`
3. Click **Load**.
4. Click the **Record Allergy** tab.
5. Fill in all fields:
   - Allergen: `Penicillin`
   - Allergen Type: `Drug`
   - Reaction Type: `ALLERGY`
   - Reactions: `Rash, Hives, Angioedema`
   - Severity: `Severe`
   - Observed / Historical: select `Observed` radio button
   - Originator: `WILLIAMS,KAREN S`
   - Comments: `Patient reports anaphylactic reaction to Amoxicillin in 2019 requiring ER visit. Cross-reactivity with all penicillin-class antibiotics assumed. Allergy bracelet placed.`
6. Click **Record Allergy**.

### Expected Result

- Green success banner: "Allergy recorded successfully."
- The page switches to the **Allergies** tab.
- The "No Known Allergies" banner is replaced by the allergy table.
- The table shows one row:
  - Allergen: **Penicillin** (bold)
  - Type: Drug
  - Reactions: Rash, Hives, Angioedema
  - Severity: **Severe** (red text, bold -- severity.severe CSS class)
  - Observed/Historical: Observed

---

## Scenario 2: Record a Food Allergy

### Steps

1. Click the **Record Allergy** tab.
2. Fill in:
   - Allergen: `Shellfish`
   - Allergen Type: `Food`
   - Reaction Type: `ALLERGY`
   - Reactions: `Throat swelling, Difficulty breathing, Urticaria`
   - Severity: `Severe`
   - Observed / Historical: select `Observed`
   - Originator: `WILLIAMS,KAREN S`
   - Comments: `Patient reports severe allergic reaction to shrimp and crab. Carries EpiPen. Dietary notified -- shellfish-free diet order placed.`
3. Click **Record Allergy**.

### Expected Result

- Green success banner: "Allergy recorded successfully."
- The Allergies tab now shows 2 rows:
  1. Penicillin (Drug, Severe, Observed)
  2. Shellfish (Food, Severe, Observed)

---

## Scenario 3: Record a Mild Historical Drug Allergy

### Steps

1. Click the **Record Allergy** tab.
2. Fill in:
   - Allergen: `Sulfa drugs`
   - Allergen Type: `Drug`
   - Reaction Type: `ADVERSE REACTION`
   - Reactions: `Nausea, Mild rash`
   - Severity: `Mild`
   - Observed / Historical: select `Historical`
   - Originator: `WILLIAMS,KAREN S`
   - Comments: `Patient reports history of mild GI upset and skin rash with Bactrim (trimethoprim-sulfamethoxazole) approximately 10 years ago. Reaction self-resolved. Not confirmed by medical records.`
3. Click **Record Allergy**.

### Expected Result

- Allergy recorded.
- The Allergies tab now shows 3 rows:
  1. Penicillin (Drug, Severe, Observed)
  2. Shellfish (Food, Severe, Observed)
  3. Sulfa drugs (Drug, Mild, Historical)
- The Severity column for Sulfa drugs does NOT have the red/bold styling (only "Severe" and "Moderate" get special styling).

---

## Scenario 4: Record an "Other" Type Allergy (Latex)

### Steps

1. Click the **Record Allergy** tab.
2. Fill in:
   - Allergen: `Latex`
   - Allergen Type: `Other`
   - Reaction Type: `ALLERGY`
   - Reactions: `Contact dermatitis, Itching, Redness`
   - Severity: `Moderate`
   - Observed / Historical: select `Observed`
   - Originator: `WILLIAMS,KAREN S`
   - Comments: `Patient develops contact dermatitis when exposed to latex gloves. Non-latex gloves to be used for all procedures. Latex allergy sign posted on door.`
3. Click **Record Allergy**.

### Expected Result

- The Allergies tab now shows 4 rows.
- The Latex entry shows:
  - Type: Other
  - Severity: **Moderate** (orange/amber styling -- severity.moderate CSS class)

---

## Scenario 5: Verify NKA Display on Patient with No Allergies

### Steps

1. Navigate to `/allergies`.
2. Enter a Patient ID with no allergies, e.g., `48`
3. Click **Load**.

### Expected Result

- The **Allergies** tab displays the green NKA banner: "No Known Allergies"
- No table is shown.

---

## Scenario 6: Verify Allergen is Required

### Steps

1. On Patient 35, click the **Record Allergy** tab.
2. Leave the **Allergen** field blank.
3. Fill in other fields:
   - Allergen Type: `Drug`
   - Reactions: `Rash`
   - Severity: `Mild`
4. Click **Record Allergy**.

### Expected Result

- Red error banner: "Allergen is required."
- No allergy is saved.
- The form remains on the Record Allergy tab.

---

## Scenario 7: Verify an Existing Allergy (via API)

The Blazor page displays allergies but the verify action is available through the API.

### Steps

1. First, retrieve the allergy IDs for Patient 35:
   - Use the workflow grain method `GetAllergiesAsync` or the API `GET /api/patient/35/allergies`.
2. Note the Allergy ID for the Penicillin allergy.
3. Call the API to verify:
   - The `IAllergyGrain.VerifyAllergyAsync()` method can be invoked via the grain interface.
   - Currently the verify action is grain-level; there is no dedicated verify button on the Allergies Blazor page.
4. Reload the allergies page to confirm the allergy is still listed (verification does not change the display).

### Expected Result

- The allergy grain's internal verification flag is set.
- The allergy continues to appear in the table unchanged.

---

## Scenario 8: Mark Allergy as Entered in Error (via API)

### Steps

1. Identify the allergy ID for the Sulfa drugs allergy on Patient 35.
2. Call the grain method `MarkAsErrorAsync()` on the allergy grain.
3. Reload the Allergies page for Patient 35.

### Expected Result

- The Sulfa drugs allergy is no longer displayed in the allergy list (or shows an error indicator if the grain filters on error status).
- The remaining 3 allergies (Penicillin, Shellfish, Latex) continue to display.

---

## Reference: Allergy Form Fields

| Field              | Type     | Required | Options / Format                        |
|--------------------|----------|----------|-----------------------------------------|
| Allergen           | Text     | Yes      | Free text (e.g., "Penicillin")          |
| Allergen Type      | Dropdown | Yes      | Drug, Food, Other                       |
| Reaction Type      | Dropdown | No       | ALLERGY, ADVERSE REACTION, PHARMACOLOGIC |
| Reactions          | Text     | No       | Comma-separated (e.g., "Rash, Hives")  |
| Severity           | Dropdown | No       | Mild, Moderate, Severe                  |
| Observed/Historical| Radio    | No       | O = Observed, H = Historical            |
| Originator         | Text     | No       | Name of entering clinician              |
| Comments           | Textarea | No       | Free text                               |

### Severity Display Styling

| Severity | CSS Class         | Visual                |
|----------|-------------------|-----------------------|
| Severe   | severity.severe   | Red text, bold        |
| Moderate | severity.moderate | Amber/orange, medium  |
| Mild     | (no special class) | Normal text           |
