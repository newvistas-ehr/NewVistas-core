# PT Measurement Wizard -- Physician Human Test Script -- WPF UI

## Prerequisites
- Login: DOCTOR1 / Password: smythVista1
- Patient: 4
- Pre-conditions: Demo data loaded (patients 1-50 from Fifty dataset). Ensure the SiloHost, WebServer, and WPF Application (NewVistas.WpfClient) are all running.

---

## Scenario 1: Launch the Wizard from PT Hub (Happy Path)

### Steps
1. Log in as **DOCTOR1** (SMITH,JOHN A / Internal Medicine)
2. In the Navigation Panel, expand **Specialty Clinical** and select **Physical Therapy**
3. In the **Patient ID** field in the toolbar, type: `4`
4. Click the **Load Body Groups** button

### Expected Result
- The PT Hub view loads with the title "Physical Therapy"
- Three navigation buttons appear at the top: **Measurement Wizard**, **Goals**, **Home Exercises**
- A grid of 12 body group tiles is displayed (Cervical, Shoulder, Elbow, Wrist, Hand, Hip, Knee, Ankle, Foot, Thoracic Spine, Lumbar Spine, TMJ)
- Body groups with no prior data show white tiles; those with data show blue-bordered tiles with a "Has Data" label

### Steps (continued)
5. Click the **Measurement Wizard** button

### Expected Result
- The wizard view opens with the title "PT Measurement Wizard"
- A **Cancel** button appears in the top-left
- The **Selection Page** is displayed with:
  - **Session Details** section: Therapist and Location text fields
  - **Select Body Regions** section with 4 region groups, each in a bordered card:
    - **Spine** (Cervical, Thoracic Spine, Lumbar Spine) -- checkbox, no laterality
    - **Upper Extremity** (Shoulder, Elbow, Wrist, Hand) -- checkbox with Right/Left/Both radio buttons when checked
    - **Lower Extremity** (Hip, Knee, Ankle, Foot) -- checkbox with Right/Left/Both radio buttons when checked
    - **TMJ** (TMJ) -- checkbox, no laterality
  - **Session Notes** text area
  - **Start Measurements >** button

---

## Scenario 2: Select Regions and Proceed to Measurement Pages

### Steps
1. On the wizard selection page, fill in:
   - **Therapist**: `Smith, John`
   - **Location**: `PT Clinic A`
2. Check the **Spine** checkbox
3. Check the **Upper Extremity** checkbox
4. Observe that **Right/Left/Both** radio buttons appear for Upper Extremity
5. Select **Right** for Upper Extremity side
6. In **Session Notes**, type: `Initial evaluation - cervical radiculopathy`
7. Click **Start Measurements >**

### Expected Result
- The wizard advances to the first measurement page
- The page title shows **Cervical (Neck)**
- The step label reads **Page 1 of 7** (3 spine + 4 upper extremity right-side pages)
- A DataGrid is displayed with the following columns:
  - Movement (read-only) | Normal (read-only) | Active ROM | Passive ROM | Pain | Strength | Comments
- The Cervical page shows 6 movement rows:
  - Flexion (45 deg), Extension (45 deg), Lateral Flexion -- Left (45 deg), Lateral Flexion -- Right (45 deg), Rotation -- Left (80 deg), Rotation -- Right (80 deg)
- Navigation buttons at the bottom: **< Back**, **Next >**, **Done (Save All)**

---

## Scenario 3: Enter Measurements and Navigate Pages

### Steps
1. On the **Cervical (Neck)** page (Page 1 of 7), enter:

   | Movement | Active ROM | Passive ROM | Pain | Strength |
   |----------|-----------|-------------|------|----------|
   | Flexion | 35 | 40 | mild | 4 |
   | Extension | 40 | 45 | | 4+ |
   | Lateral Flexion -- Left | 30 | 35 | moderate | 3+ |
   | Lateral Flexion -- Right | 40 | 45 | | 4 |
   | Rotation -- Left | 60 | 70 | mild | 4 |
   | Rotation -- Right | 75 | 80 | | 5 |

2. Click **Next >**

