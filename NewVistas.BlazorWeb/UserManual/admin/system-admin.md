# System Administration

This section covers the administrative and configuration tools available in NewVistas for System Administrators, ADPACs (Automated Data Processing Application Coordinators), IRM Specialists, IT Security Officers, and Application Administrators. These modules manage site configuration, user access, security, auditing, reference data, interoperability, messaging, and reporting.

**VistA File References:** #8989.5 (Parameters), #200 (New Person), #8932 (Security Key), #38.1 (Patient Sensitivity)

**Primary Roles:** System Administrators, ADPACs, IRM Specialists, IT Security Officers, Application Administrators, Privacy Officers

---

## Site Parameters (/site-parameters)

The Site Parameters page controls system-wide configuration settings that affect all users and all areas of the application. Changes made here take effect immediately.

![Site Parameters page showing display settings and key-value parameters](screenshots/site-parameters.png)

### Display Settings

The following display settings control how many items are shown by default on clinical pages:

| Parameter | Default Value | Description |
|-----------|---------------|-------------|
| **VitalsDisplayCount** | 10 | Number of recent vital sign readings to display on the patient vitals page |
| **OrdersDisplayCount** | 5 | Number of recent orders to display on the orders page |
| **NotesDisplayCount** | 10 | Number of recent clinical notes to display on the notes page |

> **Note:** These are default display counts. Individual pages may allow users to load additional items by scrolling or clicking "Load More." Changing these values affects all users across the facility.

### Key/Value Parameters

The Key/Value Parameters section provides a generic configuration dictionary for site-specific settings. Each parameter consists of a key name and a value.

#### Common Parameters

| Key | Example Value | Purpose |
|-----|---------------|---------|
| **FacilityName** | VA Medical Center - Example City | The display name of the facility shown in headers and reports |
| **StationNumber** | 508 | The VA station number for the facility |
| **TimeZone** | America/New_York | The default time zone for date/time display |
| **MaintenanceWindow** | Sunday 02:00-06:00 ET | Scheduled maintenance window communicated to users |
| **SessionTimeout** | 30 | Minutes of inactivity before a user session is terminated |

> **Warning:** Changes to site parameters take effect immediately and affect all users across the entire facility. Always verify parameter values before saving, and communicate changes to affected staff in advance.

### Modifying Parameters

1. Navigate to the **Site Parameters** page (/site-parameters).
2. Locate the parameter you wish to change. For display settings, adjust the numeric value directly. For key/value parameters, click the **Edit** button next to the parameter, or click **Add Parameter** to create a new one.
3. Enter the new value and click **Save**. The change takes effect immediately.

> **Tip:** Before modifying a critical parameter (such as SessionTimeout or MaintenanceWindow), send a MailMan bulletin to affected users informing them of the upcoming change.

---

## Security Key Management (/security-keys)

Security keys control access to specific functions within NewVistas. Each key grants permission to perform certain actions or access certain areas. Security keys are assigned to individual users and can be granted or revoked by administrators.

![Security Key Management page with Grant Key tab open](screenshots/security-keys-grant.png)

### Loading a User

To manage security keys for a user:

1. Enter the **User ID** in the search field at the top of the page.
2. Click **Load User**.

Once loaded, the page displays a session banner showing the user's current session status:

- **SESSION ACTIVE** (green banner) -- The user currently has an active session. Displays the device, IP address, and session timeout information.
- **NO SESSION** (grey banner) -- The user does not have an active session.

An **End Session** button is available when the user has an active session. Use this to force-terminate a user's session in security incidents.

> **Warning:** Ending a user's session will immediately disconnect them from the application. Any unsaved work will be lost. Use this only when necessary for security purposes.

### Tab 1: Security Keys

Displays all security keys currently assigned to the selected user.

Each entry shows:
- **Key Name** -- The name of the security key
- **Category** -- The functional category of the key (Clinical, Administrative, Security, System, Reporting)
- **Revoke** button -- Removes the key from the user

### Tab 2: Grant Key

Use this tab to assign new security keys to the selected user.

#### Category Filter

Keys are organized into categories. Select a category to filter the available keys:

