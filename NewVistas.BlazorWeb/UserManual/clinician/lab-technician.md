# Lab Technician Guide

This guide is written for Medical Technologists (MT/MLS), Medical Laboratory Technicians (MLTs), Phlebotomists, Histotechnologists, Cytotechnologists, and Blood Bank Technologists who use the NewVistas clinical information system. It covers the core laboratory workflows you will perform daily: specimen collection and processing, test performance and resulting, result verification, anatomic pathology, blood bank operations, and instrument interface management.

![Lab worklist showing pending orders with specimen status indicators](screenshots/lab-worklist-overview.png)

---

## Role Description

As a lab technician in NewVistas, your responsibilities include:

- **Collecting and processing specimens** -- drawing blood, receiving specimens from nursing units, verifying specimen integrity, centrifuging, aliquoting, and routing specimens to the appropriate laboratory section.
- **Performing and resulting tests** -- running assays on chemistry, hematology, coagulation, blood gas, urinalysis, point-of-care, and microbiology analyzers and entering results into the system.
- **Verifying results** -- performing technical and supervisory verification of results, validating quality control, and releasing verified results to the patient record.
- **Handling critical values** -- identifying critical (panic) values, immediately notifying the ordering provider, and documenting the notification in the system.
- **Anatomic pathology** -- accessioning surgical pathology, cytology, and autopsy specimens; performing gross and microscopic examination support; and managing the pathology case workflow.
- **Blood bank operations** -- performing type and screen, crossmatching blood products, issuing units for transfusion, monitoring transfusion reactions, and managing blood product inventory.
- **Instrument interfaces** -- monitoring automated analyzer connections, troubleshooting interface errors, and ensuring bidirectional data flow between instruments and the LIS.

---

## Daily Workflow Overview

A typical laboratory shift in NewVistas follows this six-step workflow:

1. **Review pending lab orders** (`/labs`). Open the Laboratory page and load the lab worklist. Review all orders in Ordered status that are awaiting specimen collection. Prioritize STAT and ASAP orders. Check for timed specimens and fasting requirements.

2. **Collect specimens.** For phlebotomists, print collection lists and draw rounds. For bench technologists, receive specimens from the pneumatic tube system or courier delivery. Update each order to Collected status, recording the collection time, specimen type (tube color/container), and performing laboratory section.

3. **Perform testing and enter results.** Process specimens according to standard operating procedures. Run samples on the appropriate analyzer. Enter results into the system via the Result panel or receive results automatically through the instrument interface. Flag abnormal results appropriately (H, L, CH, CL, A).

4. **Verify results.** Perform technical verification by reviewing results against quality control data, delta checks, reference ranges, and clinical plausibility. For results that pass all checks, verify to release them to the patient chart. Supervisor verification may be required for certain test categories.

5. **Handle critical values.** When a result falls in the critical (panic) value range (CH or CL flags), immediately contact the ordering provider or covering provider by telephone. Document the date/time of notification, the person notified, and the read-back confirmation in the critical value notification record.

6. **Anatomic pathology and blood bank specialized workflows.** Process surgical pathology cases through grossing, embedding, sectioning, and staining. Manage blood bank type and screen, crossmatch, and issue workflows. Monitor blood product inventory and expiration dates.

---

## Lab Processing

**Route:** `/labs`

The Laboratory page (`/labs`) is the primary interface for all lab processing workflows. For a comprehensive overview of the page layout, tabs, and general navigation, see the [Laboratory Guide](labs.md). This section focuses on the specimen collection, result entry, and verification workflows from the lab technician's perspective.

---

### Specimen Collection

Specimen collection is the process of drawing or receiving a specimen and updating the lab order from Ordered to Collected status. Proper specimen collection and labeling are critical to patient safety and result accuracy.

#### Collection Workflow

1. **Identify the patient.** Verify the patient's identity using two patient identifiers (name and date of birth, or name and patient ID). For bedside collection, check the patient's wristband. For specimens received in the lab, verify the label matches the requisition.

2. **Collect the specimen.** Draw the specimen using the appropriate technique and collection container (tube color). Follow the correct order of draw to prevent cross-contamination of additives. For non-blood specimens, verify the container type matches the test requirements.

3. **Label the specimen at the bedside.** Apply the pre-printed label to the specimen container immediately after collection, while still at the patient's bedside. Verify that the label information matches the patient's wristband.

