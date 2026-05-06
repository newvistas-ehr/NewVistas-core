# Dental

This guide covers the two dental modules in NewVistas: the **Dental Record** module for managing dental eligibility, treatment history, and treatment planning, and the **Periodontal Chart** module for documenting full-mouth periodontal assessments with probing depths, bleeding on probing, and clinical attachment levels.

**Routes:** `/dental`, `/periodontal-chart`
**VistA File:** #228 (Dental)

---

## Dental Record

**Route:** `/dental`

The Dental Record module provides a comprehensive view of the patient's dental history, eligibility status, clinical assessments, and treatment records. It is organized into three tabs with a patient summary card panel at the top of the page.

### Patient Summary Cards

The top of the Dental Record page displays five summary cards that provide an at-a-glance overview of the patient's dental status:

| Card | Description |
|---|---|
| **Eligibility** | Current VA dental eligibility class and description |
| **Periodontal Status** | Current periodontal health status (Healthy, Mild Periodontitis, Moderate Periodontitis, Severe Periodontitis) |
| **Last Exam** | Date of the most recent comprehensive dental examination |
| **Primary Dentist** | Name of the patient's assigned primary dentist |
| **Remaining Teeth** | Number of remaining natural teeth (out of 32) |

![Dental record with patient summary cards and eligibility information](screenshots/dental-record-eligibility.png)

---

### Tab 1: Dental Record

The Dental Record tab contains the patient's dental eligibility information, clinical status, and key visit dates.

#### Eligibility Section

VA dental eligibility determines the scope of dental care a veteran is entitled to receive. NewVistas tracks the patient's eligibility class, which maps to the following VA Dental Eligibility Classes:

| Class | Code | Description |
|---|---|---|
| **Class I** | SC | Service-connected dental condition. Entitled to any dental care needed for the service-connected condition. |
| **Class II** | SC-ADJUNCT 180 days | Veterans with a service-connected non-compensable dental condition who apply within 180 days of separation. One-time completion of treatment. |
| **Class IIA** | SC-CHRONIC | Veterans with a service-connected non-compensable dental condition that is chronic and was not treated within the Class II 180-day period. Adjunctive dental treatment only. |
| **Class IIB** | POW | Former prisoners of war. Entitled to any needed dental care. |
| **Class IIC** | PURPLE_HEART | Purple Heart recipients. Entitled to any needed dental care. |
| **Class III** | NSC-6M | Veterans participating in a VA vocational rehabilitation program. Dental care necessary to complete the program. Limited to 6 months. |
| **Class IV** | SC-50PCT/IU | Veterans with a service-connected disability rated 100% or receiving Individual Unemployability (IU). Entitled to any needed dental care. |
| **Class V** | CLC | Veterans receiving care in a VA Community Living Center (formerly nursing home). Entitled to dental care needed for their condition. |
| **Class VI** | HOMELESS | Veterans participating in the VA Homeless program. Entitled to any needed dental care. |

> **Note:** Dental eligibility is determined by the Veterans Benefits Administration (VBA) and is reflected in the patient's enrollment record. The eligibility class displayed here controls which dental services can be authorized. Contact the dental eligibility coordinator if the displayed eligibility class appears incorrect.

#### Clinical Status

The Clinical Status section documents the patient's current dental health profile:

| Field | Description |
|---|---|
| **Periodontal Status** | Current periodontal classification: Healthy, Mild Periodontitis, Moderate Periodontitis, or Severe Periodontitis |
| **Prosthetic Status** | Current dental prosthetic devices (e.g., "Upper complete denture", "Lower partial denture", "Implant-supported crown #19") |
| **Fluoride** | Fluoride treatment status and prescription (e.g., "Prescription fluoride toothpaste 5000ppm", "In-office fluoride varnish applied") |
| **Clinical Notes** | Free-text clinical notes about the patient's dental condition, treatment needs, or special considerations |

#### Visit Dates

| Field | Description |
|---|---|
| **Last Exam** | Date of the most recent comprehensive dental examination |
| **Last X-Ray** | Date of the most recent dental radiographic examination (panoramic, full-mouth series, or bitewings) |
| **Last Cleaning** | Date of the most recent prophylaxis or periodontal maintenance appointment |

