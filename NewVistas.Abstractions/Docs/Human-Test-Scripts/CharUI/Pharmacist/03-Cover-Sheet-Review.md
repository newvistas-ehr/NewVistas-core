# Cover Sheet Review -- Pharmacist CharUI Human Test Script

## Prerequisites

- **Login:** PHARM1 / Password: `smythVista1`
- **Security Keys:** PSO PHARMACY, PSJ RPHARM, PSA ORDERS, PSB MANAGER
- **Patient:** Select a patient with demo data loaded.
- **Pre-conditions:** SiloHost and WebServer running. Login and select a patient per `01-Login-Patient-Selection.md`.

---

## Scenario 1: View Cover Sheet (Happy Path)

### Steps

1. At the Main Menu, type: `CV` and press Enter.

### Expected Result

- Cover Sheet displays all 8 sections:
  1. **Active Problems** -- diagnoses relevant to medication therapy decisions
  2. **Allergies** -- **critical for pharmacist review** before dispensing
  3. **Active Medications** -- current med profile for DUR
  4. **Recent Vitals** -- relevant for dose adjustments (e.g., renal function for dose reduction)
  5. **Active Orders** -- pending orders awaiting pharmacist verification
  6. **Recent Notes** -- clinical context for medication decisions
  7. **Active Consults** -- may include pharmacy consult requests
  8. **Upcoming Appointments** -- follow-up scheduling context
- Patient banner at top with CWAD flags (especially "A" for Allergy).

---

## Scenario 2: Cover Sheet -- Focus on Allergy Check

### Steps

1. View Cover Sheet.
2. Focus on the **Allergies** section.
3. Cross-reference with the **Active Medications** section.

### Expected Result

- Both sections visible simultaneously on the cover sheet.
- Pharmacist can identify potential drug-allergy interactions at a glance.
- Example: Patient allergic to PENICILLIN -- check if any penicillin-class antibiotics are in active medications.

---

## Scenario 3: Cover Sheet -- Patient with No Allergies

### Steps

1. Select a patient with no documented allergies.
2. View Cover Sheet.

### Expected Result

- Allergies section shows: "No Known Allergies (NKA)"
- **Pharmacist note:** NKA status should be verified with the patient during counseling.

---

## Scenario 4: Cover Sheet for Empty Patient

### Steps

1. Select a patient with no demo data.
2. View Cover Sheet.

### Expected Result

- All sections show empty state. No errors.

---

## Scenario 5: Return to Main Menu

### Steps

1. After viewing Cover Sheet, return to Main Menu.

### Expected Result

- Main Menu with patient context preserved.
