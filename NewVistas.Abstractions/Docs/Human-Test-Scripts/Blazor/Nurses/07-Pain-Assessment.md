# Pain Assessment -- Human Test Script

## Prerequisites

- **Login:** NURSE4 / Password: `smythVista1`
- **Patient:** 30
- **Pre-conditions:**
  1. The SiloHost, WebServer, and BlazorWeb projects must all be running.
  2. Navigate to `/pain-assessment` in the browser.
  3. Enter Patient ID `30` in the Patient ID field and click **Load**.
  4. The Pain History tab loads. If no prior assessments exist, the table is empty.

---

## Scenario 1: DVPRS Pain Assessment (Happy Path)

### Steps

1. Navigate to `/pain-assessment`.
2. Enter Patient ID: `30`
3. Click **Load**.
4. Click the **New Assessment** tab.
5. Fill in:
   - Tool: `DVPRS`
   - Pain Score: `6`
   - Location: `Lower back, bilateral`
   - Character: `Aching`
   - Nurse ID: `NURSE4`
   - Onset: `Chronic`
   - Intervention: `Ibuprofen 400mg PO administered, ice pack applied to lower back`
   - Acceptable: `3`
   - Notes: `Patient reports chronic low back pain exacerbated by standing for prolonged periods. Pain described as deep aching, constant. Currently 6/10. Patient states acceptable level is 3/10.`
6. Click **Record Pain Assessment**.

### Expected Result

- Green success banner: "Assessment recorded: PAIN-..."
- The Pain History tab reloads with a new entry:
  - Date/Time: current date/time
  - Nurse: Nurse (the display name passed)
  - Tool: DVPRS
  - Score: **6/10** (row highlighted yellow -- table-warning class since score is 4-6)
  - Location: Lower back, bilateral
  - Reassess?: No
  - Post-Rx: -- (no reassessment yet)
- Click **View** on the entry to see full detail:
  - Tool: DVPRS
  - Score badge: 6/10 (warning colored)
  - Location: Lower back, bilateral
  - Character: Aching
  - Onset: Chronic
  - Acceptable Level: 3/10
  - Intervention: Ibuprofen 400mg PO administered, ice pack applied to lower back

---

## Scenario 2: Wong-Baker FACES (Pediatric)

### Steps

1. Click the **New Assessment** tab.
2. Fill in:
   - Tool: `Wong-Baker FACES`
   - Pain Score: `4`
   - Location: `Right ear`
   - Character: `Throbbing`
   - Nurse ID: `NURSE4`
   - Onset: `Acute`
   - Intervention: `Acetaminophen 160mg PO (weight-based dose)`
   - Acceptable: `0`
   - Notes: `6-year-old patient points to face #4 on Wong-Baker FACES scale. Reports right ear hurting. Otitis media diagnosed by provider. Acetaminophen administered for pain.`
3. Click **Record Pain Assessment**.

### Expected Result

- Assessment recorded successfully.
- Pain History shows:
  - Tool: WongBakerFACES
  - Score: **4/10** (yellow/warning highlighting)
  - Location: Right ear

---

## Scenario 3: FLACC (Non-Verbal / Infant)

### Steps

1. Click the **New Assessment** tab.
2. Fill in:
   - Tool: `FLACC`
   - Pain Score: `7`
   - Location: `Generalized / unable to localize`
   - Character: `Sharp` (inferred from behavioral cues)
   - Nurse ID: `NURSE4`
   - Onset: `Acute`
   - Intervention: `Sucrose pacifier, swaddling, non-nutritive sucking`
   - Acceptable: (leave blank)
   - Notes: `8-month-old infant post-circumcision. FLACC scoring: Face=2 (frequent chin quiver, clenched jaw), Legs=1 (restless, tense), Activity=2 (arched, rigid), Cry=1 (moans, whimpers), Consolability=1 (content after prolonged comforting). Total FLACC=7. Non-pharmacologic measures initiated.`
3. Click **Record Pain Assessment**.

### Expected Result

- Assessment recorded.
- Pain History entry shows:
  - Tool: FLACC
  - Score: **7/10** (red/danger highlighting since score >= 7)
  - Location: Generalized / unable to localize
- On detail view (click View):
  - The FLACC Components section may display if populated via API (the Blazor form captures the total score; individual FLACC components -- Face, Legs, Activity, Cry, Consolability -- are stored in the `FlaccComponents` sub-object and displayed in detail view when present).

---

## Scenario 4: CPOT (Critical Care Pain Observation Tool)

### Steps

1. Click the **New Assessment** tab.
2. Fill in:
   - Tool: `CPOT`
   - Pain Score: `5`
   - Location: `Unable to assess -- patient intubated and sedated`
   - Character: (leave as default)
   - Nurse ID: `NURSE4`
   - Onset: `Acute`
   - Intervention: `Fentanyl 25mcg IV push per sedation protocol`
   - Acceptable: (leave blank)
   - Notes: `ICU patient on mechanical ventilation. CPOT assessment: Facial expression=1 (tense), Body movements=2 (protection/guarding), Muscle tension=1 (rigid), Ventilator compliance=1 (coughing, fighting vent). Total CPOT=5. Fentanyl bolus administered.`
