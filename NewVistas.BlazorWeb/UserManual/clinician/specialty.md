# Specialty Clinical Modules

This guide covers the specialty clinical modules in NewVistas that support specific patient populations, disease processes, and clinical programs. Each module provides specialized documentation and workflow support beyond what is available in the general clinical modules.

**Routes:** `/oncology`, `/radiation-therapy`, `/clinical-procedures`, `/medicine`, `/spinal-cord-injury`, `/blind-rehabilitation`, `/womens-health`, `/prenatal`, `/compensation-pension`

---

## Oncology

**Route:** `/oncology`

The Oncology module supports cancer care documentation including tumor registry, staging, and treatment tracking. It provides a structured record of the patient's cancer diagnosis, extent of disease, treatment modalities, and disease status over time.

### Tumor Registry

The tumor registry maintains the definitive record of each cancer diagnosis. Each tumor registry entry includes:

| Field | Description |
|---|---|
| **Primary Site** | Anatomic site of the primary tumor (e.g., "Lung, upper lobe", "Breast, left", "Colon, sigmoid") using ICD-O-3 topography codes |
| **Histology** | Histologic type of the tumor (e.g., "Adenocarcinoma", "Squamous cell carcinoma", "Ductal carcinoma in situ") using ICD-O-3 morphology codes |
| **Diagnosis Date** | Date the cancer was diagnosed |
| **AJCC Stage** | American Joint Committee on Cancer stage group (see TNM Staging below) |
| **Status** | Current disease status: Active, InRemission, Recurrence, or Deceased |

#### Disease Status

| Status | Description |
|---|---|
| **Active** | The cancer is currently being treated or is progressing. Displayed with a red badge. |
| **InRemission** | The cancer has responded to treatment and there is no current evidence of active disease. Displayed with a green badge. |
| **Recurrence** | The cancer has returned after a period of remission. Displayed with an orange badge. |
| **Deceased** | The patient has died, with or without cancer as the cause of death. Displayed with a gray badge. |

![Tumor registry showing primary site, staging, and disease status](screenshots/oncology-tumor-registry.png)

### TNM Staging

The TNM staging system classifies the extent of cancer based on the size and extent of the primary tumor (T), regional lymph node involvement (N), and distant metastasis (M). NewVistas supports both clinical and pathologic staging:

| Staging Type | Prefix | Description |
|---|---|---|
| **Clinical Staging** | c | Based on physical examination, imaging, endoscopy, biopsy, and other pre-treatment assessments |
| **Pathologic Staging** | p | Based on surgical pathology findings, including surgical resection and lymph node dissection |

#### Clinical Staging Fields

| Field | Values | Description |
|---|---|---|
| **cT** | TX, T0, Tis, T1, T1a, T1b, T1c, T2, T2a, T2b, T3, T4, T4a, T4b | Clinical assessment of primary tumor size and extent |
| **cN** | NX, N0, N1, N1a, N1b, N2, N2a, N2b, N3, N3a, N3b | Clinical assessment of regional lymph node involvement |
| **cM** | M0, M1, M1a, M1b, M1c | Clinical assessment of distant metastasis |

#### Pathologic Staging Fields

| Field | Values | Description |
|---|---|---|
| **pT** | pTX, pT0, pTis, pT1, pT1a, pT1b, pT1c, pT2, pT2a, pT2b, pT3, pT4, pT4a, pT4b | Pathologic assessment of primary tumor |
| **pN** | pNX, pN0, pN1, pN1a, pN1b, pN2, pN2a, pN2b, pN3, pN3a, pN3b | Pathologic assessment of regional lymph nodes |
| **pM** | pM0, pM1, pM1a, pM1b, pM1c | Pathologic assessment of distant metastasis |

#### AJCC Stage Group

The combination of T, N, and M values determines the overall AJCC stage group:

| Stage Group | Description |
|---|---|
| **Stage 0** | Carcinoma in situ |
| **Stage I** | Early-stage localized disease |
| **Stage II** | Locally advanced disease (varies by cancer type) |
| **Stage III** | Regionally advanced disease |
| **Stage IV** | Distant metastatic disease |

