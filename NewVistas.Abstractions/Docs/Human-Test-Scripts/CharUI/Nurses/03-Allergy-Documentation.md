# Allergy Documentation -- Nurse CharUI Human Test Script

## Prerequisites

- **Login:** NURSE1 / Password: `smythVista1`
- **Security Keys:** ORELSE, GMRV VITALS, GMRA ALLERGY, GMPL PROBLEM, SD SCHEDULING
- **Patient:** Select a patient.
- **Pre-conditions:** SiloHost and WebServer running. Login and select a patient per `01-Login-Patient-Selection.md`.

---

## Scenario 1: List Allergies -- Patient with Allergies (Happy Path)

### Steps

1. At the Main Menu, type: `AL` and press Enter.
2. At the Allergies menu, type: `1` (List Allergies).

### Expected Result

- A table displays: #, Allergen, Severity, Reactions.
- If demo data loaded, at least 1 allergy appears.

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
2. Enter:

| Prompt | Value to Enter |
|--------|----------------|
| Allergen | `MORPHINE` |
| Type (D=Drug, F=Food, O=Other) | `D` |
| Severity (Mild, Moderate, Severe) | `Moderate` |
| Reactions (comma-separated) | `NAUSEA, VOMITING, PRURITUS` |
| Comments (optional) | `Patient reports reaction during prior hospitalization` |

3. At the confirmation prompt `Save this allergy?`, type: `Y`.

### Expected Result

- The terminal displays: `Allergy recorded: [allergy-ID]`
- Verify by listing allergies -- MORPHINE appears.

---

## Scenario 4: Record a Food Allergy

### Steps

1. At the Allergies menu, type: `2`.
2. Enter:

| Prompt | Value to Enter |
|--------|----------------|
| Allergen | `PEANUTS` |
| Type | `F` |
| Severity | `Severe` |
| Reactions | `ANAPHYLAXIS, THROAT SWELLING, URTICARIA` |
| Comments | `Patient carries EpiPen at all times` |

3. Confirm: `Y`

### Expected Result

- Allergy recorded with Type = Food, Severity = Severe.

---

## Scenario 5: Record an Environmental/Other Allergy

### Steps

1. At the Allergies menu, type: `2`.
2. Enter:

| Prompt | Value to Enter |
|--------|----------------|
| Allergen | `IODINE CONTRAST DYE` |
| Type | `O` |
| Severity | `Moderate` |
| Reactions | `HIVES, FLUSHING` |
| Comments | `Pre-medicate with diphenhydramine and prednisone before contrast studies` |

3. Confirm: `Y`

### Expected Result

- Allergy recorded with Type = Other.

---

## Scenario 6: Record Allergy -- Minimal Fields (Defaults)

### Steps

1. At the Allergies menu, type: `2`.
2. Enter:

| Prompt | Value to Enter |
|--------|----------------|
| Allergen | `AMOXICILLIN` |
| Type | (press Enter for default: D) |
| Severity | (press Enter for default: Moderate) |
| Reactions | `RASH` |
| Comments | (press Enter to skip) |

3. Confirm: `Y`

### Expected Result

- Allergy recorded with defaults: Type = Drug, Severity = Moderate, no comments.

---

## Scenario 7: Cancel Recording an Allergy

### Steps

1. At the Allergies menu, type: `2`.
2. Enter Allergen: `TEST`.
3. Fill in remaining fields.
4. At the confirmation prompt `Save this allergy?`, type: `N`.

### Expected Result

- The allergy is NOT saved.
- "TEST" does not appear in the allergy list.

---

## Scenario 8: Verify Allergy Appears on Cover Sheet and Demographics

### Steps

1. Record a new allergy (as in Scenario 3).
2. Return to Main Menu.
3. View Cover Sheet (type: `CV`).
4. View Demographics (type: `DM`).

### Expected Result

- Cover Sheet: Allergies section shows the new allergy.
- Demographics: CWAD flags show "A" (Allergy = YES).
- Demographics: Allergies section lists the allergen with severity and reactions.

---

## Scenario 9: Return to Main Menu

### Steps

1. At the Allergies menu, type `Q` or `^`.

### Expected Result

- Returns to the Main Menu with patient context preserved.
