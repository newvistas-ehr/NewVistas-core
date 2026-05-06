# Allergy Documentation -- Physician Human Test Script

## Prerequisites
- Login: DOCTOR1 / Password: smythVista1
- Patient: 9
- Pre-conditions: Demo data loaded. SiloHost, WebServer, and BlazorWeb running.

---

## Scenario 1: Document a New Drug Allergy -- Penicillin (Happy Path)

### Steps
1. Log in as **DOCTOR1** (SMITH,JOHN A / Internal Medicine)
2. Navigate to `/allergies`
3. Enter Patient ID: `9`
4. Click **Load** (or press Enter)
5. The **Allergies** tab displays (default)
6. If no allergies exist, a green banner shows: "No Known Allergies"
7. Click the **Record Allergy** tab
8. Fill in the form:
   - Allergen *: `Penicillin`
   - Allergen Type *: **Drug** (dropdown; options: Drug, Food, Other)
   - Reaction Type: **ALLERGY** (dropdown; options: ALLERGY, ADVERSE REACTION, PHARMACOLOGIC)
   - Reactions: `Anaphylaxis, Urticaria, Angioedema` (comma-separated)
   - Severity: **Severe** (dropdown; options: Mild, Moderate, Severe)
   - Observed / Historical: **Observed** (radio button; options: Observed, Historical)
   - Originator: `SMITH,JOHN A`
   - Comments: `Patient experienced anaphylactic reaction to Penicillin V in 2015. Required epinephrine and ICU admission. Cross-reactivity with cephalosporins should be considered.`
9. Click **Record Allergy**

### Expected Result
- Green success: "Allergy recorded successfully."
- The form clears and resets to defaults
- View switches to the Allergies tab
- The allergy appears in the table with columns:
  - Allergen: Penicillin (bold)
  - Type: Drug
  - Reactions: Anaphylaxis, Urticaria, Angioedema
  - Severity: **Severe** (red bold text)
  - Observed/Historical: Observed
- The "No Known Allergies" banner is no longer shown

---

## Scenario 2: Document a Food Allergy

### Steps
1. Click the **Record Allergy** tab
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
- Green success: "Allergy recorded successfully."
- Food allergy appears in the table:
  - Allergen: Shellfish
  - Type: Food
  - Severity: **Moderate** (orange text)
  - Observed/Historical: Observed

---

## Scenario 3: Document a Historical Allergy (Reported but Not Observed)

### Steps
1. Click the **Record Allergy** tab
2. Fill in:
   - Allergen *: `Sulfa Drugs`
   - Allergen Type *: **Drug**
   - Reaction Type: **ALLERGY**
   - Reactions: `Rash`
   - Severity: **Mild**
   - Observed / Historical: **Historical** (radio button)
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
1. Click the **Record Allergy** tab
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
- Entry appears in the allergy list
- Reaction Type is stored as ADVERSE REACTION (note: the table display shows Allergen, Type, Reactions, Severity, Observed/Historical -- Reaction Type may not be displayed in the list but is stored in the grain)

---

## Scenario 5: Document "Other" Allergen Type

### Steps
1. Click the **Record Allergy** tab
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
1. Click the **Allergies** tab
2. After Scenarios 1-5, the patient should have 5 allergies

### Expected Result
- Table displays all 5 allergies:

  | Allergen | Type | Reactions | Severity | Observed/Historical |
  |----------|------|-----------|----------|---------------------|
  | Penicillin | Drug | Anaphylaxis, Urticaria, Angioedema | Severe | Observed |
  | Shellfish | Food | Hives, Throat Swelling, Nausea | Moderate | Observed |
  | Sulfa Drugs | Drug | Rash | Mild | Historical |
  | Codeine | Drug | Nausea, Vomiting, Constipation | Moderate | Observed |
  | Latex | Other | Contact Dermatitis, Urticaria | Moderate | Observed |

- Severity styling:
  - "Severe" = red bold text
  - "Moderate" = orange text
  - "Mild" = default text

---

## Scenario 7: Verify Allergy Appears on Cover Sheet

### Steps
1. Navigate to `/cover-sheet`
2. Enter Patient ID: `9`
3. Click **Load**
4. Locate the **Allergies** panel in the grid

### Expected Result
- The Allergies panel shows the documented allergies
- Table columns: Allergen, Severity, Reactions
- The CWAD badge in the patient banner should include "A" (Allergy flag)

---

## Scenario 8: NKA (No Known Allergies) Display

### Steps
1. Navigate to `/allergies`
2. Enter a patient ID with no documented allergies: `50`
3. Click **Load**

### Expected Result
- A green **NKA banner** appears: "No Known Allergies"
- The allergy table is not shown
- This is the standard VistA NKA display pattern

---

## Scenario 9: Validation -- Missing Required Fields

### Steps
1. Click the **Record Allergy** tab
2. Leave the Allergen field empty
3. Click **Record Allergy**

### Expected Result
- Red error: "Allergen is required."
- The allergy is not saved

---

## Scenario 10: Pharmacologic Reaction Type

### Steps
1. Click the **Record Allergy** tab
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

---

## Appendix: Clinical Event Sourcing Verification

**Added 2026-04-27** -- Allergy documentation now emits clinical events to the
per-patient event stream (commit f93ede69) and flows to the federation outbox
when enabled.

### Steps

1. Before recording an allergy, capture the patient's current event-stream version:
   ```powershell
   $login = Invoke-RestMethod -Method Post -Uri https://localhost:7127/api/auth/login `
     -Body (@{ username = "DOCTOR1"; password = "smythVista1" } | ConvertTo-Json) `
     -ContentType "application/json"
   $before = Invoke-RestMethod -Method Get `
     -Uri "https://localhost:7127/api/patient/{patientId}/clinical-events?domain=Allergy&max=1" `
     -Headers @{ Authorization = "Bearer $($login.token)" }
   $beforeVersion = if ($before) { $before[0].version } else { 0 }
   ```
2. Record the allergy via the UI (any scenario above).
3. Re-query, filtered to the Allergy domain.

### Expected Result

- One new event with `domain = Allergy` and `version = beforeVersion + 1`.
- Event payload includes the allergen, reaction type, severity, and observed/historical flag.
- Federation outbox row inserted (if outbox enabled) -- visible on [Admin/01 Federation Dashboard](../Admin/01-Federation-Dashboard-Smoke.md).

### Verification Checklist (Event Sourcing)

- [ ] New `Allergy` event appears after recording
- [ ] Event payload contains allergen + reaction details
- [ ] Hash chain still verifies as valid
- [ ] Federation outbox row inserted (if outbox enabled)

Cross-ref: see [Blazor/Admin/08-Clinical-Event-Sourcing.md](../Admin/08-Clinical-Event-Sourcing.md) Part A Scenario 1.
