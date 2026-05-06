# Medications Review -- Pharmacist CharUI Human Test Script

## Prerequisites

- **Login:** PHARM1 / Password: `smythVista1`
- **Security Keys:** PSO PHARMACY, PSJ RPHARM, PSA ORDERS, PSB MANAGER
- **Patient:** Select a patient with demo data loaded (medications seeded).
- **Pre-conditions:**
  1. SiloHost and WebServer running.
  2. Demo outpatient pharmacy data loaded: `POST /api/outpatientpharmacy/demo/load?patientId={patientId}`
  3. Login and select a patient per `01-Login-Patient-Selection.md`.

---

## Scenario 1: List Active Medications (Happy Path)

### Steps

1. At the Main Menu, type: `ME` and press Enter.
2. At the Medications menu, type: `1` (List Active Medications).

### Expected Result

- A table displays with columns: #, Drug, Sig, Status, Fill Date, Refills.
- Demo data should show medications such as:
  ```
  #  Drug                    Sig                          Status  Fill Date     Refills
  1  LISINOPRIL 10MG TAB     TAKE ONE TABLET PO DAILY     ACTIVE  03/01/2026    5
  2  METFORMIN 500MG TAB     TAKE ONE TABLET PO BID       ACTIVE  03/01/2026    11
  3  ATORVASTATIN 40MG TAB   TAKE ONE TABLET PO QHS       ACTIVE  03/01/2026    3
  ```
- **Pharmacist review focus:** Verify drug name, dosage, route, frequency, and refill counts.

---

## Scenario 2: Review Medications for Drug Interactions

### Steps

1. List active medications (option 1).
2. Note all drug names and dosages.
3. Cross-reference with allergies:
   - Return to Main Menu, type `AL`, then `1` to list allergies.
   - Check for drug-allergy conflicts.
4. Review drug-drug interaction potential:
   - Note: The CharUI Medications menu is read-only. For full interaction checking, use the Blazor UI at `/druginteractions` or the API.

### Expected Result

- Medication list and allergy list both visible for manual cross-check.
- **Pharmacist workflow note:** In a production workflow, the pharmacist would use the full pharmacy system (Blazor UI) for DUR screening. The CharUI provides a quick reference view.

---

## Scenario 3: Review Medications for Formulary Compliance

### Steps

1. List active medications.
2. Note any non-formulary or restricted medications.
3. For full formulary lookup, use the Blazor UI at `/drugformulary` or the API: `GET /api/drugformulary/products/search?query={drugName}`.

### Expected Result

- Medication list visible for quick formulary review.

---

## Scenario 4: No Active Medications

### Steps

1. Select a patient with no medications.
2. Navigate to Medications (ME) and select option 1.

### Expected Result

- The terminal displays: `(none)`

---

## Scenario 5: Return to Main Menu

### Steps

1. At the Medications menu, type `Q` or `^`.

### Expected Result

- Returns to the Main Menu.
