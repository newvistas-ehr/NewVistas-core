# Medications Review -- Nurse CharUI Human Test Script

## Prerequisites

- **Login:** NURSE1 / Password: `smythVista1`
- **Security Keys:** ORELSE, GMRV VITALS, GMRA ALLERGY, GMPL PROBLEM, SD SCHEDULING
- **Patient:** Select a patient with demo data loaded (medications seeded).
- **Pre-conditions:** SiloHost and WebServer running. Login and select a patient per `01-Login-Patient-Selection.md`.

---

## Scenario 1: List Active Medications (Happy Path)

### Steps

1. At the Main Menu, type: `ME` and press Enter.
2. At the Medications menu, type: `1` (List Active Medications).

### Expected Result

- A table displays with columns: #, Drug, Sig, Status, Fill Date, Refills.
- Shows all active medications for the patient.
- Example:
  ```
  #  Drug                    Sig                          Status  Fill Date     Refills
  1  LISINOPRIL 10MG TAB     TAKE ONE TABLET PO DAILY     ACTIVE  03/01/2026    5
  2  METFORMIN 500MG TAB     TAKE ONE TABLET PO BID       ACTIVE  03/01/2026    11
  3  ATORVASTATIN 40MG TAB   TAKE ONE TABLET PO QHS       ACTIVE  03/01/2026    3
  ```

---

## Scenario 2: Medications -- No Active Medications

### Steps

1. Select a patient with no medications.
2. Navigate to Medications (ME) and select option 1.

### Expected Result

- The terminal displays: `(none)`
- No medication table shown.

---

## Scenario 3: Review Medications Before Administering

### Steps

1. List active medications (option 1).
2. Note the drug names, dosages, and sigs.
3. Cross-reference with the allergies list:
   - Return to Main Menu, type `AL`, then `1` to list allergies.
   - Check for any drug allergies that conflict with active medications.

### Expected Result

- Both medication and allergy lists are viewable.
- Nurse can identify potential drug-allergy conflicts (manual check).
- **Note:** This is a read-only workflow. The CharUI Medications menu does not have write operations.

---

## Scenario 4: Return to Main Menu

### Steps

1. At the Medications menu, type `Q` or `^`.

### Expected Result

- Returns to the Main Menu with patient context preserved.
