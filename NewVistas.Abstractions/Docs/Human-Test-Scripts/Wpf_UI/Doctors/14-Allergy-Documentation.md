# Allergy Documentation -- Physician Human Test Script -- WPF UI

## Prerequisites
- Login: DOCTOR1 / Password: smythVista1
- Patient: 9
- Pre-conditions: Demo data loaded. SiloHost, WebServer, and WPF Application (NewVistas.WpfClient) are running.

---

## Scenario 1: Document a New Drug Allergy -- Penicillin (Happy Path)

### Steps
1. Log in as **DOCTOR1** (SMITH,JOHN A / Internal Medicine)
2. In the Navigation Panel, select **Allergies**
3. Enter Patient ID in the toolbar: `9`
4. Click **Load** (or press Enter)
5. The **Allergies** TabItem displays (default)
6. If no allergies exist, a green banner shows: "No Known Allergies"
7. Click the **Record Allergy** TabItem
8. Fill in the form:
   - Allergen *: `Penicillin`
   - Allergen Type *: **Drug** (ComboBox; options: Drug, Food, Other)
   - Reaction Type: **ALLERGY** (ComboBox; options: ALLERGY, ADVERSE REACTION, PHARMACOLOGIC)
   - Reactions: `Anaphylaxis, Urticaria, Angioedema` (comma-separated)
   - Severity: **Severe** (ComboBox; options: Mild, Moderate, Severe)
   - Observed / Historical: **Observed** (RadioButton; options: Observed, Historical)
   - Originator: `SMITH,JOHN A`
   - Comments: `Patient experienced anaphylactic reaction to Penicillin V in 2015. Required epinephrine and ICU admission. Cross-reactivity with cephalosporins should be considered.`
9. Click **Record Allergy**

### Expected Result
- A green notification appears in the status bar: "Allergy recorded successfully."
- The form clears and resets to defaults
- View switches to the Allergies TabItem
- The allergy appears in the DataGrid with columns:
  - Allergen: Penicillin (bold)
  - Type: Drug
  - Reactions: Anaphylaxis, Urticaria, Angioedema
  - Severity: **Severe** (highlighted in bold red)
  - Observed/Historical: Observed
- The "No Known Allergies" banner is no longer shown

---

## Scenario 2: Document a Food Allergy

### Steps
1. Click the **Record Allergy** TabItem
2. Fill in:
   - Allergen *: `Shellfish`
   - Allergen Type *: **Food**
   - Reaction Type: **ALLERGY**
   - Reactions: `Hives, Throat Swelling, Nausea`
   - Severity: **Moderate**
   - Observed / Historical: **Observed**
   - Originator: `SMITH,JOHN A`
   - Comments: `Patient reports allergic reaction to shrimp and crab. Carries EpiPen. Last reaction was 6 months ago at a restaurant.`
3. Click **Record Allergy**

### Expected Result
- A green notification appears in the status bar: "Allergy recorded successfully."
- Food allergy appears in the DataGrid:
  - Allergen: Shellfish
  - Type: Food
  - Severity: **Moderate** (displayed with orange foreground)
  - Observed/Historical: Observed

---

## Scenario 3: Document a Historical Allergy (Reported but Not Observed)

### Steps
1. Click the **Record Allergy** TabItem
2. Fill in:
   - Allergen *: `Sulfa Drugs`
   - Allergen Type *: **Drug**
   - Reaction Type: **ALLERGY**
   - Reactions: `Rash`
   - Severity: **Mild**
   - Observed / Historical: **Historical** (RadioButton)
   - Originator: `SMITH,JOHN A`
   - Comments: `Patient reports childhood rash with sulfonamide antibiotics. Exact drug unknown. No documented records available. Patient mother confirmed the allergy.`
3. Click **Record Allergy**

### Expected Result
- Allergy recorded:
  - Allergen: Sulfa Drugs
  - Severity: Mild
  - Observed/Historical: **Historical**
  - Note: "Historical" means the patient reported the allergy but the clinician did not directly observe the reaction

---

## Scenario 4: Document an Adverse Drug Reaction (Not a True Allergy)