#### SEER Stage

The Surveillance, Epidemiology, and End Results (SEER) summary stage is also documented:

| SEER Stage | Description |
|---|---|
| **In Situ** | Abnormal cells present but have not invaded surrounding tissue |
| **Localized** | Cancer limited to the organ where it started |
| **Regional** | Cancer spread to nearby lymph nodes, tissues, or organs |
| **Distant** | Cancer spread to distant parts of the body |
| **Unknown** | Insufficient information to determine stage |

### Treatment Tracking

The oncology module tracks the following treatment types:

| Treatment Type | Description |
|---|---|
| **Chemotherapy** | Systemic cytotoxic drug therapy. Documents regimen name, drugs, cycles, dates, and response. |
| **Radiation** | External beam or internal radiation therapy. Links to the Radiation Therapy module for detailed fraction tracking. |
| **Surgery** | Surgical resection or debulking. Links to the Surgery module for operative details. |
| **Immunotherapy** | Immune checkpoint inhibitors, CAR-T, cytokine therapy. Documents agent, schedule, and response. |
| **Hormone Therapy** | Hormonal manipulation therapy (e.g., tamoxifen, aromatase inhibitors, androgen deprivation). Documents agent and duration. |
| **Targeted Therapy** | Molecularly targeted agents (e.g., tyrosine kinase inhibitors, monoclonal antibodies). Documents agent, biomarker basis, and response. |

---

## Radiation Therapy

**Route:** `/radiation-therapy`

The Radiation Therapy module provides detailed tracking of radiation treatment courses including dose planning, fraction delivery, and treatment status. This module works in conjunction with the Oncology module for comprehensive cancer care documentation.

### Treatment Courses

Each radiation therapy course documents a complete course of radiation treatment:

| Field | Description |
|---|---|
| **Treatment Site** | Anatomic region being irradiated (e.g., "Right breast", "Pelvis", "Brain, whole") |
| **Technique** | Radiation delivery technique (see techniques below) |
| **Total Dose** | Prescribed total dose in Gray (Gy) |
| **Fractions** | Total number of planned fractions |
| **Status** | Course status: Planned, Active, Completed, Suspended, or Cancelled |

#### Radiation Techniques

| Technique | Full Name | Description |
|---|---|---|
| **3D-CRT** | Three-Dimensional Conformal Radiation Therapy | Uses CT imaging to shape radiation beams to the tumor volume |
| **IMRT** | Intensity-Modulated Radiation Therapy | Varies the intensity of the radiation beam across the field to optimize dose distribution |
| **VMAT** | Volumetric Modulated Arc Therapy | Delivers radiation while the machine rotates around the patient, modulating dose rate, gantry speed, and MLC position |
| **SBRT** | Stereotactic Body Radiation Therapy | High-dose, precisely targeted radiation delivered in 1--5 fractions to extracranial sites |
| **SRS** | Stereotactic Radiosurgery | High-dose, precisely targeted radiation delivered in 1--5 fractions to intracranial targets |
| **Brachytherapy** | Internal Radiation Therapy | Radioactive source placed inside or next to the tumor |

![Radiation therapy course showing treatment site, technique, dose, and fraction tracking](screenshots/radiation-therapy-course.png)

#### Course Status Workflow

```
Planned  ──►  Active  ──►  Completed
                │
                ├──────►  Suspended  ──►  Active (resume)
                │
                └──────►  Cancelled
```

- **Planned** -- Treatment plan has been developed but fractions have not yet begun. Gray badge.
- **Active** -- Treatment is in progress; fractions are being delivered. Blue badge.
- **Completed** -- All planned fractions have been delivered. Green badge.
- **Suspended** -- Treatment temporarily halted (e.g., due to toxicity, illness, scheduling). Can be resumed. Yellow badge.
- **Cancelled** -- Treatment course has been permanently cancelled. Gray badge.

### Fraction Tracking

Each individual fraction (treatment session) within a course is documented:

| Field | Description |
|---|---|
| **Fraction Number** | Sequential number of this fraction within the course (e.g., "3 of 30") |
| **Date** | Date the fraction was delivered |
| **Dose** | Dose delivered in this fraction (Gy) |
| **Machine** | Treatment machine used (e.g., linear accelerator name/number) |
| **Therapist** | Radiation therapist who delivered the treatment |
| **Notes** | Treatment notes, including any deviations from plan, patient tolerance, or setup issues |

---

## Clinical Procedures

**Route:** `/clinical-procedures`

The Clinical Procedures module tracks diagnostic and therapeutic procedures performed in clinical subspecialties. These are non-surgical procedures that require specialized equipment and interpretation.

### Procedure Categories

| Category | Description | Examples |
|---|---|---|
| **EEG** | Electroencephalography | Routine EEG, continuous EEG monitoring, sleep-deprived EEG |
| **EMG** | Electromyography | Needle EMG of upper and lower extremities |
| **NCS** | Nerve Conduction Studies | Motor and sensory nerve conduction velocities |
| **Sleep Study** | Polysomnography | Diagnostic polysomnography, split-night study, CPAP titration |
| **Audiometry** | Hearing assessment | Pure tone audiometry, speech audiometry, tympanometry, OAE |
| **PFT** | Pulmonary Function Testing | Spirometry, lung volumes, DLCO, bronchoprovocation |
| **Cardiac Stress** | Cardiac stress testing | Exercise treadmill test, pharmacologic stress test, stress echocardiography, nuclear stress test |
| **Endoscopy** | Gastrointestinal endoscopy | EGD, colonoscopy, ERCP, capsule endoscopy |
| **Other** | Miscellaneous procedures | Any procedure not fitting the above categories |

### Status Workflow

```
ORDERED  ──►  SCHEDULED  ──►  IN_PROGRESS  ──►  COMPLETED
```

- **ORDERED** -- The procedure has been ordered but not yet scheduled.
- **SCHEDULED** -- The procedure has been assigned a date, time, and location.
- **IN_PROGRESS** -- The procedure is currently being performed.
- **COMPLETED** -- The procedure has been completed and results are available.

---

## Medicine

**Route:** `/medicine`

The Medicine module supports subspecialty medical consultations and procedures. It provides structured documentation for subspecialty evaluations, procedures, and results.

### Supported Subspecialties

| Subspecialty | Description | Common Activities |
|---|---|---|
| **Cardiology** | Heart and vascular medicine | Echocardiography, cardiac catheterization, electrophysiology studies, device management (pacemakers, ICDs) |
| **Pulmonology** | Lung and respiratory medicine | Bronchoscopy, chest tube management, ventilator management, sleep medicine |
| **GI (Gastroenterology)** | Digestive system medicine | Endoscopy, colonoscopy, liver biopsy, ERCP, motility studies |
| **Nephrology** | Kidney medicine | Dialysis management, kidney biopsy, transplant evaluation |
| **Rheumatology** | Autoimmune and musculoskeletal medicine | Joint aspiration/injection, infusion therapy, disease activity monitoring |
| **Neurology** | Nervous system medicine | EEG, EMG/NCS, lumbar puncture, Botox injection, neuropsychological testing |

### Status Workflow

```
ORDERED  ──►  SCHEDULED  ──►  IN_PROGRESS  ──►  COMPLETED  ──►  RESULTED
```

- **ORDERED** -- The subspecialty evaluation or procedure has been ordered.
- **SCHEDULED** -- The evaluation/procedure has been assigned a date.
- **IN_PROGRESS** -- The evaluation/procedure is currently being performed.
- **COMPLETED** -- The evaluation/procedure has been completed.
- **RESULTED** -- Final results and interpretation have been entered and are available for review.

---

## Spinal Cord Injury

**Route:** `/spinal-cord-injury`

The Spinal Cord Injury (SCI) module provides specialized documentation for veterans with spinal cord injuries and related neurological conditions. It supports comprehensive assessment, longitudinal tracking, and multidisciplinary management of this complex patient population.

### Classification

| Field | Description |
|---|---|
| **Injury Type** | Category of neurological condition: SCI (Spinal Cord Injury), Spinal Cord Disorder, MS (Multiple Sclerosis), ALS (Amyotrophic Lateral Sclerosis), or Other |
| **Etiology** | Cause of the condition: Traumatic (motor vehicle accident, fall, violence, sports, combat) or Non-traumatic (vascular, tumor, infection, degenerative, congenital) |