4. **Update the order to Collected status.** On the Laboratory page Results tab, click **"Collect"** on the Ordered lab result row. In the Collect panel that appears, enter:
   - **Collection Time** -- the date and time the specimen was actually drawn (defaults to current time)
   - **Collection Sample** -- the specimen type or tube color (e.g., "LAVENDER", "RED TOP", "BLUE TOP", "GREEN TOP", "GRAY TOP", "GOLD/SST", "URINE CUP", "SWAB")
   - **Performing Lab** -- the laboratory section that will process the specimen (e.g., "Hematology Lab", "Chemistry Lab", "Microbiology Lab")

   Click **"Submit"** to update the order status to Collected.

> **Warning:** Never pre-label specimen containers before collection. Labels must be applied at the point of collection with the patient present. Mislabeled specimens are the leading cause of laboratory errors and can result in wrong-patient results, incorrect treatment, and patient harm. Any specimen received in the lab with a labeling discrepancy must be rejected and recollected.

> **Note:** If a specimen is hemolyzed, clotted (when it should not be), insufficient in volume, collected in the wrong tube, or otherwise compromised, reject the specimen in the system by clicking "Reject" and selecting the rejection reason. A new collection will be required. Document the rejection in the order notes.

---

### Result Entry

Result entry is the process of recording test results and updating the lab order from Collected to Completed status.

#### Result Entry Workflow

1. **Locate the order.** On the Laboratory page Results tab, find the lab order with Collected status that you need to result. You can filter or search by test name, patient ID, or collection time.

2. **Click "Enter Result"** on the Collected lab result row. The Result panel opens below the results table.

3. **Enter the result data:**
   - **Result Value** -- the numeric or text result from the analyzer or manual test (e.g., "7.2", "142", "POSITIVE", "NO GROWTH")
   - **Units** -- the units of measurement (e.g., "g/dL", "mEq/L", "mg/dL", "K/cmm", "sec")
   - **Reference Low** -- the lower bound of the reference range for this test (e.g., "12.0" for hemoglobin)
   - **Reference High** -- the upper bound of the reference range for this test (e.g., "16.0" for hemoglobin)
   - **Abnormal Flag** -- select the appropriate flag:
     - **Normal** -- result is within the reference range
     - **H - High** -- result is above the reference range
     - **L - Low** -- result is below the reference range
     - **CH - Critical High** -- result is above the critical (panic) high threshold
     - **CL - Critical Low** -- result is below the critical (panic) low threshold
     - **A - Abnormal** -- result is abnormal but not classifiable as high or low (e.g., qualitative tests)

4. **Review the entered data** for accuracy. Verify the result value, units, reference range, and flag before submitting.

5. **Click "Submit"** to save the result. The order status changes from Collected to Completed. The result, units, flag, and reference range are now visible on the patient's lab results display.

> **Tip:** For panels and profiles (e.g., Basic Metabolic Panel, Complete Blood Count), each individual analyte within the panel has its own order row. Enter results for each analyte separately. The panel summary view will display all component results together once they are entered.

> **Note:** Results entered through the instrument interface (auto-verified instruments) are automatically populated in the Result fields and may proceed directly to Completed or Verified status depending on the instrument configuration and auto-verification rules. Manual review is still required for results that fail auto-verification criteria (delta check failures, QC failures, critical values).

---

### Result Verification

Result verification is the process of reviewing completed results for accuracy and releasing them to the patient chart. Verification changes the order status from Completed to Verified, making the result final.

#### Verification Workflow

1. **Locate completed results.** On the Laboratory page Results tab, identify orders with Completed status that are awaiting verification. These results have been entered but not yet released.

2. **Review the result.** Before verifying, assess:
   - **Quality control** -- Confirm that the QC for the current analytical run is within acceptable limits.
   - **Delta check** -- Compare the current result with the patient's previous result for the same test. Investigate significant changes that exceed the expected biological variation.
   - **Reference range** -- Verify that the abnormal flag is appropriate for the result value and reference range.
   - **Clinical plausibility** -- Consider whether the result is consistent with the patient's clinical picture. Investigate results that seem implausible (e.g., a potassium of 9.0 in a patient with a potassium of 4.2 yesterday).

3. **Click "Verify"** on the Completed lab result row. In the Verify panel, enter:
   - **Verifying Provider ID** -- your laboratory professional ID
   - **Verifying Provider Name** -- your full name and credentials (e.g., "Jane Smith, MT(ASCP)")

   Click **"Verify"** to finalize the result. The order status changes to Verified and the result is released to the patient chart, visible to all clinical users.

