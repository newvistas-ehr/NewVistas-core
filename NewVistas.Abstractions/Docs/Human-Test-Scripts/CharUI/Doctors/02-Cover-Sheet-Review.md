# Cover Sheet Review -- Physician CharUI Human Test Script

## Prerequisites

- **Login:** DOCTOR1 / Password: `smythVista1`
- **Patient:** Select any patient with demo data (e.g., search for patient by name)
- **Pre-conditions:**
  1. SiloHost and WebServer are running.
  2. Demo data is loaded for the selected patient.
  3. Login as DOCTOR1 and select a patient (see `01-Login-Patient-Selection.md`).

---

## Scenario 1: View Cover Sheet with Full Data (Happy Path)

### Steps

1. At the Main Menu, type: `CV` and press Enter.

### Expected Result

- The Cover Sheet displays 8 sections in order:

1. **Active Problems** table:
   | # | ICD | Problem | Status | Onset |
   Expect at least 1 row if demo data loaded. Each row shows the ICD-10 code, diagnosis, ACTIVE status, and onset date.

2. **Allergies** table:
   | # | Allergen | Severity | Reactions |
   Expect at least 1 allergy entry (e.g., Penicillin). If none documented, shows "No Known Allergies (NKA)".

3. **Active Medications** table:
   | # | Drug | Sig | Status |
   Shows current prescriptions with drug name, sig (instructions), and status.

4. **Recent Vitals** list:
   ```
   Temperature      98.6     F                    03/31/2026 14:00
   Blood Pressure   120/80   mmHg                 03/31/2026 14:00
   ```
   Each vital shows type, value, units, abnormal flag (if any), and date/time.

5. **Active Orders** table:
   | # | Order | Type | Status | Date |
   Shows pending/active orders.

6. **Recent Notes** table:
   | # | Type | Author | Status | Date |
   Shows recent TIU documents.

7. **Active Consults** table:
   | # | To Service | Status | Urgency | Date |
   Shows pending/active consultation requests.

8. **Upcoming Appointments** table:
   | # | Date/Time | Clinic | Provider | Status |
   Shows future scheduled appointments.

- After displaying all sections, the menu returns to the Cover Sheet action prompt or Main Menu.

---

## Scenario 2: Cover Sheet with No Data (Empty Patient)

### Steps

1. Select a new patient with no demo data loaded (use `SP` to select a patient that has not been seeded).
2. At the Main Menu, type: `CV` and press Enter.

### Expected Result

- Each section displays its empty-state message:
  - Active Problems: "(none)" or empty table
  - Allergies: "No Known Allergies (NKA)"
  - Active Medications: "(none)"
  - Recent Vitals: "(none)"
  - Active Orders: "(none)"
  - Recent Notes: "(none)"
  - Active Consults: "(none)"
  - Upcoming Appointments: "(none)"

---

## Scenario 3: Cover Sheet Shows Patient Banner

### Steps

1. Select a patient who is admitted (has ADT admission record) and is service-connected.
2. At the Main Menu, type: `CV` and press Enter.

### Expected Result

- The patient banner at the top of the cover sheet shows:
  - Patient name
  - Sex (M/F)
  - Age
  - DOB
  - SSN last 4
  - If admitted: Admission status with room/bed
  - If service-connected: SC percentage
  - CWAD flags if applicable (C=Crisis, W=Warning, A=Allergy, D=Advance Directive)

---

## Scenario 4: Cover Sheet with Abnormal Lab Values

### Steps

1. Pre-condition: Patient has lab results with abnormal flags (e.g., high potassium, low hemoglobin) from demo data.
2. View the cover sheet.

### Expected Result

- In the Recent Vitals or lab data sections, abnormal values display with flag indicators (e.g., `*H*` for high, `*L*` for low).
- Values are clearly distinguishable from normal results.

---

## Scenario 5: Return to Main Menu from Cover Sheet

### Steps

1. After viewing the cover sheet, press Enter or type the quit/back command.

### Expected Result

- The Main Menu reappears.
- The patient context remains selected (patient banner still visible).