### Steps
1. Click the **Record Allergy** TabItem
2. Fill in:
   - Allergen *: `Codeine`
   - Allergen Type *: **Drug**
   - Reaction Type: **ADVERSE REACTION**
   - Reactions: `Nausea, Vomiting, Constipation`
   - Severity: **Moderate**
   - Observed / Historical: **Observed**
   - Originator: `SMITH,JOHN A`
   - Comments: `Patient experiences severe nausea and vomiting with codeine-containing products. This is a pharmacologic side effect, not a true allergic reaction. Can use other opioids with caution.`
3. Click **Record Allergy**

### Expected Result
- Entry appears in the allergy DataGrid
- Reaction Type is stored as ADVERSE REACTION (note: the DataGrid display shows Allergen, Type, Reactions, Severity, Observed/Historical -- Reaction Type may not be displayed in the list but is stored in the grain)

---

## Scenario 5: Document "Other" Allergen Type

### Steps
1. Click the **Record Allergy** TabItem
2. Fill in:
   - Allergen *: `Latex`
   - Allergen Type *: **Other**
   - Reaction Type: **ALLERGY**
   - Reactions: `Contact Dermatitis, Urticaria`
   - Severity: **Moderate**
   - Observed / Historical: **Observed**
   - Originator: `SMITH,JOHN A`
   - Comments: `Latex allergy documented. Use only non-latex gloves and products during all procedures. Alert surgical team.`
3. Click **Record Allergy**

### Expected Result
- Allergy recorded with Type: Other
- Important for surgical and procedural safety

---

## Scenario 6: Verify All Allergies Display Correctly

### Steps
1. Click the **Allergies** TabItem
2. After Scenarios 1-5, the patient should have 5 allergies

### Expected Result
- DataGrid displays all 5 allergies:

  | Allergen | Type | Reactions | Severity | Observed/Historical |
  |----------|------|-----------|----------|---------------------|
  | Penicillin | Drug | Anaphylaxis, Urticaria, Angioedema | Severe | Observed |
  | Shellfish | Food | Hives, Throat Swelling, Nausea | Moderate | Observed |
  | Sulfa Drugs | Drug | Rash | Mild | Historical |
  | Codeine | Drug | Nausea, Vomiting, Constipation | Moderate | Observed |
  | Latex | Other | Contact Dermatitis, Urticaria | Moderate | Observed |

- Severity styling:
  - "Severe" = highlighted in bold red
  - "Moderate" = displayed with orange foreground
  - "Mild" = default text

---

## Scenario 7: Verify Allergy Appears on Cover Sheet

### Steps
1. In the Navigation Panel, select **Cover Sheet**
2. Enter Patient ID in the toolbar: `9`
3. Click **Load**
4. Locate the **Allergies** panel in the grid

### Expected Result
- The Allergies panel shows the documented allergies
- DataGrid columns: Allergen, Severity, Reactions
- The CWAD status indicator in the patient banner should include "A" (Allergy flag)

---

## Scenario 8: NKA (No Known Allergies) Display

### Steps
1. In the Navigation Panel, select **Allergies**
2. Enter a patient ID with no documented allergies in the toolbar: `50`
3. Click **Load**

### Expected Result
- A green **NKA banner** appears: "No Known Allergies"
- The allergy DataGrid is not shown
- This is the standard VistA NKA display pattern

---

## Scenario 9: Validation -- Missing Required Fields

### Steps
1. Click the **Record Allergy** TabItem
2. Leave the Allergen field empty
3. Click **Record Allergy**

### Expected Result
- A red error notification appears in the status bar: "Allergen is required."
- The allergy is not saved

---

## Scenario 10: Pharmacologic Reaction Type

### Steps
1. Click the **Record Allergy** TabItem
2. Fill in:
   - Allergen *: `Metformin`
   - Allergen Type: **Drug**
   - Reaction Type: **PHARMACOLOGIC**
   - Reactions: `Diarrhea, GI Upset, Lactic Acidosis Risk`
   - Severity: **Moderate**
   - Observed / Historical: **Observed**
   - Originator: `SMITH,JOHN A`
   - Comments: `Known pharmacologic effect of Metformin. Patient requires dose adjustment. Use extended-release formulation.`
3. Click **Record Allergy**

### Expected Result
- Allergy/intolerance recorded
- Reaction Type: PHARMACOLOGIC stored in the grain state
