# Interoperability and Integration

This section covers the interoperability and integration tools in NewVistas that enable data exchange with external systems, laboratory instruments, internal messaging, performance reporting, population health management, and patient-facing services.

**Routes:** /fhir, /lab-edi, /lab-instruments, /mailman, /gpra-reporting, /icare-dashboard, /patient-portal

**Primary Roles:** Interface Engineers, Lab Information Managers, System Administrators, Quality Managers, Clinical Informaticists, Patient Portal Administrators

---

## FHIR Gateway (/fhir)

The FHIR (Fast Healthcare Interoperability Resources) Gateway manages bidirectional data exchange with external healthcare systems using the HL7 FHIR R4 standard. It supports resource mapping, document generation, endpoint management, and connection health monitoring.

![FHIR Gateway showing resource mapping configuration and endpoint status](screenshots/fhir-resource-mapping.png)

### FHIR R4 Resources

The following FHIR R4 resources are supported for data exchange:

| FHIR Resource | NewVistas Domain | Direction | Description |
|---------------|-----------------|-----------|-------------|
| **Patient** | Patient Demographics | Inbound/Outbound | Core patient identity, demographics, and contact information |
| **Condition** | Problem List | Inbound/Outbound | Active and historical diagnoses, including onset date and status |
| **AllergyIntolerance** | Allergies | Inbound/Outbound | Allergies and adverse reactions with severity and reaction details |
| **Observation** | Vitals, Labs | Inbound/Outbound | Clinical observations including vital signs, lab results, and social history |
| **MedicationRequest** | Pharmacy | Outbound | Medication orders, prescriptions, and refill requests |
| **DiagnosticReport** | Lab Results, Radiology | Inbound/Outbound | Diagnostic study results with narrative and coded findings |
| **Encounter** | ADT, Visits | Outbound | Healthcare encounters, admissions, discharges, and transfers |
| **Appointment** | Scheduling | Inbound/Outbound | Scheduled and requested appointments |

### Capability Statement

The FHIR Gateway publishes a capability statement at the `/metadata` endpoint that describes:
- Supported FHIR version (R4)
- Available resource types and their supported interactions (read, search, create, update)
- Supported search parameters for each resource type
- Security requirements (OAuth2)

### Supported Operations

| Operation | Description |
|-----------|-------------|
| **Read** | Retrieve a single resource by ID |
| **Search** | Query resources by supported search parameters |
| **Create** | Submit a new resource from an external system |
| **Update** | Modify an existing resource |
| **$everything** | Retrieve all data for a patient (Patient/$everything) |

### Search Parameters

Common search parameters supported across resources:

| Parameter | Resources | Description |
|-----------|-----------|-------------|
| `_id` | All | Resource identifier |
| `patient` | Condition, Observation, MedicationRequest, etc. | Patient reference |
| `date` | Observation, Encounter, Appointment | Date or date range |
| `status` | Condition, MedicationRequest, Encounter | Resource status |
| `code` | Observation, Condition | Clinical code (LOINC, SNOMED, ICD-10) |
| `category` | Observation, DiagnosticReport | Resource category |

### CCDA Document Generation

The FHIR Gateway supports generation of Consolidated Clinical Document Architecture (CCDA) documents for Health Information Exchange (HIE):

| Document Type | Description | Common Use |
|---------------|-------------|------------|
| **CCD (Continuity of Care Document)** | Comprehensive patient summary | Transfer of care, referrals, patient request |
| **Discharge Summary** | Summary of an inpatient stay | Discharge to another facility or primary care |
| **Referral Note** | Summary for a referral recipient | Specialty care or community referrals |
| **Progress Note** | Documentation of a clinical encounter | Ongoing care coordination |

#### Generating a CCDA Document

1. Navigate to the FHIR Gateway page (/fhir).
2. Select the **Documents** section.
3. Search for the patient by name or ID.
4. Select the document type (CCD, Discharge Summary, Referral Note, or Progress Note).
5. For Discharge Summary and Progress Note, select the encounter or date range to include.
6. Click **Generate Document**.
7. Review the generated document and click **Send** to transmit via the configured HIE connection, or **Download** to save locally.

