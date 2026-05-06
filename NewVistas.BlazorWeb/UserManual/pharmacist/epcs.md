# EPCS (Electronic Prescribing for Controlled Substances)

**Route:** `/epcs`

The EPCS (Electronic Prescribing for Controlled Substances) module manages electronic prescribing, receiving, and processing of controlled substance prescriptions in compliance with DEA EPCS regulations (21 CFR Part 1311) and CURES (Controlled substance Utilization Review and Evaluation System) requirements. This module supports the complete lifecycle of electronic controlled substance prescriptions, provider credential management, PDMP integration, and audit trail documentation.

---

## Tabs

The EPCS module is organized into three tabs.

### Tab 1: E-Prescriptions

The E-Prescriptions tab displays all electronic controlled substance prescriptions processed through the system.

#### Status Filter

Filter prescriptions by status:

| Status | Description |
|--------|-------------|
| **Draft** | Prescription has been started but not yet signed by the prescriber |
| **Signed** | Prescription has been signed with two-factor authentication but not yet transmitted |
| **Transmitted** | Prescription has been electronically transmitted to the dispensing pharmacy |
| **Acknowledged** | Dispensing pharmacy has acknowledged receipt of the prescription |
| **Error** | Transmission or processing error occurred (requires investigation) |
| **Cancelled** | Prescription has been cancelled by the prescriber or pharmacy |

#### E-Prescription List Columns

| Column | Description |
|--------|-------------|
| Rx ID | Electronic prescription identifier |
| Transaction Type | Type of transaction (see below) |
| Patient | Patient name and identifier |
| Drug | Medication name and strength |
| DEA Schedule | DEA schedule (II, III, IV, V) |
| Status | Current prescription status |
| Prescriber | Prescriber name and credential |
| Date | Date the prescription was created or received |

#### Transaction Types

| Type | Description |
|------|-------------|
| **NewRx** | New electronic prescription |
| **RefillRequest** | Pharmacy-initiated refill request to the prescriber |
| **RefillResponse** | Prescriber response to a refill request (approved, denied, or modified) |
| **CancelRx** | Cancellation request from the prescriber |
| **CancelRxResponse** | Pharmacy response to a cancellation request |
| **RxChangeRequest** | Pharmacy-initiated request to change a prescription (e.g., generic substitution, therapeutic alternative) |
| **RxChangeResponse** | Prescriber response to a change request |
| **RxRenewalRequest** | Pharmacy-initiated renewal request |
| **RxRenewalResponse** | Prescriber response to a renewal request |

![E-prescription list](screenshots/epcs-prescription-list.png)

#### Creating a New E-Prescription

To create a new electronic controlled substance prescription, use the following fields:

| Field | Required | Description |
|-------|----------|-------------|
| Transaction Type | Yes | Select the transaction type (NewRx, RefillRequest, RefillResponse, CancelRx, etc.) |
| Drug Name | Yes | Controlled substance name and strength |
| DEA Schedule | Yes | DEA schedule: II, III, IV, or V |
| NDC | No | National Drug Code (11-digit format) |
| Quantity | Yes | Total quantity to dispense |
| Days Supply | Yes | Number of days the quantity should last |
| Refills Authorized | Conditional | Number of refills authorized. **Schedule II cannot have refills (must be 0).** |
| Sig/Directions | Yes | Complete prescriber directions for the patient |
| Diagnosis Code | No | ICD-10 diagnosis code for the indication |
| Prescriber NPI | Yes | Prescriber's National Provider Identifier |
| Prescriber DEA | Yes | Prescriber's DEA registration number |
| Prescriber Name | Yes | Prescriber's full name and credential |
| Credential ID | Yes | Prescriber's EPCS credential identifier (for 2FA) |
| Prescription ID | No | Internal prescription identifier (auto-generated if not provided) |
| Pharmacy NCPDP ID | Yes | Dispensing pharmacy's NCPDP identifier |

> **Warning:** DEA Schedule II prescriptions **cannot have refills**. The Refills Authorized field must be 0 for Schedule II medications. The system enforces this rule and will reject any Schedule II prescription with refills greater than 0.

![New e-Rx form](screenshots/epcs-new-erx-form.png)

> **Note:** Creating and signing an EPCS prescription requires two-factor authentication (2FA). The prescriber must authenticate using their registered credential (hardware token, biometric, or soft token) before the prescription can be signed and transmitted.

---

### Tab 2: Prescription Detail

The Prescription Detail tab displays the complete information for a selected e-prescription, including the full audit trail and two-factor authentication events.

#### Detail Sections

- **Prescription Information** -- All fields from the e-prescription (drug, dose, quantity, days supply, refills, SIG, etc.)
- **Patient Information** -- Patient demographics and identifiers
- **Prescriber Information** -- Prescriber name, NPI, DEA number, and credential used for signing
- **Pharmacy Information** -- Dispensing pharmacy name, NCPDP ID, and address
- **Status History** -- Complete status change history with timestamps
- **2FA Events** -- Two-factor authentication events including:
  - Authentication method used (hardware token, biometric, soft token)
  - Date and time of authentication
  - Success or failure status
  - Credential identifier
- **Transmission Log** -- Electronic transmission details including send/receive timestamps and any error messages

> **Note:** The audit trail is maintained for DEA compliance and cannot be modified or deleted. All access to the audit trail is itself logged.

---

### Tab 3: Provider Credentials

