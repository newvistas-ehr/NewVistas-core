# NewVistas — A Modern Clinical Information System

NewVistas is an open, modular Electronic Health Record platform inspired by the U.S. Department of Veterans Affairs' VistA and the Indian Health Service's RPMS. Built on **.NET 10**, **Microsoft Orleans** virtual actors, **Blazor Server**, and **ASP.NET Core**, it delivers a distributed, cloud-ready architecture that preserves four decades of VistA clinical wisdom while giving sites a contemporary developer and user experience.

## Patient Care & Clinical Documentation
The heart of NewVistas. Includes the **Patient Registration & Demographics** module, **CPRS-style Cover Sheet**, **Problem List**, **Allergies**, **Vitals**, **Progress Notes (TIU)**, **Health Summary**, **Health Factors**, **Clinical Reminders**, **Encounter Forms**, and **Care Team** management — every chart element a clinician needs at the point of care.

## Orders & Results
A unified **CPRS Order Entry** workflow spanning **Lab**, **Imaging/Radiology**, **Consults**, **Medications**, **Diet**, and **Procedures**, plus **Order Sets**, **Order Checks**, and reusable **Orderable Item** catalogs.

## Pharmacy Suite
Comprehensive medication management: **Outpatient Pharmacy**, **Inpatient Pharmacy**, **IV Admixture**, **BCMA** (bedside scanning), **CMOP** (mail-order), **Auto-Refill**, **Drug File / Formulary**, **Drug Interaction Checking**, **Drug Utilization Review (DUR)**, **Pharmacy POS** claims, **EPCS** controlled-substance e-prescribing, and **Controlled Substances** accountability.

## Laboratory
Full lab lifecycle: **Lab Test Catalog**, **Accessioning & Worklists**, **Auto-Verify Rules**, **QC**, **Anatomic Pathology**, **Blood Bank** with crossmatch and transfusion tracking, **Lab Instrument Interfaces**, **Lab EDI**, and **Lab Shipping/Manifests**.

## Imaging & Radiology
**Radiology Order Tracking**, **Rad Tech** worklists, **Rad Protocols**, **Exam Tracking**, **Imaging Study Management**, and **Radiation Therapy** course/treatment planning.

## Scheduling & Access
**Clinic Scheduling**, **Provider Schedules**, **Appointment Wait Lists**, **Patient Recall**, **External Referrals**, and **Consult Service Directory**.

## Inpatient & ADT
**Admission/Discharge/Transfer**, **Bed Management**, **Ward Census**, **Nursing Acuity**, **Care Plans**, **Triage**, **Shift Handoff**, **Task Worklists**, and **Inpatient Pharmacy Profiles**.

## Specialty Care
Dedicated modules for **Mental Health**, **Suicide Prevention & Safety Plans**, **Substance Abuse Treatment**, **Social Work**, **Dental** with **Periodontal Charting**, **Dietetics**, **Women's Health & Prenatal**, **Oncology** (tumors, treatments, cancer registry), **Spinal Cord Injury**, **Polytrauma/TBI**, **Blind Rehabilitation**, **Geriatrics & Extended Care**, **Home Health**, **Home Telehealth**, **Surgery & Anesthesia**, **Emergency Department**, **Compensation & Pension** with DBQs, and a full **Physical Therapy** subsystem (sessions, goals, home exercises, referrals).

## Revenue Cycle & Financial
**Integrated Billing**, **EDI Claims**, **ERA**, **Accounts Receivable** with aging, **Cashier/Agent Cashier**, **Collection Letters**, **Means Test**, **Insurance & Eligibility Verification**, **Auto-Eligibility Determination**, **Prior Authorization**, **Beneficiary Travel**, **Fee Basis**, **Patient Benefit Plans**, and **Pharmacy POS**.

## Logistics & Administration
**IFCAP** purchasing (vendors, purchase orders/requests, receiving), **Drug Accountability**, **Ward Stock**, **Engineering Work Orders**, **Prosthetics**, **Voluntary Service**, **Site Parameters**, **Bulletins/MailMan**, and **Mass Casualty Incident** management.

## Quality, Compliance & Reporting
**Clinical Quality Measures (CQM)**, **GPRA Reporting**, **Quality Management Incidents & Reviews**, **Audit Trail**, **HIPAA Disclosures**, **Release of Information**, **Advance Directives**, **Patient Risk / PRF**, **MST History**, **Decision Support (DSS)**, **Event Capture**, and the **iCare Dashboard**.

## Public Health & Surveillance
**Immunizations & Forecasting**, **Infection Control / HAI**, **Outbreak Tracking**, **Electronic Case Reporting (eCR)**, **PCC Surveillance**, **Lab Surveillance Taxonomies**, and **Clinical Case Registries**.

## Master Data & Identity
**Master Patient Index (MPI)** with correlation, matching, and merge; **Top Matching** algorithms; **ICD-10**, **CPT**, **LOINC**, **DRG Grouper**, and **Lexicon** terminology services; **New Person** directory; **Patient Access Control** and **Security Key Management**.

## Interoperability
**FHIR Gateway**, **Direct Secure Messaging** with **C-CDA** generation, **Data Segmentation for Privacy (DS4P)**, **Bulk Data Export**, **SMART on FHIR Authorization**, **Lab EDI/HL7**, and **Treating Facility List** for cross-site data sharing.

## Research
**Research Studies & IRB**, **Research Subjects**, and **Transplant Donor/Waitlist** management.

## Architecture Highlights
- **300+ Orleans grains** model every clinical and administrative entity as an independently scalable virtual actor
- A single **Patient Workflow Grain** orchestrates cross-domain operations, mirroring VistA's MUMPS routine boundaries (ORWCV, ORWPT, ORWDX, GMPLSAVE, …)
- **Multiple front-ends**: Blazor Server web UI, WPF desktop, character-mode (CPRS-style) terminal UI, and a dedicated **Patient Portal**
- **Pluggable persistence**: in-memory for development, SQL Server / SQL Express for production
- **REST + OpenAPI** API surface for third-party integration
- Fully **unit and functional tested** against an Orleans `TestCluster`

NewVistas re-imagines VistA for the cloud era — every File #, every MUMPS routine reference, every clinical workflow — rebuilt as type-safe, serializable, horizontally scalable C# in a single coherent codebase.