### Endpoint Management

Configure connections to external FHIR-enabled systems:

- **Endpoint URL** -- The base URL of the external FHIR server
- **Display Name** -- Friendly name for the connection
- **Connection Status** -- Real-time status indicator (Connected, Disconnected, Error)
- **Last Successful Transaction** -- Timestamp of the most recent successful exchange
- **Error Count** -- Number of failed transactions since the last reset

#### Adding a New Endpoint

1. Click **Add Endpoint**.
2. Enter the endpoint URL, display name, and description.
3. Configure the OAuth2 client credentials (see below).
4. Click **Test Connection** to verify connectivity.
5. If the test is successful, click **Save**.

### Connection Health Monitoring

The FHIR Gateway continuously monitors the health of each configured endpoint:

- **Heartbeat Check** -- Periodic pings to the endpoint's `/metadata` URL
- **Transaction Success Rate** -- Percentage of successful transactions over the last 24 hours
- **Latency** -- Average response time for transactions
- **Alert Thresholds** -- Configurable thresholds for error rate and latency that trigger notifications

> **Warning:** If a FHIR endpoint shows a Disconnected or Error status, investigate immediately. Failed connections can result in missing clinical data for patients being treated at external facilities.

### OAuth2 Authorization

External FHIR endpoints are authenticated using the OAuth2 client credentials grant:

- **Client ID** -- The unique identifier for the NewVistas application at the external authorization server
- **Client Secret** -- The secret key used to authenticate (stored encrypted)
- **Token Endpoint** -- The URL of the external authorization server's token endpoint
- **Scopes** -- The OAuth2 scopes requested (e.g., `patient/*.read`, `system/*.read`)
- **Token Refresh** -- Automatic token refresh is handled by the gateway; configure the refresh interval to match the token expiration period

> **Note:** Client secrets are encrypted at rest and are not displayed in the user interface after initial entry. To change a client secret, enter the new value in the configuration form.

---

## Lab EDI (/lab-edi)

The Lab EDI (Electronic Data Interchange) page manages electronic message exchange with reference laboratories and other external lab systems using HL7 messaging standards.

![Lab EDI message status showing outbound orders and inbound results](screenshots/lab-edi-message-status.png)

### Message Types

#### Outbound Messages (NewVistas to External Lab)

| Message Type | HL7 Code | Description |
|-------------|----------|-------------|
| **Order Message** | ORM | General order message for lab test requests |
| **Laboratory Order** | OML | Specific laboratory order message with specimen details |

#### Inbound Messages (External Lab to NewVistas)

| Message Type | HL7 Code | Description |
|-------------|----------|-------------|
| **Observation Result** | ORU | Lab results returned in response to an order |
| **Unsolicited Lab Result** | OUL | Lab results sent without a corresponding order in the system |

### Message Status

| Status | Description |
|--------|-------------|
| **SENT** | The outbound message has been transmitted to the external lab |
| **ACKNOWLEDGED** | The external lab has acknowledged receipt of the message (ACK received) |
| **REJECTED** | The external lab has rejected the message (NAK received); review error details |
| **RESULT_RECEIVED** | Results have been received from the external lab and are ready for filing |

### Operations

- **Send** -- Transmit a pending order to the external lab. Orders are typically sent automatically but can be manually triggered.
- **View Message** -- View the full HL7 message content for troubleshooting or verification.
- **Resubmit** -- Resend a rejected or failed message after correcting the issue.
- **File Results** -- Accept received results and file them into the patient's lab record.

#### Reviewing and Filing Lab Results

1. Navigate to the Lab EDI page (/lab-edi).
2. Filter for messages with status **RESULT_RECEIVED**.
3. Click on a result message to view the full details, including patient, test, and result values.
4. Review the results for accuracy and completeness.
5. Click **File Results** to add the results to the patient's lab record.
6. If there are discrepancies, flag the result for manual review before filing.

