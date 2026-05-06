# Nursing Assessment -- Human Test Script

## Prerequisites

- **Login:** NURSE1 / Password: `smythVista1`
- **Patient:** 16
- **Pre-conditions:**
  1. The SiloHost, WebServer, and BlazorWeb projects must all be running.
  2. Navigate to `/nursing` in the browser.
  3. Enter Patient ID `16` in the Patient ID field and click **Load Patient**.
  4. The page loads with 4 tabs: Assessments, Care Plan, Acuity, Unit Census.
  5. If no prior assessments exist, the Assessments tab shows "No assessments recorded."

---

## Scenario 1: Initial/Admission Assessment (Happy Path -- All Body Systems)

### Steps

1. Navigate to `/nursing`.
2. Enter Patient ID: `16`
3. Click **Load Patient**.
4. On the **Assessments** tab, scroll down to the "Record New Assessment" section.
5. Fill in all fields:
   - Type: `Initial`
   - Nurse ID: `NURSE1`
   - Nurse Name: `JOHNSON,MARY R`
   - Location: `Ward 3A Room 301`
   - LOC (AVPU): `Alert`
   - Breath Sounds: `Clear`
   - O2 Therapy: `RoomAir`
   - SpO2 (%): `97`
   - Heart Rhythm: `Regular`
   - Edema: `None`
   - Skin Integrity: `Intact`
   - Braden Score (6-23): `18`
   - Pain Score (0-10): `3`
   - Pain Location: `Right lower abdomen`
   - Bowel Sounds: `Active`
   - Appetite: `Fair`
   - Urine Output (mL): `250`
   - Foley Catheter: `No`
   - Anxiety: `Mild`
   - Mood: `Anxious`
   - Morse Score (0-125): `35`
   - Fall Risk: `Moderate`
   - Mobility: `AssistedDevice`
   - Narrative Notes: `New admission from ED. Patient is alert and oriented x4. Ambulates with rolling walker. Right lower abdominal tenderness noted. IV access established in left forearm, 20g. Fall precautions initiated per Morse score 35.`
6. Click **Record Assessment**.

### Expected Result

- Green success banner appears.
- The Assessments table now shows one row:
  - Date/Time: current date/time
  - Type: Initial
  - Nurse: JOHNSON,MARY R
  - Status: Draft
  - Pain: 3/10
  - Morse: 35
  - Braden: 18
- Click the assessment row to expand the detail view. Verify all fields match:
  - LOC: Alert
  - Orientation: (empty or as set)
  - Breath Sounds: Clear
  - O2 Therapy: RoomAir
  - SpO2: 97.0%
  - Heart Rhythm: Regular
  - Edema: None
  - Skin: Intact
  - Braden Score: 18
  - Pain: 3/10 Right lower abdomen
  - Bowel Sounds: Active
  - Appetite: Fair
  - Urine Output: 250 mL
  - Foley: No
  - Anxiety: Mild
  - Mood: Anxious
  - Morse Score: 35 -- Moderate
  - Mobility: AssistedDevice
  - Narrative Notes displayed below detail grid.

---

## Scenario 2: Focused Assessment (Respiratory Only)

### Steps

1. Scroll down to the "Record New Assessment" section.
2. Fill in:
   - Type: `Focused`
   - Nurse ID: `NURSE1`
   - Nurse Name: `JOHNSON,MARY R`
   - Location: `Ward 3A Room 301`
   - LOC (AVPU): `Alert`
   - Breath Sounds: `Wheezes`
   - O2 Therapy: `NasalCannula`
   - SpO2 (%): `92`
   - Heart Rhythm: (leave as `--`)
   - Edema: (leave as `--`)
   - Skin Integrity: (leave as `--`)
   - Braden Score: (leave blank)
   - Pain Score (0-10): `2`
   - Pain Location: `Chest, bilateral`
   - Bowel Sounds: (leave as `--`)
   - Appetite: (leave as `--`)
   - Urine Output: (leave blank)
   - Foley Catheter: `No`
   - Anxiety: `Moderate`
   - Mood: `Anxious`
   - Morse Score: (leave blank)
   - Fall Risk: (leave as `--`)
   - Mobility: (leave as `--`)
   - Narrative Notes: `Focused respiratory assessment. Patient reports increased SOB over past 2 hours. Bilateral wheezes auscultated in all lung fields. SpO2 92% on room air, O2 initiated at 2L/min via NC. SpO2 improved to 95% on supplemental O2. MD notified.`
3. Click **Record Assessment**.

### Expected Result

- Assessment recorded successfully.
- The Assessments table now shows 2 rows. The newest entry:
  - Type: Focused
  - Pain: 2/10
  - Morse: -- (blank)
  - Braden: -- (blank)