### Neurological Assessment

| Field | Description |
|---|---|
| **Neurological Level** | Highest spinal level with normal sensory and motor function. Ranges from C1 to S5. |
| **AIS Grade** | American Spinal Injury Association Impairment Scale grade |

#### AIS (ASIA Impairment Scale) Grades

| Grade | Description |
|---|---|
| **A -- Complete** | No sensory or motor function is preserved in sacral segments S4-S5 |
| **B -- Sensory Incomplete** | Sensory but not motor function is preserved below the neurological level and includes sacral segments S4-S5 |
| **C -- Motor Incomplete** | Motor function is preserved below the neurological level, and more than half of key muscles below the neurological level have a muscle grade less than 3 |
| **D -- Motor Incomplete** | Motor function is preserved below the neurological level, and at least half of key muscles below the neurological level have a muscle grade of 3 or more |
| **E -- Normal** | Sensory and motor function are normal in all segments. Patient may have had prior deficits. |

![SCI classification showing neurological level and AIS grade](screenshots/sci-classification.png)

### Management Domains

The SCI module tracks management across several key domains:

| Domain | Description |
|---|---|
| **Bladder Management** | Type of bladder management (intermittent catheterization, indwelling catheter, condom catheter, suprapubic catheter, volitional voiding), frequency, complications |
| **Bowel Management** | Bowel program details (digital stimulation, suppositories, mini-enema, oral medications), frequency, bowel accident frequency |
| **Locomotion** | Mobility status (manual wheelchair, power wheelchair, ambulates with device, ambulates independently), wheelchair specifications, seating evaluation dates |
| **Living Situation** | Current living arrangement (independent, with caregiver, assisted living, VA CLC, other), home modifications, attendant care needs |
| **Associated Conditions** | Chronic conditions associated with SCI: neurogenic pain, spasticity, autonomic dysreflexia, pressure injuries, DVT, UTI, respiratory complications, depression |

### Longitudinal Tracking

| Feature | Description |
|---|---|
| **Annual Encounters** | SCI patients are recommended to have a comprehensive annual evaluation. The module tracks annual encounter dates, findings, and follow-up plans. |
| **FIM Scores** | Functional Independence Measure scores track the patient's functional status across motor and cognitive domains. Scores range from 18 (total dependence) to 126 (complete independence). |

---

## Blind Rehabilitation

**Route:** `/blind-rehabilitation`

The Blind Rehabilitation module supports veterans with visual impairment, providing documentation for visual assessment, rehabilitation program enrollment, and adaptive device management.

### Visual Acuity

| Field | Description |
|---|---|
| **OD (Right Eye)** | Best corrected visual acuity in the right eye |
| **OS (Left Eye)** | Best corrected visual acuity in the left eye |
| **Legal Blindness Status** | Whether the patient meets the criteria for legal blindness (best corrected VA 20/200 or worse in the better eye, or visual field 20 degrees or less) |

Visual acuity values include: 20/20 through 20/200, Count Fingers (CF), Hand Motion (HM), Light Perception (LP), and No Light Perception (NLP).

### Program Admissions

The Blind Rehabilitation module tracks admissions to VA Blind Rehabilitation programs:

| Field | Description |
|---|---|
| **Program** | Type of rehabilitation program (Blind Rehabilitation Center, Visual Impairment Services Team, Computer/Electronic Access Training, Low Vision Clinic) |
| **Admission Date** | Date of program admission |
| **Discharge Date** | Date of program discharge |
| **Goals** | Rehabilitation goals established at admission (e.g., "Independent travel", "Computer access", "Daily living skills") |
| **Discharge Summary** | Summary of progress toward goals and recommendations at discharge |

### Adaptive Devices

The module tracks adaptive devices prescribed or issued to the patient:

| Device Category | Examples |
|---|---|
| **Low Vision Aids** | Magnifiers (handheld, stand, spectacle-mounted), CCTV/video magnifiers, telescopes |
| **Mobility Aids** | White cane (support, identification, or long), guide dog, GPS navigation devices |
| **Assistive Technology** | Screen readers (JAWS, NVDA), screen magnification software (ZoomText), refreshable braille displays, talking devices |
| **Daily Living Aids** | Talking watches/clocks, talking scales, tactile marking systems, adapted kitchen tools, large-print materials |

---

## Women's Health

**Route:** `/womens-health`

The Women's Health module supports preventive care screenings and health maintenance for female veterans. It tracks adherence to evidence-based screening recommendations and integrates with clinical reminders.

### Screening Dashboard

The screening dashboard tracks the status of recommended health screenings:

| Screening | Description | Recommended Interval |
|---|---|---|
| **Mammography** | Breast cancer screening via mammogram | Every 1--2 years based on age and risk factors |
| **Cervical (Pap/HPV)** | Cervical cancer screening via Pap smear and/or HPV testing | Pap every 3 years (age 21--29), co-testing every 5 years (age 30--65) |
| **Bone Density (DEXA)** | Osteoporosis screening via dual-energy X-ray absorptiometry | Age 65+ or earlier based on risk factors |
| **Colorectal** | Colorectal cancer screening | Beginning at age 45, per USPSTF guidelines |

#### Screening Status Badges

| Status | Badge Color | Description |
|---|---|---|
| **DUE** | Yellow | Screening is currently recommended and should be scheduled or performed |
| **OVERDUE** | Red | Screening is past the recommended date and should be performed as soon as possible |
| **SCHEDULED** | Blue | Screening has been scheduled but not yet performed |
| **COMPLETED** | Green | Screening has been performed and results are available |
| **DECLINED** | Gray | Patient has declined the recommended screening after informed discussion |

