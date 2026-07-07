# NewVistas — Features Overview

NewVistas is a modern clinical information system inspired by the U.S. Department of Veterans Affairs' VistA electronic health record. It carries forward the clinical breadth and workflow fidelity that made VistA one of the most complete health records ever built, while running on a contemporary, distributed software foundation.

This document describes what the system does from the point of view of the people who use it — clinicians, nurses, pharmacists, schedulers, billing staff, and administrators. It is organized by area of care rather than by software component, so a reader can find the capabilities that matter to their role.

---

## The Cover Sheet: A Clinician's Starting Point

Every patient encounter begins at the cover sheet, a single screen that assembles the most important facts about a patient in one place. It draws together active problems, current medications, allergies, recent vital signs, recent lab results, upcoming appointments, active clinical reminders, and recent notes. The layout adapts to the clinical specialty of the user, so a primary care physician, a surgeon, and a mental health clinician each see an arrangement suited to their work. The cover sheet is designed for speed: it surfaces the recent and the relevant without requiring the clinician to open a dozen separate screens.

---

## Patient Identity, Registration, and Enrollment

NewVistas maintains a complete patient record covering demographics, contact information, emergency contacts, veteran status, and military service history. New patients are registered and enrolled through a dedicated workflow that captures eligibility and assigns the patient to the appropriate priority group.

Because patients often exist in more than one system or are registered more than once, NewVistas includes a Master Patient Index that searches across records, identifies likely matches using name, date of birth, and other identifiers, and links duplicate records under a single shared identity. When duplicates are confirmed, a patient-merge workflow consolidates them while preserving the clinical history from each. The system tracks both the local record number and the enterprise-wide identifier, so a patient can be recognized consistently across facilities.

Sensitive records receive additional protection. Access controls can restrict who may open a designated patient's chart, and every override is recorded for later review.

---

## Orders and Computerized Provider Order Entry

Order entry is the backbone of day-to-day clinical work, and NewVistas provides a full computerized provider order entry (CPOE) capability. Providers place orders for medications, laboratory tests, imaging studies, procedures, consults, diets, and more from a unified catalog of orderable items.

Before an order is submitted, the system runs clinical checks that look for drug–allergy conflicts, duplicate orders, drug interactions, and questionable dosing frequency, and it surfaces any warnings to the ordering provider. Orders are signed electronically with the provider's credentials, and nursing staff verify them before they are acted upon.

The full order lifecycle is supported: orders can be held and released, discontinued with a documented reason, renewed with a reset stop date, or parked and saved without release for later completion. Orders can be transferred between locations or services, and event-delayed orders can be configured to release automatically when a triggering event — such as an admission or transfer — occurs. Order sets bundle commonly used combinations of orders into templates, including conditional logic, so that a standard protocol can be initiated in a single step. Orders can be associated with diagnosis codes and with the special treatment factors used in veterans' care and billing.

---

## Medications and Pharmacy

The pharmacy capabilities span both outpatient and inpatient settings and follow a prescription from entry through dispensing.

When a prescription is placed, the system validates controlled-substance requirements, including DEA schedule rules and detoxification flags. Prescriptions can be transmitted electronically to a pharmacy, filled and refilled with eligibility checking, dispensed with lot and product tracking, and verified by a pharmacist. The system records patient counseling, generates labels, and maintains a complete refill history. Prescriptions move through clear status stages — active, pending, and non-active — and the system calculates quantity, days' supply, and refill constraints.

Medication safety is woven throughout. NewVistas checks for drug–drug interactions with severity grading, drug–allergy contraindications against the patient's reaction history, and duplicate therapy within a drug class. It maintains a drug master file with formulary information, supports automatic-refill authorization rules, and manages inpatient needs such as IV admixture preparation and ward stock. A formulary index confirms whether a given drug is covered, and drug-safety advisories surface contraindications and warnings at the point of prescribing.

---

## Nursing and Inpatient Care

Nurses have a dedicated set of tools for inpatient care. The Medication Administration Record is generated from a patient's active inpatient orders and highlights medications that are due, so nothing is missed during a shift. Barcode Medication Administration (BCMA) supports scan-and-administer at the bedside, linking each administration back to the underlying order and capturing details such as injection site and the reason a PRN medication was given.

Beyond medications, nursing staff document admission and shift assessments, build and update care plans with specific interventions, and work from task worklists that show orders awaiting verification and medications coming due. The system supports triage at admission, acuity and complexity scoring to inform staffing, and structured end-of-shift handoff reports so care transitions cleanly between shifts.

---

## Laboratory

The laboratory subsystem manages a test from order to verified result. Specimens are accessioned into the lab, collected with tube-type tracking, and processed on technologist worklists. Results are recorded with value, unit, reference range, and abnormal flags, and are verified under provider signature before they become final. Critical results trigger notification and require acknowledgment, and reflex testing chains can be configured so that one result automatically prompts a follow-up test.

