# Vital Signs Recording -- Nurse CharUI Human Test Script

## Prerequisites

- **Login:** NURSE1 / Password: `smythVista1`
- **Security Keys:** ORELSE, GMRV VITALS, GMRA ALLERGY, GMPL PROBLEM, SD SCHEDULING
- **Patient:** Select a patient with demo data loaded.
- **Pre-conditions:** SiloHost and WebServer running. Login and select a patient per `01-Login-Patient-Selection.md`.

---

## Scenario 1: View Latest Vitals (Happy Path)

### Steps

1. At the Main Menu, type: `VT` and press Enter.
2. At the Vitals menu, type: `1` (View Latest Vitals).

### Expected Result

- A formatted list showing the most recent vital sign per type:
  ```
  Temperature      98.6     F                    03/31/2026 14:00
  Pulse            72       /min                 03/31/2026 14:00
  Respiration      16       /min                 03/31/2026 14:00
  Blood Pressure   128/82   mmHg                 03/31/2026 14:00
  SpO2             97       %                    03/31/2026 14:00
  Pain             3        /10                  03/31/2026 14:00
  Weight           185      lb                   03/31/2026 14:00
  Height           70       in                   03/31/2026 14:00
  ```
- Abnormal values flagged with `*H*` or `*L*`.

---

## Scenario 2: Record Full Vital Signs Set -- Normal Values (Happy Path)

### Steps

1. At the Vitals menu, type: `2` (Record Vitals).
2. The terminal displays: `Enter values. Press Enter to skip a measurement.`
3. Enter each vital:

| Prompt | Value to Enter |
|--------|----------------|
| Temperature (F) | `98.4` |
| Pulse | `68` |
| Respiration | `14` |
| Blood Pressure (systolic/diastolic) | `118/76` |
| SpO2 (%) | `99` |
| Pain (0-10) | `0` |
| Weight (lb) | `172` |
| Height (in) | `68` |

4. Review the summary:
   ```
   Values to record:
     Temperature              98.4
     Pulse                    68
     Respiration              14
     Blood Pressure           118/76
     SpO2                     99
     Pain                     0
     Weight                   172
     Height                   68
   ```
5. At the confirmation prompt `Save vitals?`, type: `Y`.

### Expected Result

- The terminal displays: `Vitals recorded successfully.`
- Verify with option 1 -- all values appear with current date/time.

---

## Scenario 3: Record Vitals -- Abnormal/Critical Values

### Steps

1. At the Vitals menu, type: `2` (Record Vitals).
2. Enter abnormal values simulating a deteriorating patient:

| Prompt | Value to Enter |
|--------|----------------|
| Temperature (F) | `103.8` |
| Pulse | `125` |
| Respiration | `30` |
| Blood Pressure (systolic/diastolic) | `82/48` |
| SpO2 (%) | `86` |
| Pain (0-10) | `10` |
| Weight (lb) | (press Enter to skip) |
| Height (in) | (press Enter to skip) |

3. Confirm: `Y`

### Expected Result

- Vitals recorded successfully.
- When viewing vitals (option 1), abnormal flags appear:
  - Temperature 103.8 `*H*` (critical high)
  - Pulse 125 `*H*`
  - Respiration 30 `*H*`
  - Blood Pressure 82/48 `*L*` (hypotension)
  - SpO2 86 `*L*` (critical low)
  - Pain 10 `*H*`

---

## Scenario 4: Record Vitals -- Partial Set (Common Nursing Scenario)

### Steps

1. At the Vitals menu, type: `2`.
2. Enter only the vitals taken during a routine check:

| Prompt | Value to Enter |
|--------|----------------|
| Temperature (F) | `98.6` |
| Pulse | `76` |
| Respiration | `18` |
| Blood Pressure | `130/84` |
| SpO2 (%) | `96` |
| Pain (0-10) | `4` |
| Weight (lb) | (press Enter to skip) |
| Height (in) | (press Enter to skip) |

3. Summary shows 6 values (Weight and Height skipped).
4. Confirm: `Y`

### Expected Result

- Only the 6 entered vitals are recorded.
- Weight and Height retain their previous values.

---

## Scenario 5: Record Vitals -- Single Measurement (Quick Check)

### Steps

1. At the Vitals menu, type: `2`.
2. Skip all except Blood Pressure:

| Prompt | Value to Enter |
|--------|----------------|
| Temperature (F) | (Enter to skip) |
| Pulse | (Enter to skip) |
| Respiration | (Enter to skip) |
| Blood Pressure | `145/92` |
| SpO2 (%) | (Enter to skip) |
| Pain (0-10) | (Enter to skip) |
| Weight (lb) | (Enter to skip) |
| Height (in) | (Enter to skip) |

3. Summary shows only Blood Pressure.
4. Confirm: `Y`

### Expected Result

- Only blood pressure recorded.

---

## Scenario 6: Record Vitals -- Post-Medication Recheck

### Steps

1. Record initial vitals (Blood Pressure: `168/98`, Pulse: `96`).
2. Wait or note the time.
3. Record follow-up vitals 30 minutes later (Blood Pressure: `142/88`, Pulse: `82`).

### Expected Result

- Both sets of vitals are recorded with different timestamps.
- Latest vitals view (option 1) shows the most recent values.

---

## Scenario 7: Cancel Recording Vitals

### Steps

1. At the Vitals menu, type: `2`.
2. Enter several vital values.
3. At the confirmation prompt `Save vitals?`, type: `N`.

### Expected Result

- No vitals are saved.
- Returns to the Vitals menu.
- Previous vital values remain unchanged.

---

## Scenario 8: Record Vitals -- Skip All Measurements

### Steps

1. At the Vitals menu, type: `2`.
2. Press Enter at every prompt to skip all.

### Expected Result

- Summary shows no values.
- No confirmation prompt (nothing to save) or system indicates nothing to record.
- Returns to the Vitals menu.

---

## Scenario 9: Verify Vitals Appear on Cover Sheet

### Steps

1. Record a full set of vitals (as in Scenario 2).
2. Return to Main Menu.
3. View Cover Sheet (type: `CV`).

### Expected Result

- The "Recent Vitals" section on the cover sheet shows the vitals just recorded.
- Values match what was entered.

---

## Scenario 10: Return to Main Menu

### Steps

1. At the Vitals menu, type `Q` or `^`.

### Expected Result

- Returns to the Main Menu with patient context preserved.