> **Tip:** Set up automatic filing for routine reference lab results to reduce manual workload. Use manual review for new test types or when the external lab has been recently onboarded.

### Troubleshooting EDI Messages

- **REJECTED messages** -- Check the NAK response for error codes and descriptions. Common issues include invalid patient identifiers, unknown test codes, or formatting errors.
- **Missing results** -- If expected results have not arrived, check the outbound order status (was it ACKNOWLEDGED?) and contact the reference lab.
- **Duplicate results** -- The system flags potential duplicates based on order number and test code. Review flagged duplicates before filing.

---

## Lab Instruments (/lab-instruments)

The Lab Instruments page manages the configuration and monitoring of laboratory analyzers and point-of-care testing devices that interface directly with NewVistas.

### Instrument Configuration

Each instrument record includes:

- **Name** -- Display name of the instrument
- **Type** -- The category of testing the instrument performs
- **Manufacturer** -- Device manufacturer name
- **Model** -- Specific device model
- **Serial Number** -- Unique device identifier
- **Connection Type** -- How the instrument communicates with NewVistas

#### Instrument Types

| Type | Description | Example Instruments |
|------|-------------|-------------------|
| **CHEMISTRY** | General chemistry analyzers | Roche cobas, Siemens Atellica |
| **HEMATOLOGY** | Complete blood count and differential analyzers | Sysmex XN, Beckman Coulter DxH |
| **COAGULATION** | Coagulation testing instruments | Stago STA-R, Siemens CS-5100 |
| **BLOOD_GAS** | Blood gas and electrolyte analyzers | Radiometer ABL, Siemens RAPIDPoint |
| **URINALYSIS** | Automated urine analysis systems | Roche cobas u, Sysmex UF |
| **POINT_OF_CARE** | Bedside and near-patient testing devices | i-STAT, Accu-Chek, CoaguChek |
| **MICROBIOLOGY** | Microbiology culture and identification systems | bioMerieux VITEK, BD Phoenix |

#### Connection Types

| Connection | Description |
|------------|-------------|
| **SERIAL** | RS-232 serial port connection (legacy instruments) |
| **TCP-IP** | Network-based TCP/IP connection |
| **HL7** | HL7 messaging protocol over TCP/IP |
| **ASTM** | ASTM E1381/E1394 protocol (common for lab instruments) |
| **USB** | USB direct connection (point-of-care devices) |

### Data Flow

Results from laboratory instruments follow this flow:

1. **Received** -- Raw data is received from the instrument via the configured connection
2. **Validated** -- The system validates the data against expected formats, ranges, and patient matching rules
3. **Filed** -- Validated results are filed into the patient's lab record

> **Note:** Results that fail validation are held for manual review. Common validation failures include unmatched patient identifiers, results outside plausible ranges, or incomplete data sets.

### Troubleshooting

The following tools are available for troubleshooting instrument connectivity:

| Action | Description |
|--------|-------------|
| **Ping** | Test network connectivity to the instrument (TCP-IP connections only) |
| **View Logs** | Review the communication log showing all messages sent and received |
| **Reset Connection** | Close and re-establish the connection to the instrument |
| **Send Test Message** | Send a test query to verify two-way communication |

#### Troubleshooting Steps

1. Check the instrument's connection status on the Lab Instruments page.
2. If the status shows Disconnected, click **Ping** (for TCP-IP connections) to test network connectivity.
3. Review the communication **Logs** for error messages or timeout events.
4. If the connection was recently interrupted, click **Reset Connection** to re-establish communication.
5. Send a **Test Message** to verify the instrument responds correctly.
6. If issues persist, check the physical connection (cable, network port) and the instrument's own interface settings.

> **Warning:** Resetting a connection may interrupt any in-progress result transmissions. Coordinate with the laboratory before resetting connections during peak testing hours.

---

## MailMan (/mailman)

MailMan is the internal messaging system for facility-wide staff communication, based on the VistA MailMan system. It supports direct messages, bulletin boards, and distribution lists.

![MailMan inbox with priority indicators and unread message counts](screenshots/mailman-inbox.png)