> **Warning:** Verification is a legal attestation that the result is accurate and ready for clinical use. Once a result is verified, it becomes part of the permanent medical record. If an error is discovered after verification, the result must be amended through the formal amendment process, which creates an addendum to the original result while preserving the audit trail.

#### Critical Value Notification

When a result has a CH (Critical High) or CL (Critical Low) flag, the system displays a **red critical value banner** that cannot be dismissed until the notification is documented.

1. **Immediately call the ordering provider** (or covering provider) by telephone. Do not rely on electronic notification alone for critical values.

2. **Communicate the critical value.** State the patient name, patient ID, test name, result value, and units. Request a verbal read-back from the provider to confirm accurate communication.

3. **Document the notification** in the critical value notification record:
   - Date and time of notification
   - Name and role of the person notified
   - Read-back confirmed (Yes/No)
   - Any immediate orders received

4. **Click "Acknowledge Critical Value"** to clear the banner and complete the critical value workflow.

> **Warning:** Critical value notification must occur within 30 minutes of result verification per laboratory policy. Failure to notify within the required timeframe is a patient safety event that must be reported. If the ordering provider cannot be reached, follow the critical value escalation chain: covering provider, department chief, nursing supervisor, hospital operator.

![Specimen collection form with tube type selection and bedside labeling reminder](screenshots/lab-specimen-collection.png)

---

## Lab EDI (/lab-edi)

**Route:** `/lab-edi`

The Lab EDI (Electronic Data Interchange) page manages the electronic exchange of lab orders and results between NewVistas and external laboratory information systems, reference laboratories, and health information exchanges. It provides five functional areas: Incoming Orders, Outgoing Results, Interface Status, Error Queue, and Mapping.

### Message Types

The Lab EDI system processes the following HL7 message types:

#### Outbound Messages (NewVistas to External Systems)

| Message Type | Description |
|---|---|
| **ORM** (Order Message) | Transmits new lab orders, order modifications, and order cancellations to reference laboratories and external LIS systems |
| **OML** (Laboratory Order Message) | Transmits laboratory-specific order information including specimen requirements and collection instructions |

#### Inbound Messages (External Systems to NewVistas)

| Message Type | Description |
|---|---|
| **ORU** (Observation Result Unsolicited) | Receives lab results from reference laboratories, including result values, units, reference ranges, and abnormal flags |
| **OUL** (Unsolicited Laboratory Observation) | Receives laboratory-specific observation data including specimen-level information and instrument identifiers |

### Message Statuses

| Status | Description |
|---|---|
| **SENT** | Message has been transmitted to the external system. Awaiting acknowledgment. |
| **ACKNOWLEDGED** | External system has acknowledged receipt of the message (ACK received). |
| **REJECTED** | External system has rejected the message due to a validation error or processing failure. The rejection reason is displayed in the message detail view. |
| **RESULT_RECEIVED** | A result message has been received from the external system and is available for review and filing. |

### Incoming Orders

The Incoming Orders view displays lab orders received from external systems (e.g., orders from clinics using a different EHR that send orders to the hospital lab). Each incoming order can be reviewed, accepted (filed to the lab worklist), or rejected (returned to the sending system with a reason).

### Outgoing Results

The Outgoing Results view displays results that have been transmitted to external systems. Monitor this view for REJECTED results that may need to be re-transmitted after correcting the rejection cause.

### Interface Status

The Interface Status view displays the real-time connection status of all configured EDI interfaces. Each interface row shows the interface name, partner system, connection protocol (TCP/IP, VPN, SFTP), last message sent/received timestamp, and connection health indicator (green = active, yellow = degraded, red = down).

### Error Queue

The Error Queue displays messages that failed to process due to parsing errors, mapping failures, or validation issues. Each error entry shows the message type, timestamp, error description, and raw message content. Errors must be investigated, corrected, and reprocessed or discarded.

### Mapping

The Mapping view manages the translation tables that map between NewVistas internal codes (test codes, specimen types, units) and external system codes. When an incoming message contains an unmapped code, it is routed to the Error Queue. Use the Mapping view to add the missing mapping and reprocess the message.

---

## Lab Instruments (/lab-instruments)

**Route:** `/lab-instruments`

The Lab Instruments page manages the configuration, monitoring, and troubleshooting of automated laboratory analyzers connected to NewVistas. Instrument interfaces enable bidirectional communication: orders and worklists are sent from NewVistas to the analyzer, and results are returned from the analyzer to NewVistas.

![Lab instrument interface dashboard showing connected analyzers and status](screenshots/lab-instrument-interface.png)

### Instrument Configuration

