# Pharmacy Benefits and Prior Authorization -- Pharmacist Human Test Script

## Prerequisites

- **Login:** PHARM3 (MARTINEZ,CARLOS R -- Ambulatory Pharmacy) / Password: `smythVista1`
- **Patient:** 35
- **Pre-conditions:**
  1. The SiloHost, WebServer, and BlazorWeb projects must all be running.
  2. Navigate to `/pharmacybenefits` in the browser.
  3. Load demo data via the API: `POST /api/pharmacybenefits/demo/load?patientId=35`
  4. After demo load, the patient benefits tab should show an active insurance plan.

---

## Scenario 1: View Patient Benefit Plan Details

### Steps

1. Navigate to `/pharmacybenefits`.
2. The **Patient Benefits** tab should be active (default).
3. Enter Patient ID: `35` and click **Load Patient**.
4. The plan card displays with insurance details.

### Expected Result

- The plan card shows:
  - Plan Name (e.g., "TRICARE STANDARD" or similar demo plan)
  - Status badge: **ACTIVE** (green)
  - Insurer name
  - Member ID
  - Group number
  - Effective Date
  - Expiration Date
- Copay Tiers displayed in boxes:
  - Tier 1: dollar amount (Preferred Generic)
  - Tier 2: dollar amount (Preferred Brand)
  - Tier 3: dollar amount (Non-Preferred)
  - Deductible: dollar amount with MET/Not met indicator

---

## Scenario 2: Check Drug Coverage -- Tier 1 (Covered, Low Copay)

### Steps

1. With the patient loaded, locate the **Coverage Check** section.
2. Enter Drug ID: `50-LISINOPRIL` in the Drug ID field.
3. Click **Check Coverage**.

### Expected Result

- The coverage result shows:
  - Drug name: LISINOPRIL (or the drug ID)
  - Coverage status badge: **COVERED** (green) or **FORMULARY**
  - Tier: 1
  - Copay: the Tier 1 copay amount (e.g., $5.00 or $10.00)
  - No PA REQUIRED badge
  - Quantity limit (if any) or dash

---

## Scenario 3: Check Drug Coverage -- Requires Prior Auth

### Steps

1. Enter a Drug ID that requires PA in the formulary. Check what drugs were seeded in the demo:
   - Drug ID: `50-ADALIMUMAB` (or another specialty drug in the formulary)
2. Click **Check Coverage**.

### Expected Result

- The coverage result shows:
  - Coverage status: COVERED or RESTRICTED
  - A **PA REQUIRED** badge in red/orange
  - RequiresPriorAuth: true
  - The tier and copay are displayed but the drug cannot be filled without PA approval.

---

## Scenario 4: Submit Prior Authorization with Clinical Justification

### Steps

1. Submit a PA via the API:
   ```
   POST /api/pharmacybenefits/patients/35/prior-auths
   {
     "drugId": "50-ADALIMUMAB",
     "drugName": "ADALIMUMAB 40MG INJ PEN",
     "diagnosisCode": "M06.9",
     "diagnosisDescription": "Rheumatoid arthritis, unspecified",
     "clinicalJustification": "Patient has failed methotrexate and hydroxychloroquine. Meets step therapy criteria for biologic DMARD initiation.",
     "providerId": "PROV-001",
     "providerName": "DR. JANE SMITH",
     "requestedQuantity": 2,
     "requestedDaysSupply": 30
   }
   ```
2. Return to the UI and click **Load Patient** to refresh.
3. The Prior Authorizations section should show the new PA.

### Expected Result

- The PA table shows:
  - Drug: ADALIMUMAB 40MG INJ PEN
  - Status: **PENDING** (yellow badge)
  - Requested date: current date
  - Expires: (not yet set)
  - Provider: DR. JANE SMITH
  - Action buttons: **Approve** and **Deny** visible

---

## Scenario 5: Approve Prior Authorization (Reviewer Pharmacist)

### Steps

1. In the Prior Authorizations table, find the PENDING PA from Scenario 4.
2. Click the **Approve** button.
3. The Approve Prior Authorization panel opens:
   - Reviewer ID: enter `PHARM1`
   - Reviewer Name: enter `WILLIAMS,ROBERT L`
   - Expiration Date: select a date 1 year from now (e.g., `2027-03-29`)
   - Notes: enter `Meets criteria per formulary management committee guidelines. Step therapy documented.`
4. Click **Submit**.

### Expected Result

- The PA table refreshes. The PA row now shows:
  - Status: **APPROVED** (green badge)
  - Expires: the selected date (03/29/2027)
- The Approve/Deny buttons are no longer visible for this PA.
- The drug coverage check should now clear without PA blocking.

---

## Scenario 6: Deny Prior Authorization with Reason

### Steps

1. Submit another PA via the API for a different drug:
   ```
   POST /api/pharmacybenefits/patients/35/prior-auths
   {
     "drugId": "50-BRAND-DRUG",
     "drugName": "BRAND NAME EXPENSIVE DRUG 100MG",
     "diagnosisCode": "E11.9",
     "diagnosisDescription": "Type 2 diabetes mellitus without complications",
     "clinicalJustification": "Patient requests brand name only.",
     "providerId": "PROV-002",
     "providerName": "DR. MARK JONES"
   }
   ```
2. Refresh the patient. The new PA appears as PENDING.
3. Click the **Deny** button on the new PA.
4. The Deny Prior Authorization panel opens:
   - Reviewer ID: enter `PHARM1`
   - Reviewer Name: enter `WILLIAMS,ROBERT L`
   - Denial Reason: enter `Generic equivalent available and clinically appropriate. No documented allergy or intolerance to generic formulation. Patient preference alone does not meet PA criteria.`
5. Click **Submit**.

### Expected Result

- The PA status changes to **DENIED** (red badge).
- The denial reason is recorded in the PA details.
- No Approve/Deny buttons remain for this PA.

---

## Scenario 7: View Formulary by Tier

### Steps

1. Click the **Plan Formulary** tab.
2. Enter Plan ID: `TRICARE-STD` (or the plan ID from the demo data) and click **Load Formulary**.
3. The formulary table loads with columns: Drug Name, Tier, Coverage, PA?, Copay Override, Qty Limit, Day Supply, Step Therapy.
4. Use the Tier dropdown to filter:
   - Select **Tier 1 -- Preferred Generic**. Click or observe auto-filter.
   - Select **Tier 2 -- Preferred Brand**.
   - Select **Tier 3 -- Non-Preferred**.
   - Select **All tiers** to restore.
5. Check the **PA Required only** checkbox to see only drugs requiring prior authorization.

### Expected Result

- Tier 1 drugs show green "Tier 1" badge and typically lower or no copay override.
- Tier 2 drugs show "Tier 2" badge and moderate copay.
- Tier 3 drugs show "Tier 3" badge and higher copay.
- PA Required filter shows only drugs with "Yes" in the PA? column.
- Columns like Qty Limit and Day Supply show limits where applicable, or dashes where not set.
- Step Therapy column shows "Yes" or "No".