### Inbox

The inbox displays all received messages, sorted by date (most recent first). Each message shows:

- **Sender** -- Name of the person or system that sent the message
- **Subject** -- Message subject line
- **Date/Time** -- When the message was received
- **Priority** -- Visual priority indicator

#### Message Priority

| Priority | Display | Description |
|----------|---------|-------------|
| **NORMAL** | No indicator | Standard priority message |
| **HIGH** | Yellow indicator | Important message requiring attention |
| **URGENT** | Red indicator | Time-sensitive message requiring immediate attention |

### Compose

Create and send a new message:

1. Click **Compose** from the MailMan page.
2. Enter one or more recipients in the **To** field (search by name or user ID). You can also select a distribution list.
3. Enter the **Subject** line.
4. Compose the message **Body**.
5. Set the **Priority** (Normal, High, or Urgent).
6. Optionally attach a file by clicking **Attach**.
7. Click **Send**.

> **Tip:** Use URGENT priority sparingly. Reserve it for messages requiring same-day action. Overuse of high-priority messages reduces their effectiveness.

### Bulletin Boards

Bulletin boards are shared message areas organized by topic. Staff can subscribe to bulletin boards to receive automatic notifications.

- **View Bulletins** -- Browse active bulletin boards and their posted messages
- **Post** -- Add a new message to a bulletin board (requires appropriate permissions)
- **Subscribe/Unsubscribe** -- Manage your bulletin board subscriptions

### Distribution Lists

Distribution lists group users for targeted messaging. They are managed by system administrators.

- **View Lists** -- See all available distribution lists and their members
- **Create List** -- Create a new distribution list (administrator only)
- **Edit Members** -- Add or remove members from a distribution list
- **Delete List** -- Remove a distribution list that is no longer needed

> **Note:** Messages sent to a distribution list are delivered to all current members. Changes to list membership do not affect messages already sent.

---

## GPRA Reporting (/gpra-reporting)

The GPRA (Government Performance and Results Act) Reporting page generates performance measurement reports for VA clinical quality indicators. These reports are critical for facility performance evaluation and national VA reporting requirements.

![GPRA scorecard showing measure results with trends and benchmarks](screenshots/gpra-scorecard.png)

### Reporting Periods

| Period Type | Description |
|-------------|-------------|
| **Fiscal Year** | Full federal fiscal year (October 1 through September 30) |
| **Quarter** | Fiscal year quarter (Q1: Oct-Dec, Q2: Jan-Mar, Q3: Apr-Jun, Q4: Jul-Sep) |

### Measure Sets

| Measure Set | Focus Area | Key Measures |
|-------------|------------|--------------|
| **ALL** | Complete set of all GPRA measures | All measures across all domains |
| **PRIMARY_CARE** | Primary care quality indicators | Access, continuity, chronic disease management |
| **MENTAL_HEALTH** | Mental health screening and treatment | Depression screening, PTSD treatment, substance use |
| **PREVENTION** | Preventive care and screening | Immunizations, cancer screening, health risk assessments |
| **CHRONIC_DISEASE** | Chronic disease management | Diabetes, hypertension, heart failure management |

### Summary Scorecard

The report generates a scorecard with the following columns:

| Column | Description |
|--------|-------------|
| **Measure** | Name and description of the GPRA measure |
| **Numerator** | Count of patients meeting the measure criteria |
| **Denominator** | Count of patients eligible for the measure |
| **Rate** | Performance rate (numerator / denominator as a percentage) |
| **Trend** | Direction of change compared to the previous reporting period (improving, declining, stable) |
| **Benchmark** | National VA benchmark for the measure |
| **Ranking** | Facility ranking relative to other VA facilities (percentile) |

### Common GPRA Measures

| Measure | Description | Benchmark |
|---------|-------------|-----------|
| **Influenza Vaccination** | Percentage of eligible patients who received the annual influenza vaccine | Varies by year |
| **HbA1c Control** | Percentage of diabetic patients with HbA1c < 9.0% | National VA average |
| **Blood Pressure Control** | Percentage of hypertensive patients with BP < 140/90 | National VA average |
| **Depression Screening** | Percentage of patients screened for depression using PHQ-2/PHQ-9 | National VA average |

