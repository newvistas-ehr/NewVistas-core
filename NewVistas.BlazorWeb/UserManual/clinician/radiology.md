# Radiology and Imaging

This guide covers the two modules that manage diagnostic imaging within NewVistas: the **Radiology** module for ordering studies, tracking examinations, and recording interpretations, and the **Imaging** module for capturing and viewing clinical images through PACS integration.

Both modules map to the VistA Radiology/Nuclear Medicine package and the VistA Imaging system, preserving the workflows that radiology technologists, radiologists, and ordering providers use daily.

---

## Radiology

**Route:** `/radiology`
**VistA File:** #75.1 (Radiology/Nuclear Medicine)

The Radiology module tracks diagnostic imaging studies from the moment an order is placed through technologist examination and radiologist interpretation. It supports six imaging modalities and enforces a clear status workflow to ensure every study reaches a final interpretation.

![Radiology studies list showing pending, examined, and completed studies](screenshots/radiology-studies-list.png)

### Tab 1: Studies

The Studies tab displays all radiology orders and studies for the selected patient in reverse chronological order. Each row in the studies table shows:

| Column | Description |
|---|---|
| **Date** | Date the study was ordered or performed |
| **Procedure** | Name of the imaging procedure (e.g., "CT Abdomen/Pelvis with Contrast") |
| **Type** | Imaging modality -- one of General Radiology, CT, MRI, Ultrasound, Nuclear Medicine, or Mammography |
| **Requesting Provider** | The clinician who placed the radiology order |
| **Status** | Current status in the workflow: PENDING, EXAMINED, or COMPLETED |
| **Report** | Indicator showing whether a radiology report has been filed |
| **Actions** | Context-sensitive action buttons based on current status and user role |

#### Status Badges

- **PENDING** -- The order has been placed but the study has not yet been performed. Displayed with a yellow badge.
- **EXAMINED** -- The technologist has performed the imaging examination and images are available for interpretation. Displayed with a blue badge.
- **COMPLETED** -- The radiologist has interpreted the study and filed a final report. Displayed with a green badge.

#### Filtering and Sorting

You can filter the studies list by:

- **Status** -- Show only PENDING, EXAMINED, or COMPLETED studies
- **Imaging Type** -- Filter by modality (General Radiology, CT, MRI, Ultrasound, Nuclear Medicine, Mammography)
- **Date Range** -- Restrict the list to a specific time period

Click any column header to sort the table by that column.

### Tab 2: Study Detail

Selecting a study from the Studies tab opens the Study Detail view, which provides the complete record for a single radiology examination.

![Radiology study detail with report and critical result flag](screenshots/radiology-study-detail.png)

#### Order Information

| Field | Description |
|---|---|
| **Procedure Name** | The ordered imaging procedure |
| **CPT Code** | Current Procedural Terminology code for the procedure |
| **Imaging Type** | Modality category |
| **Requesting Provider** | Ordering clinician |
| **Urgency** | Priority level of the order (Routine, Urgent, STAT) |
| **Location** | Facility or imaging suite where the study is to be performed |
| **Clinical History** | Relevant clinical history provided by the ordering provider |
| **Reason for Study** | Clinical indication or question to be answered |
| **Order Date** | Date and time the order was placed |

#### Examination Information

This section appears once a technologist has performed the examination:

| Field | Description |
|---|---|
| **Examination Date** | Date and time the imaging was performed |
| **Technologist** | Name of the technologist who performed the examination |
| **Images Acquired** | Number of images captured |
| **Contrast Used** | Whether contrast media was administered, and type if applicable |
| **Patient Position** | Positioning used during the study |
| **Technical Notes** | Any technical comments from the technologist |

#### Interpretation

This section appears once a radiologist has completed their interpretation:

| Field | Description |
|---|---|
| **Radiologist** | Interpreting radiologist |
| **Report Date** | Date and time the report was finalized |
| **Report Text** | Full narrative radiology report |
| **Impression** | Summary impression section of the report |
| **Critical Result** | Flag indicating whether the finding is a critical result requiring immediate notification |
| **Critical Result Notification** | Documentation of provider notification for critical results |

> **Warning:** Studies flagged as critical results require immediate provider notification. The system will prompt the interpreting radiologist to document that the ordering provider (or covering provider) has been notified of the critical finding. This notification must be completed before the report can be finalized.

---

### Imaging Modalities

NewVistas supports six imaging modalities, each with specific procedural considerations:

| Modality | Description | Common Procedures |
|---|---|---|
| **General Radiology** | Conventional X-ray imaging | Chest X-ray, skeletal surveys, abdominal series |
| **CT** | Computed Tomography | CT head, CT chest, CT abdomen/pelvis, CT angiography |
| **MRI** | Magnetic Resonance Imaging | MRI brain, MRI spine, MRI joint, MRA |
| **Ultrasound** | Sonographic imaging | Abdominal US, renal US, thyroid US, vascular duplex |
| **Nuclear Medicine** | Radioisotope-based imaging | Bone scan, thyroid scan, VQ scan, PET/CT |
| **Mammography** | Breast imaging | Screening mammogram, diagnostic mammogram |

---

### Ordering a Radiology Study

Ordering providers place radiology orders through the Radiology module or through the CPOE Orders module. The following fields are available when placing a radiology order:

| Field | Required | Description |
|---|---|---|
| **Procedure Name** | Yes | The specific imaging procedure to be ordered. Select from the procedure catalog or type to search. |
| **CPT Code** | No | Auto-populated based on the selected procedure. Can be overridden if needed. |
| **Imaging Type** | No | Modality category. Auto-populated based on the selected procedure. |
| **Requesting Provider** | No | Defaults to the currently signed-in provider. Can be changed if ordering on behalf of another clinician. |
| **Urgency** | No | Priority level: Routine (default), Urgent, or STAT. |
| **Location** | No | Preferred imaging location or facility. |
| **Clinical History** | No | Relevant patient history that will assist the radiologist in interpretation. |
| **Reason for Study** | No | Clinical indication and the specific question to be answered. |

![Order radiology study form](screenshots/radiology-order-form.png)

#### Placing a Radiology Order (Ordering Provider)

1. Navigate to the Radiology module at `/radiology`.
2. Click the **Order Study** button on the Studies tab.
3. Enter the **Procedure Name** (required). As you type, matching procedures appear in a dropdown. Select the appropriate procedure.
4. Review and complete the remaining fields. Provide thorough Clinical History and Reason for Study to support the radiologist's interpretation.
5. Click **Submit Order** to place the order.

> **Tip:** Providing detailed clinical history and a specific clinical question in the Reason field helps radiologists deliver a more focused and useful interpretation.

The order will appear in the Studies list with a PENDING status. The study will also appear in the radiology worklist for technologists to schedule and perform.

---

### Performing an Examination (Technologist)

Technologists use the Radiology module to document that an imaging examination has been performed.

1. Navigate to the Radiology module at `/radiology` and locate the PENDING study in the Studies list.
2. Click the **Perform Exam** action button on the study row.
3. Record the examination details:
   - **Examination Date** -- defaults to the current date and time
   - **Technologist** -- defaults to the currently signed-in user
   - **Images Acquired** -- number of images captured
   - **Contrast Used** -- indicate whether contrast was administered
   - **Technical Notes** -- document any technical issues, patient positioning, or other relevant information
4. Verify that images have been uploaded or transmitted to PACS.
5. Click **Complete Examination** to advance the study status from PENDING to EXAMINED.

> **Note:** Once a study is marked as EXAMINED, the images are available for radiologist interpretation. The study will appear in the radiologist's reading worklist.

---

### Interpreting a Study (Radiologist)

Radiologists use the Study Detail view to document their interpretation and file a final report.

