# Clinician / Provider Guide

This guide is written for physicians, nurse practitioners, physician assistants, and other licensed independent practitioners who use the NewVistas clinical information system. It covers the core clinical workflows you will perform daily: reviewing patient data, writing and signing orders, documenting encounters, managing problem lists, and reviewing results.

![Provider Dashboard overview](screenshots/provider-dashboard-overview.png)

---

## Role Description

As a clinician or provider in NewVistas, your responsibilities include:

- **Reviewing patient data** -- demographics, active problems, allergies, medications, lab results, vitals, and clinical reminders via the Cover Sheet and individual clinical modules.
- **Writing and signing orders** -- placing laboratory, pharmacy, radiology, consult, nursing, diet, and vitals orders through the Computerized Provider Order Entry (CPOE) system.
- **Documenting encounters** -- creating progress notes, discharge summaries, consult notes, surgical notes, crisis notes, and advance directives using the TIU (Text Integration Utilities) document framework.
- **Managing problem lists** -- adding, reviewing, and updating the patient's active and inactive problem list with ICD-10 coded diagnoses.
- **Reviewing results** -- monitoring lab results, vital signs, radiology reports, and consult outcomes for actionable findings.
- **Addressing clinical reminders** -- resolving due clinical reminders for preventive care, screenings, and follow-up actions.
- **Signing documents** -- applying your electronic signature to orders and clinical notes to authorize and finalize them.

---

## Daily Workflow Overview

A typical clinical day in NewVistas follows this six-step workflow:

1. **Sign in and review the Provider Dashboard** (`/provider-dashboard`). The dashboard shows your scheduled patients for the day, your patient panels, and action items requiring your attention such as unsigned notes and pending cosignatures.

2. **Select a patient via Patient Lookup** (`/patient-lookup`). Search by patient name, ID, or other identifiers. Selecting a patient establishes the patient context that carries across all clinical modules.

3. **Review the Cover Sheet** (`/cover-sheet`). This is the CPRS-style unified overview of the patient's clinical status. It shows active problems, allergies, medications, clinical reminders, recent labs, recent vitals, appointments, and active orders in a single grid view.

4. **Document and order**. Based on your clinical assessment:
   - Update the problem list (`/problems`) with new diagnoses or status changes.
   - Write orders (`/orders`) for labs, medications, radiology, consults, and other services.
   - Document the encounter (`/notes`) with a progress note or other document type.
   - Address clinical reminders (`/reminders`) that are due for the patient.

5. **Sign all orders and notes**. Use your electronic signature code to sign pending orders and unsigned notes. Unsigned documents appear as action items on your Provider Dashboard until signed.

6. **End-of-day review on the Provider Dashboard**. Before ending your session, return to the Provider Dashboard to verify that all notes are signed, all orders are authorized, and no action items remain pending.

---

## Provider Dashboard

**Route:** `/provider-dashboard`

The Provider Dashboard is your home base in NewVistas. It provides an at-a-glance summary of your clinical workload for the day.

![Today's Schedule panel on the Provider Dashboard](screenshots/provider-dashboard-schedule.png)

### Today's Schedule

The Today's Schedule panel lists all patients scheduled to see you today. Each row shows:

- Patient name and ID
- Appointment time
- Clinic location
- Appointment type (e.g., Follow-Up, New Patient, Consult)
- Check-in status

Click any patient row to navigate directly to their Cover Sheet.

### My Patients

The My Patients panel lists patients currently assigned to your care, including inpatients on your service and outpatients in your primary care panel.

### Upcoming

The Upcoming section shows appointments scheduled for the next several days, helping you prepare for future visits.

### Action Items

The Action Items panel surfaces work that requires your immediate attention:

- **Unsigned Notes** -- Progress notes, discharge summaries, and other documents you have authored but not yet signed.
- **Pending Cosignatures** -- Notes written by trainees or other providers that require your cosignature for completion.
- **Unsigned Orders** -- Orders that have been placed but not yet signed with your electronic signature.
- **Abnormal Results** -- Lab results or other findings flagged as abnormal that you have not yet acknowledged.

![Action Items panel on the Provider Dashboard](screenshots/provider-dashboard-action-items.png)

> **Tip:** Make it a habit to clear your Action Items panel before the end of each clinical session. Unsigned notes and orders may delay patient care.

---

## Module Quick Reference

The following table lists all clinical modules available to clinicians, with links to their detailed documentation.

| Module | Route | Documentation |
|---|---|---|
| Cover Sheet | `/cover-sheet` | [cover-sheet.md](cover-sheet.md) |
| Patient Lookup | `/patient-lookup` | [patient-lookup.md](patient-lookup.md) |
| Orders (CPOE) | `/orders` | [orders.md](orders.md) |
| Clinical Notes (TIU) | `/notes` | [notes.md](notes.md) |
| Problems | `/problems` | [problems-allergies.md](problems-allergies.md) |
| Allergies | `/allergies` | [problems-allergies.md](problems-allergies.md) |
| Medications | `/medications` | [medications.md](medications.md) |
| Laboratory | `/labs` | [labs.md](labs.md) |
| Vitals | `/vitals` | [vitals.md](vitals.md) |
| Consults | `/consults` | [consults.md](consults.md) |
| Radiology | `/radiology` | [radiology.md](radiology.md) |
| Surgery | `/surgery` | [surgery.md](surgery.md) |
| Mental Health | `/mental-health` | [mental-health.md](mental-health.md) |
| Immunizations | `/immunizations` | [immunizations.md](immunizations.md) |
| Clinical Reminders | `/reminders` | [reminders.md](reminders.md) |
| Care Team | `/care-team` | [care-team.md](care-team.md) |
| Health Summary | `/health-summary` | [health-summary.md](health-summary.md) |
| Physical Therapy | `/pt` | [physical-therapy.md](physical-therapy.md) |
| Dental | `/dental` | [dental.md](dental.md) |
| Specialty Care | Various | [specialty.md](specialty.md) |

---

## Sub-Role Guides

Certain clinical staff members operate within the clinician role but have specialized workflows. Refer to these guides for role-specific instructions:

- **[Nurse Guide](nurse.md)** -- Covers nursing-specific workflows including vital sign entry, medication administration (BCMA), nursing assessments, nursing care plans, triage, and task worklists.
- **[Lab Technician Guide](lab-technician.md)** -- Covers the laboratory specimen collection workflow, result entry, result verification, instrument interfaces, and lab batch processing.

---

## Related Guides

- **[Pharmacist Guide](../pharmacist/index.md)** -- For pharmacy staff handling prescription processing, drug utilization review, IV pharmacy, controlled substances, and medication dispensing.
- **[Administrator Guide](../admin/index.md)** -- For system administrators managing site parameters, user access, scheduling configuration, and system maintenance.