### Generating a Report

1. Navigate to the GPRA Reporting page (/gpra-reporting).
2. Select the **Measure Set** (ALL, PRIMARY_CARE, MENTAL_HEALTH, PREVENTION, or CHRONIC_DISEASE).
3. Select the **Reporting Period** (fiscal year and/or quarter).
4. Click **Generate Report**.
5. Review the scorecard results.
6. Click **Export** to download the report as CSV or PDF.

> **Note:** GPRA reports are based on clinical data in the system. Ensure that all clinical encounters, orders, and results are documented and filed before generating reports to avoid undercounting.

---

## iCare Dashboard (/icare-dashboard)

The iCare Dashboard provides population health management tools for clinical teams, enabling proactive management of patient panels, clinical reminders, and quality measures.

![iCare Dashboard showing patient panel with reminders and risk indicators](screenshots/icare-dashboard.png)

### Patient Panel

The patient panel displays all patients assigned to a provider or clinical team:

| Column | Description |
|--------|-------------|
| **Patient Name** | Patient name and identifier |
| **Last Visit** | Date of the most recent clinical encounter |
| **Next Appointment** | Date of the next scheduled appointment |
| **Reminders** | Count of due and overdue clinical reminders |
| **Risk Level** | Patient risk stratification (Low, Moderate, High) based on clinical criteria |

### Clinical Reminders

Track and manage clinical reminders across the patient panel:

- **Due Reminders** -- Reminders that are currently due based on clinical guidelines
- **Overdue Reminders** -- Reminders that are past due and require attention
- **Completion Rates** -- Percentage of reminders completed by type (e.g., annual exam, immunization, screening)
- **Drill-Down** -- Click on a reminder count to see the individual patient's reminder list and take action

### Performance Metrics

| Metric | Description |
|--------|-------------|
| **Panel Size** | Total number of patients assigned to the provider or team |
| **Access Rate** | Percentage of panel patients seen within the defined access standard (e.g., within 7 days of requested appointment) |
| **Reminder Completion** | Percentage of due clinical reminders that have been addressed |
| **Quality Measures** | Performance on GPRA and local quality measures for the panel population |

### Actionable Patient Lists

Generate focused lists of patients requiring specific follow-up:

- **Overdue for Appointments** -- Patients who have not been seen within the expected timeframe
- **Open Clinical Reminders** -- Patients with due or overdue clinical reminders
- **Abnormal Lab Results** -- Patients with lab results flagged as abnormal that have not been reviewed or acted upon
- **Recently Discharged** -- Patients discharged from inpatient care within the last 30 days who need post-discharge follow-up

> **Tip:** Review the iCare Dashboard at the start of each clinic day to identify patients on your schedule who have open reminders or pending results. This allows you to address multiple needs during a single encounter.

---

## Patient Portal (/patient-portal)

The Patient Portal provides patients with secure online access to their health information and communication tools. Administrators use this page to manage portal configuration, content visibility, and secure messaging.

### Patient-Facing Features

The portal provides patients access to the following:

| Feature | Description |
|---------|-------------|
| **Appointments** | View upcoming and past appointments; request new appointments |
| **Medications** | View current medications and request refills |
| **Lab Results** | View filed lab results (subject to release delay settings) |
| **Vitals** | View recent vital sign readings |
| **Allergies** | View documented allergies and adverse reactions |
| **Problems** | View active problem list |
| **Immunizations** | View immunization history |

### Secure Messaging

The Patient Portal includes a secure messaging system that allows patients to communicate with their healthcare team.

#### Message Categories

| Category | Description |
|----------|-------------|
| **APPOINTMENT** | Questions about scheduling, rescheduling, or cancelling appointments |
| **MEDICATION** | Questions about medications, refills, or side effects |
| **TEST_RESULT** | Questions about lab results, imaging, or other diagnostic tests |
| **REFERRAL** | Questions about referrals, consults, or specialty care |
| **GENERAL** | General healthcare questions or other topics |