1. Navigate to the Radiology module at `/radiology` and locate the EXAMINED study in the Studies list, or access the study from your reading worklist.
2. Click the study row to open the Study Detail view.
3. Review the order information, clinical history, and reason for study.
4. Click the **Interpret Study** action button.
5. Enter the radiology report using the standard report format:
   - **Examination** -- Procedure performed and technique
   - **Clinical Indication** -- Reason for the study
   - **Comparison** -- Prior studies used for comparison, if any
   - **Findings** -- Detailed description of imaging findings, organized by anatomic region or system
   - **Impression** -- Summary of key findings and conclusions, numbered if multiple findings are present
6. If the findings constitute a critical result, check the **Critical Result** flag. The system will require documentation of provider notification before the report can be finalized.
7. Click **Sign Report** to finalize the interpretation and advance the study status from EXAMINED to COMPLETED.

#### Standard Radiology Report Format

A complete radiology report in NewVistas follows this structure:

```
EXAMINATION: [Procedure name and technique]

CLINICAL INDICATION: [Reason for study]

COMPARISON: [Prior studies, if any]

FINDINGS:
[Detailed description of findings]

IMPRESSION:
1. [Primary finding and conclusion]
2. [Secondary finding, if applicable]
3. [Additional findings, if applicable]
```

> **Tip:** Use numbered impressions when multiple findings are present. List the most clinically significant finding first.

---

### Status Workflow

The radiology status workflow enforces a sequential progression:

```
PENDING  ──►  EXAMINED  ──►  COMPLETED
  │                              ▲
  │   (Order placed)             │
  │                              │
  └── Technologist performs ─────┘
      examination, then          │
      Radiologist interprets ────┘
```

- **PENDING to EXAMINED** -- Triggered when the technologist documents that the imaging examination has been performed.
- **EXAMINED to COMPLETED** -- Triggered when the radiologist signs the final interpretation report.

> **Note:** Studies cannot skip statuses. A study must be examined before it can be interpreted, and it must be interpreted before it reaches COMPLETED status.

---

### Critical Results

Critical results are imaging findings that require immediate clinical action. Examples include:

- Acute intracranial hemorrhage
- Aortic dissection
- Pulmonary embolism
- Tension pneumothorax
- Free intraperitoneal air
- Spinal cord compression
- Ectopic pregnancy

When a radiologist flags a study as a critical result, the system enforces the following workflow:

1. The radiologist checks the **Critical Result** checkbox on the interpretation form.
2. The system displays a notification panel requiring the radiologist to document provider notification.
3. The radiologist records:
   - **Provider Notified** -- Name of the provider who was contacted
   - **Notification Method** -- How the provider was contacted (phone, in-person, secure message)
   - **Notification Date/Time** -- When notification occurred
   - **Read-Back Confirmed** -- Whether the provider read back the critical finding
4. The report cannot be signed until notification is documented.

> **Warning:** Failure to document critical result notification will prevent the radiology report from being finalized. This safeguard ensures compliance with ACR Practice Guidelines for communication of diagnostic imaging findings.

---

## Imaging

**Route:** `/imaging`
**VistA File:** #2005 (Image)

The Imaging module provides access to clinical images associated with a patient's record. This includes radiology images stored in PACS as well as clinical photographs, scanned documents, and other image types captured during patient care.

![Imaging viewer showing patient images](screenshots/imaging-viewer.png)

### Tab 1: Images

The Images tab lists all images associated with the selected patient. Each row in the images table shows:

| Column | Description |
|---|---|
| **Date** | Date the image was captured or acquired |
| **Type** | Image type: XRAY, CT, MRI, ULTRASOUND, PHOTO, or DOCUMENT |
| **Description** | Brief description of the image content |
| **Images** | Number of individual images in the image group |
| **Status** | Current status of the image record |
| **Captured By** | Person who captured or uploaded the image |

#### Image Types