To keep the record fast and current, NewVistas maintains a running summary of each patient's most recent result for every test type, along with a history indexed by standardized test codes. The lab also supports quality-control records and electronic data interchange with outside laboratories, so results from external partners flow into the same record.

---

## Vital Signs

Vital signs — temperature, blood pressure, heart rate, respiratory rate, oxygen saturation, height, and weight — are recorded with relevant qualifiers and abnormal flags. Clinicians can view the latest set at a glance, review history over a chosen date range, or trend a single measurement over time to watch how a patient is responding.

---

## Problems, Allergies, and Reminders

The problem list captures each active diagnosis with its code, onset date, responsible provider, and, where relevant, a service-connected flag. Problems can be inactivated with a resolution date, and the full history is preserved with an audit trail.

Allergies are documented with the offending agent, reaction type, severity, and specifics of the reaction, and this information drives the safety checks that run during ordering. When an allergy is removed or changed, the prior entry is preserved historically.

Clinical reminders prompt preventive and follow-up care. Each reminder carries a category, priority, and frequency, appears on the cover sheet when it is due, and is marked complete by the evaluating provider.

---

## Notes and Documentation

Clinical documentation follows a full authoring lifecycle. Providers create progress notes tied to a document type, an author, a location, and the relevant visit. Notes are signed by the author and, where required, cosigned by a supervising provider. Existing notes can be amended with change tracking or extended with an addendum, and note history can be reviewed by date range. Document types are defined centrally so that documentation stays consistent across the organization.

---

## Appointments and Scheduling

Scheduling covers the full arc of an appointment. Staff can view available slots for a clinic or a specific provider, see a clinic's daily capacity including remaining and overbooked slots, and schedule appointments with a defined provider, time, duration, and purpose. Patients are checked in and out with actual times recorded.

The system supports rescheduling and cancellation with enforcement of notice windows and cancellation policies, and it maintains waitlists that patients can join and leave. Provider availability is managed through schedule blocks, with unavailability such as vacation or training recorded separately. Appointment reminders are generated in batches, and reminder and confirmation letters are produced automatically.

---

## Consults and Referrals

Consults coordinate care between services. A provider requests a consult with an urgency level and a provisional diagnosis; the consulting service accepts it and assigns a provider, schedules it to a clinic and date, and completes it with a result note. Throughout, the status is tracked and tracking comments record routing and delays. Consults can be cancelled or discontinued with a reason, and referrals to outside facilities are flagged as interfacility.

---

## Surgery

The surgery subsystem tracks operative events from scheduling through completion. A case is scheduled with its principal procedure, procedure code, date, and specialty, along with the anesthesia technique and pre-operative diagnosis. On completion, the operative report and post-operative diagnosis are recorded, together with the surgeon, anesthesiologist, and location. Cases can be cancelled with a documented reason, and a tracking view supports operating-room scheduling.

---

## Radiology and Imaging

Imaging studies are ordered with clinical history and indication and progress through examination and reporting. The system tracks exam date and time, contrast agent details and any contrast reaction, and radiation dose. Reports are generated with an impression and diagnostic codes, signed, and — when findings are urgent — flagged as critical with notification. Prior studies can be linked for comparison, reports can be amended or corrected, and technologists are assigned to studies with body part and laterality recorded.

---

## Pathology and Blood Bank

Anatomic pathology cases move from specimen receipt through gross and microscopic examination to a coded, released diagnosis, with quality-assurance review and critical-result notification along the way. Cytopathology exams follow their own workflow. The blood bank manages blood-product inventory, performs crossmatching, and maintains each patient's transfusion history.

---

## Admission, Discharge, and Transfer

Inpatient movement is tracked as a sequence of admission, transfer, and discharge events. Admissions record the ward, room and bed, specialty, and attending provider. Transfers carry forward length-of-stay, and discharges capture disposition — home, transfer to another facility, or death — along with discharge and related diagnoses. Because movement events can drive other activity, they can also release event-delayed orders and feed a live ward census.

---

## Mental Health and Patient Safety

NewVistas supports mental health screening through a library of instruments, each with its own scoring and interpretation rules. Screens are recorded with the instrument, date, score, interpretation, and individual responses, and screening history is retained. The system includes suicide-prevention risk screening and flagging and supports structured safety planning. Pain is assessed on standardized scales, with location, quality, and functional interference documented over time.

---

## Immunizations

Immunizations are recorded with the vaccine name, standardized code, date, series, lot, manufacturer, site, route, and dose, and the full history is available. A forecasting capability projects which immunizations are due next based on established schedules.

---

## Diet, Prosthetics, and Dental

