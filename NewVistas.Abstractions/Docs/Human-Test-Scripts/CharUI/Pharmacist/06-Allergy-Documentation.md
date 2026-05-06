# Allergy Documentation -- Pharmacist CharUI Human Test Script

## Prerequisites

- **Login:** PHARM1 / Password: `smythVista1`
- **Security Keys:** PSO PHARMACY, PSJ RPHARM, PSA ORDERS, PSB MANAGER
- **Patient:** Select a patient.
- **Pre-conditions:** SiloHost and WebServer running. Login and select a patient per `01-Login-Patient-Selection.md`.

---

## Scenario 1: List Allergies (Happy Path)

### Steps

1. At the Main Menu, type: `AL` and press Enter.
2. At the Allergies menu, type: `1` (List Allergies).

### Expected Result

- Table displays: #, Allergen, Severity, Reactions.
- **Pharmacist focus:** Allergy information is critical before dispensing. Verify all documented allergies against the medication profile.

---

## Scenario 2: List Allergies -- NKA

### Steps

1. Select a patient with no allergies.
2. Navigate to Allergies (AL) and list.

### Expected Result

- `No Known Allergies (NKA)`
- **Pharmacist note:** NKA should be actively confirmed with the patient during counseling, especially for new prescriptions.

---

## Scenario 3: Record a Drug Allergy (Happy Path)

### Steps

1. At the Allergies menu, type: `2` (Record New Allergy).
2. Enter:

| Prompt | Value to Enter |
|--------|----------------|
| Allergen | `SULFONAMIDES` |
| Type (D=Drug, F=Food, O=Other) | `D` |
| Severity (Mild, Moderate, Severe) | `Severe` |
| Reactions (comma-separated) | `STEVENS-JOHNSON SYNDROME, RASH, FEVER` |
| Comments (optional) | `Documented SJS reaction in 2020. Cross-sensitivity with thiazide diuretics should be considered.` |

3. Confirm: `Y`

### Expected Result

- `Allergy recorded: [allergy-ID]`
- **Pharmacist workflow:** Document drug allergies discovered during medication reconciliation or patient counseling.

---

## Scenario 4: Record a Drug Class Allergy

### Steps

1. At the Allergies menu, type: `2`.
2. Enter:

| Prompt | Value to Enter |
|--------|----------------|
| Allergen | `NSAIDs (Non-Steroidal Anti-Inflammatory Drugs)` |
| Type | `D` |
| Severity | `Moderate` |
| Reactions | `BRONCHOSPASM, URTICARIA` |
| Comments | `Class allergy - avoid all NSAIDs including ibuprofen, naproxen, ketorolac. ASA 81mg tolerated per cardiology.` |

3. Confirm: `Y`

### Expected Result

- Drug class allergy documented with cross-sensitivity notes.

---

## Scenario 5: Record a Contrast/Excipient Allergy

### Steps

1. At the Allergies menu, type: `2`.
2. Enter:

| Prompt | Value to Enter |
|--------|----------------|
| Allergen | `IODINATED CONTRAST DYE` |
| Type | `O` |
| Severity | `Moderate` |
| Reactions | `HIVES, FLUSHING, NAUSEA` |
| Comments | `Pre-medicate per protocol: Prednisone 50mg PO 13h, 7h, 1h prior + Diphenhydramine 50mg PO 1h prior to contrast study.` |

3. Confirm: `Y`

### Expected Result

- Allergy recorded with detailed pre-medication protocol in comments.

---

## Scenario 6: Record Allergy -- Minimal Fields

### Steps

1. At the Allergies menu, type: `2`.
2. Enter:

| Prompt | Value to Enter |
|--------|----------------|
| Allergen | `AMOXICILLIN` |
| Type | (Enter for default: D) |
| Severity | (Enter for default: Moderate) |
| Reactions | `RASH` |
| Comments | (Enter to skip) |

3. Confirm: `Y`

### Expected Result

- Allergy recorded with defaults.

---

## Scenario 7: Cancel Recording an Allergy

### Steps

1. At the Allergies menu, type: `2`.
2. Fill in fields.
3. At confirmation, type: `N`.

### Expected Result

- Allergy NOT saved.

---

## Scenario 8: Return to Main Menu

### Steps

1. At the Allergies menu, type `Q` or `^`.

### Expected Result

- Returns to the Main Menu.
