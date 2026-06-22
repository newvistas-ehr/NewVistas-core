# Controlled-Substance Safety Gate (SafetyConfirm) -- Physician Human Test Script

Verifies the Tier-2 patient-safety confirmation that fires when a dangerous
controlled-substance order is entered. The gate is the `<SafetyConfirm>`
component (UI-CONVENTIONS.md §8): it is deliberately designed to defeat
muscle-memory -- the safe choice (Cancel) is the primary, right-most button,
and the override is secondary, left, and disabled until the prescriber proves
they read the warning. Orders in the **lethal** range cannot be overridden by a
single prescriber at all.

## Prerequisites

- **Login (prescriber):** DOCTOR1 (SMITH,JOHN A) / Password: `smythVista1`
- **Login (pharmacist, Scenario 4 only):** PHARM1 / Password: `smythVista1`
- **Patient:** 30
- **Pre-conditions:**
  1. The SiloHost, WebServer, and BlazorWeb projects must all be running.
  2. Navigate to `/epcs` (Electronic Prescribing of Controlled Substances).

### Gate thresholds (for reference)

Daily quantity = `Quantity ÷ Days Supply`.

| Surface | Schedule | Warn (type-to-confirm) | Lethal (co-sign required) |
|---------|----------|------------------------|---------------------------|
| EPCS (`/epcs`) | II | over 12 /day | 24 /day or more |
| EPCS (`/epcs`) | III–V | over 24 /day | 48 /day or more |
| Pharmacy POS (`/pharmacy-pos`) | (no schedule captured) | over 20 /day | 40 /day or more |

---

## Scenario 1: Lethal-Range Order Is Blocked (Co-Sign Required)

This is the headline case: a prescription for ~100 Nucynta/day must not be
approvable by a single click.

### Steps

1. Log in as **DOCTOR1**.
2. Navigate to `/epcs`.
3. Enter Patient ID: `30` and click **Load**.
4. Click **New e-Rx** to open the creation form.
5. Fill in:
   - Drug Name: `NUCYNTA 100MG TAB`
   - DEA Schedule: `II`
   - Quantity: `100`
   - Days Supply: `1`
   - (Other fields may be left as-is.)
6. Click **Create e-Prescription**.

### Expected Result

- A modal overlay appears with a **red** title bar: **"⚠ Dangerous order — confirmation required"**.
- A red hazard box reads approximately:
  *"NUCYNTA 100MG TAB — 100 units over 1 day(s) = 100/day. This is in the LETHAL range for a Schedule II controlled substance."*
- A co-sign notice states the order **cannot be overridden by a single prescriber** and requires a pharmacist or attending co-signature.
- **No type-to-confirm or reason fields are shown** (override is not available solo).
- Two buttons at the bottom:
  - **Override** — on the left, **disabled / greyed out** (cannot be clicked).
  - **Cancel / Modify order** — on the right, navy (primary). This is where muscle memory clicks.
- Click **Cancel / Modify order**.
- The modal closes and returns to the form. **No prescription is created** — the e-Prescription table does **not** gain a new row.

---

## Scenario 2: Warn-Range Order Requires Type-to-Confirm + Audited Reason

A high-but-not-lethal dose can be overridden, but only after the prescriber
types the drug name exactly and records a reason.

### Steps

1. On `/epcs` with Patient `30` loaded, click **New e-Rx**.
2. Fill in:
   - Drug Name: `OXYCODONE 5MG TAB`
   - DEA Schedule: `II`
   - Quantity: `15`
   - Days Supply: `1`
   - Sig / Directions: `TAKE AS DIRECTED`
3. Click **Create e-Prescription**.

### Expected Result (gate)

- The same red **"Dangerous order"** modal appears, with a hazard box reading approximately:
  *"OXYCODONE 5MG TAB — 15 units over 1 day(s) = 15/day, far above the usual maximum for a Schedule II controlled substance. Confirm this dose is intentional."*
- This time the modal shows **two input fields**:
  - *"To override, type **OXYCODONE 5MG TAB** exactly:"*
  - *"Reason for override (required, audited):"*
- The **Override** button is **disabled** until **both**:
  - the typed value exactly matches the drug name, **and**
  - a non-empty reason is entered.

### Steps (complete the override)

4. In the first field, type the drug name exactly: `OXYCODONE 5MG TAB`.
5. In the reason field, type: `Opioid-tolerant hospice patient; dose reviewed with attending.`
6. Confirm the **Override** button is now **enabled**, then click it.

### Expected Result (override accepted)

- The modal closes and the prescription **is** created.
- The new row appears in the e-Prescription table (Drug: OXYCODONE 5MG TAB, Schedule: II, Status: Draft).
- Open the prescription (**View**): the **Sig** now carries the audited override note appended, e.g. `TAKE AS DIRECTED [OVERRIDE: Opioid-tolerant hospice patient; dose reviewed with attending.]`.

### Negative check

- Repeat steps 1–3, but at the modal type the drug name **incorrectly** (e.g. `oxycodone`) or leave the reason blank → the **Override** button stays **disabled**. Click **Cancel / Modify order** → no prescription created.

---

## Scenario 3: Normal Order Is NOT Gated (Regression Guard)

The gate must stay out of the way of routine prescribing.

### Steps

1. On `/epcs` with Patient `30` loaded, click **New e-Rx**.
2. Fill in:
   - Drug Name: `OXYCODONE 5MG TAB`
   - DEA Schedule: `II`
   - Quantity: `20`
   - Days Supply: `5` (= 4 /day, well under the Schedule II warn threshold of 12 /day)
   - Refills Authorized: `0`
3. Click **Create e-Prescription**.

### Expected Result

- **No modal appears.** The prescription is created immediately and appears in the table.
- This confirms a normal controlled-substance order (and the existing EPCS test scripts) are unaffected by the gate.

---

## Scenario 4: Pharmacy POS Dispense Gate (Pharmacist)

The same component guards dispensing at Point of Sale, using a generic
quantity-per-day threshold (no DEA schedule is captured at POS).

### Steps

1. Log out, then log in as **PHARM1**.
2. Navigate to `/pharmacy-pos`.
3. Enter Patient ID: `30` and click **Load**.
4. Click **New Claim** to open the claim form.
5. Fill in (minimum):
   - Drug Name: `OXYCODONE 5MG TAB`
   - Qty Dispensed: `200`
   - Days Supply: `1`
   - (BIN / PCN / NCPDP Version as required by the form.)
6. Click **Submit Claim**.

### Expected Result

- The **"Dangerous order — confirmation required"** modal appears with a hazard box reading approximately:
  *"OXYCODONE 5MG TAB — 200 dispensed over 1 day(s) = 200/day. This is an implausibly high daily quantity and is in the LETHAL range."*
- **Override** is disabled (lethal range); **Cancel / Modify order** is primary.
- Click **Cancel / Modify order** → the claim is **not** submitted.
- (Optional) Re-enter with Qty Dispensed `25`, Days Supply `1` (= 25 /day, in the 20–40 warn band) → the type-to-confirm + reason variant appears; typing the drug name and a reason enables **Override**, and submitting then posts the claim.

---

## Notes

- The gate is implemented once in `Components/Shared/SafetyConfirm.razor` and
  wired into `Epcs.razor` (`CreatePrescription`) and `PharmacyPos.razor`
  (`SubmitClaim`). Thresholds live in those pages' `AssessRxSafety` /
  `AssessDispenseSafety` helpers.
- Patient-safety friction here is by design and is enforced by the component,
  **not** by button color (there is no danger-colored button in the convention).
- Override reasons are appended to the order's Sig so the justification persists
  on the record (auditable).
