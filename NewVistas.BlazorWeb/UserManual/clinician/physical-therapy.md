# Physical Therapy

Routes: `/pt` (hub), `/pt/{bodygroup}` (per-body-group session pages)

Physical Therapy in NewVistas provides dedicated workflows for recording **Range of Motion (ROM)** and **Manual Muscle Testing (MMT) strength** measurements organized by anatomical body group. Each body group has its own page with measurement entry, side-by-side session comparison, and history.

---

## PT Hub (`/pt`)

The PT Hub is the entry point for all physical therapy workflows. After loading a patient, it displays all 12 body groups as clickable cards, highlighting which ones already have recorded data.

![PT Hub with body group cards](screenshots/pt-hub-body-groups.png)

### Body Groups

| Body Group | Movements | Description |
|---|---|---|
| **Cervical (Neck)** | 6 | Flexion, Extension, Lateral Flexion L/R, Rotation L/R |
| **Shoulder** | 8 | Flexion, Extension, Abduction, Adduction, Internal/External Rotation, Horizontal Ab/Adduction |
| **Elbow** | 4 | Flexion, Extension, Pronation, Supination |
| **Wrist** | 4 | Flexion, Extension, Radial/Ulnar Deviation |
| **Hand** | 7 | Grip, Lateral/Tip/Palmar Pinch, Finger Flex/Ext, Thumb Opposition |
| **Hip** | 6 | Flexion, Extension, Abduction, Adduction, Internal/External Rotation |
| **Knee** | 2 | Flexion, Extension |
| **Ankle** | 4 | Dorsiflexion, Plantarflexion, Inversion, Eversion |
| **Foot** | 4 | Toe Flexion/Extension, Toe Abduction/Adduction |
| **Thoracic Spine** | 6 | Flexion, Extension, Rotation L/R, Lateral Flexion L/R |
| **Lumbar Spine** | 6 | Flexion, Extension, Rotation L/R, Lateral Flexion L/R |
| **TMJ** | 4 | Opening, Lateral Excursion L/R, Protrusion |

Click any body group card to navigate to that group's dedicated session page.

---

## Body Group Session Page (`/pt/{bodygroup}`)

Each body group has its own page (e.g., `/pt/Cervical`, `/pt/Shoulder`, `/pt/Hand`). The page has three tabs: **Record Session**, **Compare (Last 2)**, and **History**.

### Record Session Tab

Record ROM and strength measurements for the selected body group.

![PT Session — Record tab with measurement grid](screenshots/pt-session-record.png)

#### Session Details

| Field | Description |
|---|---|
| **Session Date** | Date and time of the PT session |
| **Side** | Bilateral, Left, or Right |
| **Therapist** | Name of the treating physical therapist |
| **Location** | Clinic or facility name |

#### ROM Measurement Grid

For each movement in the body group, enter:

| Column | Description |
|---|---|
| **Movement** | The specific movement being measured (read-only) |
| **Normal** | Reference normal ROM in degrees (read-only) |
| **Active ROM** | Degrees the patient achieves independently |
| **Passive ROM** | Degrees the therapist achieves with manual assistance |
| **Pain** | Pain notation (e.g., "pain at end range", "sharp at 90 degrees") |

![ROM measurement entry grid](screenshots/pt-rom-grid.png)

#### Strength Measurement Grid

For each movement, enter the Manual Muscle Testing grade:

| Column | Description |
|---|---|
| **Movement** | The movement being tested (read-only) |
| **Grade** | MMT grade using standard notation: 0, 1, 1+, 2-, 2, 2+, 3-, 3, 3+, 4-, 4, 4+, 5-, 5 |
| **Comments** | Additional notes for this measurement |

**MMT Grading Scale:**

| Grade | Description |
|---|---|
| **0** | No visible or palpable contraction |
| **1** | Visible or palpable contraction, no movement |
| **2** | Full ROM with gravity eliminated |
| **3** | Full ROM against gravity |
| **4** | Full ROM against gravity with moderate resistance |
| **5** | Full ROM against gravity with maximal resistance (normal) |

> **Note:** The `+` and `-` modifiers (e.g., 3+, 4-) map to ±0.33 internally for trending but display in their original notation.

#### Session Notes

Free-text area for overall session observations, patient tolerance, treatment modifications, or plan adjustments.

