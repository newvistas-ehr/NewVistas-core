# Care Team

This guide covers the Care Team module in NewVistas, which manages the assignment of clinical providers to a patient's care team. The module implements the VA's Patient Care Management Module (PCMM) framework for primary care team assignments and extends it to support multidisciplinary care teams across all clinical settings.

**Route:** `/care-team`

---

## PCMM Framework

The NewVistas Care Team module is based on the VA's **Patient Care Management Module (PCMM)**, which is the authoritative system for documenting patient-provider care relationships. PCMM assignments determine which providers are responsible for a patient's care and influence how patients appear on provider dashboards and worklists throughout the system.

### Care Team Roles

Each member of a patient's care team is assigned one of the following roles:

| Role | Abbreviation | Description |
|---|---|---|
| **Primary Care Provider** | PCP | The patient's designated primary care provider. Responsible for comprehensive, longitudinal primary care. Only one PCP may be assigned at a time. |
| **Nurse** | NURSE | Registered nurse or licensed practical nurse assigned to the patient's primary care team. Supports care coordination, triage, and patient education. |
| **Social Worker** | SOCIAL_WORKER | Licensed clinical social worker providing psychosocial support, resource coordination, and discharge planning. |
| **Pharmacist** | PHARMACIST | Clinical pharmacist providing medication management, drug therapy monitoring, and medication reconciliation. |
| **Dietitian** | DIETITIAN | Registered dietitian providing nutritional assessment, counseling, and medical nutrition therapy. |
| **Psychologist** | PSYCHOLOGIST | Licensed psychologist providing psychological assessment, psychotherapy, and behavioral health services. |
| **Psychiatrist** | PSYCHIATRIST | Psychiatrist providing psychiatric evaluation, medication management, and mental health treatment. |
| **Care Coordinator** | CARE_COORDINATOR | Staff member responsible for coordinating the patient's care across multiple providers, services, and settings. |
| **Specialist** | SPECIALIST | A medical or surgical specialist involved in the patient's care (e.g., cardiologist, endocrinologist, orthopedic surgeon). |
| **Chaplain** | CHAPLAIN | Spiritual care provider offering pastoral support and spiritual counseling. |
| **Physical Therapist** | PT | Physical therapist providing evaluation and treatment for mobility, strength, and functional rehabilitation. |
| **Occupational Therapist** | OT | Occupational therapist providing evaluation and treatment for activities of daily living and functional independence. |
| **Speech-Language Pathologist** | SLP | Speech-language pathologist providing evaluation and treatment for speech, language, swallowing, and cognitive-communication disorders. |
| **Other** | OTHER | Any other clinical staff member involved in the patient's care who does not fit into the above categories. A free-text description of the role is required when this category is used. |

### Assignment Sources

Each care team assignment includes an assignment source that documents how the provider was added to the team:

| Source | Description |
|---|---|
| **PCMM** | The assignment was made through the PCMM primary care team assignment process. This is the standard method for PCP and primary care team assignments. |
| **CONSULT** | The provider was added to the care team as a result of a consult or referral. For example, when a cardiology consult is completed, the consulting cardiologist may be added to the care team. |
| **MANUAL** | The assignment was made manually by a clinician or care coordinator. Used for ad hoc team member additions outside of the PCMM and consult processes. |

---

## Key Functions

### View Team

The main view of the Care Team module displays the patient's current care team roster. Each row shows:

| Column | Description |
|---|---|
| **Provider** | Name and credentials of the team member |
| **Role** | Care team role (PCP, NURSE, SOCIAL_WORKER, etc.) |
| **Assignment Source** | How the provider was added to the team (PCMM, CONSULT, MANUAL) |
| **Assignment Date** | Date the provider was assigned to the team |
| **Status** | Active or Inactive |

![Care team roster showing current team members with roles](screenshots/care-team-roster.png)

The care team roster is organized with the PCP listed first, followed by other team members in alphabetical order by role. Active team members are displayed prominently; inactive assignments are shown in a separate section or filtered out by default.

---

### Assign PCP

The PCP assignment is the most important care team function. The Primary Care Provider is the clinician with overall responsibility for the patient's longitudinal primary care.

To assign or change the patient's PCP:

1. Navigate to the Care Team module at `/care-team`.
2. Click the **Assign PCP** button.
3. Complete the PCP assignment form:

| Field | Required | Description |
|---|---|---|
| **Provider** | Yes | Select the provider to designate as the PCP. Type to search by name. Only providers with active PCP-eligible credentials will appear in the search results. |
| **Effective Date** | No | Date the assignment takes effect. Defaults to today. |
| **Comments** | No | Reason for the assignment or change (e.g., "Provider retirement -- transferring panel to Dr. Smith", "Patient requested provider change"). |

4. Click **Assign** to save the PCP designation.

![Assign PCP form with provider search](screenshots/care-team-assign-pcp.png)

> **Note:** Only one PCP can be assigned to a patient at a time. Assigning a new PCP automatically ends the previous PCP assignment. The previous assignment is preserved in the team history.

> **Warning:** Changing a patient's PCP affects which patients appear on the Provider Dashboard under "My Patients." The patient will move from the previous PCP's panel to the new PCP's panel. Ensure that both the outgoing and incoming providers are aware of the panel change.