#### Responding to Secure Messages

1. Navigate to the Patient Portal page (/patient-portal).
2. Click on the **Secure Messages** section.
3. Review pending messages from patients, sorted by date and category.
4. Click on a message to view its contents.
5. Compose a response and click **Send**.

> **Note:** Secure messages are part of the patient's medical record. Responses should be clinically appropriate, professional, and documented as part of the care process.

### Release Delays

Clinicians can configure release delays for lab results to allow time for clinical review before results are visible to patients on the portal.

- **Default Delay** -- The standard delay period applied to all lab results (e.g., 3 business days)
- **Custom Delays** -- Specific test types can have longer or shorter delay periods based on clinical sensitivity
- **Immediate Release** -- Clinicians can manually release results immediately after review if the default delay period has not yet elapsed

> **Warning:** Release delays are intended to ensure that patients do not see concerning results before their clinician has had an opportunity to review them and plan appropriate communication. Configure delays carefully and ensure that clinical staff review results within the delay period.

#### Configuring Release Delays

1. Navigate to the Patient Portal page (/patient-portal).
2. Click on the **Settings** section.
3. Set the **Default Release Delay** (in business days).
4. To configure custom delays for specific test types, click **Add Custom Delay**, select the test type, and set the delay period.
5. Click **Save**.

---

## Common Integration Workflows

### Onboarding a New Reference Laboratory

1. **Configure the Lab EDI connection** -- Add the new reference lab's endpoint on the Lab EDI page. Enter the connection details (address, port, protocol version).
2. **Map test codes** -- Ensure that the reference lab's test codes are mapped to the corresponding NewVistas lab test codes.
3. **Send a test order** -- Submit a test order to verify end-to-end connectivity and message formatting.
4. **Verify result receipt** -- Confirm that results from the test order are received, validated, and can be filed correctly.
5. **Enable automatic filing** -- Once the connection is verified, enable automatic filing for routine result types.

### Connecting a New Lab Instrument

1. **Add the instrument** -- On the Lab Instruments page, click Add Instrument and enter the device details (name, type, manufacturer, model, serial number).
2. **Configure the connection** -- Select the connection type and enter the connection parameters (COM port for serial, IP address/port for TCP-IP, etc.).
3. **Test connectivity** -- Use the Ping and Test Message tools to verify communication.
4. **Map result codes** -- Map the instrument's result codes to NewVistas lab test codes.
5. **Validate results** -- Process several known samples and verify that results are received, validated, and filed correctly.
6. **Go live** -- Enable the instrument for routine use and monitor the first few days of operation closely.

### Setting Up a New FHIR Endpoint

1. **Obtain credentials** -- Get the OAuth2 client ID, client secret, and token endpoint URL from the external system's administrator.
2. **Add the endpoint** -- On the FHIR Gateway page, click Add Endpoint and enter the URL and display name.
3. **Configure authentication** -- Enter the OAuth2 credentials and scopes.
4. **Test the connection** -- Click Test Connection to verify authentication and connectivity.
5. **Configure resource mappings** -- Enable the FHIR resources you need to exchange with this endpoint.
6. **Monitor transactions** -- Review the transaction log for the first few days to catch any mapping or formatting issues.

---

## Screenshots Reference

| Screenshot | Description |
|------------|-------------|
| ![FHIR resource mapping](screenshots/fhir-resource-mapping.png) | FHIR Gateway resource mapping configuration |
| ![Lab EDI message status](screenshots/lab-edi-message-status.png) | Lab EDI message list with status indicators |
| ![MailMan inbox](screenshots/mailman-inbox.png) | MailMan inbox with priority and read status |
| ![GPRA scorecard](screenshots/gpra-scorecard.png) | GPRA report scorecard with benchmarks and trends |
| ![iCare Dashboard](screenshots/icare-dashboard.png) | iCare Dashboard with patient panel and reminders |
| ![Patient Portal](screenshots/patient-portal.png) | Patient Portal administration page |