![Women's health screening dashboard showing due, completed, and overdue screenings](screenshots/womens-health-screening-dashboard.png)

---

## Prenatal Care

**Route:** `/prenatal`

The Prenatal Care module supports obstetric care documentation for pregnant veterans. It provides structured tracking of the pregnancy timeline, prenatal visits, and key laboratory results.

### Pregnancy Information

| Field | Description |
|---|---|
| **EDD** | Estimated Date of Delivery, calculated from last menstrual period or ultrasound dating |
| **Gestational Age** | Current gestational age in weeks and days |
| **Gravida** | Total number of pregnancies (including current) |
| **Para** | Number of deliveries reaching viability (may be expressed as full term/preterm/abortions/living) |
| **Risk Factors** | Identified risk factors for the current pregnancy (e.g., advanced maternal age, gestational diabetes, hypertension, previous preterm delivery) |

### Prenatal Visit Records

Each prenatal visit is documented with the following standard measurements:

| Field | Unit | Description |
|---|---|---|
| **Gestational Age** | weeks + days | Gestational age at the time of the visit |
| **Weight** | lbs or kg | Maternal weight |
| **Blood Pressure** | mmHg | Systolic/diastolic blood pressure |
| **Fundal Height** | cm | Measurement from the pubic symphysis to the top of the uterus (correlates with gestational age in weeks) |
| **FHT (Fetal Heart Tones)** | bpm | Fetal heart rate |
| **Protein** | Negative/Trace/1+/2+/3+/4+ | Urine protein dipstick result (screening for preeclampsia) |
| **Glucose** | Negative/Trace/1+/2+/3+/4+ | Urine glucose dipstick result (screening for gestational diabetes) |
| **Edema** | None/Trace/1+/2+/3+/4+ | Peripheral edema assessment |

### Key Laboratory Results

The prenatal module tracks the following standard prenatal labs:

| Lab Test | Timing | Purpose |
|---|---|---|
| **Blood Type and Rh** | First visit | ABO blood type and Rh factor determination; Rh-negative patients require RhoGAM |
| **CBC** | First visit, 28 weeks | Screen for anemia and other hematologic conditions |
| **GBS (Group B Streptococcus)** | 36--37 weeks | Screen for GBS colonization; positive results require intrapartum antibiotic prophylaxis |
| **Glucose Tolerance Test** | 24--28 weeks | Screen for gestational diabetes mellitus |

---

## Compensation and Pension

**Route:** `/compensation-pension`

The Compensation and Pension (C&P) module supports the medical examination process for disability compensation and pension claims. This module is used by VA examiners to document findings that are submitted to the Veterans Benefits Administration (VBA) for rating decisions.

### C&P Examinations

Each C&P examination tracks:

| Field | Description |
|---|---|
| **Examination Type** | Type of C&P examination (e.g., General Medical, PTSD, Musculoskeletal, Hearing Loss, TBI) |
| **Requested By** | Entity that requested the examination (typically VBA Regional Office) |
| **Examiner** | VA clinician performing the examination |
| **Status** | Current status in the examination workflow |

#### Examination Status Workflow

```
REQUESTED  ──►  SCHEDULED  ──►  IN_PROGRESS  ──►  COMPLETED  ──►  SUBMITTED_TO_VBA
```

- **REQUESTED** -- VBA has requested the examination. Gray badge.
- **SCHEDULED** -- The examination has been assigned a date and examiner. Blue badge.
- **IN_PROGRESS** -- The examination is currently being conducted. Yellow badge.
- **COMPLETED** -- The examiner has completed the examination and documentation. Green badge.
- **SUBMITTED_TO_VBA** -- The examination results have been submitted to VBA for rating. Purple badge.

![C&P examination workflow showing status progression from requested to submitted](screenshots/cp-exam-workflow.png)

### Disability Benefits Questionnaires (DBQs)

DBQs are standardized examination forms used by C&P examiners to document disability-specific findings. Each DBQ has its own status workflow:

```
DRAFT  ──►  COMPLETED  ──►  SIGNED  ──►  SUBMITTED
```

- **DRAFT** -- The DBQ is being prepared. The examiner is entering findings. Gray badge.
- **COMPLETED** -- All required fields have been completed. Blue badge.
- **SIGNED** -- The examiner has applied their electronic signature to the DBQ. Green badge.
- **SUBMITTED** -- The DBQ has been submitted as part of the C&P examination package to VBA. Purple badge.

DBQs cover specific disability categories including:

- General Medical
- PTSD
- Mental Disorders (non-PTSD)
- Musculoskeletal (Back, Neck, Shoulder, Knee, Hip, Ankle, Hand/Fingers)
- Hearing Loss and Tinnitus
- Traumatic Brain Injury (TBI)
- Respiratory Conditions
- Cardiovascular Conditions
- Skin Conditions
- Diabetes Mellitus
- Eye Conditions
- And many more condition-specific forms

### Medical Opinion

The Medical Opinion section of a C&P examination documents the examiner's professional assessment of the nexus (connection) between the veteran's current condition and their military service:

| Field | Description |
|---|---|
| **Condition** | The medical condition being evaluated |
| **Military Service Connection** | The examiner's opinion on whether the condition is related to military service |
| **Rationale** | Detailed medical rationale supporting the opinion, based on review of service treatment records, post-service records, and current examination findings |
| **Opinion Statement** | Standardized opinion language (e.g., "At least as likely as not (50% or greater probability) that the condition is related to military service") |

> **Note:** The C&P examination is a medical examination, not a legal determination. The examiner provides medical findings and opinions; the actual disability rating decision is made by VBA rating officials.

---

## Related Modules

- **[Orders (CPOE)](orders.md)** -- Orders for specialty procedures, lab tests, and consultations are placed through the Orders module.
- **[Consults](consults.md)** -- Referrals to specialty services are tracked through the Consults module.
- **[Radiology](radiology.md)** -- Imaging studies supporting specialty care are managed through the Radiology module.
- **[Laboratory](labs.md)** -- Laboratory results supporting specialty evaluations are reviewed in the Labs module.
- **[Clinical Notes (TIU)](notes.md)** -- Specialty encounter notes, operative notes, and DBQs may be documented through the Notes module.
- **[Surgery](surgery.md)** -- Surgical procedures performed as part of oncology or other specialty treatment are tracked in the Surgery module.
- **[Medications](medications.md)** -- Specialty medications (chemotherapy, immunotherapy, MAT) are managed through the Medications module.