The Provider Credentials tab manages EPCS credentials for authorized prescribers. Each prescriber who writes electronic controlled substance prescriptions must have a valid, active EPCS credential.

#### Credential List Columns

| Column | Description |
|--------|-------------|
| Provider Name | Prescriber's full name and credential |
| NPI | National Provider Identifier |
| DEA Number | DEA registration number |
| Credential Type | Type of EPCS authentication credential (see below) |
| Status | Credential status: **ACTIVE**, **SUSPENDED**, or **REVOKED** |
| Expiration | Credential expiration date |

#### Credential Types

| Type | Description |
|------|-------------|
| **Hardware Token** | Physical authentication device (e.g., smart card, USB security key) |
| **Biometric** | Biometric authentication (e.g., fingerprint, facial recognition) |
| **Soft Token** | Software-based authentication token (e.g., authenticator app on a mobile device) |

#### Credential Statuses

| Status | Description |
|--------|-------------|
| **ACTIVE** | Credential is valid and can be used for EPCS signing |
| **SUSPENDED** | Credential is temporarily suspended (e.g., pending investigation, lost device) |
| **REVOKED** | Credential has been permanently revoked and cannot be reactivated |

![Provider credentials](screenshots/epcs-provider-credentials.png)

> **Warning:** Expired or suspended credentials prevent the prescriber from signing EPCS prescriptions. Monitor the Expiration column and proactively notify prescribers when their credentials are approaching expiration.

---

## Key Functions

### Receive E-Prescriptions from External Providers

The EPCS module receives electronic prescriptions from external prescribers via NCPDP SCRIPT standard messaging.

1. Incoming e-prescriptions appear in the E-Prescriptions tab with the appropriate status.
2. The system validates the incoming prescription for required fields, DEA compliance, and prescriber credential verification.
3. Valid prescriptions enter the pharmacy verification workflow (see [Outpatient Pharmacy](outpatient.md)).
4. Invalid prescriptions receive Error status with documented error reasons.

### PDMP Integration

The Prescription Drug Monitoring Program (PDMP) integration allows pharmacists to query the state PDMP database directly from the EPCS module.

1. Select a patient or e-prescription.
2. Click **Query PDMP** to retrieve the patient's controlled substance dispensing history from the state PDMP.
3. Review the PDMP report for:
   - Controlled substances dispensed at other pharmacies
   - Multiple prescribers for the same controlled substance class
   - Overlapping prescriptions
   - Total morphine milligram equivalent (MME) calculations
   - Potential indicators of doctor shopping or diversion
4. Document the PDMP review in the patient's record.

> **Note:** PDMP query requirements vary by state. Many states require a PDMP check before dispensing any controlled substance. Follow your facility's policy and state regulations for PDMP query timing and documentation.

![PDMP integration](screenshots/epcs-pdmp-integration.png)

### DEA Compliance

The EPCS module enforces DEA EPCS requirements (21 CFR Part 1311):

- **Two-factor authentication** -- Prescribers must authenticate with two of three categories: something they know (password/PIN), something they have (hardware token/smart card), or something they are (biometric). Both factors must be from different categories.
- **Identity proofing** -- Prescribers must undergo identity proofing before receiving EPCS credentials.
- **Credential management** -- Credentials must be managed by an authorized credential service or the facility's EPCS administrator.
- **Application audit** -- The EPCS application undergoes periodic third-party audits to maintain compliance.
- **Record retention** -- All EPCS records must be maintained for a minimum of two years (longer per state requirements).

### Audit Trail

The EPCS module maintains a complete, tamper-resistant audit trail of all controlled substance prescribing activity.

The audit trail records:

- Prescription creation, modification, signing, transmission, and cancellation events
- Two-factor authentication events (success and failure)
- PDMP query events
- Credential management events (issuance, suspension, revocation)
- User access events (login, logout, prescription access)
- System events (errors, timeouts, configuration changes)

> **Tip:** Review the audit trail periodically for unusual patterns, such as high-volume prescribing, repeated authentication failures, or prescriptions created outside normal business hours. These patterns may warrant further investigation.

---

## Common EPCS Workflows

### Processing an Incoming Controlled Substance E-Prescription

1. Locate the new e-prescription in the E-Prescriptions tab (Status: Transmitted or Acknowledged).
2. Review the prescription details for completeness and clinical appropriateness.
3. Query the PDMP if required by state law or facility policy.
4. Verify the prescriber's DEA registration and EPCS credential status.
5. Process the prescription through the standard verification and dispensing workflow.
6. The prescription status updates as it progresses through the workflow.

### Handling a Refill Request for a Controlled Substance

1. For Schedule III-V medications with authorized refills, process the refill through the standard outpatient refill workflow.
2. For Schedule II medications (no refills allowed), initiate an RxRenewalRequest to the prescriber through the EPCS module.
3. Monitor for the prescriber's RxRenewalResponse.
4. If approved, process the new prescription through verification and dispensing.

### Responding to a CancelRx Request

1. When a prescriber sends a CancelRx request, the e-prescription appears with a pending cancellation indicator.
2. Review the cancellation request and check whether the prescription has already been dispensed.
3. If not yet dispensed, cancel the prescription and send a CancelRxResponse confirming the cancellation.
4. If already dispensed, contact the prescriber to discuss options (the prescription cannot be un-dispensed, but the patient can return unused medication).

> **Note:** Once a controlled substance prescription has been dispensed, it cannot be electronically cancelled. The physical medication must be returned and processed through the return/waste workflow per DEA regulations.