---

### Add Team Member

To add a provider to the patient's care team:

1. Navigate to the Care Team module at `/care-team`.
2. Click the **Add Team Member** button.
3. Complete the team member form:

| Field | Required | Description |
|---|---|---|
| **Provider** | Yes | Select the provider to add. Type to search by name. |
| **Role** | Yes | Select the care team role from the dropdown (NURSE, SOCIAL_WORKER, PHARMACIST, DIETITIAN, PSYCHOLOGIST, PSYCHIATRIST, CARE_COORDINATOR, SPECIALIST, CHAPLAIN, PT, OT, SLP, OTHER). |
| **Assignment Source** | No | Select the source of the assignment: PCMM, CONSULT, or MANUAL. Defaults to MANUAL. |
| **Effective Date** | No | Date the assignment takes effect. Defaults to today. |
| **Comments** | No | Additional context for the assignment. |

4. Click **Add** to add the team member.

> **Tip:** When adding a specialist to the care team, include the specialty in the Comments field (e.g., "Cardiology -- managing atrial fibrillation") to provide context for other team members.

---

### Remove Team Member

To remove a provider from the patient's care team:

1. Navigate to the Care Team module at `/care-team`.
2. Locate the team member to be removed in the roster.
3. Click the **Remove** action button on the team member's row.
4. Confirm the removal. The team member's status will change from Active to Inactive.

> **Note:** Removing a team member does not delete their assignment record. The assignment is preserved in the team history with an end date, maintaining a complete audit trail of care team changes.

---

### Team History

The Team History view provides a complete chronological record of all care team assignments for the patient, including current and past assignments.

![Care team history showing current and historical assignments](screenshots/care-team-history.png)

Each row in the team history shows:

| Column | Description |
|---|---|
| **Provider** | Name and credentials of the team member |
| **Role** | Care team role |
| **Assignment Source** | PCMM, CONSULT, or MANUAL |
| **Start Date** | Date the assignment began |
| **End Date** | Date the assignment ended (blank for active assignments) |
| **Status** | Active or Inactive |
| **Comments** | Any comments associated with the assignment or its termination |

The team history is valuable for understanding the patient's care continuity. It answers questions such as:

- Who was the patient's PCP before the current one?
- When was the patient referred to a mental health provider?
- How long has the current care team been in place?
- What changes were made to the care team and why?

---

## PCP Assignments and the Provider Dashboard

PCP assignments have a direct impact on how patients appear throughout the system:

- **Provider Dashboard "My Patients"** -- The Provider Dashboard at `/provider-dashboard` includes a "My Patients" panel that lists all patients for whom the currently signed-in provider is the assigned PCP. When a patient's PCP is changed, the patient moves from one provider's panel to another.

- **Panel Size Tracking** -- The number of patients assigned to a provider's PCP panel is tracked for workload management purposes.

- **Care Continuity** -- When a patient calls the clinic, the scheduling and triage system routes them to their assigned PCP's team whenever possible.

- **Clinical Reminders** -- Some clinical reminders are attributed to the patient's PCP for resolution, appearing as action items on the PCP's dashboard.

> **Tip:** Review your "My Patients" panel on the Provider Dashboard regularly to ensure that the listed patients accurately reflect your current panel. If patients appear who should not be on your panel, or if expected patients are missing, check the Care Team module for assignment discrepancies.

---

## Common Workflows

### Establishing a New Patient's Care Team

1. After the patient is registered in the system, navigate to `/care-team`.
2. Click **Assign PCP** and select the patient's designated primary care provider.
3. Click **Add Team Member** to add the primary care nurse.
4. Add additional team members as appropriate for the patient's needs (social worker, pharmacist, mental health provider, etc.).
5. Verify the care team roster is complete and accurate.

### Transferring a Patient to a New PCP

1. Navigate to `/care-team` for the patient being transferred.
2. Click **Assign PCP**.
3. Select the new PCP from the provider search.
4. Enter a comment documenting the reason for the transfer (e.g., "Previous PCP retired", "Patient relocated to a different clinic").
5. Click **Assign** to complete the transfer. The previous PCP assignment will automatically end.
6. Consider whether other team members (nurse, social worker) should also be updated to reflect the new primary care team.

### Adding a Specialist After a Consult

1. After a consult is completed (e.g., a cardiology consult), navigate to `/care-team`.
2. Click **Add Team Member**.
3. Select the consulting specialist as the provider.
4. Set the role to **SPECIALIST**.
5. Set the assignment source to **CONSULT**.
6. Add a comment noting the specialty and clinical reason (e.g., "Cardiology -- management of newly diagnosed heart failure per consult #12345").
7. Click **Add**.

---

## Related Modules

- **[Provider Dashboard](index.md)** -- PCP assignments determine which patients appear on the "My Patients" panel.
- **[Consults](consults.md)** -- Consult outcomes may trigger care team additions.
- **[Clinical Reminders](reminders.md)** -- Some reminders are attributed to the patient's PCP.
- **[Cover Sheet](cover-sheet.md)** -- The patient's current care team may be summarized on the Cover Sheet.
