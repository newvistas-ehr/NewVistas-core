# Allergy Documentation -- Physician CharUI Human Test Script

## Prerequisites

- **Login:** DOCTOR1 / Password: `smythVista1`
- **Security Keys:** PROVIDER, ORES, TIU SIGN, GMRA ALLERGY, GMRV VITALS, GMPL PROBLEM
- **Patient:** Select a patient.
- **Pre-conditions:** SiloHost and WebServer running. Login and select a patient per `01-Login-Patient-Selection.md`.

---

## Scenario 1: List Allergies -- Patient with Allergies (Happy Path)

### Steps

1. At the Main Menu, type: `AL` and press Enter.
2. At the Allergies menu, type: `1` (List Allergies).

### Expected Result

- A table displays with columns: #, Allergen, Severity, Reactions.
- If demo data is loaded, at least one allergy should appear (e.g., Penicillin).
- Example:
  ```
  #  Allergen       Severity   Reactions
  1  PENICILLIN     Severe     RASH, HIVES, ANAPHYLAXIS
  2  SULFA DRUGS    Moderate   RASH
  ```

---

## Scenario 2: List Allergies -- No Known Allergies

### Steps

1. Select a patient with no documented allergies.
2. Navigate to Allergies (AL) and select option 1.

### Expected Result

- The terminal displays: `No Known Allergies (NKA)`

---

## Scenario 3: Record a Drug Allergy (Happy Path)

### Steps

1. At the Allergies menu, type: `2` (Record New Allergy).
2. Enter the following field-by-field:

| Prompt | Value to Enter |
|--------|----------------|
| Allergen | `PENICILLIN` |
| Type (D=Drug, F=Food, O=Other) | `D` |
| Severity (Mild, Moderate, Severe) | `Severe` |
| Reactions (comma-separated) | `RASH, HIVES, ANAPHYLAXIS` |
| Comments (optional) | `Documented anaphylactic reaction in 2018. Carry EpiPen.` |

3. At the confirmation prompt `Save this allergy?`, type: `Y`.

### Expected Result

- The terminal displays: `Allergy recorded: [allergy-ID]`
- Returns to the Allergies menu.
- Verify by listing allergies (option 1) -- PENICILLIN appears with Severe severity and listed reactions.

---

## Scenario 4: Record a Food Allergy

### Steps

1. At the Allergies menu, type: `2` (Record New Allergy).
2. Enter:

| Prompt | Value to Enter |
|--------|----------------|
| Allergen | `SHELLFISH` |
| Type (D=Drug, F=Food, O=Other) | `F` |
| Severity | `Moderate` |
| Reactions (comma-separated) | `HIVES, THROAT SWELLING` |
| Comments (optional) | `Patient reports reaction after eating shrimp` |

3. Confirm: `Y`

### Expected Result

- Allergy recorded with Type = Food.

---

## Scenario 5: Record an Environmental/Other Allergy

### Steps

1. At the Allergies menu, type: `2` (Record New Allergy).
2. Enter:

| Prompt | Value to Enter |
|--------|----------------|
| Allergen | `LATEX` |
| Type (D=Drug, F=Food, O=Other) | `O` |
| Severity | `Mild` |
| Reactions (comma-separated) | `CONTACT DERMATITIS` |
| Comments (optional) | (press Enter to skip) |

3. Confirm: `Y`

### Expected Result

- Allergy recorded with Type = Other, Severity = Mild.

---

## Scenario 6: Record Allergy -- Minimal Fields

### Steps

1. At the Allergies menu, type: `2` (Record New Allergy).
2. Enter:

| Prompt | Value to Enter |
|--------|----------------|
| Allergen | `ASPIRIN` |
| Type (D=Drug, F=Food, O=Other) | (press Enter for default: D) |
| Severity | (press Enter for default: Moderate) |
| Reactions (comma-separated) | `GI UPSET` |
| Comments (optional) | (press Enter to skip) |

3. Confirm: `Y`

### Expected Result

- Allergy recorded with defaults: Type = Drug, Severity = Moderate.

---

## Scenario 7: Record Allergy -- Severe Drug Allergy with Multiple Reactions

### Steps

1. At the Allergies menu, type: `2`.
2. Enter:

| Prompt | Value to Enter |
|--------|----------------|
| Allergen | `CODEINE` |
| Type | `D` |
| Severity | `Severe` |
| Reactions | `NAUSEA, VOMITING, RESPIRATORY DEPRESSION, ALTERED MENTAL STATUS` |
| Comments | `Cross-reactivity concern with all opioid agonists. Pharmacy alert required.` |

3. Confirm: `Y`

### Expected Result

- Allergy recorded with multiple reactions listed.

---

## Scenario 8: Cancel Recording an Allergy

### Steps

1. At the Allergies menu, type: `2` (Record New Allergy).
2. Enter Allergen: `TEST ALLERGEN`.
3. Continue through remaining fields.
4. At the confirmation prompt `Save this allergy?`, type: `N`.

### Expected Result

- The allergy is NOT saved.
- Returns to the Allergies menu.
- Verify "TEST ALLERGEN" does NOT appear in the allergy list.

---

## Scenario 9: Return to Main Menu

### Steps

1. At the Allergies menu, type `Q` or `^` to return.

### Expected Result

- Returns to the Main Menu with patient context preserved.