3. Click **Record Pain Assessment**.

### Expected Result

- Assessment recorded.
- Pain History entry shows:
  - Tool: CPOT
  - Score: **5/10** (yellow highlighting)
  - Location: Unable to assess -- patient intubated and sedated

---

## Scenario 5: Post-Intervention Reassessment Showing Improvement

### Steps

1. Click the **Pain History** tab.
2. Locate the DVPRS assessment from Scenario 1. Click **View** to see the detail.
3. Note the Assessment ID displayed in the detail header (e.g., `PAIN-abc123...`).
4. Click the **Reassessment** tab.
5. Fill in:
   - Initial Assessment ID: (paste the Assessment ID from Scenario 1)
   - Post-Rx Score: `3`
   - Minutes Since: `30`
   - Intervention: `Ibuprofen 400mg PO given 30 minutes ago`
6. Click **Reassess**.

### Expected Result

- Success banner: "Reassessment recorded"
- Switch to the **Pain History** tab and reload (click **Load** again).
- A new entry appears in the Pain History:
  - Reassess?: **Yes**
  - Post-Rx: **3** (the post-intervention score)
  - Score: (the current reassessment score)
- Click **View** on the reassessment entry. The detail shows:
  - Reassessment section visible:
    - Post-Rx Score: 3/10
    - Minutes Since: 30
    - The score dropped from 6/10 to 3/10 (matches the patient's acceptable pain level).

---

## Scenario 6: Severe Pain Assessment (NRS 0-10)

### Steps

1. Click the **New Assessment** tab.
2. Fill in:
   - Tool: `NRS (0-10)`
   - Pain Score: `9`
   - Location: `Right hip, surgical site`
   - Character: `Stabbing`
   - Nurse ID: `NURSE4`
   - Onset: `Acute`
   - Intervention: `Morphine 4mg IV administered. Position of comfort. Ice to surgical site.`
   - Acceptable: `4`
   - Notes: `Patient 2 hours post right hip arthroplasty. Reports severe stabbing pain at surgical site rated 9/10. Morphine 4mg IV push administered per PCA order. Will reassess in 15-30 minutes. Pain goal discussed with patient -- states 4/10 acceptable.`
3. Click **Record Pain Assessment**.

### Expected Result

- Pain History shows the new entry with:
  - Tool: NumericRatingScale
  - Score: **9/10** (red/danger row highlighting)
  - The row should be visually prominent (table-danger class for scores >= 7).

---

## Scenario 7: Zero Pain Assessment (Post-Recovery)

### Steps

1. Click the **New Assessment** tab.
2. Fill in:
   - Tool: `NRS (0-10)`
   - Pain Score: `0`
   - Location: (leave blank)
   - Character: (leave as default)
   - Nurse ID: `NURSE4`
   - Onset: `Acute`
   - Intervention: (leave blank)
   - Acceptable: `0`
   - Notes: `Patient denies pain. Ambulating without difficulty. Discharge criteria met for pain management.`
3. Click **Record Pain Assessment**.

### Expected Result

- Pain History shows entry with:
  - Score: **0/10** (green/success badge)
  - No row highlighting (no pain).

---

## Reference: Pain Assessment Tools

| Tool              | Population                     | Score Range | Description                                    |
|-------------------|--------------------------------|-------------|------------------------------------------------|
| NRS               | Adult, verbal                  | 0-10        | Numeric Rating Scale                           |
| DVPRS             | Adult (DoD/VA standard)        | 0-10        | Defense and Veterans Pain Rating Scale + supplemental (Activity, Sleep, Mood, Stress interference) |
| Wong-Baker FACES  | Pediatric (age 3+), non-English | 0-10        | Cartoon faces from happy to crying             |
| FLACC             | Infant/non-verbal              | 0-10        | Face, Legs, Activity, Cry, Consolability       |
| CPOT              | ICU/sedated/intubated          | 0-8         | Critical Care Pain Observation Tool            |
| VAS               | Adult, verbal                  | 0-100mm     | Visual Analog Scale (line)                     |

### Pain Score Severity

| Score Range | Severity | Row Color       |
|-------------|----------|-----------------|
| 0           | None     | No highlight    |
| 1-3         | Mild     | Blue/info       |
| 4-6         | Moderate | Yellow/warning  |
| 7-10        | Severe   | Red/danger      |

### Pain Character Options (dropdown)

- Sharp
- Dull
- Burning
- Throbbing
- Aching
- Pressure
- Stabbing

### Pain Onset Options (dropdown)

- Acute
- Chronic
- Intermittent