Each instrument entry contains the following configuration fields:

| Field | Description |
|---|---|
| **Name** | Instrument display name (e.g., "Chemistry Analyzer 1", "Hematology Sysmex XN-1000") |
| **Type** | Instrument category (see Instrument Types below) |
| **Manufacturer** | Equipment manufacturer (e.g., "Beckman Coulter", "Siemens", "Roche", "Sysmex", "Abbott") |
| **Model** | Specific model number (e.g., "AU5800", "Atellica", "cobas 8000", "XN-1000", "Alinity") |
| **Serial Number** | Equipment serial number for asset tracking |
| **Connection Type** | Communication protocol: SERIAL, TCP-IP, HL7, ASTM, or USB |
| **IP Address / Port** | Network address and port number (for TCP-IP and HL7 connections) |
| **COM Port / Baud Rate** | Serial port settings (for SERIAL and ASTM connections) |

### Instrument Types

| Type | Description | Common Tests |
|---|---|---|
| **CHEMISTRY** | Automated chemistry analyzers | BMP, CMP, Liver Function, Lipid Panel, Cardiac Enzymes, Renal Function |
| **HEMATOLOGY** | Hematology analyzers and cell counters | CBC, Differential, Reticulocyte Count, ESR |
| **COAGULATION** | Coagulation analyzers | PT/INR, PTT, Fibrinogen, D-Dimer |
| **BLOOD_GAS** | Blood gas and electrolyte analyzers | ABG, VBG, Lactate, ionized Calcium |
| **URINALYSIS** | Automated urine analyzers | UA Dipstick, Urine Microscopy |
| **POINT_OF_CARE** | Point-of-care testing devices | Glucose, iSTAT, Rapid Strep, Rapid Flu, COVID, Urine hCG |
| **MICROBIOLOGY** | Microbiology identification and susceptibility systems | Culture ID, MIC/Susceptibility, Blood Culture monitoring |

### Data Flow Monitoring

The instrument status dashboard displays real-time data flow metrics for each connected instrument:

| Metric | Description |
|---|---|
| **Connection Status** | Connected (green), Intermittent (yellow), or Disconnected (red) |
| **Last Communication** | Timestamp of the most recent message sent or received |
| **Orders Sent (24hr)** | Number of orders transmitted to the instrument in the last 24 hours |
| **Results Received (24hr)** | Number of results received from the instrument in the last 24 hours |
| **Errors (24hr)** | Number of interface errors in the last 24 hours |
| **Uptime** | Percentage of time the interface has been connected in the current day |

### Troubleshooting

When an instrument interface shows a degraded or disconnected status, use the following troubleshooting tools available on the instrument detail view:

| Action | Description |
|---|---|
| **Ping** | Sends a network ping to the instrument's IP address to verify network connectivity. Displays the response time or timeout error. |
| **View Logs** | Opens the interface communication log showing the last 100 messages exchanged (both sent and received) with timestamps, message types, and content. |
| **Reset Connection** | Drops the current connection and re-establishes it. Use this to recover from stale connections or after instrument maintenance. |
| **Send Test Message** | Transmits a test query message to the instrument to verify bidirectional communication. The instrument should respond with an acknowledgment or test result. |

> **Tip:** If an instrument shows "Disconnected" status but the physical analyzer is powered on and operational, check the following in order: (1) network cable is connected, (2) IP address and port match the instrument configuration, (3) the instrument's LIS interface is enabled in the instrument's own settings menu, (4) no firewall is blocking the port, (5) try Reset Connection.

---

## Anatomic Pathology (/anatomic-pathology)

**Route:** `/anatomic-pathology`

The Anatomic Pathology page manages the workflow for surgical pathology, cytology, and autopsy cases. It maps to the VistA Anatomic Pathology package and provides a complete case management system from specimen accessioning through final signout.

![Anatomic pathology case list with summary cards and case detail view](screenshots/anatomic-pathology-cases.png)

### Tabs

The Anatomic Pathology page is organized into five tabs: Cases, Surgical Path, Cytology, Autopsy, and Accession.

### Summary Cards

At the top of the page, four summary cards display current workload metrics:

| Card | Description |
|---|---|
| **Total Cases** | Total number of active cases across all pathology types |
| **Surgical Path** | Number of active surgical pathology cases |
| **Cytology** | Number of active cytology cases |
| **Autopsy** | Number of active autopsy cases |

### Cases Tab

The Cases tab displays all pathology cases in a unified view with the following columns:

| Column | Description |
|---|---|
| **Accession #** | Unique accession number assigned at accessioning (format: SP-YYYY-NNNNN, CY-YYYY-NNNNN, or AU-YYYY-NNNNN) |
| **Type** | Case type: Surgical Pathology (SP), Cytology (CY), or Autopsy (AU) |
| **Patient** | Patient name and ID |
| **Specimen Source** | Anatomic site or specimen description (e.g., "Left breast, upper outer quadrant", "Cervical pap smear", "Complete autopsy") |
| **Received** | Date and time the specimen was received in the pathology laboratory |
| **Status** | Current case status (see Case Workflow below) |
| **Diagnosis** | Preliminary or final diagnosis (blank until microscopic examination) |
| **Pathologist** | Assigned pathologist |

### Case Types

#### Surgical Pathology (SP)

Surgical pathology cases involve tissue specimens removed during surgical procedures, biopsies, and excisions. These are the most common anatomic pathology cases.

**Common specimen types:**
- Biopsies (skin, liver, kidney, prostate, bone marrow)
- Excisions (skin lesions, breast lumpectomies, appendectomies)
- Resections (colectomy, gastrectomy, mastectomy, nephrectomy)
- Frozen sections (intraoperative consultation)

**Key data fields:**
- Gross description (dictated macroscopic examination)
- Cassette count and section summary
- Microscopic description
- Final diagnosis with ICD-10 and SNOMED codes
- Margin status (for excisions and resections)
- Staging (TNM staging for malignant specimens)
- Synoptic report (CAP cancer protocol when applicable)

#### Cytology (CY)

Cytology cases involve the examination of cells from body fluids, washings, brushings, and fine needle aspirates. Gynecologic cytology (Pap smears) uses the Bethesda System for reporting.

**Common specimen types:**
- Gynecologic (Pap smear, ThinPrep, SurePath)
- Non-gynecologic (body fluids, bronchial washings, FNA)

**Bethesda System reporting categories (gynecologic cytology):**
- NILM (Negative for Intraepithelial Lesion or Malignancy)
- ASC-US (Atypical Squamous Cells of Undetermined Significance)
- ASC-H (Atypical Squamous Cells, cannot exclude HSIL)
- LSIL (Low-grade Squamous Intraepithelial Lesion)
- HSIL (High-grade Squamous Intraepithelial Lesion)
- SCC (Squamous Cell Carcinoma)
- AGC (Atypical Glandular Cells)
- AIS (Adenocarcinoma In Situ)
- Adenocarcinoma

**Key data fields:**
- Specimen adequacy (Satisfactory, Unsatisfactory)
- Screening result
- Diagnosis category (Bethesda for gyn; descriptive for non-gyn)
- Ancillary testing (HPV, flow cytometry, immunocytochemistry)

#### Autopsy (AU)

Autopsy cases involve post-mortem examination to determine the cause and manner of death. Autopsies may be complete (full body) or limited (specific organs or body regions).

**Key data fields:**
- Autopsy type (Complete, Limited, External only)
- Authorization (Next of kin consent, Medical examiner order)
- External examination findings
- Internal examination findings by organ system
- Cause of death (immediate cause, antecedent causes, contributing factors)
- Manner of death (Natural, Accident, Suicide, Homicide, Undetermined)
- Toxicology results
- Neuropathology findings (if applicable)
- Clinical-pathologic correlation
- Provisional anatomic diagnoses (issued within 24-48 hours)
- Final autopsy report (issued within 30-60 days)

### Case Workflow

Anatomic pathology cases progress through the following workflow statuses:

```
Accessioned ──> Gross ──> Microscopic ──> Signed Out ──> Addendum/Amend
```

| Status | Description |
|---|---|
| **Accessioned** | Specimen has been received, logged, and assigned an accession number. Awaiting gross examination. |
| **Gross** | Gross (macroscopic) examination is in progress or completed. Tissue has been described, measured, and sectioned into cassettes for processing. |
| **Microscopic** | Slides have been prepared (after tissue processing, embedding, sectioning, and staining) and are available for microscopic examination by the pathologist. |
| **Signed Out** | The pathologist has completed the microscopic examination, rendered a diagnosis, and electronically signed the report. The report is final and released to the patient chart. |
| **Addendum** | An addendum has been added to a previously signed-out case (e.g., additional testing results, revised diagnosis, clinical correlation). The original signed report is preserved. |
| **Amended** | The original diagnosis has been formally amended (changed). The original report, the amendment, and the reason for the amendment are all preserved in the audit trail. |

### Processing a Case