Diet orders capture type, texture and consistency, modifications, and calorie level, and can be discontinued when no longer needed. Prosthetic items are issued with their code, category, quantity, cost, and date, with service-connected status noted where relevant. The dental subsystem maintains dental patient records, treatment plans and procedures with tooth surfaces and materials, cost and insurance details, and periodontal charting.

---

## Women's Health, Maternity, and Newborn Care

NewVistas tracks pregnancy episodes and prenatal visits and maintains newborn records and nursery admissions and discharges. A women's health capability generates notifications and maintains an index to support timely follow-up on results and screenings specific to women's care.

---

## Oncology

The oncology capabilities include a cancer registry that records each case with its site and stage, tumor-level tracking, and treatment planning for chemotherapy and radiation. Radiation therapy is managed as a course with defined dose and fractions, down to individual treatment sessions.

---

## Specialized Programs and Registries

Reflecting the breadth of care in a large integrated health system, NewVistas includes dedicated support for a range of specialized populations and programs: spinal cord injury care, polytrauma with its own registry, blind rehabilitation, geriatric and extended care with structured assessments, substance-abuse treatment episodes, compensation and pension evaluations, transplant eligibility and waiting lists, and enrollment of patients in clinical research studies. Genomic and pharmacogenomic testing are supported, including profiling that informs how a patient is likely to metabolize particular drugs.

---

## Home Care and Telehealth

Care that happens in the patient's home is fully represented. Home-care episodes are managed from start of care through discharge, with standardized assessments, care plans, individual visit records, and a daily census. Home telehealth extends monitoring into the home through connected devices such as blood-pressure cuffs, scales, and pulse oximeters. Device readings are ingested on a daily basis, and readings outside expected ranges automatically generate alerts for clinical review.

---

## Eligibility, Insurance, and Billing

NewVistas carries a complete financial and eligibility apparatus. Coverage can be verified in real time before a service, and personal insurance policies are maintained with primary and secondary designations and subscriber relationships. Claim status inquiries are tracked along with their responses.

Billing supports charges, copays, adjustments, refunds, and write-offs. Copay obligations reflect a patient's means-test outcome and priority group, and service-connected conditions drive copay-waiver logic. A billing clock tracks the period of patient responsibility. Accounts receivable is managed with payment and adjustment posting, aging reports, and collection tracking. The system also supports fee-basis care — authorizations, invoices, vendor management, and batch payments to outside providers — and electronic claim transmission with remittance processing.

---

## Quality, Compliance, and Public Health

Quality and compliance are first-class concerns. NewVistas tracks clinical quality measures against recognized programs, manages adverse-event and incident reporting, and supports quality-review cases. A comprehensive audit trail records clinical activity for later review and reporting. On the public-health side, the system supports electronic case reporting for disease surveillance, infection-control monitoring, and outbreak investigation, along with condition surveillance and case matching.

Sensitive information is protected through data-segmentation rules that govern access to specially protected records, and release-of-information workflows manage disclosure authorizations and track what was released. Record-tracking and incomplete-record management help ensure charts are complete and accounted for.

---

## Controlled Substances

Controlled substances receive dedicated handling that goes beyond standard prescribing. The system maintains dispensing and destruction logs, records DEA and state inspections with their own audit trail, enforces schedule-based refill limits, and applies detoxification flags and interaction checks specific to controlled drugs.

---

## Interoperability and External Connections

NewVistas is built to exchange information with the wider health ecosystem. It exposes a FHIR interface for standards-based access to patient, encounter, condition, and observation data, and supports SMART on FHIR applications. It handles electronic claim submission and remittance, secure clinical messaging over the Direct protocol, laboratory data interchange with outside labs, electronic prescribing to retail pharmacies, and export to a national data warehouse. Multi-site federation allows queries and analytics across facilities, and each patient's treating facilities and primary facility are tracked.

---

## Reference Data and Directories

Underpinning the clinical work are the master files and directories that keep terminology and resources consistent: a drug file with formulary and product information, diagnosis and procedure code sets with validation, standardized laboratory codes, procedure codes, and directories for consult services, pharmacies, providers, facilities, clinics, and nursing units. A lexicon search helps users find the right term or code quickly.

---

## Security and Access

Access to the system is governed by role- and key-based security that mirrors the permission model of the underlying VistA heritage, so that the ability to sign orders, verify labs, or authenticate documents is granted deliberately. Internal messaging routes notifications and communications to the appropriate users' mailboxes.

---

## A Note on the Foundation

While this document focuses on features rather than technology, one architectural choice is worth noting because it shapes what the system can do. NewVistas is built on a distributed actor model in which each patient, order, prescription, and clinical record behaves as an independent, stateful software entity. All clinical activity is coordinated through a single workflow layer, which keeps the business rules consistent no matter whether a request arrives from the clinical interface, the programming interface, or an external partner. The result is a system that preserves the deep clinical fidelity of VistA — including its faithful mapping to VistA's underlying data structures — on a foundation designed for reliability and scale.