---

### Tab 2: Treatment History

The Treatment History tab displays a complete record of all dental treatments -- planned, in progress, completed, cancelled, and referred out.

#### Filters

The treatment history can be filtered by status:

| Filter | Description |
|---|---|
| **All** | Show all treatments regardless of status |
| **Planned** | Show only treatments that have been planned but not yet started |
| **Completed** | Show only treatments that have been completed |
| **InProgress** | Show only treatments currently in progress |
| **Cancelled** | Show only treatments that were cancelled |
| **ReferredOut** | Show only treatments that were referred to an outside provider |

#### Treatment History Table

Each row in the treatment history table shows:

| Column | Description |
|---|---|
| **Date** | Date the treatment was performed (or planned date, if not yet performed) |
| **CDT Code** | Current Dental Terminology code for the procedure |
| **Description** | Description of the dental procedure |
| **Category** | Treatment category (see categories below) |
| **Teeth** | Tooth number(s) involved |
| **Provider** | Treating dentist or hygienist |
| **Status** | Current status: Planned, InProgress, Completed, Cancelled, or ReferredOut |
| **Charge** | Fee associated with the treatment |

![Dental treatment history with status filters](screenshots/dental-treatment-history.png)

#### Status Workflow

Dental treatments follow this status progression:

```
Planned  ──►  InProgress  ──►  Completed
   │              │
   │              └──────────►  Cancelled
   │
   ├──────────────────────────►  Cancelled
   │
   └──────────────────────────►  ReferredOut
```

- **Planned to InProgress** -- Treatment has begun (e.g., first appointment of a multi-visit procedure)
- **InProgress to Completed** -- Treatment is finished
- **Planned/InProgress to Cancelled** -- Treatment is cancelled (with reason documented)
- **Planned to ReferredOut** -- Treatment is referred to an outside provider (specialist, community dentist)

---

### Tab 3: New Treatment

The New Treatment tab provides the form for documenting a new dental treatment.

| Field | Required | Description |
|---|---|---|
| **CDT Code** | Yes | Current Dental Terminology code. Type to search the CDT catalog (e.g., "D0120" for periodic oral evaluation, "D2740" for crown - porcelain/ceramic). |
| **Description** | Yes | Procedure description. Auto-populated from the CDT code but can be edited. |
| **Category** | Yes | Treatment category. Select from the 10 standard categories (see below). |
| **Tooth Number** | No | Tooth number using the Universal Numbering System (1--32 for permanent teeth, A--T for primary teeth). Leave blank for procedures not specific to a tooth (e.g., oral evaluation, prophylaxis). |
| **Surface** | No | Tooth surface involved, using standard abbreviations: M (Mesial), O (Occlusal), D (Distal), B (Buccal/Facial), L (Lingual). Multiple surfaces can be selected (e.g., "MOD" for a mesial-occlusal-distal restoration). |
| **Date** | Yes | Date the treatment was performed or is planned. |
| **Provider** | Yes | Treating dentist or hygienist. Defaults to the currently signed-in user. |
| **Fee** | No | Fee for the procedure. |
| **Notes** | No | Clinical notes about the treatment, including materials used, complications, or patient instructions. |

![New dental treatment form](screenshots/dental-new-treatment.png)

#### Treatment Categories

| Category | Description | Common CDT Codes |
|---|---|---|
| **Diagnostic** | Examinations, radiographs, diagnostic casts | D0120, D0150, D0210, D0220, D0330 |
| **Preventive** | Prophylaxis, fluoride, sealants | D1110, D1120, D1208, D1351 |
| **Restorative** | Fillings, crowns, inlays, onlays | D2140, D2150, D2160, D2740, D2750 |
| **Endodontics** | Root canals, apicoectomies | D3310, D3320, D3330, D3410 |
| **Periodontics** | Scaling and root planing, periodontal surgery, maintenance | D4341, D4342, D4910, D4260 |
| **Prosthodontics (Removable)** | Complete dentures, partial dentures, relines | D5110, D5120, D5213, D5214, D5730 |
| **Prosthodontics (Fixed)** | Bridges, pontics, retainers | D6210, D6240, D6750 |
| **Oral Surgery** | Extractions, surgical extractions, biopsies | D7140, D7210, D7220, D7285 |
| **Orthodontics** | Comprehensive orthodontic treatment, limited treatment, retainers | D8080, D8090, D8680 |
| **Adjunctive General Services** | Anesthesia, drugs, occlusal guards, emergency treatment | D9110, D9215, D9230, D9940 |