### Expected Result
- The wizard advances to **Thoracic Spine** (Page 2 of 7)
- The Thoracic Spine page shows 6 movement rows:
  - Flexion (30 deg), Extension (25 deg), Rotation -- Left (30 deg), Rotation -- Right (30 deg), Lateral Flexion -- Left (25 deg), Lateral Flexion -- Right (25 deg)
- The cervical data entered in Step 1 is preserved (verified by clicking Back)

### Steps (continued)
3. On the **Thoracic Spine** page, leave all fields empty (skip this body group)
4. Click **Next >**

### Expected Result
- The wizard advances to **Lumbar Spine** (Page 3 of 7)

### Steps (continued)
5. On the **Lumbar Spine** page, enter:

   | Movement | Active ROM | Pain | Strength |
   |----------|-----------|------|----------|
   | Flexion | 45 | moderate | 3 |
   | Extension | 15 | moderate | 3 |

   Leave all other rows blank.

6. Click **Next >**

### Expected Result
- The wizard advances to **Right Shoulder** (Page 4 of 7)
- The title shows "Right Shoulder" (not just "Shoulder")
- 8 movement rows are displayed for the shoulder

### Steps (continued)
7. On the **Right Shoulder** page, enter:

   | Movement | Active ROM | Passive ROM | Strength |
   |----------|-----------|-------------|----------|
   | Flexion | 160 | 175 | 4 |
   | Abduction | 150 | 170 | 4- |

   Leave other rows blank.

8. Click **Next >** three more times to skip through Right Elbow (Page 5), Right Wrist (Page 6), and Right Hand (Page 7)

### Expected Result
- On the Right Hand page (Page 7 of 7), the **Next >** button is hidden
- Only **< Back** and **Done (Save All)** are visible

---

## Scenario 4: Save All Measurements (Done)

### Steps
1. On the last page (Right Hand, Page 7 of 7), click **Done (Save All)**

### Expected Result
- A brief loading spinner appears next to the Done button
- The wizard closes and navigates back to the **PT Hub**
- The PT Hub now shows **Has Data** badges (blue-bordered tiles) on:
  - **Cervical** (6 measurements entered)
  - **Lumbar Spine** (2 measurements entered)
  - **Shoulder** (2 measurements entered)
- Thoracic Spine, Elbow, Wrist, and Hand remain without data badges (empty pages were skipped)

---

## Scenario 5: Verify Saved Data in PT Session View

### Steps
1. On the PT Hub, click the **Cervical (Neck)** tile

### Expected Result
- The PT Session view opens for Cervical
- Click **Compare (Last 2)** tab

### Steps (continued)
2. Click **Compare (Last 2)**

### Expected Result
- The comparison view shows at least 1 recent session
- The session displays:
  - **Side**: Bilateral
  - **Therapist**: Smith, John
  - **ROM Count**: 6
  - **Strength Count**: 6
  - **Notes**: Initial evaluation - cervical radiculopathy

### Steps (continued)
3. Click **< Back to PT Hub** to return

---

## Scenario 6: Wizard with Both Sides (Bilateral Extremity)

### Steps
1. On the PT Hub, click **Measurement Wizard**
2. Fill in:
   - **Therapist**: `Smith, John`
   - **Location**: `PT Clinic A`
3. Check **Lower Extremity** only
4. Select **Both** for the side
5. Click **Start Measurements >**

### Expected Result
- The wizard shows **Page 1 of 8** (4 body groups x 2 sides)
- Pages are ordered:
  1. Right Hip
  2. Left Hip
  3. Right Knee
  4. Left Knee
  5. Right Ankle
  6. Left Ankle
  7. Right Foot
  8. Left Foot

### Steps (continued)
6. On the **Right Knee** page (Page 3 of 8), enter:

   | Movement | Active ROM | Passive ROM | Strength |
   |----------|-----------|-------------|----------|
   | Flexion | 125 | 130 | 4 |
   | Extension | 0 | 0 | 4 |

7. Click **Done (Save All)**

### Expected Result
- The wizard saves the Right Knee data and returns to the PT Hub
- The **Knee** tile now shows a "Has Data" badge
- Hip, Ankle, and Foot tiles remain unchanged (empty pages skipped)

