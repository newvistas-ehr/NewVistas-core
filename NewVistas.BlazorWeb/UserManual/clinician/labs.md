# Laboratory

**Route:** `/labs`

The Laboratory page is the comprehensive interface for lab ordering, result review, specimen collection, result entry, and result verification in NewVistas. It maps to VistA File #63 and the LR (Laboratory) package. The page provides three tabs: Results, Current Summary, and Order/Submit.

![Results tab showing summary cards and lab results table with abnormal flags](screenshots/labs-results-tab.png)

---

## Tabs

### Results Tab

The Results tab is the primary view for reviewing lab orders and results for a patient.

#### Loading Results

1. Enter the **Patient ID** in the toolbar.
2. Click **Load Results** to retrieve the patient's lab orders and results.
3. Alternatively, click **Load Demo** to seed sample lab data for demonstration purposes (uses patient ID "PATIENT-DEMO-001" if no ID is entered).

#### Summary Cards

At the top of the Results tab, three summary cards provide quick metrics:

| Card | Description |
|---|---|
| **Total Orders** | Total number of lab orders for the patient |
| **Abnormal** | Count of results with any abnormality flag (H, L, CH, CL, A) |
| **Pending** | Count of orders in Ordered, Collected, or Pending status |

The Abnormal count is displayed in orange and the Pending count in brown to draw attention.

#### Results Table

The results table displays all lab orders and results with the following columns:

| Column | Description |
|---|---|
| **Test** | Name of the lab test (e.g., "CBC", "Basic Metabolic Panel", "Creatinine") |
| **Result** | Result value; abnormal results appear in bold red. A dash (--) indicates no result yet. |
| **Units** | Units of measurement (e.g., "K/cmm", "mg/dL", "mEq/L"). A dash if not yet resulted. |
| **Flag** | Abnormality flag displayed as a colored badge (see Abnormality Flags below) |
| **Status** | Order status displayed as a colored badge (Ordered, Collected, Completed, Verified, Cancelled) |
| **Collected** | Date and time the specimen was collected in MM/DD/YY HH:MM format |
| **Actions** | Action buttons available based on the current status of the order |

#### Row Highlighting

Lab result rows are highlighted based on abnormality:

- **Light yellow background** -- results with H (High) or L (Low) flags
- **Light red background** -- results with CH (Critical High) or CL (Critical Low) flags

#### Inline Action Panels

Clicking an action button opens an inline panel below the results table for the selected workflow stage:

**Collect Panel** (appears when clicking "Collect" on an Ordered result):
- Collection Time -- date/time picker
- Collection Sample -- text field (e.g., "LAVENDER", "RED TOP")
- Performing Lab -- text field (e.g., "Hematology Lab")
- Submit and Cancel buttons

**Result Panel** (appears when clicking "Enter Result" on a Collected result):
- Result Value -- text field
- Units -- text field
- Reference Low -- text field
- Reference High -- text field
- Abnormal Flag -- dropdown (Normal, H - High, L - Low, CH - Critical High, CL - Critical Low, A - Abnormal)
- Submit and Cancel buttons

**Verify Panel** (appears when clicking "Verify" on a Completed result):
- Verifying Provider ID -- text field
- Verifying Provider Name -- text field
- Verify and Cancel buttons

### Lab Status Lifecycle

Lab orders progress through the following statuses:

```
Ordered ──> Collected ──> Completed ──> Verified
                                   └──> (Cancelled at any stage)
```

| Status | Badge Color | Description |
|---|---|---|
| **Ordered** | Purple | Lab test has been ordered; specimen has not yet been collected |
| **Collected** | Blue | Specimen has been collected; awaiting result entry |
| **Completed** | Green | Result has been entered; awaiting pathologist/supervisor verification |
| **Verified** | Green | Result has been verified by an authorized provider; final result |
| **Cancelled** | Gray | Order has been cancelled |

### Current Summary Tab

The Current Summary tab provides a consolidated view of the patient's most recent lab results organized by test, with trend data.

1. Enter the **Patient ID** in the toolbar.
2. Click **Load Summary** to retrieve the consolidated results.

If abnormal results exist, an **Abnormal Results** banner appears at the top listing all abnormal findings with colored badges.

**Summary Table Columns:**

| Column | Description |
|---|---|
| **Test** | Name of the lab test |
| **LOINC** | LOINC code for the test (displayed in monospace font) |
| **Value** | Most recent result value; abnormal values appear in bold red |
| **Units** | Units of measurement |
| **Ref Range** | Reference range (e.g., "0.7-1.3") |
| **Flag** | Abnormality flag badge |
| **Date** | Date of the most recent result in MM/DD/YY format |
| **Trend (last 3)** | The three most recent values connected by arrows (e.g., "1.0 -> 1.1 -> 1.2!"), with "!" indicating abnormal values |
| **Facility** | Facility code where the result was produced |

> **Tip:** The Trend column is particularly useful for identifying worsening or improving trends over time. Values marked with "!" indicate that result was abnormal.

### Order / Submit Tab

![Order/Submit form with test name, specimen type, and category fields](screenshots/labs-order-submit.png)

The Order/Submit tab provides two forms:

#### Order New Lab Test

Place a new lab order with the following fields:

| Field | Required | Description |
|---|---|---|
| **Test Name** | Yes | Name of the lab test to order (e.g., "CBC", "BMP", "Creatinine") |
| **LOINC / Test Code** | No | LOINC code for the test (e.g., "2160-0") |
| **Category** | No | Lab category: HEMATOLOGY, CHEMISTRY, MICROBIOLOGY, COAGULATION, URINALYSIS, SEROLOGY, BLOOD BANK |
| **Specimen Type** | No | Type of specimen: Blood, Serum, Urine, CSF, Tissue, Stool, Sputum, Swab |
| **Ordering Provider** | No | Name of the ordering provider |

Click **Place Order** to submit. The order is created in "Ordered" status.

#### Ingest Result (HL7-style)

This form allows direct submission of a completed result, simulating an HL7 feed or external lab interface:

| Field | Required | Description |
|---|---|---|
| **LOINC Code** | Yes | LOINC code for the test |
| **Test Name** | No | Name of the test |
| **Value** | Yes | Result value |
| **Units** | No | Units of measurement |
| **Reference Range** | No | Reference range string |
| **Abnormal Flag** | No | Normal, High, Low, CriticalHigh, CriticalLow, Abnormal |
| **Facility Code** | No | Originating facility code (defaults to "688") |
| **Panel Name** | No | Panel grouping name (e.g., "CBC", "BMP") |

Click **Ingest Result** to submit. The result is immediately available in the Current Summary tab.

---

## Collecting a Specimen

Follow these steps to record specimen collection for an ordered lab test:

1. **Load the patient's lab results** on the Results tab.
2. Find the lab order in "Ordered" status and click the **Collect** button.
3. The Collect panel opens below the table. Enter the **Collection Time**, **Collection Sample** (tube type or specimen container), and **Performing Lab** (the laboratory section that will process the specimen).
4. Click **Submit** to record the collection. The order status changes from "Ordered" to "Collected" and a success message confirms the action.

---

## Entering Results

Follow these steps to enter a lab result for a collected specimen:

1. **Load the patient's lab results** on the Results tab.
2. Find the lab order in "Collected" status and click the **Enter Result** button.
3. The Result panel opens below the table. Enter:
   - **Result Value** (e.g., "7.5")
   - **Units** (e.g., "K/cmm")
   - **Reference Low** (e.g., "4.5") and **Reference High** (e.g., "11.0")
   - **Abnormal Flag** -- select the appropriate flag if the result is outside the reference range
4. Review the entered data for accuracy.
5. Click **Submit** to record the result. The order status changes from "Collected" to "Completed."

> **Warning:** Ensure that the result value, units, and abnormal flag are accurate before submitting. Incorrect lab results can lead to inappropriate clinical decisions. Always double-check the result against the analyzer output.

---

## Verifying Results

Follow these steps to verify a completed lab result:

1. **Load the patient's lab results** on the Results tab.
2. Find the lab order in "Completed" status (without a verifying provider) and click the **Verify** button.
3. The Verify panel opens. Enter the **Verifying Provider ID** and **Verifying Provider Name**.
4. Click **Verify** to finalize the result. The status changes to "Verified."

> **Note:** Verification is the final step in the lab result lifecycle. A verified result is considered the official, final result and is available for clinical decision-making.

---

## Abnormality Flags

Lab results are flagged based on their relationship to the reference range:

| Flag | Meaning | Badge Color | Clinical Significance |
|---|---|---|---|
| *(none)* | Normal | Green | Result is within the normal reference range |
| **H** | High | Orange/Amber | Result is above the normal reference range |
| **L** | Low | Blue | Result is below the normal reference range |
| **CH** | Critical High | Red | Result is critically elevated -- immediate provider notification required |
| **CL** | Critical Low | Red | Result is critically low -- immediate provider notification required |
| **A** | Abnormal | Orange/Amber | Result is abnormal (used when direction is not specified) |

> **Warning:** **Critical values (CH and CL) require immediate provider notification.** Per clinical laboratory standards, critical results must be communicated to the responsible provider within a defined time frame (typically 30-60 minutes). If you receive a critical value notification, acknowledge the result and take appropriate clinical action immediately.

---

## Related Lab Modules

NewVistas includes several specialized laboratory modules that extend the core lab functionality:

### Lab EDI (`/lab-edi`)

The Lab Electronic Data Interchange page manages electronic interfaces for lab orders and results. It handles:
- Outbound order messages to reference laboratories
- Inbound result messages from reference laboratories
- HL7 message tracking and error resolution

### Lab Instruments (`/lab-instruments`)

The Lab Instruments page manages interfaces between laboratory analyzers and the NewVistas system. It includes:
- Instrument configuration and connection management
- Autoverification rules -- rules that automatically verify results meeting defined criteria
- Instrument quality control tracking

### Anatomic Pathology (`/anatomic-pathology`)

The Anatomic Pathology page handles surgical pathology, cytology, and autopsy cases. It includes:
- Case accessioning and tracking
- Specimen logging
- Pathology report generation
- Case index and search

### Blood Bank (`/blood-bank`)

The Blood Bank page manages transfusion medicine operations including:
- Type and screen testing
- Crossmatch ordering and results
- Transfusion reaction documentation
- Blood product inventory management

![Specimen collection workflow showing Collect, Result, and Verify stages](screenshots/labs-specimen-workflow.png)

---

## Tips for Lab Management

- **Check the Summary tab** for trend data before making clinical decisions based on a single lab value.
- **Use the Abnormal count** on the summary cards to quickly identify patients with results requiring attention.
- **Verify results promptly** -- unverified results may not be visible in all clinical views.
- **Use LOINC codes** when ordering to ensure results are properly mapped for trending and clinical decision support.
- **Review the reference range** context when interpreting flagged results -- a value just outside the reference range may not be clinically significant.