---

## Periodontal Chart

**Route:** `/periodontal-chart`

The Periodontal Chart module provides a full-mouth periodontal assessment tool for documenting probing depths, bleeding on probing, recession, and calculated clinical attachment levels. This is the standard six-point charting used in comprehensive periodontal evaluations.

### Chart Structure

The periodontal chart covers all 32 teeth (numbered 1--32 using the Universal Numbering System), with **6 measurement sites per tooth**:

| Site Abbreviation | Full Name | Location |
|---|---|---|
| **MB** | Mesio-Buccal | Mesial surface, buccal/facial aspect |
| **B** | Buccal | Mid-buccal/facial surface |
| **DB** | Disto-Buccal | Distal surface, buccal/facial aspect |
| **ML** | Mesio-Lingual | Mesial surface, lingual/palatal aspect |
| **L** | Lingual | Mid-lingual/palatal surface |
| **DL** | Disto-Lingual | Distal surface, lingual/palatal aspect |

This yields a maximum of **192 sites** for a full-mouth charting (32 teeth x 6 sites).

### Measurements

At each site, the following measurements are recorded:

| Measurement | Unit | Description |
|---|---|---|
| **Probing Depth** | mm | Depth of the periodontal pocket measured from the gingival margin to the base of the sulcus/pocket. Measured with a calibrated periodontal probe. |
| **BOP (Bleeding on Probing)** | Yes/No | Whether bleeding was observed upon gentle probing of the sulcus. Indicates active inflammation. |
| **Recession** | mm | Distance from the cemento-enamel junction (CEJ) to the gingival margin. Positive values indicate gingival recession (margin apical to CEJ). |
| **CAL (Clinical Attachment Level)** | mm (calculated) | **CAL = Probing Depth + Recession**. This is automatically calculated by the system. Represents the total loss of connective tissue attachment. |

![Periodontal chart with six-point measurements for all teeth](screenshots/periodontal-chart-measurements.png)

### Disease Classification by Clinical Attachment Level

The calculated CAL values are used to classify periodontal disease severity:

| CAL Range | Classification | Staging | Clinical Significance |
|---|---|---|---|
| **1--2 mm** | Normal / Stage I | Stage I Periodontitis (if pathologic) | Healthy attachment or early disease. Monitor closely. Gingivitis may be present without attachment loss. |
| **3--4 mm** | Moderate / Stage II | Stage II Periodontitis | Moderate attachment loss. Scaling and root planing indicated. Closer recall interval recommended (3--4 months). |
| **>= 5 mm** | Severe / Stage III--IV | Stage III or IV Periodontitis | Severe attachment loss. May require periodontal surgery. Risk of tooth loss. Referral to periodontist should be considered. |

### Summary Metrics

After completing the periodontal charting, the system calculates and displays the following summary metrics:

| Metric | Description |
|---|---|
| **Average Probing Depth** | Mean probing depth across all measured sites (mm) |
| **BOP Sites** | Number of sites with bleeding on probing, and the percentage of total measured sites |
| **Sites > 4 mm** | Number and percentage of sites with probing depth greater than 4 mm |
| **Max Probing Depth** | Maximum probing depth recorded at any site (mm) |
| **Average CAL** | Mean clinical attachment level across all measured sites (mm) |

### Clinical Alerts

The periodontal chart generates clinical alerts based on the following thresholds:

> **Warning:** **BOP > 30%** -- When more than 30% of measured sites demonstrate bleeding on probing, this indicates active periodontal disease. The patient should be placed on a periodontal maintenance program with 3-month recall intervals, and treatment intensification should be considered.

> **Warning:** **Probing depths >= 5 mm** -- Sites with probing depths of 5 mm or greater may require surgical intervention (e.g., flap surgery, osseous surgery, guided tissue regeneration). Referral to a periodontist should be considered, especially if the sites are not responding to non-surgical treatment.

