# NewVistas Clinical Information System — User Manual

## Overview

NewVistas is a modern clinical information system inspired by the U.S. Department of Veterans Affairs' **VistA** (Veterans Health Information Systems and Technology Architecture) electronic health record. It provides comprehensive clinical, administrative, and financial functionality for healthcare organizations.

Built on Microsoft Orleans distributed actor technology with a Blazor Server interactive frontend, NewVistas delivers real-time clinical workflows across more than 80 functional modules spanning patient care, pharmacy operations, laboratory services, surgical management, specialty clinical domains, and financial/administrative systems.

NewVistas maps directly to VistA's FileMan database structure. Each clinical domain corresponds to one or more VistA file numbers (e.g., Patient = File #2, Orders = File #100, TIU Documents = File #8925, Labs = File #63). Users familiar with CPRS (Computerized Patient Record System) will recognize many of the same workflows and terminology.

![NewVistas home page after login](screenshots/home-page.png)

---

## Role-Based Guides

This manual is organized by role. Select the guide that matches your job function:

| Guide | Audience | Description |
|---|---|---|
| [Clinician / Provider Guide](clinician/index.md) | Physicians, NPs, PAs, Nurses, Surgeons, Radiologists, Lab Techs, Mental Health Providers, Dental Providers | Core clinical workflows: patient assessment, ordering, documentation, results review, nursing, lab processing |
| [Pharmacist Guide](pharmacist/index.md) | Pharmacists, Pharmacy Technicians | Prescription processing, formulary management, drug accountability, controlled substances, BCMA |
| [Administrative Guide](admin/index.md) | Registration Clerks, Billing Specialists, HIM Staff, Quality/Safety Officers, Social Workers, System Administrators | Registration, scheduling, billing, records management, system configuration, quality reporting |

For system requirements, login procedures, navigation, and common UI patterns shared by all roles, see the [Getting Started Guide](getting-started.md).

---

## Quick Links

### Clinical

| Module | Route | Guide |
|---|---|---|
| Cover Sheet | `/cover-sheet` | [Clinician](clinician/cover-sheet.md) |
| Orders | `/orders` | [Clinician](clinician/orders.md) |
| Notes | `/notes` | [Clinician](clinician/notes.md) |
| Problems & Allergies | `/problems`, `/allergies` | [Clinician](clinician/problems-allergies.md) |
| Medications | `/medications` | [Clinician](clinician/medications.md) |
| Labs | `/labs` | [Clinician](clinician/labs.md) |
| Vitals | `/vitals` | [Clinician](clinician/vitals.md) |
| Consults | `/consults` | [Clinician](clinician/consults.md) |
| Radiology & Imaging | `/radiology`, `/imaging` | [Clinician](clinician/radiology.md) |
| Surgery & Anesthesia | `/surgery`, `/anesthesia-tracking` | [Clinician](clinician/surgery.md) |
| Mental Health | `/mental-health`, `/suicide-prevention` | [Clinician](clinician/mental-health.md) |
| Nursing | `/nursing` | [Clinician](clinician/nurse.md) |
| Physical Therapy | `/pt` | [Clinician](clinician/physical-therapy.md) |

### Pharmacy

| Module | Route | Guide |
|---|---|---|
| Pharmacy Hub | `/pharmacy` | [Pharmacist](pharmacist/pharmacy-hub.md) |
| Outpatient Rx | `/outpatientpharmacy` | [Pharmacist](pharmacist/outpatient.md) |
| Inpatient Meds | `/inpatientpharmacy` | [Pharmacist](pharmacist/inpatient.md) |
| Controlled Substances | `/controlled-substances` | [Pharmacist](pharmacist/controlled-substances.md) |
| Drug Formulary | `/drugformulary` | [Pharmacist](pharmacist/formulary.md) |

### Administrative

| Module | Route | Guide |
|---|---|---|
| Registration | `/registration` | [Admin](admin/registration.md) |
| Scheduling | `/scheduling` | [Admin](admin/scheduling.md) |
| ADT & Beds | `/adt`, `/beds` | [Admin](admin/adt-bed-management.md) |
| Billing & Finance | `/integrated-billing`, `/accounts-receivable` | [Admin](admin/billing.md) |
| System Administration | `/site-parameters`, `/security-keys` | [Admin](admin/system-admin.md) |

---

## Getting Help

If you encounter issues with the NewVistas system:

1. **Application errors** — Note the error message and page. Report to your local IRM support.
2. **Access issues** — Contact your site administrator for Access Code/Verify Code resets or security key assignments.
3. **Clinical workflow questions** — Refer to the role-specific guides above or consult your facility's clinical informatics team.
4. **Data discrepancies** — Contact your Health Information Management (HIM) department.
