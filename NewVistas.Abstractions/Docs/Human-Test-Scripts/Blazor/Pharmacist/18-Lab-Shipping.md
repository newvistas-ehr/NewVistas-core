# Specimen Shipping Manifests -- Pharmacist Human Test Script

## Prerequisites

- **Login:** PHARM1 (WILLIAMS,ROBERT L -- Clinical Pharmacy) / Password: `smythVista1`
- **Pre-conditions:**
  1. The SiloHost, WebServer, and BlazorWeb projects must all be running.
  2. Navigate to `/lab-shipping` in the browser.
  3. The Automated Lab Instruments module should have demo data loaded. If not:
     ```
     POST /api/labinstruments/demo/load
     ```
     This seeds 6 demo instruments and 3 shipping configs (Quest Diagnostics, LabCorp, ARUP Laboratories).

---

## Scenario 1: Create Shipping Manifest

### Steps

1. Navigate to `/lab-shipping`.
2. Click the **Create Manifest** tab.
3. Fill in the manifest creation form:
   - Shipping Config ID: `QUEST-001` (or the ID of a seeded shipping config)
   - Destination Lab: `Quest Diagnostics`
   - Shipping Method: `FedEx Priority Overnight`
   - Shipping Conditions: `Ambient temperature, no freeze`
   - Container Type: `Insulated Shipping Box`
   - Created By: `PHARM1`
   - Notes: `Weekly specimen batch for reference lab send-out`
4. Click **Create Manifest** (or the submission button).

### Expected Result

- A success message appears with the new Manifest ID (e.g., "Manifest created: LA-SHIP:XXXXXXXX").
- Switch to the **Manifest Lookup** tab.
- Enter the manifest ID and click **Load Manifest**.
- The manifest detail panel shows:
  - Manifest ID
  - Destination: Quest Diagnostics
  - Config ID: QUEST-001
  - Status: **OPEN** (green badge)
  - Method: FedEx Priority Overnight
  - Conditions: Ambient temperature, no freeze
  - Container: Insulated Shipping Box
  - Created: current date/time
  - Created By: PHARM1
  - Specimens: 0 active, 0 removed

---

## Scenario 2: Add Specimens to Manifest

### Steps

1. With the OPEN manifest loaded, add specimens via the API:
   ```
   POST /api/labshipping/manifests/{manifestId}/specimens
   {
     "specimenId": "SPEC-001",
     "patientId": "4",
     "testName": "TSH (Thyroid Stimulating Hormone)",
     "loincCode": "3016-3",
     "specimenType": "Serum",
     "collectionDate": "2026-03-29T08:00:00Z"
   }
   ```
   ```
   POST /api/labshipping/manifests/{manifestId}/specimens
   {
     "specimenId": "SPEC-002",
     "patientId": "9",
     "testName": "Vitamin D, 25-Hydroxy",
     "loincCode": "1989-3",
     "specimenType": "Serum",
     "collectionDate": "2026-03-29T08:30:00Z"
   }
   ```
   ```
   POST /api/labshipping/manifests/{manifestId}/specimens
   {
     "specimenId": "SPEC-003",
     "patientId": "16",
     "testName": "HbA1c",
     "loincCode": "4548-4",
     "specimenType": "Whole Blood",
     "collectionDate": "2026-03-29T09:00:00Z"
   }
   ```
2. Reload the manifest in the UI by entering the manifest ID and clicking **Load Manifest**.

### Expected Result

- The Specimens section now shows 3 active, 0 removed.
- The specimens table displays:
  | Specimen ID | Patient | Test | LOINC | Type | Collected | Status | Actions |
  |------------|---------|------|-------|------|-----------|--------|---------|
  | SPEC-001 | 4 | TSH (Thyroid Stimulating Hormone) | 3016-3 | Serum | 03/29 08:00 | **ACTIVE** (green) | Remove |
  | SPEC-002 | 9 | Vitamin D, 25-Hydroxy | 1989-3 | Serum | 03/29 08:30 | **ACTIVE** (green) | Remove |
  | SPEC-003 | 16 | HbA1c | 4548-4 | Whole Blood | 03/29 09:00 | **ACTIVE** (green) | Remove |
