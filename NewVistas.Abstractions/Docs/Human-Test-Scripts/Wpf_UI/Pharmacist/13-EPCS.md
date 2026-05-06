# Electronic Prescribing of Controlled Substances (EPCS) -- Pharmacist Human Test Script -- WPF UI

## Prerequisites

- **Login:** PHARM3 (MARTINEZ,CARLOS R -- receives EPCS prescriptions) / Password: `smythVista1`
- **Additional Login for Prescribing:** DOCTOR1 (or any provider account) / Password: `smythVista1`
- **Patient:** 30
- **Pre-conditions:**
  1. The SiloHost, WebServer, and WPF Application (NewVistas.WpfClient) must all be running.
  2. In the Navigation Panel, select **EPCS**.
  3. Ensure Patient 30 exists in the system.

---

## Scenario 1: Receive and Acknowledge New EPCS Prescription

### Steps

1. First, create an EPCS prescription (simulating what a provider would transmit).
2. In the Navigation Panel, select **EPCS**.
3. Enter Patient ID: `30` in the Patient ID field in the toolbar and click **Load**.
4. Click **New e-Rx** to open the creation form.
5. Fill in:
   - Transaction Type: **NewRx** (value 0)
   - Drug Name: `OXYCODONE 5MG TAB`
   - DEA Schedule: `II`
   - NDC: `00406-0512-01`
   - Quantity: `120`
   - Days Supply: `30`
   - Refills Authorized: `0` (Schedule II -- no refills)
   - Sig / Directions: `TAKE 1 TABLET BY MOUTH EVERY 6 HOURS AS NEEDED FOR PAIN`
   - Diagnosis Code: `G89.29`
   - Prescriber NPI: `1234567890`
   - Prescriber DEA: `AS1234567`
   - Prescriber Name: `DR. JANE SMITH`
   - Credential ID: `EPCS-CRED-001`
   - Prescription ID: `RX-EPCS-001`
   - Pharmacy NCPDP ID: `1234567`
   - Pharmacy Name: `VA MAIN PHARMACY`
   - Pharmacy Address: `500 Veterans Way`
6. Click **Create e-Prescription**.
7. The prescription appears in the DataGrid with Status: **Draft** (gray status indicator) and Signed: **Unsigned** (yellow status indicator).

### Expected Result

- The e-Prescription DataGrid shows the new entry:
  - Drug: OXYCODONE 5MG TAB
  - Schedule: II
  - Type: **NewRx** status indicator (blue)
  - Status: **Draft** status indicator
  - Prescriber: DR. JANE SMITH
  - Signed: **Unsigned** (yellow status indicator)
  - Date: today's date
  - Actions: View and Sign buttons

---

## Scenario 2: View EPCS Prescription Details with 2FA Verification Record

### Steps

1. On the E-Prescriptions TabItem, click **View** on the OXYCODONE e-prescription (or right-click and select **View**).
2. The view switches to the **Prescription Detail** TabItem (tab 1).

### Expected Result

- The detail view shows comprehensive information in card sections:
  - **Drug Information:**
    - Drug Name: OXYCODONE 5MG TAB
    - NDC: 00406-0512-01
    - DEA Schedule: II
    - Quantity: 120
    - Transaction: NewRx
    - Days Supply: 30
    - Refills: 0
    - Sig: TAKE 1 TABLET BY MOUTH EVERY 6 HOURS AS NEEDED FOR PAIN
    - Diagnosis: G89.29
  - **Prescriber Information:**
    - NPI, DEA, Name, Credential ID
  - **Pharmacy Information:**
    - NCPDP ID, Name, Address
  - **Status Information:**
    - Current Status: Draft
    - Signed: No
  - **2FA Verification Record:** (empty until signed)
    - After signing (Scenario 1 continued), this section shows:
      - 2FA Method used
      - Certificate Thumbprint
      - Verification timestamp
      - Valid: Yes/No

