# Clinical Notes (Access Denied Scenarios) -- Nurse CharUI Human Test Script

## Prerequisites

- **Login:** NURSE1 / Password: `smythVista1`
- **Security Keys:** ORELSE, GMRV VITALS, GMRA ALLERGY, GMPL PROBLEM, SD SCHEDULING
- **Keys NOT held:** PROVIDER (cannot write notes), TIU SIGN (cannot sign), TIU COSIGN (cannot cosign)
- **Patient:** Select a patient with demo data loaded (notes should exist).
- **Pre-conditions:** SiloHost and WebServer running. Login and select a patient per `01-Login-Patient-Selection.md`.

---

## Scenario 1: List Recent Notes (Permitted -- Read-Only)

### Steps

1. At the Main Menu, type: `NO` and press Enter.
2. At the Notes menu, type: `1` (List Recent Notes).

### Expected Result

- Table displays: #, Type, Subject, Author, Status, Date.
- Nurses CAN view all notes -- no restriction on reading.

---

## Scenario 2: View Note Detail (Permitted -- Read-Only)

### Steps

1. At the Notes menu, type: `2` (View Note Detail).
2. Select a note by number.

### Expected Result

- Full note detail displayed including report text.
- Nurses CAN view note content.

---

## Scenario 3: Attempt to Write a New Note -- ACCESS DENIED

### Steps

1. At the Notes menu, type: `3` (Write New Note).

### Expected Result

- The terminal displays: `You do not hold the PROVIDER key. Note entry is not permitted.`
- Returns to the Notes menu.
- **No prompts for Document Type, Subject, or text appear.**

---

## Scenario 4: Attempt to Sign a Note -- ACCESS DENIED

### Steps

1. At the Notes menu, type: `4` (Sign Note).

### Expected Result

- The terminal displays: `You do not hold the TIU SIGN key.`
- Returns to the Notes menu.
- **No list of unsigned notes appears.**

---

## Scenario 5: Attempt to Cosign a Note -- ACCESS DENIED

### Steps

1. At the Notes menu, type: `5` (Cosign Note).

### Expected Result

- The terminal displays: `You do not hold the TIU COSIGN key.`
- Returns to the Notes menu.

---

## Scenario 6: Attempt to Add an Addendum -- ACCESS DENIED

### Steps

1. At the Notes menu, type: `6` (Add Addendum).

### Expected Result

- The terminal displays: `You do not hold the PROVIDER key.`
- Returns to the Notes menu.

---

## Scenario 7: Attempt to Amend a Note -- ACCESS DENIED

### Steps

1. At the Notes menu, type: `7` (Amend Note).

### Expected Result

- The terminal displays: `You do not hold the PROVIDER key.`
- Returns to the Notes menu.

---

## Scenario 8: Verify All Write Operations Are Denied (Summary)

### Steps

1. Attempt each write operation in sequence:
   - Option 3: Write New Note
   - Option 4: Sign Note
   - Option 5: Cosign Note
   - Option 6: Add Addendum
   - Option 7: Amend Note

### Expected Result

| Option | Key Required | Nurse Has? | Expected Message |
|--------|-------------|------------|------------------|
| 3 | PROVIDER | No | `You do not hold the PROVIDER key. Note entry is not permitted.` |
| 4 | TIU SIGN | No | `You do not hold the TIU SIGN key.` |
| 5 | TIU COSIGN | No | `You do not hold the TIU COSIGN key.` |
| 6 | PROVIDER | No | `You do not hold the PROVIDER key.` |
| 7 | PROVIDER | No | `You do not hold the PROVIDER key.` |

- All 5 operations are denied.
- No clinical data is modified.
- The Notes menu continues to function normally for read operations (options 1 and 2).

---

## Scenario 9: Discharge Summary Access Denied

### Steps

1. At the Main Menu, type: `DC` (D/C Summaries).
2. Verify read access:
   - Type `1` (List D/C Summaries) -- should work.
   - Type `2` (View Summary Detail) -- should work.
3. Attempt write operations:
   - Type `3` (Create New D/C Summary).
   - Type `4` (Sign Summary).

### Expected Result

- Options 1 and 2: Permitted (read-only).
- Option 3: `You do not hold the PROVIDER key.`
- Option 4: `You do not hold the TIU SIGN key.`

---

## Scenario 10: Encounter Creation Access Denied

### Steps

1. At the Main Menu, type: `EN` (Encounter/PCE).
2. Verify read access:
   - Type `1` (List Encounters) -- should work.
   - Type `2` (View Encounter Detail) -- should work.
3. Attempt to create:
   - Type `3` (Create New Encounter).

### Expected Result

- Options 1, 2: Permitted.
- Option 3: `You do not hold the PROVIDER key.`
- **Note:** Options 4 (Add Diagnosis), 5 (Add Procedure), and 6 (Check Out) do NOT require the PROVIDER key and should be accessible to nurses.

---

## Scenario 11: Return to Main Menu

### Steps

1. At the Notes menu, type `Q` or `^`.

### Expected Result

- Returns to the Main Menu.