Follow these steps to process an anatomic pathology case from accessioning through signout:

1. **Accession the specimen.** On the Accession tab, enter the patient ID, specimen source, clinical history, and any special instructions. The system generates a unique accession number (e.g., SP-2026-00142). Print the accession label and apply it to the specimen container and requisition. The case status is set to Accessioned.

2. **Perform the gross examination.** On the case detail view, open the Gross Examination section. Dictate or type the gross description, including specimen dimensions, weight, color, consistency, and identifying features. Record the number of cassettes submitted and a section summary describing what tissue is in each cassette. Update the case status to Gross.

3. **Process the specimen.** This step occurs in the histology lab (tissue processing, embedding, microtomy, and staining). While this physical processing occurs outside the system, update any processing notes in the case record as needed. When slides are prepared and ready for microscopic examination, update the case status to Microscopic.

4. **Perform the microscopic examination.** The pathologist reviews the H&E-stained slides and any special stains or immunohistochemistry. The pathologist enters the microscopic description and any ancillary test results in the case record.

5. **Sign out the case.** The pathologist enters the final diagnosis, including:
   - **Diagnosis text** -- the full pathologic diagnosis for each specimen part
   - **ICD-10 code** -- the diagnostic code (e.g., C50.911 for malignant neoplasm of unspecified site of right female breast)
   - **SNOMED code** -- the SNOMED-CT code for the morphology and topography
   - **Margin status** -- for excision/resection specimens (Negative, Positive, Close)
   - **Staging** -- TNM pathologic staging for malignant specimens
   - **Electronic signature** -- the pathologist applies their e-signature to finalize the report

   The case status changes to Signed Out and the report is released to the patient chart.

6. **Addendum (if needed).** If additional information becomes available after signout (e.g., immunohistochemistry results, molecular testing, consultation with another pathologist), click "Add Addendum" to append supplementary findings to the signed report. The original report is preserved intact.

> **Warning:** A malignant diagnosis (any cancer finding) requires immediate notification of the ordering provider. Do not wait for the provider to see the result in the chart. Call the ordering provider directly, communicate the diagnosis, and document the notification in the case record. This is analogous to the critical value notification process for clinical laboratory results.

> **Note:** Frozen section cases require real-time communication between the operating room and the pathology lab. The pathologist dictates the frozen section diagnosis by telephone to the surgeon. The frozen section result and the final diagnosis (which may differ after permanent sections are reviewed) are both recorded in the case.

![Anatomic pathology case detail showing gross description and microscopic diagnosis](screenshots/anatomic-pathology-case-detail.png)

---

## Blood Bank (/blood-bank)

**Route:** `/blood-bank`

The Blood Bank page manages all blood bank and transfusion medicine workflows. It maps to the VistA VBECS (VistA Blood Establishment Computer Software) package. The page provides four tabs: Patient Record, Crossmatches, Transfusions, and Inventory.

![Blood bank page showing patient record with type and screen results](screenshots/blood-bank-patient-record.png)

---

### Patient Record Tab

The Patient Record tab displays the patient's blood bank history and current type and screen status. This is the first tab to review before processing any blood bank request.

#### Patient Blood Bank Fields

| Field | Description |
|---|---|
| **ABO Group** | Patient's ABO blood type: A, B, AB, or O |
| **Rh Type** | Patient's Rh factor: Positive or Negative |
| **Antibody Screen** | Result of the most recent antibody screen: Negative (no unexpected antibodies detected) or Positive (unexpected antibodies detected, requiring identification) |
| **DAT Result** | Direct Antiglobulin Test (Coombs) result: Negative or Positive. A positive DAT may indicate autoimmune hemolytic anemia, transfusion reaction, or hemolytic disease of the newborn. |
| **Identified Antibodies** | List of clinically significant antibodies identified in the patient's serum (e.g., "Anti-K", "Anti-Fya", "Anti-Jka", "Anti-E"). These antibodies require antigen-negative units for transfusion. |
| **Transfusion History** | Summary of the patient's transfusion history including number of previous transfusions, dates, products, and any adverse reactions |
| **Special Requirements** | Special transfusion requirements based on the patient's history and clinical needs (e.g., "Irradiated products", "CMV negative", "Leukoreduced", "Washed", "HbS negative", "Antigen negative: K, Fya") |

> **Note:** Always review the patient's identified antibodies and special requirements before selecting units for crossmatch. Failure to honor antibody and special requirement flags can result in a hemolytic transfusion reaction, which is a life-threatening event.

---