- Click the Focused assessment row. Detail shows:
  - Breath Sounds: Wheezes
  - O2 Therapy: NasalCannula
  - SpO2: 92.0%
  - Other systems show "--" for fields left blank.

---

## Scenario 3: Shift Assessment (Abbreviated)

### Steps

1. Scroll down to "Record New Assessment".
2. Fill in:
   - Type: `Shift`
   - Nurse ID: `NURSE1`
   - Nurse Name: `JOHNSON,MARY R`
   - Location: `Ward 3A Room 301`
   - LOC (AVPU): `Alert`
   - Breath Sounds: `Clear`
   - O2 Therapy: `RoomAir`
   - SpO2 (%): `97`
   - Heart Rhythm: `Regular`
   - Edema: `1+`
   - Skin Integrity: `Intact`
   - Braden Score: `18`
   - Pain Score: `2`
   - Pain Location: `Right lower abdomen`
   - Bowel Sounds: `Hypoactive`
   - Appetite: `Poor`
   - Urine Output: `180`
   - Foley Catheter: `No`
   - Anxiety: `None`
   - Mood: `Calm`
   - Morse Score: `35`
   - Fall Risk: `Moderate`
   - Mobility: `AssistedDevice`
   - Narrative Notes: `Shift assessment. Patient resting comfortably. 1+ pedal edema noted bilateral LE -- new finding. Bowel sounds hypoactive, last BM 2 days ago. Appetite poor, ate 25% of dinner tray. Continue fall precautions and I&O monitoring.`
3. Click **Record Assessment**.

### Expected Result

- Assessment recorded successfully.
- The Assessments table now shows 3 rows. Newest entry:
  - Type: Shift
  - Pain: 2/10
  - Morse: 35
  - Braden: 18

---

## Scenario 4: Sign an Assessment

### Steps

1. In the Assessments table, click the row for the **Initial** assessment from Scenario 1.
2. The detail card expands showing all assessment fields.
3. Verify the Status shows **Draft**.
4. Click the **Sign Assessment** button (only visible when Status is Draft).

### Expected Result

- The assessment detail reloads.
- Status changes from Draft to **Signed**.
- The **Sign Assessment** button is no longer visible.
- In the Assessments table, the row now shows Status: Signed (the row may have a different styling class, `row-signed`).

---

## Scenario 5: Assessment with Critical Findings

### Steps

1. Record a new assessment:
   - Type: `PRN`
   - Nurse ID: `NURSE1`
   - Nurse Name: `JOHNSON,MARY R`
   - Location: `Ward 3A Room 301`
   - LOC (AVPU): `Pain`
   - Breath Sounds: `Crackles`
   - O2 Therapy: `NonRebreather`
   - SpO2 (%): `86`
   - Heart Rhythm: `Irregular`
   - Edema: `3+`
   - Skin Integrity: `Impaired`
   - Braden Score: `12`
   - Pain Score: `9`
   - Pain Location: `Chest, substernal`
   - Bowel Sounds: `Absent`
   - Appetite: `NPO`
   - Urine Output: `50`
   - Foley Catheter: `Yes`
   - Anxiety: `Severe`
   - Mood: `Agitated`
   - Morse Score: `85`
   - Fall Risk: `High`
   - Mobility: `Bedrest`
   - Narrative Notes: `Rapid response called. Patient found with acute change in mental status -- responds only to painful stimuli. Bibasilar crackles, SpO2 86% on NRB 15L. Irregular heart rhythm on monitor. Foley placed, urine output 50mL in 4 hours. MD at bedside. Anticipate transfer to ICU.`
2. Click **Record Assessment**.

### Expected Result

- Assessment recorded with all critical values.
- The Assessments table shows the PRN assessment with:
  - Pain: 9/10
  - Morse: 85
  - Braden: 12
- Detail view shows critical values:
  - LOC: Pain (not Alert)
  - SpO2: 86%
  - Braden: 12 (high risk for pressure injury)
  - Morse: 85 (high fall risk)

---

## Reference: Assessment Scoring Scales

### Braden Scale (Pressure Injury Risk)
| Score   | Risk Level           |
|---------|---------------------|
| 19-23   | No risk             |
| 15-18   | Mild risk           |
| 13-14   | Moderate risk       |
| 10-12   | High risk           |
| 6-9     | Very high risk      |

### Morse Fall Scale
| Score   | Risk Level    |
|---------|--------------|
| 0-24    | Low risk      |
| 25-50   | Moderate risk |
| 51+     | High risk     |

### AVPU Level of Consciousness
| Level        | Description                          |
|-------------|--------------------------------------|
| Alert       | Awake, oriented, responsive          |
| Verbal      | Responds to verbal stimuli           |
| Pain        | Responds only to painful stimuli     |
| Unresponsive| No response to any stimuli           |
