# Administrative Guide

This guide covers the administrative modules of NewVistas, the clinical information system built on VistA heritage. Administrative functions span patient registration, scheduling, billing, health information management, quality assurance, and system configuration. Each module is designed around the workflows and data structures established by the Department of Veterans Affairs VistA system, updated for modern web-based operation.

## Who This Guide Is For

The administrative guide serves the following roles:

- **Registration Clerks** -- Patient enrollment, eligibility determination, demographic maintenance, and Master Patient Index operations.
- **Billing Specialists** -- Copay management, accounts receivable, EDI claims processing, fee basis authorizations, and revenue cycle oversight.
- **HIM Staff** -- Release of information, record tracking, coding, incomplete records management, and audit trail review.
- **Quality and Safety Officers** -- Incident reporting, peer review, infection control surveillance, and patient safety program administration.
- **Social Workers** -- Homelessness screening, caregiver support, advance directives, and community resource coordination.
- **System Administrators** -- User access management, feature flags, system monitoring, and interoperability configuration.

Each role may use multiple modules depending on facility staffing and local policy. The modules are organized by functional area rather than by role, so a single user may need to reference several sections of this guide.

## Module Quick Reference

The table below lists every administrative module with its documentation file, primary route, and a brief description. Use this as a starting point to navigate to the section you need.

| Module | File | Primary Route(s) | Description |
|--------|------|-------------------|-------------|
| Registration and Eligibility | [registration.md](registration.md) | `/patient-lookup`, `/patient-edit`, `/registration`, `/means-test`, `/service-connected`, `/beneficiary-travel`, `/patient-recall`, `/mpi`, `/patient-merge` | Patient demographics, enrollment, eligibility determination, means testing, service-connected conditions, beneficiary travel, MPI operations, and record merging. |
| Scheduling and Appointments | [scheduling.md](scheduling.md) | `/scheduling`, `/appointment-waitlist`, `/patient-recall` | Appointment booking, clinic management, wait list administration, and patient recall programs. |
| ADT and Bed Management | [adt-bed-management.md](adt-bed-management.md) | `/adt`, `/beds`, `/ed` | Admissions, discharges, transfers, bed board visualization, ward census, and emergency department tracking. |
| Billing and Finance | [billing.md](billing.md) | `/integrated-billing`, `/accounts-receivable`, `/agent-cashier`, `/edi-billing`, `/fee-basis`, `/ifcap`, `/drg`, `/compensation-pension` | Copay accounts, accounts receivable, cashier operations, electronic claims, fee basis, procurement, DRG grouping, and C&P exam tracking. |
| Health Information Management | [him.md](him.md) | `/release-of-information`, `/record-tracking`, `/incomplete-records`, `/audit-trail`, `/icd10`, `/drg`, `/health-summary`, `/patient-merge`, `/security` | Release of information, record tracking, incomplete records, audit trails, ICD-10 coding, DRG grouping, health summaries, record merging, and patient record security. |
| Quality, Safety, and Infection Control | [quality-safety.md](quality-safety.md) | `/quality-management`, `/patient-advocate`, `/infection-control`, `/suicide-prevention`, `/clinical-registries`, `/polytrauma`, `/audit-trail` | Incident reporting, peer review, root cause analysis, patient advocacy, HAI surveillance, outbreak management, antibiogram review, suicide prevention, clinical registries, and polytrauma/TBI screening. |
| Social Work | [social-work.md](social-work.md) | `/social-work` | Homelessness screening, caregiver support, advance directives, and community resource referrals. |
| System Administration | [system-admin.md](system-admin.md) | `/system-admin` | User management, security keys, feature flags, system parameters, and monitoring. |
| Interoperability and Integration | [interoperability.md](interoperability.md) | `/interoperability` | HL7 messaging, FHIR resources, Direct messaging, and external system connectivity. |
| Community and Extended Care | [community-programs.md](community-programs.md) | `/community-programs` | Community-based programs, home-based primary care, adult day health care, and extended care coordination. |
| Emergency and Mass Casualty | [emergency.md](emergency.md) | `/emergency` | Emergency operations, mass casualty incident management, and disaster response coordination. |
| Procurement and Facilities | [procurement.md](procurement.md) | `/procurement` | Procurement workflows, facilities management, equipment tracking, and supply chain operations. |
| Clinical Registries and Special Programs | [registries.md](registries.md) | `/clinical-registries` | Disease-specific registries, special population tracking, and programmatic reporting. |

## Navigating Administrative Modules

Administrative modules are accessible from the sidebar navigation under the **Admin** section. The sidebar groups modules by functional area, matching the organization of this guide.

![Administrative module navigation in the sidebar](screenshots/admin-sidebar-navigation.png)

To access any module:

1. Log in to NewVistas with credentials that have the appropriate administrative security keys assigned.
2. Locate the **Admin** section in the left sidebar navigation.
3. Click the module name to open it.
4. If the module has multiple tabs, the first tab loads by default. Click other tab headers to switch views.

> **Note:** Module visibility in the sidebar depends on your assigned security keys. If you do not see a module you expect to access, contact your system administrator to verify your key assignments.

## Common Administrative Concepts

Several concepts recur across administrative modules:

- **Feature Flags** -- Some modules or features are gated behind feature flags that system administrators can enable or disable. The documentation notes which features require flags.
- **Status Workflows** -- Most administrative records follow defined status progressions (for example, a means test moves from NOT_TESTED through IN_PROGRESS to COMPLETED). Status transitions are enforced by the system and cannot be skipped.
- **VistA File References** -- Each module references the original VistA FileMan file numbers for traceability. These appear in section headers and are useful when cross-referencing with legacy VistA documentation.
- **Audit Logging** -- All administrative actions are captured in the audit trail. The audit trail module (documented in [him.md](him.md)) provides search and export capabilities for compliance review.

## Getting Help

If you encounter issues with any administrative module:

1. Check the specific module documentation for troubleshooting tips and common workflows.
2. Review the [Getting Started](../getting-started.md) guide for general navigation and system orientation.
3. Contact your facility's system administrator for access issues or unexpected behavior.
4. For urgent patient safety concerns, follow your facility's established escalation procedures without waiting for system resolution.