### Crossmatches Tab

The Crossmatches tab manages the crossmatch workflow: matching compatible blood products to a specific patient and verifying serologic compatibility before issuing units for transfusion.

#### Crossmatch Workflow

Follow these steps to perform a crossmatch:

1. **Receive the transfusion request.** Review the provider's order for blood products, including the product type (Packed RBCs, Platelets, FFP, Cryoprecipitate), number of units, and any special requirements. Verify the patient's blood bank specimen is current (type and screen within 72 hours, or as per institutional policy).

2. **Perform the type and screen.** If the patient does not have a current type and screen on file, perform ABO/Rh typing and an antibody screen.
   - **ABO/Rh Typing:** Perform forward and reverse typing to determine the patient's ABO group and Rh type. Two separate determinations are required for the first typing. Compare with historical type on file -- any discrepancy must be investigated before proceeding.
   - **Antibody Screen:** Test the patient's serum against a panel of screening cells. If the screen is positive, proceed to antibody identification using a panel of cells with known antigen profiles. Document all identified antibodies.

3. **Select compatible units.** From the blood bank inventory, identify units that are:
   - ABO/Rh compatible with the patient
   - Antigen-negative for any clinically significant antibodies the patient has
   - Meeting all special requirements (irradiated, CMV negative, etc.)
   - Not expired (check expiration date)
   - Not assigned to another patient

4. **Perform the crossmatch.** For each selected unit, perform serologic crossmatch testing:
   - **Immediate Spin (IS) crossmatch** -- detects ABO incompatibility
   - **Antiglobulin (AHG) crossmatch** -- detects clinically significant antibodies (required when the antibody screen is positive or the patient has a history of antibodies)
   - **Electronic (computer) crossmatch** -- may be used in place of serologic crossmatch when the antibody screen is negative, no history of clinically significant antibodies, and two ABO typings are on file and concordant

   Record the crossmatch result for each unit: Compatible or Incompatible. Incompatible units must not be issued.

5. **Issue units.** Once crossmatch is complete and compatible, the units are available for issue. When the nursing unit requests the blood product:
   - Verify the patient ID on the request matches the crossmatch record
   - Verify the unit number on the blood product bag matches the crossmatch record
   - Verify the ABO/Rh on the blood product bag matches the expected type
   - Record the issue time and the name of the person picking up the product
   - Click "Issue" to update the unit status from Crossmatched to Issued

> **Warning:** Blood product identification errors are the most common cause of fatal transfusion reactions. Always perform a final bedside verification of the patient ID, unit number, ABO/Rh, and expiration date before the transfusion is started. Two qualified individuals must independently verify the identification at the bedside.

---

### Transfusions Tab

The Transfusions tab records and monitors active and completed transfusions. Each transfusion record documents the full transfusion event from start to completion.

#### Transfusion Record Fields

| Field | Description |
|---|---|
| **Product** | Blood product type (e.g., "Packed RBCs", "Platelets", "Fresh Frozen Plasma", "Cryoprecipitate", "Whole Blood") |
| **Unit Number** | Unique identification number of the blood product unit |
| **ABO/Rh** | Blood type of the product unit |
| **Start Time** | Date and time the transfusion was initiated |
| **End Time** | Date and time the transfusion was completed or discontinued |
| **Volume** | Volume transfused in milliliters (mL) |
| **Rate** | Infusion rate (mL/hr) |
| **Reaction** | Whether a transfusion reaction occurred: None, or reaction type (see below) |
| **Vital Signs** | Pre-transfusion, 15-minute, and post-transfusion vital signs (temperature, pulse, blood pressure, respiration, SpO2) |
| **Administering Nurse** | Name of the nurse who started and monitored the transfusion |
| **Verifying Technologist** | Name of the blood bank technologist who issued the unit |

#### Transfusion Monitoring Workflow

1. **Start.** Record the pre-transfusion vital signs (temperature, pulse, blood pressure, respiration, SpO2). Document the start time. The first 15 minutes of any transfusion are the highest risk period for acute reactions -- the infusion rate should be slow during this period.

2. **Monitor.** Record vital signs at 15 minutes after the start of the transfusion. Continue to monitor the patient per institutional protocol (typically every 30-60 minutes during the transfusion and at completion). Document any symptoms reported by the patient (chills, fever, back pain, dyspnea, urticaria, chest pain, anxiety).

3. **Complete.** When the transfusion is finished, record the end time, total volume transfused, and post-transfusion vital signs. Update the transfusion status to Completed.