- Remove buttons are visible because the manifest status is OPEN.

---

## Scenario 3: Remove a Specimen

### Steps

1. On the loaded manifest, locate SPEC-002 (Vitamin D) in the specimens table.
2. Click the **Remove** button on the SPEC-002 row.

### Expected Result

- The specimen row now shows:
  - Status: **REMOVED** (red badge)
  - The row is visually dimmed (row-removed class)
  - The Remove button disappears for this specimen
- The header updates to: "Specimens (2 active, 1 removed)"
- The specimen is soft-deleted (still visible but marked as removed).

---

## Scenario 4: Ship Manifest (Mark as Shipped)

### Steps

1. Ship the manifest via the API:
   ```
   POST /api/labshipping/manifests/{manifestId}/ship
   {
     "trackingNumber": "FX-7891234567",
     "shippedBy": "PHARM1"
   }
   ```
2. Reload the manifest in the UI.

### Expected Result

- The manifest detail shows:
  - Status: **SHIPPED** (blue or purple badge)
  - Tracking#: FX-7891234567
  - Shipped date: current date/time
- The Remove buttons on specimens are no longer visible (manifest is no longer OPEN).
- Only active specimens (not removed) are included in the shipment.

---

## Scenario 5: Mark Manifest as Received

### Steps

1. Record receipt at the reference lab via the API:
   ```
   POST /api/labshipping/manifests/{manifestId}/receive
   {
     "receivedBy": "QUEST-LAB-TECH",
     "notes": "All specimens received in good condition. Chain of custody verified."
   }
   ```
2. Reload the manifest in the UI.

### Expected Result

- The manifest detail shows:
  - Status: **RECEIVED** (green badge)
  - Received date: current date/time
- The manifest lifecycle is: OPEN -> SHIPPED -> RECEIVED.

---

## Scenario 6: Cancel an Open Manifest

### Steps

1. Create a new manifest (repeat Scenario 1 with different notes).
2. Cancel it via the API before shipping:
   ```
   POST /api/labshipping/manifests/{manifestId}/cancel
   {
     "reason": "Specimens need to be re-collected due to hemolysis"
   }
   ```
3. Reload the manifest in the UI.

### Expected Result

- The manifest detail shows:
  - Status: **CANCELLED** (red badge)
- No further actions can be performed on a cancelled manifest.
- Specimens that were on this manifest need to be added to a new manifest.

---

## Scenario 7: View and Update Shipping Configs (Reference Labs)

### Steps

1. Click the **Shipping Configs** tab.
2. The tab loads the list of reference lab shipping configurations.
3. If demo data was loaded, the configs include:
   - Quest Diagnostics
   - LabCorp
   - ARUP Laboratories
4. View a config's details by selecting it (click or load).

### Expected Result

- Each shipping config shows:
  - Config ID (e.g., QUEST-001, LABCORP-001, ARUP-001)
  - Lab Name
  - Address
  - Contact information
  - Preferred shipping method
  - Default container type
  - Account number
  - Active status
- To create or update a config, use the API:
  ```
  POST /api/labshipping/configs
  {
    "configId": "MAYO-001",
    "labName": "Mayo Clinic Laboratories",
    "address": "200 First Street SW, Rochester, MN 55905",
    "contactPhone": "1-800-533-1710",
    "preferredShippingMethod": "FedEx Priority Overnight",
    "defaultContainerType": "Cold Pack Insulated Box",
    "accountNumber": "MAYO-VA-5000",
    "isActive": true
  }
  ```
- After creating, refresh the configs tab. The new config appears in the list.
- These configs are referenced when creating new shipping manifests (the Shipping Config ID field).

### Cross-Reference with Lab Instruments

- The shipping configs tie into the Automated Lab Instruments module (Script reference: `/labinstruments`).
- Instruments configured for reference lab send-outs use these shipping configs to batch specimens into manifests.
- The seeded demo instruments include reference lab connections (e.g., Abbott i-STAT 1, Nova StatStrip).