| Type | Description | Examples |
|---|---|---|
| **XRAY** | Conventional radiographic images | Chest X-ray, bone films |
| **CT** | Computed tomography image series | CT slices, 3D reconstructions |
| **MRI** | Magnetic resonance image series | MRI sequences, MRA images |
| **ULTRASOUND** | Sonographic images | Ultrasound captures, Doppler images |
| **PHOTO** | Clinical photographs | Wound photos, dermatology images, pre/post-operative photos |
| **DOCUMENT** | Scanned documents | Consent forms, outside records, advance directives |

### Tab 2: Detail

Selecting an image record opens the Detail tab, which shows the full image metadata and provides access to the image viewer.

#### Image Metadata

| Field | Description |
|---|---|
| **Object Type** | Classification of the image object |
| **Procedure Description** | The clinical procedure associated with the image |
| **Image URL** | Direct URL to the full-resolution image in the image store |
| **Thumbnail** | URL to the thumbnail-resolution preview |
| **Image Count** | Number of individual images in the group |
| **DICOM Study UID** | Unique identifier linking to the PACS study. Used for cross-referencing with the DICOM server. |
| **Comments** | Clinical comments associated with the image |
| **Capture Date** | Date and time of image capture |
| **Captured By** | Person who captured or uploaded the image |

---

### Capturing Clinical Images

To add a new image record for a patient:

1. Navigate to the Imaging module at `/imaging`.
2. Click the **Capture Image** button on the Images tab.
3. Complete the capture form:

| Field | Required | Description |
|---|---|---|
| **Object Type** | Yes | Select the type of image being captured (XRAY, CT, MRI, ULTRASOUND, PHOTO, DOCUMENT) |
| **Procedure Description** | Yes | Describe the clinical context for the image |
| **Image URL** | Yes | URL or file path to the image. For PACS images, this is populated automatically. |
| **Thumbnail** | No | URL to a thumbnail version of the image |
| **Image Count** | No | Number of images in this group. Defaults to 1. |
| **DICOM Study UID** | No | The DICOM Study Instance UID for PACS integration. Required for radiology images. |
| **Comments** | No | Additional clinical comments about the image |

4. Click **Save** to create the image record.

---

### PACS Integration

NewVistas integrates with Picture Archiving and Communication Systems (PACS) through the DICOM Study UID field. This integration enables:

- **Cross-referencing** -- Radiology images stored in PACS are linked to the patient's NewVistas record via the DICOM Study UID.
- **Viewer launch** -- Clicking an image with a DICOM Study UID can launch the PACS viewer for full diagnostic-quality image review.
- **Study correlation** -- Images from the Imaging module can be correlated with radiology orders in the Radiology module through shared DICOM identifiers.

> **Note:** PACS viewer integration requires the PACS system to be configured at the site level. Contact your system administrator if the PACS viewer is not launching as expected.

---

### Common Workflows

#### Reviewing Radiology Results (Ordering Provider)

1. Navigate to the Radiology module at `/radiology`.
2. Locate the study in the Studies list. Completed studies show a green COMPLETED badge and a report indicator.
3. Click the study row to open the Study Detail view.
4. Review the report text and impression.
5. If the study is flagged as a critical result, verify that you have been notified and acknowledge the finding.

#### Viewing Clinical Images

1. Navigate to the Imaging module at `/imaging`.
2. Browse the Images list or filter by image type.
3. Click an image row to open the Detail view.
4. Click the image thumbnail or the **View Full Image** button to open the full-resolution image.
5. For DICOM images, click **Open in PACS** to launch the PACS viewer for diagnostic-quality review.

---

## Related Modules

- **[Orders (CPOE)](orders.md)** -- Radiology orders can also be placed through the general Orders module.
- **[Cover Sheet](cover-sheet.md)** -- Recent radiology results appear in the Cover Sheet overview.
- **[Health Summary](health-summary.md)** -- Radiology reports can be included in generated health summaries.