| Category | Example Keys | Purpose |
|----------|-------------|---------|
| **Clinical** | ORES, PROVIDER, NURSE, PHARMACIST, DENTAL | Access to clinical functions such as ordering, prescribing, and charting |
| **Administrative** | REGISTRATION, SCHEDULING, ADT, BILLING | Access to administrative functions such as patient registration and scheduling |
| **Security** | SECURITY_OFFICER, PRIVACY_OFFICER | Access to security management and privacy functions |
| **System** | SYSADMIN, PROGRAMMER, DG_SECURITY | Access to system-level configuration and programming tools |
| **Reporting** | REPORTS_MANAGER, GPRA_COORDINATOR | Access to reporting and performance measurement tools |

#### Granting a Key

1. Select the desired **Category** from the filter dropdown.
2. Browse the list of available keys. Only keys not already assigned to the user are shown.
3. Click the **Grant** button next to the key you wish to assign.
4. The key is immediately assigned and appears on the Security Keys tab.

> **Note:** Granting a security key takes effect immediately. The user will have access to the associated functions on their next page load or action. No application restart is required.

### Tab 3: Key Audit Log

The Key Audit Log records all security key changes and session events for the selected user. This is critical for compliance and security investigations.

#### Event Types

| Event Type | Description |
|------------|-------------|
| **KEY_GRANTED** | A security key was assigned to the user |
| **KEY_REVOKED** | A security key was removed from the user |
| **SESSION_STARTED** | The user initiated a new session (login) |
| **SESSION_ENDED** | The user ended their session normally (logout or timeout) |
| **SESSION_FORCE_ENDED** | An administrator forcefully terminated the user's session |

Each audit log entry includes:
- Timestamp
- Event type
- Key name (for key events)
- Acting administrator (who performed the action)
- Reason or notes (if provided)

### Key Management Workflow

Follow this workflow when a new role assignment or access request is received:

1. **Verify the request** -- Confirm the access request has been approved by the appropriate supervisor or service chief. Check that required training has been completed.
2. **Load the user** -- Enter the User ID and click Load User. Verify you have the correct person by reviewing the session banner information.
3. **Review current keys** -- Check the Security Keys tab to see what access the user already has. Avoid granting duplicate or conflicting keys.
4. **Grant the appropriate keys** -- Navigate to the Grant Key tab, filter by category, and grant each required key. Refer to the role-to-key mapping table in the Common Workflows section below.
5. **Verify and document** -- Return to the Security Keys tab to confirm the keys were assigned. Check the Key Audit Log to verify the events were recorded. Document the change in accordance with local policy.

---

## Access Control

NewVistas implements a multi-layered access control system based on the VistA security model.

### User Registration

New users are set up with the following credentials:

- **Access Code** -- The user's login identifier (similar to a username)
- **Verify Code** -- The user's authentication code (similar to a password)
- **User Class** -- The user's role classification (e.g., Physician, Nurse, Clerk, Administrator)

### Multi-Factor Authentication (MFA)

MFA is required for all users with elevated access (Clinical, Security, or System keys).

- **TOTP (Time-based One-Time Password)** -- Users enroll a TOTP authenticator application (such as Google Authenticator or Microsoft Authenticator) during initial setup.
- MFA is prompted at each login after the access and verify codes are accepted.

### Electronic Signature

Certain actions (signing notes, approving orders, completing assessments) require an electronic signature.

- The electronic signature code is a separate credential from the access and verify codes.
- Users must enter their electronic signature code to complete signature-required actions.
- Electronic signatures are recorded in the audit trail with the user ID, timestamp, and action performed.

### Session Management

- Sessions have a configurable timeout period (set via the SessionTimeout site parameter).
- Users are warned before their session expires and can extend it.
- Administrators can view active sessions and force-end sessions from the Security Key Management page.
- Session events (start, end, force-end) are recorded in the Key Audit Log.

---

## Audit Trail (/audit-trail)

The Audit Trail page provides a comprehensive log of all significant actions performed in the system. It is the primary tool for compliance monitoring, security investigations, and operational oversight.

### Filters

The audit trail can be filtered using the following criteria:

- **Domain** -- The functional area of the action (e.g., Orders, Notes, Patient, Security, Administration)
- **Date Range** -- Start and end dates to narrow the time window
- **Entity ID** -- A specific patient ID, order ID, user ID, or other entity identifier

### Event Log Columns

