# Drug Interaction Screening -- Pharmacist Human Test Script

## Prerequisites

- **Login:** PHARM1 (WILLIAMS,ROBERT L -- Clinical Pharmacy) / Password: `smythVista1`
- **Patient:** 16
- **Pre-conditions:**
  1. The SiloHost, WebServer, and BlazorWeb projects must all be running.
  2. Navigate to `/interaction-blocking` in the browser.
  3. Ensure the Drug Interaction Dataset is loaded. Navigate to `/drug-interactions` and click **Load Demo Dataset** on the Dataset Status tab if not already loaded. Verify the status shows "Dataset: LOADED" and "Cache: READY".
  4. Ensure Patient 16 has at least one active prescription. Use:
     ```
     POST /api/outpatientpharmacy/demo/load?patientId=16
     ```

---

## Scenario 1: Screen Prescription -- No Interactions Found (Cleared)

### Steps

1. Navigate to `/interaction-blocking`.
2. Enter Patient ID: `16` and click **Load**.
3. Click **+ Screen Rx**.
4. Fill in the form:
   - Prescription ID: `RX-SCREEN-001`
   - Drug Name: `LISINOPRIL 10MG TAB`
   - New Drug Ingredient IEN: `1898`
   - New Drug Ingredient Name: `LISINOPRIL`
   - Existing Med Ingredient IEN: `6809`
   - Existing Med Ingredient Name: `METFORMIN`
   - Screened By: `PHARM1`
5. Click **Run Screen**.

### Expected Result

- Success banner: "Screening completed: IS-XXXXXXXX"
- The Detail tab opens automatically.
- Status: **Cleared** (green badge).
- Interactions: 0 total, 0 blocking, 0 overridden.
- The message "No drug interactions found -- prescription is cleared." appears.
- On the All tab, the screening row shows Status: Cleared with 0 in Blocking column.

---

## Scenario 2: Screen -- Significant Interaction Found (BlockedPendingOverride)

### Steps

1. Click **+ Screen Rx**.
2. Fill in:
   - Prescription ID: `RX-SCREEN-002`
   - Drug Name: `WARFARIN 5MG TAB`
   - New Drug Ingredient IEN: `1190`
   - New Drug Ingredient Name: `WARFARIN`
   - Existing Med Ingredient IEN: `3345`
   - Existing Med Ingredient Name: `ASPIRIN`
   - Screened By: `PHARM1`
3. Click **Run Screen**.

### Expected Result

- The Detail tab opens.
- Status: **Blocked** (red badge) -- InteractionScreeningStatus.BlockedPendingOverride.
- Interaction Findings table shows at least one row:
  - New Drug Ingredient: WARFARIN
  - Existing Drug Ingredient: ASPIRIN
  - Severity: **Significant** (orange badge) or **Contraindicated** (red badge)
  - Blocking column: **BLOCKED** (red text)
  - Action column: **Override** button
- The Blocked tab now shows this screening.

---

## Scenario 3: Override a Blocking Interaction with Clinical Reason

### Steps

1. On the Detail tab from Scenario 2, locate the blocking interaction row.
2. Click the **Override** button on the WARFARIN/ASPIRIN interaction.
3. The Override form appears:
   - Pharmacist ID: enter `PHARM1`
   - Clinical Reason: enter `Provider intentionally co-prescribing low-dose aspirin 81mg with warfarin for mechanical heart valve. INR monitoring every 2 weeks. Benefits outweigh bleeding risk.`
4. Click **Submit Override**.

### Expected Result

- Success banner: "Override submitted for interaction #1."
- The interaction row now shows:
  - Blocking column: "Overridden by PHARM1"
  - Override button disappears for this row.
- If this was the only blocking interaction, the screening status changes to **Overridden** (gold badge).
- The screening no longer appears on the Blocked tab.

---

## Scenario 4: Check if Prescription is Cleared for Fill After Override

### Steps

1. After overriding all blocking interactions, verify clearance via the API:
   ```
   GET /api/patient/16/interactionscreening/prescription/RX-SCREEN-002/cleared
   ```
2. Alternatively, check the screening detail -- if status is Cleared or OverriddenByPharmacist, the Rx is cleared for fill.

### Expected Result

- API response: `{ "prescriptionId": "RX-SCREEN-002", "cleared": true }`
- The prescription can now proceed to fill in the Outpatient Pharmacy page.

---

## Scenario 5: Multiple Interactions -- Partial Override

### Steps

1. Click **+ Screen Rx**.
2. Fill in:
   - Prescription ID: `RX-SCREEN-003`
   - Drug Name: `FLUCONAZOLE 200MG TAB`
   - New Drug Ingredient IEN: `2450`
   - New Drug Ingredient Name: `FLUCONAZOLE`
   - Existing Med Ingredient IEN: `1190`
   - Existing Med Ingredient Name: `WARFARIN`
   - Screened By: `PHARM1`
3. Click **Run Screen**.
4. If multiple findings appear, override only the first one:
   - Click **Override** on finding #1.
   - Pharmacist ID: `PHARM1`
   - Reason: `Short course fluconazole 3 days only. Will increase INR monitoring frequency.`
   - Click **Submit Override**.

### Expected Result

- After overriding only one of multiple blocking interactions:
  - The screening status remains **Blocked** (because not all blocking interactions are overridden).
  - The overridden finding shows "Overridden by PHARM1".
  - The remaining blocking finding still shows **BLOCKED** with an Override button.
  - The Cleared API check returns `cleared: false` until all are overridden.

---

## Scenario 6: Moderate Interaction -- Warning Only (Not Blocking)

### Steps

1. Click **+ Screen Rx**.
2. Fill in:
   - Prescription ID: `RX-SCREEN-004`
   - Drug Name: `OMEPRAZOLE 20MG CAP`
   - New Drug Ingredient IEN: `7646`
   - New Drug Ingredient Name: `OMEPRAZOLE`
   - Existing Med Ingredient IEN: `1898`
   - Existing Med Ingredient Name: `LISINOPRIL`
   - Screened By: `PHARM1`
3. Click **Run Screen**.

### Expected Result

- If a Moderate or Minor interaction is found:
  - Status: **Cleared** (green badge) -- moderate interactions do not block.
  - Interaction Findings table shows the finding with:
    - Severity: **Moderate** (yellow badge) or **Minor** (green badge)
    - Blocking column: "Warning" (not BLOCKED)
    - No Override button needed (not blocking).
- If no interaction is found, the screening shows "No drug interactions found."
- In either case, the prescription is cleared for fill without requiring override.
