# Vital Signs -- Physician CharUI Human Test Script

## Prerequisites

- **Login:** DOCTOR1 / Password: `smythVista1`
- **Security Keys:** PROVIDER, ORES, TIU SIGN, GMRA ALLERGY, GMRV VITALS, GMPL PROBLEM
- **Patient:** Select a patient with demo data loaded.
- **Pre-conditions:** SiloHost and WebServer running. Login and select a patient per `01-Login-Patient-Selection.md`.

---

## Scenario 1: View Latest Vitals (Happy Path)

### Steps

1. At the Main Menu, type: `VT` and press Enter.
2. At the Vitals menu, type: `1` (View Latest Vitals).

### Expected Result

- A formatted list of the most recent vital signs:
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
- Abnormal values show with flag indicators (e.g., `*H*` or `*L*`).
- Only the most recent measurement per vital type is shown.

---

## Scenario 2: View Vitals -- No Data

### Steps

1. Select a patient with no recorded vitals.
2. Navigate to Vitals (VT) and select option 1.

### Expected Result

- "(none)" or an empty display indicating no vitals on file.

---

## Scenario 3: Record Vitals -- Full Set (Happy Path)

### Steps

1. At the Vitals menu, type: `2` (Record Vitals).
2. The terminal displays: `Enter values. Press Enter to skip a measurement.`
3. Enter each vital sign:

| Prompt | Value to Enter |
|--------|----------------|
| Temperature (F) | `98.6` |
| Pulse | `72` |
| Respiration | `16` |
| Blood Pressure (systolic/diastolic) | `128/82` |
| SpO2 (%) | `97` |
| Pain (0-10) | `3` |
| Weight (lb) | `185` |
| Height (in) | `70` |

4. The terminal displays a summary:
   ```
   Values to record:
     Temperature              98.6
     Pulse                    72
     Respiration              16
     Blood Pressure           128/82
     SpO2                     97
     Pain                     3
     Weight                   185
     Height                   70
   ```
5. At the confirmation prompt `Save vitals?`, type: `Y`.

### Expected Result

- The terminal displays: `Vitals recorded successfully.`
- Verify by viewing latest vitals (option 1) -- all 8 measurements appear with current date/time.

---

## Scenario 4: Record Vitals -- Partial Set (Skip Some)

### Steps

1. At the Vitals menu, type: `2` (Record Vitals).
2. Enter only some vitals, skip others:

| Prompt | Value to Enter |
|--------|----------------|
| Temperature (F) | `101.2` |
| Pulse | `110` |
| Respiration | `22` |
| Blood Pressure | `90/60` |
| SpO2 (%) | `91` |
| Pain (0-10) | (press Enter to skip) |
| Weight (lb) | (press Enter to skip) |
| Height (in) | (press Enter to skip) |

3. The summary shows only the entered values.
4. Confirm: `Y`

### Expected Result

- Only the 5 entered vitals are recorded.
- Skipped measurements are not saved.
- View latest vitals shows the new values for Temperature, Pulse, Respiration, BP, and SpO2. Weight and Height retain their previous values (if any).

---

## Scenario 5: Record Vitals -- Abnormal Values

### Steps

1. At the Vitals menu, type: `2` (Record Vitals).
2. Enter abnormal values:

| Prompt | Value to Enter |
|--------|----------------|
| Temperature (F) | `103.1` |
| Pulse | `130` |
| Respiration | `28` |
| Blood Pressure | `85/50` |
| SpO2 (%) | `88` |
| Pain (0-10) | `9` |
| Weight (lb) | (press Enter to skip) |
| Height (in) | (press Enter to skip) |

3. Confirm: `Y`

### Expected Result

- Vitals recorded successfully.
- When viewing latest vitals, abnormal values display with flag indicators:
  - Temperature 103.1 `*H*`
  - Pulse 130 `*H*`
  - Respiration 28 `*H*`
  - Blood Pressure 85/50 `*L*`
  - SpO2 88 `*L*`
  - Pain 9 `*H*`

---

## Scenario 6: Record Vitals -- Single Measurement

### Steps

1. At the Vitals menu, type: `2` (Record Vitals).
2. Skip all but one:

| Prompt | Value to Enter |
|--------|----------------|
| Temperature (F) | (press Enter to skip) |
| Pulse | (press Enter to skip) |
| Respiration | (press Enter to skip) |
| Blood Pressure | `142/95` |
| SpO2 (%) | (press Enter to skip) |
| Pain (0-10) | (press Enter to skip) |
| Weight (lb) | (press Enter to skip) |
| Height (in) | (press Enter to skip) |

3. Summary shows only Blood Pressure: 142/95.
4. Confirm: `Y`

### Expected Result

- Only the blood pressure is recorded.

---

## Scenario 7: Cancel Recording Vitals

### Steps

1. At the Vitals menu, type: `2` (Record Vitals).
2. Enter some values.
3. At the confirmation prompt `Save vitals?`, type: `N`.

### Expected Result

- No vitals are saved.
- Returns to the Vitals menu.

---

## Scenario 8: Record Vitals -- Skip All Measurements

### Steps

1. At the Vitals menu, type: `2` (Record Vitals).
2. Press Enter at every prompt (skip all measurements).

### Expected Result

- The summary shows no values to record.
- The system either does not prompt for confirmation or indicates nothing to save.
- Returns to the Vitals menu.

---

## Scenario 9: Return to Main Menu

### Steps

1. At the Vitals menu, type `Q` or `^` to return.

### Expected Result

- Returns to the Main Menu with patient context preserved.