---

## Scenario 3: Sign EPCS Prescription with 2FA and Cancel

### Sign Steps

1. Return to the **E-Prescriptions** TabItem (tab 0).
2. Click **Sign** on the Draft/Unsigned OXYCODONE prescription (or right-click and select **Sign**).
3. A dialog window appears with the Sign form:
   - 2FA Method: select **Hardware Token** (value 1) from the ComboBox
   - Certificate Thumbprint: enter `A1B2C3D4E5F6A1B2C3D4E5F6A1B2C3D4E5F6A1B2`
   - Prescription Hash: enter `SHA256:abcdef1234567890abcdef1234567890`
   - Valid: **Yes**
4. Click **Sign with 2FA**.

### Expected Result

- The prescription row updates:
  - Status changes to **Signed** (green status indicator)
  - Signed column shows **Signed** (green status indicator)
  - The Sign button disappears.

### Cancel Steps

5. Create another e-prescription for testing cancellation (repeat Scenario 1 with drug: `ALPRAZOLAM 0.5MG TAB`, DEA Schedule: `IV`).
6. Cancel it via the API:
   ```
   POST /api/epcs/30/prescriptions/{epcsId}/cancel
   { "reason": "Provider requested cancellation - changed to different medication" }
   ```
7. Refresh the E-Prescriptions TabItem.

### Expected Result

- The cancelled prescription shows Status: **Cancelled** (red status indicator).
- No Sign button is available for cancelled prescriptions.

---

## Scenario 4: Manage Provider EPCS Credentials (Activate, Suspend)

### Steps

1. Click the **Provider Credentials** TabItem (tab 2).
2. The TabItem loads the list of EPCS credential records in the DataGrid.
3. Manage credentials via the API:
   ```
   POST /api/epcs/credentials
   {
     "providerId": "PROV-001",
     "providerName": "DR. JANE SMITH",
     "deaNumber": "AS1234567",
     "npi": "1234567890",
     "credentialLevel": "FULL",
     "twoFactorMethod": "HardwareToken",
     "isActive": true
   }
   ```
4. Suspend a credential:
   ```
   POST /api/epcs/credentials/{credentialId}/suspend
   { "reason": "Pending renewal of DEA registration" }
   ```
5. Reactivate:
   ```
   POST /api/epcs/credentials/{credentialId}/activate
   ```
6. Refresh the Provider Credentials TabItem after each action.

### Expected Result

- The credentials DataGrid shows provider entries with:
  - Provider Name
  - DEA Number
  - NPI
  - Credential Level (FULL, LIMITED, etc.)
  - 2FA Method
  - Status (Active/Suspended)
- Suspended credentials prevent the provider from signing EPCS prescriptions.
- Reactivation restores signing capability.

---

## Scenario 5: View Prescriptions by Transmission Status

### Steps

1. Return to the **E-Prescriptions** TabItem (tab 0).
2. Use the status filter ComboBox:
   - Select **Draft** (value 0): shows only unsigned prescriptions.
   - Select **Signed** (value 1): shows signed but not yet transmitted.
   - Select **Transmitted** (value 2): shows prescriptions sent to the pharmacy.
   - Select **Acknowledged** (value 3): shows prescriptions confirmed by receiving pharmacy.
   - Select **Error** (value 4): shows prescriptions with transmission errors.
   - Select **Cancelled** (value 5): shows cancelled prescriptions.
   - Clear the filter to show all.

### Expected Result

- Each filter correctly narrows the DataGrid to matching statuses.
- The status indicator colors are consistent:
  - Draft: gray
  - Signed: green
  - Transmitted: blue
  - Acknowledged: green
  - Error: red
  - Cancelled: red
- DEA schedules tested: II (oxycodone), IV (alprazolam).
- 2FA Methods available: None (0), Hardware Token (1), Biometric (2), One-Time Password (3), Smart Card (4), Mobile Authenticator (5).
