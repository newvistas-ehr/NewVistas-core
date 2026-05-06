# Problems and Allergies

This page documents the Problem List and Allergies modules in NewVistas. Problems and allergies are foundational patient data that feed into clinical decision support, order checks, the Cover Sheet, and CWAD flags. Keeping these lists accurate and up to date is critical for patient safety.

---

## Problem List

**Route:** `/problems`

The Problem List page manages a patient's active and inactive clinical diagnoses. It maps to VistA File #9000011 and the GMPL (Problem List) package. Problem data is embedded on the `PatientState` grain.

![Problem List view showing active and inactive diagnoses with ICD-10 codes](screenshots/problems-list-view.png)

### Tabs

The Problem List page has two tabs:

#### Problem List Tab

The Problem List tab displays the patient's documented problems in a table format.

**Filter:** An **Active only** checkbox toggle at the top of the list filters the display to show only active problems when checked (this is the default). Uncheck it to see both active and inactive problems.

**Table Columns:**

| Column | Description |
|---|---|
| **Diagnosis** | Full text description of the diagnosis (e.g., "Type 2 Diabetes Mellitus without complications") |
| **Code** | ICD-10 diagnosis code displayed in monospace font (e.g., `E11.9`) |
| **Status** | ACTIVE (green text) or INACTIVE (gray text) |
| **Onset** | Date of onset in MM/DD/YYYY format |
| **Condition** | Clinical condition category: ACUTE, CHRONIC, PERMANENT, or TRANSCRIBED |
| **SC** | Service Connected indicator -- displays "Yes" if the problem is related to a service-connected disability |

If no problems are found matching the current filter, the table displays "No problems found."

#### Add Problem Tab

![Add Problem form with ICD-10 code entry](screenshots/problems-add-form.png)

The Add Problem tab provides a form for adding a new problem to the patient's problem list.

**Form Fields:**

| Field | Required | Description |
|---|---|---|
| **Diagnosis** | Yes | Free-text description of the diagnosis (e.g., "Type 2 Diabetes Mellitus") |
| **ICD-10 Code** | No | The ICD-10-CM diagnosis code (e.g., "E11.9"). Use the ICD-10 Browser (`/icd10`) to look up codes. |
| **Condition** | No | Select from: ACUTE, CHRONIC, PERMANENT, TRANSCRIBED |
| **Priority** | No | Select from: ACUTE, CHRONIC |
| **Onset Date** | No | Date when the problem was first identified or when symptoms began |
| **Provider** | No | Name of the responsible provider for this problem |
| **Clinic** | No | Name of the clinic where the problem was documented |
| **Service Connected** | No | Checkbox indicating whether the problem is related to a service-connected disability |
| **Comments** | No | Additional notes or comments about the problem |

### Adding a Problem

Follow these steps to add a new problem to a patient's problem list:

1. **Enter the Patient ID** in the lookup bar and click **Load** to load the patient's existing problem list.

2. **Switch to the Add Problem tab** by clicking the "Add Problem" tab button.

3. **Enter the diagnosis.** Type the full diagnosis description in the Diagnosis field. This is the only required field, but you should complete as many fields as possible for clinical accuracy.

4. **Enter the ICD-10 code.** Type the ICD-10-CM code directly, or navigate to the ICD-10 Browser (`/icd10`) in a separate tab to look up the correct code. Common codes include:
   - `E11.9` -- Type 2 diabetes mellitus without complications
   - `I10` -- Essential (primary) hypertension
   - `J06.9` -- Acute upper respiratory infection, unspecified
   - `M54.5` -- Low back pain

5. **Complete the remaining fields** -- set the Condition, Onset Date, Provider, Clinic, and Service Connected flag as appropriate. Add any relevant Comments.

6. **Click Add Problem** to save the new problem. A success message confirms the addition, and the problem appears in the Problem List tab.

> **Tip:** Always include an ICD-10 code when adding a problem. Coded problems enable clinical decision support, workload reporting, and quality measurement. The ICD-10 Browser (`/icd10`) provides a searchable interface for finding the correct code.

---

## Allergies

**Route:** `/allergies`

The Allergies page manages a patient's allergy and adverse reaction documentation. Allergy data is embedded on the `PatientState` grain and feeds into the Cover Sheet, CWAD "A" flag, and order check clinical decision support (drug-allergy interaction checking).

![Allergy list with severity badges showing Drug, Food, and Other allergen types](screenshots/allergies-list-view.png)

### Tabs

The Allergies page has two tabs:

#### Allergies Tab

The Allergies tab displays the patient's documented allergies in a table format.

**Table Columns:**