| Column | Description |
|--------|-------------|
| **Timestamp** | Date and time the event occurred (in the facility's configured time zone) |
| **Domain** | The functional area where the event occurred |
| **Action** | The type of action performed (Created, Updated, Deleted, Viewed, Signed, etc.) |
| **Entity ID** | The identifier of the affected record |
| **User** | The user who performed the action |
| **Details** | Additional context about the event |

### Export

Click the **Export** button to download the filtered audit trail results as a CSV file for external analysis or compliance reporting.

### Use Cases

- **Breach Investigation** -- When a potential privacy breach is reported, use the Entity ID filter with the affected patient's ID to review all access to their records during the relevant time period.
- **User Activity Review** -- Filter by user to review all actions performed by a specific individual during a given period. Useful for investigating suspicious activity or verifying compliance.
- **Compliance Monitoring** -- Run regular reviews of audit trail data to identify patterns such as after-hours access, excessive record views, or unauthorized access attempts.
- **Incident Correlation** -- When investigating an incident, use the date range filter to identify all events that occurred during the timeframe of interest across all domains.

### Audit Review

1. Navigate to the **Audit Trail** page (/audit-trail).
2. Set the appropriate filters (domain, date range, and/or entity ID) based on your investigation needs.
3. Review the event log entries. Click on individual entries to see full details. Use the Export button to download results for further analysis.

> **Tip:** Schedule regular audit trail reviews as part of your facility's compliance program. Weekly reviews of security-related events and monthly reviews of clinical access patterns are recommended.

![Audit Trail page with date range and domain filters applied](screenshots/audit-trail-filters.png)

---

## Security (/security)

The Security page manages patient record sensitivity levels and access controls for sensitive patient records. It is organized into three tabs.

### Tab 1: Patient Sensitivity

Patient sensitivity designations control who can access a patient's records and what warnings are displayed when accessing those records.

#### Sensitivity Levels

| Level | Description | Access Control |
|-------|-------------|---------------|
| **STANDARD** | Default sensitivity level for all patients | Normal access rules apply based on security keys |
| **ELEVATED** | Records of patients who are also employees or have other elevated privacy needs | Access triggers an alert to the Privacy Officer; requires acknowledgment before viewing |
| **HIGH** | Records requiring maximum protection (e.g., VIP patients, certain legal cases) | Access restricted to explicitly authorized providers only |

> **Danger:** HIGH-sensitivity records are only accessible to providers who have been explicitly added to the patient's Authorized Providers list. All other users, regardless of their security keys, will be blocked from accessing these records. Unauthorized access attempts are logged and reported.

#### Sensitivity Categories

Patient sensitivity can be assigned for the following reasons:

| Category | Description |
|----------|-------------|
| **Employee** | The patient is also an employee of the facility |
| **VIP** | The patient is a high-profile individual requiring additional privacy protections |
| **Legal** | The patient's records are subject to legal proceedings or special legal protections |
| **Clinical** | The patient has requested enhanced privacy for clinical reasons |

#### Setting Patient Sensitivity

1. Navigate to the **Security** page (/security).
2. Search for the patient using the patient search.
3. On the **Patient Sensitivity** tab, select the appropriate sensitivity level.
4. Choose the applicable category.
5. Enter a justification for the sensitivity designation.
6. Click **Save**.

### Tab 2: Access Log

The Access Log shows all access events for sensitive patient records, including:

- Who accessed the record
- Date and time of access
- Whether the access was authorized
- Whether any break-the-glass events occurred (accessing records outside normal authorization)

### Tab 3: Authorized Providers

For patients with HIGH sensitivity, this tab manages the list of providers who are explicitly authorized to access the patient's records.

- **Add Provider** -- Grant a provider access to the patient's records
- **Remove Provider** -- Revoke a provider's access
- **Audit History** -- View the history of authorization changes

---

## Master Patient Index (/mpi)

The Master Patient Index (MPI) is the authoritative source of patient identity information across the VA healthcare system.

### MPI Search

Search for patients across all VA facilities using:
- Full name
- Social Security Number (last 4 or full)
- Date of birth
- Integration Control Number (ICN)

### Identity Correlations

View and manage cross-facility identity correlations for a patient. Each correlation links the patient's local identifier at one facility to their identifiers at other facilities and external systems.

### Duplicate Detection

The system automatically flags potential duplicate patient records based on matching criteria (name, date of birth, SSN). Review flagged duplicates and determine whether they should be merged or confirmed as distinct patients.

### ICN Management

The Integration Control Number (ICN) is the national patient identifier used across all VA systems. The MPI page allows you to:
- View a patient's ICN
- Request a new ICN for patients who do not have one
- Resolve ICN conflicts when a patient has been assigned multiple ICNs

### Merge Queue

Suspected duplicates that require resolution are placed in the merge queue. See the Patient Merge section below for the merge process.

---

## Patient Merge (/patient-merge)

The Patient Merge page allows administrators to merge duplicate patient records into a single authoritative record.

> **Danger:** Patient merge is irreversible. Once two records are merged, they cannot be separated. Always verify patient identity thoroughly before proceeding with a merge.

### Merge Process

1. **Select Source Patient** -- Enter the patient ID for the record that will be merged (this record will be deactivated after the merge).
2. **Select Target Patient** -- Enter the patient ID for the record that will be retained as the surviving record.
3. **Side-by-Side Comparison** -- The system displays both patient records side by side, highlighting differences in demographics, identifiers, and clinical data.
4. **Merge Preview** -- Review what data will be carried from the source to the target record. The preview shows which records (orders, notes, appointments, lab results, etc.) will be reassigned.
5. **Execute Merge** -- After thorough review, click **Execute Merge** to combine the records. Enter your electronic signature to confirm.

> **Warning:** Before executing a merge, verify the following:
> - Both records belong to the same patient (check SSN, date of birth, and other identifiers)
> - The target (surviving) record has the most accurate demographic information
> - All clinical data from the source record will be preserved in the target record
> - Other facilities have been notified if the patient has records at multiple sites

---

## ICD-10 Browser (/icd10)

The ICD-10 Browser provides access to the International Classification of Diseases, 10th Revision diagnostic codes used throughout the system.

### Features

- **Search** -- Search for ICD-10 codes by code number or description text
- **Browse** -- Navigate the ICD-10 hierarchy by chapter, block, and category
- **Code Details** -- View full code descriptions, includes/excludes notes, and coding guidelines
- **Effective Dates** -- Each code has effective and expiration dates reflecting annual CMS updates

### Reference Data Maintenance

ICD-10 codes are updated annually by the Centers for Medicare and Medicaid Services (CMS), with new codes taking effect on **October 1** of each year.

> **Note:** ICD-10 code updates should be loaded before October 1 of each year to ensure new codes are available when they become effective. Coordinate with the IRM team to schedule the annual update.

---

## Lexicon (/lexicon)

The Lexicon provides a medical terminology browser and mapping tool for standardized code systems used in the clinical record.

### Features

- **Search** -- Search for medical terms by keyword or phrase
- **Browse Hierarchy** -- Navigate the hierarchical structure of terminology systems
- **Mappings** -- View cross-references between terminology systems:
  - **SNOMED CT** -- Systematized Nomenclature of Medicine Clinical Terms
  - **ICD-10** -- International Classification of Diseases, 10th Revision
  - **CPT** -- Current Procedural Terminology
  - **LOINC** -- Logical Observation Identifiers Names and Codes

> **Note:** The Lexicon is a read-only reference tool. Terminology updates are managed through the national VA data standardization process and cannot be modified locally.

---

## Engineering (/engineering)

The Engineering page manages facility work orders and the building/room directory. It is organized into two tabs.

### Tab 1: Work Orders

Work orders track maintenance requests, repairs, and facility improvements.

#### Work Order Fields

- **WO Number** -- System-generated unique identifier
- **Title** -- Brief description of the work needed
- **Description** -- Detailed description of the issue or request
- **Location** -- Building, floor, and room where the work is needed
- **Priority** -- Urgency of the work order

#### Priority Levels

| Priority | Description | Expected Response |
|----------|-------------|-------------------|
| **EMERGENCY** | Life safety, critical infrastructure failure | Immediate response |
| **URGENT** | Significant operational impact | Within 24 hours |
| **ROUTINE** | Standard maintenance or repair | Within 1-2 weeks |
| **SCHEDULED** | Planned preventive maintenance | Per maintenance schedule |

#### Work Order Status

```
SUBMITTED → ASSIGNED → IN_PROGRESS → COMPLETED → CLOSED
                          ↘ ON_HOLD
                                       ↘ CANCELLED
```

#### Work Order Categories

| Category | Examples |
|----------|---------|
| **ELECTRICAL** | Power outages, lighting, outlets, wiring |
| **PLUMBING** | Leaks, clogs, water supply, fixtures |
| **HVAC** | Heating, cooling, ventilation, air quality |
| **STRUCTURAL** | Walls, floors, ceilings, doors, windows |
| **SAFETY** | Fire systems, alarms, emergency exits, signage |
| **BIOMEDICAL** | Medical equipment repair and calibration |
| **GROUNDS** | Landscaping, parking lots, sidewalks, exterior |
| **OTHER** | Requests not covered by the above categories |

### Tab 2: Facilities

The Facilities tab maintains the directory of buildings, floors, and rooms across the facility campus.

#### Facility Record Fields

- **Building** -- Building name or number
- **Floor** -- Floor level
- **Room** -- Room number or identifier
- **Department** -- Department assigned to the room
- **Room Type** -- Classification of the room's purpose

#### Room Types

| Room Type | Description |
|-----------|-------------|
| **OFFICE** | Administrative or staff office space |
| **EXAM_ROOM** | Clinical examination room |
| **WARD** | Inpatient ward or bed area |
| **LAB** | Laboratory space |
| **PHARMACY** | Pharmacy space |
| **OR** | Operating room |
| **STORAGE** | Storage area |
| **MECHANICAL** | Mechanical or utility space |

#### Facility Status

| Status | Description |
|--------|-------------|
| **ACTIVE** | Room is currently in use |
| **UNDER_RENOVATION** | Room is undergoing renovation and temporarily unavailable |
| **DECOMMISSIONED** | Room is permanently out of service |

- **Maintenance Scheduling** -- Schedule and track preventive maintenance for facility systems (HVAC, fire safety, elevators, etc.) by room or building.

---

## FHIR Gateway (/fhir)

The FHIR (Fast Healthcare Interoperability Resources) Gateway manages connections to external healthcare systems using the HL7 FHIR R4 standard.

### Endpoint Management

Configure and manage connections to external FHIR-enabled systems:
- **Endpoint URL** -- The base URL of the external FHIR server
- **Connection Status** -- Current health of the connection (Connected, Disconnected, Error)
- **Last Successful Transaction** -- Timestamp of the most recent successful data exchange
- **Authentication** -- OAuth2 client credentials and scopes

### FHIR R4 Resource Mappings

The following FHIR resources are supported for data exchange:

| FHIR Resource | NewVistas Domain | Description |
|---------------|-----------------|-------------|
| **Patient** | Patient Demographics | Core patient identity and demographics |
| **Condition** | Problem List | Active and historical diagnoses |
| **AllergyIntolerance** | Allergies | Allergy and adverse reaction information |
| **Observation** | Vitals, Labs | Clinical observations including vital signs and lab results |
| **MedicationRequest** | Pharmacy | Medication orders and prescriptions |
| **DiagnosticReport** | Lab Results, Radiology | Diagnostic study results |
| **Encounter** | ADT, Visits | Healthcare encounters and admissions |
| **Appointment** | Scheduling | Scheduled appointments |

### Transaction Log

View the history of all FHIR transactions:
- Inbound and outbound messages
- Success and failure status
- Error details for failed transactions
- Retry options for failed messages

### OAuth2 Authorization

Configure OAuth2 client credentials for authenticating with external FHIR endpoints:
- Client ID and client secret management
- Scope configuration
- Token refresh settings

![FHIR Gateway connections page showing endpoint status](screenshots/fhir-gateway-connections.png)

---

## MailMan (/mailman)

MailMan is the internal messaging system for facility-wide communication among staff. It is based on the VistA MailMan system.

![MailMan inbox showing messages with priority indicators](screenshots/mailman-inbox.png)

### Inbox

View received messages with:
- Sender name
- Subject line
- Date and time received
- Priority indicator
- Read/unread status

### Compose

Create a new message:
- **To** -- One or more recipients (search by name or user ID)
- **Subject** -- Message subject line
- **Body** -- Message content (supports plain text)
- **Priority** -- Normal, High, or Urgent
- **Attachment** -- Optional file attachment

### Folders

Organize messages into folders for easy retrieval. Default folders include Inbox, Sent, and Drafts. Custom folders can be created.

### System Bulletins

System bulletins are facility-wide announcements that are automatically delivered to all users or specific distribution groups. Common uses include:

- **Downtime Notices** -- Scheduled maintenance windows and system outages
- **Policy Changes** -- Updates to clinical or administrative policies
- **Security Alerts** -- Notifications about security incidents, phishing attempts, or policy reminders

### Distribution Groups

Manage named groups of users for targeted messaging. Distribution groups can be based on:
- Department or service line
- Role or user class
- Committee or project team
- Custom groupings

---

## GPRA Reporting (/gpra-reporting)

The GPRA (Government Performance and Results Act) Reporting page generates performance measurement reports for VA clinical quality indicators.

![GPRA report showing measure results with benchmarks](screenshots/gpra-report.png)

### Measure Sets

Reports can be generated for the following measure sets:

| Measure Set | Focus Area |
|-------------|------------|
| **ALL** | Complete set of all GPRA measures |
| **PRIMARY_CARE** | Primary care quality indicators |
| **MENTAL_HEALTH** | Mental health screening and treatment measures |
| **PREVENTION** | Preventive care and screening measures |
| **CHRONIC_DISEASE** | Chronic disease management measures |

### Report Parameters

- **Measure Set** -- Select the measure set to report on
- **Date Range** -- Fiscal year, quarter, or custom date range
- **Facility** -- Station or division (defaults to current facility)

### Results Columns

| Column | Description |
|--------|-------------|
| **Measure** | Name of the GPRA measure |
| **Numerator** | Count of patients meeting the measure criteria |
| **Denominator** | Count of eligible patients |
| **Rate** | Percentage (numerator / denominator) |
| **Benchmark** | National VA benchmark for the measure |
| **Ranking** | Facility ranking relative to other VA facilities |

### Export

Click **Export** to download the report results as a CSV or PDF file for distribution, presentation, or further analysis.

---

## iCare Dashboard (/icare-dashboard)

The iCare Dashboard provides a population health management view for clinical teams, showing patient panels, clinical reminders, and performance metrics.

### Patient Panels

View and manage provider patient panels:
- Patient list with demographics
- Last visit date
- Next scheduled appointment
- Due and overdue clinical reminders
- Risk level indicators

### Clinical Reminders

Track clinical reminders across the patient panel:
- Reminders due and overdue
- Completion rates by reminder type
- Drill-down to individual patient reminder lists

### Performance Metrics

| Metric | Description |
|--------|-------------|
| **Panel Size** | Number of patients assigned to the provider's panel |
| **Access Rate** | Percentage of panel patients seen within the defined access standard |
| **Reminder Completion** | Percentage of due reminders that have been completed |
| **Quality Measures** | GPRA and local quality measure performance for the panel |

### Actionable Patient Lists

Generate filtered lists of patients requiring action:
- Patients overdue for appointments
- Patients with open clinical reminders
- Patients with abnormal lab results pending review
- Patients recently discharged who need follow-up

---

## Common Workflows

### New User Setup

Follow this process when setting up access for a new staff member.

1. **Create the user account** -- Register the new user with an access code, verify code, and user class. Enable MFA and set up electronic signature.
2. **Determine required security keys** -- Based on the user's role, identify the security keys needed. Use the following role-to-key mapping table as a guide:

| Role | Required Keys |
|------|--------------|
| Physician | PROVIDER, ORES |
| Nurse | NURSE, ORES |
| Pharmacist | PHARMACIST, ORES |
| Registration Clerk | REGISTRATION, SCHEDULING |
| Ward Clerk | ADT, SCHEDULING |
| Social Worker | PROVIDER (or NURSE depending on local policy) |
| System Administrator | SYSADMIN, DG_SECURITY |
| ADPAC | SYSADMIN (limited), REPORTS_MANAGER |
| Privacy Officer | PRIVACY_OFFICER, SECURITY_OFFICER |
| GPRA Coordinator | REPORTS_MANAGER, GPRA_COORDINATOR |

3. **Grant the security keys** -- Navigate to Security Key Management (/security-keys), load the user, and grant each required key from the Grant Key tab.
4. **Verify access** -- Have the user log in and confirm they can access the appropriate functions. Check the Key Audit Log to verify the setup was recorded properly.

### Security Incident Investigation

Follow this process when a potential security or privacy incident is reported.

1. **Document the report** -- Record the details of the reported incident: what happened, when, who is involved, and what records may be affected.
2. **Preserve the evidence** -- Navigate to the Audit Trail (/audit-trail) and export the relevant logs before any corrective action is taken.
3. **Review the audit trail** -- Filter by the affected patient's Entity ID and the relevant time period. Identify all access events during the window of concern.
4. **Check user sessions** -- Navigate to Security Key Management (/security-keys) and load each user identified in the audit trail. Review their session history and key assignments.
5. **Assess the scope** -- Determine how many records were affected and whether the access was authorized or unauthorized.
6. **Take corrective action** -- If unauthorized access is confirmed, revoke the user's security keys and/or force-end their session. Document the corrective action.
7. **Report and follow up** -- Complete the incident report per facility policy. Notify the Privacy Officer and any other required parties. Schedule follow-up reviews as needed.

### System Configuration Change

Follow this process when making a change to site parameters or other system-wide configuration.

1. **Document the change request** -- Record what is being changed, why, and who approved the change.
2. **Assess impact** -- Determine which users and functions will be affected by the change. Review the current parameter values.
3. **Communicate the change** -- Send a MailMan bulletin to affected users informing them of the planned change, the expected timing, and any action they need to take.
4. **Implement the change** -- Navigate to the appropriate configuration page (Site Parameters, Security, etc.) and make the change.
5. **Verify and document** -- Confirm the change is working as expected. Check the audit trail to verify the change was recorded. Document the completion of the change request.

---

## System Health Monitoring

Regular monitoring of system health indicators helps identify issues before they impact clinical operations.

| Check | Location | Frequency | Purpose |
|-------|----------|-----------|---------|
| **Orleans Dashboard** | http://localhost:8080 | Continuous | Monitor grain activations, message throughput, and silo health |
| **Audit Trail Anomalies** | /audit-trail | Daily | Identify unusual access patterns, failed access attempts, or after-hours activity |
| **Security Key Reviews** | /security-keys | Quarterly | Verify that users have only the keys appropriate for their current role |
| **Site Parameters** | /site-parameters | Monthly | Review configuration settings for accuracy and appropriateness |
| **FHIR Connections** | /fhir | Daily | Check connection status and review failed transactions |
| **Incomplete Records** | /audit-trail | Weekly | Identify unsigned notes, incomplete orders, or other outstanding items |
| **GPRA Extracts** | /gpra-reporting | Monthly | Run performance reports and identify areas needing improvement |
| **Engineering Work Orders** | /engineering | Weekly | Review open work orders, especially those in EMERGENCY or URGENT priority |

![Orleans Dashboard showing grain activations and silo status](screenshots/orleans-dashboard.png)

---

## Tips and Best Practices

1. **Follow the principle of least privilege.** Grant only the security keys a user needs for their current role. Regularly review key assignments and revoke keys that are no longer needed when staff change roles or leave.

2. **Monitor the audit trail proactively.** Do not wait for an incident to be reported before reviewing audit data. Regular proactive reviews help identify issues early and demonstrate compliance.

3. **Use MailMan for change communication.** Always notify affected users before making system configuration changes. Include the nature of the change, when it will take effect, and any action users need to take.

4. **Test configuration changes during low-usage periods.** When possible, make system-wide changes during the configured maintenance window to minimize the impact on clinical operations.

5. **Maintain a change log.** In addition to the automated audit trail, maintain a manual log of planned configuration changes with dates, approvals, and outcomes. This supports compliance audits and troubleshooting.

6. **Review FHIR connection health daily.** Failed interoperability transactions can affect clinical data availability. Address connection issues promptly and resubmit failed transactions.

7. **Keep reference data current.** Schedule ICD-10 updates before October 1 each year. Coordinate with national VA data standardization efforts for Lexicon updates.

8. **Secure the patient merge process.** Always have a second administrator verify patient identity before executing a merge. Document the rationale for each merge decision.

9. **Review GPRA reports monthly.** Performance measurement is a core VA requirement. Monthly reviews allow you to identify trends and intervene before quarterly or annual targets are missed.

10. **Use the Orleans Dashboard for performance troubleshooting.** If users report slowness or errors, the Orleans Dashboard provides real-time visibility into grain activations, message throughput, and silo health. Look for grain activation storms, high message queue depths, or silo connectivity issues.