### Completing a Periodontal Chart

1. Navigate to the Periodontal Chart module at `/periodontal-chart`.
2. The chart displays a grid of all 32 teeth with 6 sites each.
3. For each tooth present in the mouth:
   - Enter the **Probing Depth** (mm) at each of the 6 sites (MB, B, DB, ML, L, DL).
   - Mark **BOP** (Yes/No) at each site.
   - Enter **Recession** (mm) at sites where gingival recession is present. Enter 0 for sites with no recession.
4. The system automatically calculates **CAL** (Probing Depth + Recession) for each site.
5. For missing teeth, mark the tooth as absent. No measurements are required for missing teeth.
6. Review the **Summary Metrics** at the bottom of the chart.
7. Review any **Clinical Alerts** generated by the system.
8. Click **Save** to record the periodontal charting.

> **Tip:** For efficiency, many clinicians work through the chart systematically: upper right (teeth 1--8), upper left (teeth 9--16), lower left (teeth 17--24), then lower right (teeth 25--32). Some clinicians prefer to chart all buccal surfaces first, then flip to chart all lingual surfaces.

### Comparing Periodontal Charts Over Time

The Periodontal Chart module stores historical charting data, allowing clinicians to compare measurements between examinations. This is critical for:

- **Treatment response assessment** -- Are probing depths decreasing after scaling and root planing?
- **Disease progression monitoring** -- Is attachment loss increasing despite treatment?
- **Maintenance interval determination** -- Is the current recall interval sufficient to maintain periodontal stability?

---

## Common Workflows

### Comprehensive Dental Examination

1. Navigate to `/dental` and review the patient summary cards for eligibility, last exam date, and periodontal status.
2. Verify or update the patient's **Eligibility** class on the Dental Record tab.
3. Perform and document the clinical examination. Add a new treatment entry on the **New Treatment** tab with CDT code D0150 (Comprehensive Oral Evaluation) or D0120 (Periodic Oral Evaluation).
4. Take dental radiographs as indicated. Document with the appropriate CDT code (e.g., D0210 for full-mouth series, D0274 for bitewings).
5. Navigate to `/periodontal-chart` and complete a full six-point periodontal charting.
6. Review the periodontal summary metrics and clinical alerts.
7. Update the **Clinical Status** section on the Dental Record tab with the current periodontal status and any clinical notes.
8. Develop a treatment plan by adding **Planned** treatments on the New Treatment tab for all recommended procedures.

### Emergency Dental Visit

1. Navigate to `/dental` and review the patient's dental record and eligibility.
2. Document the emergency visit by adding a new treatment entry with CDT code D9110 (Palliative/Emergency Treatment of Dental Pain).
3. Document any additional procedures performed during the emergency visit (e.g., extraction D7140, incision and drainage D7510, temporary restoration D2940).
4. In the treatment notes, document the chief complaint, clinical findings, diagnosis, treatment provided, and follow-up instructions.
5. Update the treatment status from Planned to InProgress or Completed as appropriate.
6. Schedule any necessary follow-up appointments.
7. If the patient requires treatment beyond the scope of the emergency visit, add Planned treatments to the treatment history for future scheduling.

### Periodontal Maintenance Visit

1. Navigate to `/dental` and review the patient's last cleaning date and periodontal status.
2. Navigate to `/periodontal-chart` and complete a new six-point periodontal charting.
3. Compare the new charting with the previous charting to assess stability or progression.
4. Perform prophylaxis (D1110) or periodontal maintenance (D4910) as indicated.
5. Document the treatment on the **New Treatment** tab.
6. Update the **Clinical Status** section with any changes to the periodontal status.
7. Determine the appropriate recall interval based on periodontal stability.

---

## Related Modules

- **[Orders (CPOE)](orders.md)** -- Dental consult orders are placed through the Orders module.
- **[Clinical Notes (TIU)](notes.md)** -- Dental examination notes and operative notes are documented through the Notes module.
- **[Radiology](radiology.md)** -- Dental radiographic images (panoramic, CBCT) may be managed through the Radiology and Imaging modules.
- **[Health Summary](health-summary.md)** -- Dental treatment history can be included in health summary reports.
