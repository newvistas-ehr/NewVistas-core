# Medications

**Route:** `/medications`

The Medications page provides a read-only view of a patient's active medication profile. It displays all medications currently prescribed or active for the patient, including drug name, directions (sig), status, fill date, and remaining refills.

![Active medication list showing drug names, sigs, and refill counts](screenshots/medications-active-list.png)

---

## Loading the Medication List

1. Navigate to `/medications`.
2. Enter the **Patient ID** in the lookup bar.
3. Click **Load** (or press **Enter**).

The system retrieves the patient's active medications from the `PatientWorkflowGrain.GetActiveMedicationsAsync()` method and displays them in a table.

---

## Medication Table

The medication table displays all active medications for the patient.

| Column | Description |
|---|---|
| **Drug Name** | Name and strength of the medication (e.g., "METFORMIN 500MG TAB", "LISINOPRIL 10MG TAB") |
| **Sig** | Directions for use, also known as the "sig" (e.g., "TAKE ONE TABLET BY MOUTH TWICE DAILY WITH MEALS") |
| **Status** | Current medication status (see status values below) |
| **Fill Date** | Date the prescription was last filled or dispensed, in MM/DD/YYYY format |
| **Refills** | Number of refills remaining on the prescription |

### Medication Statuses

| Status | Description |
|---|---|
| **Active** | Medication is currently prescribed and should be taken as directed |
| **Hold** | Medication has been temporarily suspended by a provider |
| **Discontinued** | Medication has been permanently stopped |
| **Expired** | Prescription has passed its expiration date and can no longer be filled |

If the patient has no active medications, the table displays "No active medications."

![Medication detail row showing sig, status, and refill information](screenshots/medications-detail-row.png)

---

## Ordering New Medications

> **Note:** The Medications page is a **read-only** view of the current medication profile. To write a new medication order, navigate to the **Orders page** (`/orders`) and select the **Pharmacy** order type in the New Order tab. You can also use the **New Medication** button in the Cover Sheet's Workflow Actions bar, which navigates directly to the Orders page with the Pharmacy order type pre-selected.

For detailed instructions on placing medication orders, see the [Orders documentation](orders.md).

---

## Related Pharmacy Pages

NewVistas includes several pharmacy-related modules that work together with the Medications view. These modules are primarily used by pharmacy staff but may be relevant to clinicians:

### Outpatient Pharmacy (`/outpatientpharmacy`)

The Outpatient Pharmacy page is used by pharmacy staff to process and dispense outpatient prescriptions. It includes:
- Prescription queue management
- Label printing
- Drug utilization review
- Patient counseling documentation

For detailed information, see the [Pharmacist Guide](../pharmacist/index.md).

### Inpatient Pharmacy (`/inpatientpharmacy`)

The Inpatient Pharmacy page manages medication orders for admitted patients. It includes:
- Unit dose dispensing
- IV admixture preparation
- Medication administration scheduling
- Ward stock management

### Pharmacy Hub (`/pharmacy`)

The Pharmacy Hub is the central operations dashboard for pharmacy staff. It provides an overview of pending prescriptions, workload metrics, and queue status across all pharmacy operations.

### Pharmacy Benefits (`/pharmacybenefits`)

The Pharmacy Benefits page is used to verify a patient's pharmacy benefit coverage, including:
- Formulary status for medications
- Prior authorization requirements
- Copay information
- Preferred alternatives for non-formulary drugs

### Additional Pharmacy Modules

| Module | Route | Description |
|---|---|---|
| IV Pharmacy | `/ivpharmacy` | IV admixture preparation and dispensing |
| CMOP | `/cmop` | Consolidated Mail Outpatient Pharmacy for mail-order prescriptions |
| Controlled Substances | `/controlled-substances` | DEA Schedule II-V medication tracking and dispensing |
| Auto Refill | `/auto-refill` | Automatic prescription refill enrollment and management |
| EPCS | `/epcs` | Electronic Prescribing for Controlled Substances |
| Drug Formulary | `/drug-formulary` | Facility formulary management and drug lookup |
| Drug Interaction Data | `/drug-interaction-data` | Drug-drug interaction database and checking |
| Drug Utilization Review | `/drug-utilization-review` | Prospective and retrospective DUR assessments |

---

## Clinical Considerations

- **Medication Reconciliation** -- Review the medication list with the patient at every encounter to ensure accuracy. Ask about over-the-counter medications, supplements, and medications prescribed by other providers.
- **Allergy Cross-Reference** -- The medication list should be reviewed in conjunction with the patient's allergy list. Order checks on the Orders page automatically flag drug-allergy interactions, but this only works when allergies are properly documented.
- **Refill Monitoring** -- Check the Refills column to identify medications that are running low on refills and may need renewal.
- **Polypharmacy Review** -- For patients with many active medications, consider whether all medications are still indicated and whether there are opportunities to simplify the regimen.