| Column | Description |
|---|---|
| **Allergen** | Name of the allergen (e.g., "Penicillin", "Sulfa Drugs", "Peanuts") |
| **Type** | Allergen type: Drug, Food, or Other |
| **Reactions** | Comma-separated list of documented reactions (e.g., "Rash, Hives, Anaphylaxis") |
| **Severity** | Severity level with color coding (see below) |
| **Observed/Historical** | Whether the reaction was directly observed or is reported from patient history |

**Severity Color Coding:**

| Severity | Display | Description |
|---|---|---|
| **Mild** | Standard text | Minor reaction that does not significantly affect the patient (e.g., mild rash) |
| **Moderate** | Amber text | Reaction that causes notable discomfort or requires treatment (e.g., widespread hives) |
| **Severe** | Red text | Life-threatening or potentially life-threatening reaction (e.g., anaphylaxis, angioedema) |

If the patient has no documented allergies, the page displays a prominent **"No Known Allergies"** banner.

#### Record Allergy Tab

![Record Allergy form with allergen type, severity, and reaction fields](screenshots/allergies-record-form.png)

The Record Allergy tab provides a form for documenting a new allergy or adverse reaction.

**Form Fields:**

| Field | Required | Description |
|---|---|---|
| **Allergen** | Yes | Name of the allergen (e.g., "Penicillin") |
| **Allergen Type** | Yes | Select from: Drug, Food, Other |
| **Reaction Type** | No | Select from: ALLERGY, ADVERSE REACTION, PHARMACOLOGIC |
| **Reactions** | No | Comma-separated list of reactions (e.g., "Rash, Itching, Hives") |
| **Severity** | No | Select from: Mild, Moderate, Severe |
| **Observed / Historical** | No | Whether the reaction was directly observed by a clinician (Observed) or reported from patient/family history (Historical) |
| **Comments** | No | Additional notes about the allergy (e.g., date of reaction, context, specific drug formulation) |

### Recording an Allergy

1. **Enter the Patient ID** and click **Load** to load the patient's existing allergy list.
2. **Switch to the Record Allergy tab.**
3. **Enter the allergen name** (required) and select the **Allergen Type** (Drug, Food, or Other).
4. **Select the Reaction Type** -- ALLERGY for true immune-mediated reactions, ADVERSE REACTION for non-immune reactions, PHARMACOLOGIC for expected pharmacologic side effects.
5. **Enter reactions** -- list the specific signs and symptoms observed (comma-separated).
6. **Select the severity** -- Mild, Moderate, or Severe.
7. **Indicate Observed or Historical** -- Observed means a clinician directly witnessed the reaction; Historical means it is reported from the patient or medical records.
8. **Add any comments** providing additional context.
9. **Click Record Allergy** to save.

---

## NKA (No Known Allergies) Documentation

An empty allergy list is clinically ambiguous -- it could mean the patient has no allergies, or it could mean allergies have never been assessed. It is important to explicitly document **No Known Allergies (NKA)** when you have verified that the patient reports no allergies.

> **Warning:** Never assume an empty allergy list means the patient has no allergies. Always ask the patient about allergies and document the result. If the patient reports no allergies, formally document NKA. An undocumented allergy status could lead to a drug-allergy interaction being missed by the order checking system.

When no allergies are documented, the Allergies tab displays a "No Known Allergies" banner. The Cover Sheet Allergies panel also displays "No Known Allergies" for patients with an empty allergy list.

---

## Clinical Safety Integration

Problems and allergies are not isolated data points -- they are integral to NewVistas clinical safety systems:

### Order Checks
- **Drug-Allergy Interaction** -- when a medication order is placed, the system checks the order against the patient's allergy list. If the ordered drug matches a documented allergen, a warning is generated on the Orders page.
- **Duplicate Order** -- the system uses the problem list to identify potentially duplicate diagnostic or treatment orders.

### CWAD Badge
- **A (Allergies)** -- the "A" flag in the Cover Sheet CWAD badge is set when the patient has documented allergies. This provides a visual alert to all providers viewing the patient's chart.
- **Crisis Notes and Advance Directives** set the "C" and "D" flags respectively, which are related to documentation rather than the problem list.

### Cover Sheet Integration
- The **Active Problems** panel on the Cover Sheet displays problems with their ICD-10 codes and onset dates.
- The **Allergies** panel on the Cover Sheet displays allergens with severity and reactions.
- Both panels update automatically when problems or allergies are added or modified.

### Clinical Reminders
- Some clinical reminders are triggered by specific problems on the problem list (e.g., a diabetes diagnosis may trigger reminders for HbA1c screening, eye exams, and foot exams).

> **Tip:** Keep the problem list current. Inactivate problems that have resolved, and ensure all active diagnoses have accurate ICD-10 codes. An accurate problem list improves clinical decision support, quality reporting, and care coordination.