> **Warning:** If a transfusion reaction is suspected at any point during the transfusion, **stop the transfusion immediately**. Maintain IV access with normal saline. Notify the provider and the blood bank. Return the blood product bag and attached tubing to the blood bank for investigation. Draw a post-transfusion blood specimen (EDTA and clot tube) and send to the blood bank along with the first post-reaction urine specimen. Document all details of the reaction in the transfusion record, including:
> - Time the reaction was recognized
> - Signs and symptoms observed
> - Vital signs at the time of the reaction
> - Volume transfused before the reaction
> - Actions taken
> - Provider notified (name, time)

#### Transfusion Reaction Types

| Reaction Type | Severity | Key Signs/Symptoms |
|---|---|---|
| **Febrile Non-Hemolytic** | Mild | Temperature rise >= 1 C, chills, rigors |
| **Allergic (Mild)** | Mild | Urticaria, pruritus, localized hives |
| **Allergic (Anaphylactic)** | Severe | Hypotension, bronchospasm, angioedema, stridor |
| **Acute Hemolytic** | Severe/Fatal | Fever, flank/back pain, hemoglobinuria, DIC, renal failure |
| **Transfusion-Related Acute Lung Injury (TRALI)** | Severe | Acute respiratory distress, bilateral pulmonary infiltrates, hypoxemia within 6 hours |
| **Transfusion-Associated Circulatory Overload (TACO)** | Moderate-Severe | Dyspnea, hypertension, pulmonary edema, elevated BNP |
| **Bacterial Contamination** | Severe | High fever, rigors, hypotension, rapid onset during or shortly after transfusion |

![Transfusion monitoring screen showing vital sign timeline and reaction documentation](screenshots/blood-bank-transfusion-monitoring.png)

---

### Inventory Tab

The Inventory tab manages the blood bank's product inventory, including receiving units from the blood supplier, monitoring expiration dates, quarantining units, discarding expired or compromised units, and transferring units between facilities.

#### Inventory Functions

| Function | Description |
|---|---|
| **Current Inventory** | Displays all blood products currently in inventory, organized by product type and ABO/Rh. Shows unit number, product type, ABO/Rh, collection date, expiration date, status (Available, Crossmatched, Issued, Quarantined), and storage location. |
| **Receive Units** | Log new units received from the blood supplier. Enter the unit number, product type, ABO/Rh, collection date, expiration date, and supplier. Perform visual inspection for discoloration, clots, or damage before accepting. |
| **Expiration Monitoring** | Displays units approaching expiration within a configurable window (default: 7 days). Units expiring within 48 hours are highlighted in red. Units expiring within 7 days are highlighted in yellow. Expired units are flagged for discard. |
| **Quarantine** | Move units to quarantine status when there is a quality concern (e.g., temperature excursion, donor callback, positive bacterial culture). Quarantined units cannot be crossmatched or issued until released by the medical director. |
| **Discard** | Record the discard of expired, damaged, or recalled units. Enter the discard reason, authorizing person, and disposition method. Discarded units are removed from the available inventory but retained in the audit log. |
| **Transfer** | Transfer units to another facility or blood bank within the health system. Document the receiving facility, transport conditions, and chain of custody. |

> **Note:** Blood product storage temperatures are critical. Packed RBCs must be stored at 1-6 C, Platelets at 20-24 C with continuous agitation, FFP and Cryoprecipitate at <= -18 C. If a unit has been out of controlled storage for more than 30 minutes, it cannot be returned to inventory and must be transfused or discarded. The system logs temperature excursion events when reported.

> **Tip:** Run the Expiration Monitoring report at the start of each shift to identify units that need to be used soon or returned to the blood supplier for credit. This helps minimize wastage of a scarce resource.

---

## Screenshots Reference

The following screenshots illustrate key laboratory workflows in NewVistas:

- ![Lab worklist showing pending orders organized by priority and specimen status](screenshots/lab-worklist-overview.png)
- ![Specimen collection form with tube type dropdown and bedside labeling warning](screenshots/lab-specimen-collection.png)
- ![Anatomic pathology case detail showing gross description and microscopic diagnosis fields](screenshots/anatomic-pathology-case-detail.png)
- ![Blood bank crossmatch screen showing compatible units and serologic results](screenshots/blood-bank-crossmatch.png)
- ![Transfusion monitoring screen with vital sign timeline and reaction documentation](screenshots/blood-bank-transfusion-monitoring.png)
- ![Lab instrument interface dashboard showing connected analyzers and communication status](screenshots/lab-instrument-interface.png)