#### Recording a Session

1. Enter the **Patient ID** and click **Load**.
2. Set the **Side** (Bilateral, Left, or Right) and enter the **Therapist** name.
3. Enter **Active ROM** and/or **Passive ROM** values for each movement measured (leave blank to skip).
4. Note any **Pain** observed during ROM testing.
5. Enter **Strength** grades for each movement tested.
6. Add any **Session Notes**.
7. Click **Record Session** to save.

> **Tip:** You don't need to fill in every movement. Only movements with at least one value entered (ROM or strength) are recorded.

---

### Compare (Last 2) Tab

Displays a side-by-side comparison of the two most recent sessions for the selected body group. This is the key clinical view for tracking progress.

![Side-by-side session comparison](screenshots/pt-compare-sessions.png)

The comparison table shows:

| Column | Description |
|---|---|
| **Movement** | Movement name |
| **Normal** | Reference normal ROM |
| **Active** (per session) | Active ROM value |
| **Passive** (per session) | Passive ROM value |
| **Strength** (per session) | MMT grade |

Values significantly below normal ROM (< 75% of normal) are highlighted in red. Strength grades below 3 are also highlighted as deficient.

Session notes from each session are displayed below the comparison table.

---

### History Tab

Search and view all recorded sessions for the body group within a date range.

![Session history list](screenshots/pt-session-history.png)

| Column | Description |
|---|---|
| **Date** | Session date and time |
| **Side** | Bilateral, Left, or Right |
| **Therapist** | Treating therapist name |
| **ROM Measurements** | Count of ROM measurements recorded |
| **Strength Measurements** | Count of strength measurements recorded |
| **Notes** | Truncated session notes |

---

## Normal ROM Reference Ranges

The system includes reference normal ROM values for each body group and movement, displayed in the measurement grid for clinician reference:

### Cervical

| Movement | Normal ROM |
|---|---|
| Flexion | 45° |
| Extension | 45° |
| Lateral Flexion (L/R) | 45° |
| Rotation (L/R) | 80° |

### Shoulder

| Movement | Normal ROM |
|---|---|
| Flexion | 180° |
| Extension | 60° |
| Abduction | 180° |
| Adduction | 45° |
| Internal Rotation | 70° |
| External Rotation | 90° |
| Horizontal Abduction | 45° |
| Horizontal Adduction | 135° |

### Elbow

| Movement | Normal ROM |
|---|---|
| Flexion | 150° |
| Extension | 0° |
| Pronation | 80° |
| Supination | 80° |

### Wrist

| Movement | Normal ROM |
|---|---|
| Flexion | 80° |
| Extension | 70° |
| Radial Deviation | 20° |
| Ulnar Deviation | 30° |

### Hip

| Movement | Normal ROM |
|---|---|
| Flexion | 120° |
| Extension | 30° |
| Abduction | 45° |
| Adduction | 30° |
| Internal Rotation | 45° |
| External Rotation | 45° |

### Knee

| Movement | Normal ROM |
|---|---|
| Flexion | 135° |
| Extension | 0° |

### Ankle

| Movement | Normal ROM |
|---|---|
| Dorsiflexion | 20° |
| Plantarflexion | 50° |
| Inversion | 35° |
| Eversion | 15° |

---

## Example Workflow: Morning PT Session

Based on a typical session (e.g., neck ROM, both arms, both hands, plus strength):

1. Open **PT Hub** (`/pt`) and load the patient.
2. Click **Cervical (Neck)** — record ROM for all 6 cervical movements, set Side to Bilateral.
3. Click **Record Session**, then use the **Back** button to return to the hub.
4. Click **Shoulder** — set Side to Left, record ROM and strength for 8 shoulder movements. Record Session. Repeat for Right side.
5. Click **Hand** — set Side to Left, record grip/pinch strength grades and finger ROM. Record Session. Repeat for Right side.
6. Use the **Compare (Last 2)** tab on any body group to review progress against prior sessions.

---

## Related Pages

| Page | Route | Description |
|---|---|---|
| Orders | `/orders` | PT-related orders and referrals |
| Consults | `/consults` | PT consultation requests |
| Notes | `/notes` | PT progress notes and documentation |
| Cover Sheet | `/cover-sheet` | Patient overview including active problems |