---

## Scenario 7: Validation -- No Region Selected

### Steps
1. On the PT Hub, click **Measurement Wizard**
2. Do not check any region checkboxes
3. Click **Start Measurements >**

### Expected Result
- An error message appears: "Select at least one body region to measure."
- The wizard stays on the selection page

---

## Scenario 8: Validation -- Invalid Strength Grade

### Steps
1. On the PT Hub, click **Measurement Wizard**
2. Check **TMJ**
3. Click **Start Measurements >**
4. On the TMJ page (Page 1 of 1), enter:

   | Movement | Active ROM | Strength |
   |----------|-----------|----------|
   | Opening | 38 | 6 |

5. Click **Done (Save All)**

### Expected Result
- An error message appears: "Invalid strength grade '6' for Opening on TMJ."
- The wizard stays on the current page
- No data is saved (the invalid grade prevents the entire save)

### Steps (continued)
6. Correct the Strength value for Opening from `6` to `5`
7. Click **Done (Save All)**

### Expected Result
- The save succeeds and the wizard returns to the PT Hub
- The **TMJ** tile shows a "Has Data" badge

---

## Scenario 9: Navigate Back to Selection and Change Regions

### Steps
1. On the PT Hub, click **Measurement Wizard**
2. Check **Spine** and **Upper Extremity** (Right)
3. Click **Start Measurements >** (Page 1 of 7: Cervical)
4. Enter any value in the Cervical Flexion Active ROM field (e.g., `40`)
5. Click **< Back** repeatedly until the selection page reappears

### Expected Result
- The wizard returns to the Selection Page (step 0)
- All region checkboxes retain their checked state
- Session details (Therapist, Location) retain their values

### Steps (continued)
6. Uncheck **Upper Extremity** (leaving only Spine checked)
7. Click **Start Measurements >**

### Expected Result
- The wizard now shows **Page 1 of 3** (spine only: Cervical, Thoracic, Lumbar)
- The previously entered Cervical Flexion data is cleared (pages were regenerated from the new selection)

---

## Scenario 10: Cancel the Wizard

### Steps
1. On the PT Hub, click **Measurement Wizard**
2. Check **Spine**, enter some Therapist/Location values
3. Click **Start Measurements >**
4. Enter some measurements on Page 1
5. Click **< Cancel** (top-left)

### Expected Result
- The wizard closes immediately and returns to the **PT Hub**
- No data is saved
- The body group tiles are unchanged from before the wizard was launched

---

## Scenario 11: Navigate to Body Group from PT Hub (Existing Flow)

This verifies the existing single-body-group session flow still works alongside the wizard.

### Steps
1. On the PT Hub, click the **Ankle** tile directly (not via the wizard)

### Expected Result
- The PT Session view opens for **Ankle**
- The Record Session tab is displayed with 4 movement rows:
  - Dorsiflexion (20 deg), Plantarflexion (50 deg), Inversion (35 deg), Eversion (15 deg)
- The session detail fields (Side, Therapist, Location) are shown
- A **< Back to PT Hub** button returns to the hub

---

## Scenario 12: Per-Page Notes Override

### Steps
1. From the PT Hub, click **Measurement Wizard**
2. Fill in:
   - **Therapist**: `Smith, John`
   - **Location**: `PT Clinic A`
   - **Session Notes**: `General follow-up`
3. Check **Spine**
4. Click **Start Measurements >**
5. On the **Cervical (Neck)** page, enter Flexion Active ROM: `40`
6. In the **Page Notes** field, type: `Cervical-specific note`
7. Click **Next >**
8. On the **Thoracic Spine** page, enter Flexion Active ROM: `25`
9. Leave the **Page Notes** field empty
10. Click **Done (Save All)**

### Expected Result
- The wizard saves and returns to the PT Hub
- Navigate to Cervical > Compare: the session notes show **"Cervical-specific note"** (page notes override global)
- Navigate to Thoracic Spine > Compare: the session notes show **"General follow-up"** (global notes used as fallback)
