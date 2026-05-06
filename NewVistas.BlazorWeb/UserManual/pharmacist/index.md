# Pharmacist Guide

## Role Description

This guide is intended for **Pharmacists** (PharmD, RPh), **Pharmacy Technicians** (CPhT), and **Pharmacy Specialists** working within the NewVistas Clinical Information System. Pharmacists and pharmacy staff are responsible for a wide range of clinical and operational activities, including:

- Reviewing and verifying medication orders (inpatient and outpatient)
- Processing outpatient prescriptions and inpatient unit dose orders
- Preparing IV admixtures and compounded sterile preparations
- Managing the drug formulary and National Drug File
- Performing drug accountability and controlled substance oversight
- Supporting BCMA (Bar Code Medication Administration) workflows
- Managing auto-refill programs and CMOP (Centralized Mail-Out Pharmacy) transmissions
- Processing EPCS (Electronic Prescribing for Controlled Substances) transactions
- Administering pharmacy benefits and point-of-sale claims

NewVistas maps to the legacy VistA pharmacy packages, including Outpatient Pharmacy V7, Inpatient Medications, IV Pharmacy, Controlled Substances, Drug Accountability, National Drug File, BCMA, Auto-Refill, CMOP, EPCS, and Pharmacy Benefits Management.

---

## Daily Workflow Overview

A typical pharmacy shift follows these seven steps. The order may vary depending on your facility's policies, but this sequence reflects standard practice.

1. **Review the Pharmacy Hub (`/pharmacy`)** -- Begin each shift by reviewing the Pharmacy Hub dashboard. Check pending verification queues, auto-refill alerts, controlled substance alerts, CMOP transmission status, and inventory alerts. The Hub provides a single view of all pharmacy operations requiring attention.

2. **Process outpatient prescriptions (`/outpatientpharmacy`)** -- Review pending outpatient prescriptions. Perform clinical screening (drug-allergy, drug-drug interactions, duplicate therapy, dose range checks, formulary status). Verify prescriptions for dispensing. Generate labels and patient information leaflets. Provide patient counseling as required.

3. **Manage inpatient medications (`/inpatientpharmacy`)** -- Review incoming inpatient medication orders. Verify orders for clinical appropriateness. Generate unit dose cart fill lists. Process stat and urgent orders immediately. Monitor ward stock levels and replenishment needs.

4. **Prepare IV admixtures (`/iv-pharmacy`)** -- If assigned to the IV room, review pending IV orders. Verify solution and additive compatibility. Compound orders using aseptic technique. Label preparations with patient information, solution contents, rate, beyond-use date, and preparer identification.

5. **Controlled substance activities (`/controlled-substances`)** -- Perform scheduled controlled substance counts. Conduct or participate in inspections (routine, random, incident-based, or change-of-shift). Investigate and document any discrepancies. Record dispensing transactions with required witness verification.

6. **Formulary and drug file maintenance (`/drugformulary`, `/drugfile`)** -- Search the National Drug File for formulary status, generic equivalents, and drug class information. Maintain facility drug file entries including orderable items, routes, dose units, and synonyms. Coordinate changes with the Pharmacy and Therapeutics (P&T) Committee.

7. **Process auto-refills and CMOP transmissions (`/auto-refill`, `/cmop`)** -- Review auto-refill prescriptions due for processing. Manage patient enrollment and suspensions. Review the CMOP suspense queue, create transmission batches, and submit electronically. Track shipping status and handle returns.

![Pharmacy daily workflow overview](screenshots/pharmacist-daily-workflow.png)

---

## Module Quick Reference

| Module | Route | Description |
|--------|-------|-------------|
| [Pharmacy Hub](pharmacy-hub.md) | `/pharmacy` | Central pharmacy operations dashboard with pending queues, alerts, and module navigation |
| [Outpatient Pharmacy](outpatient.md) | `/outpatientpharmacy` | Outpatient prescription processing, clinical screening, verification, dispensing, and counseling |
| [Inpatient Pharmacy](inpatient.md) | `/inpatientpharmacy` | Inpatient medication order review, verification, unit dose cart fill, and stat order processing |
| [IV Pharmacy](iv-pharmacy.md) | `/iv-pharmacy` | IV admixture order management, compatibility verification, compounding, and labeling |
| [Controlled Substances](controlled-substances.md) | `/controlled-substances` | DEA Schedule II-V management, dispensing logs, inspections, and count reconciliation |
| [Drug Accountability](drug-accountability.md) | `/drugaccountability` | Drug inventory, accountability transactions, and physical inventory reconciliation |
| [Auto-Refill and CMOP](auto-refill-cmop.md) | `/auto-refill`, `/cmop` | Automatic refill enrollment and management, centralized mail-out pharmacy transmissions |
| [Drug Formulary](formulary.md) | `/drugformulary` | National Drug File formulary search, product details, and drug classification |
| [Drug File](formulary.md) | `/drugfile` | Facility drug file maintenance, orderable items, routes, and dose units |
| [BCMA (Pharmacist)](bcma.md) | `/bcma` | Bar Code Medication Administration support, MAR review, and administration history |
| [EPCS](epcs.md) | `/epcs` | Electronic Prescribing for Controlled Substances, PDMP integration, and DEA compliance |
| [Pharmacy Benefits](benefits-pos.md) | `/pharmacybenefits` | Patient pharmacy benefit verification, prior authorization, and copay information |
| [Pharmacy POS](benefits-pos.md) | `/pharmacy-pos` | Point-of-sale claims processing, adjudication, and payment transactions |
| [Ward Stock](ward-stock.md) | `/ward-stock` | Ward stock medication inventory, PAR level management, and replenishment |

---

## Related Guides

- **Clinician Guide** -- Physicians and other prescribers interact with pharmacy through the ordering system (CPOE). See the Clinician Guide for order entry workflows that generate pharmacy verification tasks.
- **Nurse Guide** -- Nurses are the primary BCMA users who administer medications. See the Nurse Guide for medication administration workflows.
- **Administrator Guide** -- System administrators manage pharmacy user roles, security keys, and system parameters. See the Administrator Guide for pharmacy system configuration.

---

![Pharmacy Hub dashboard overview](screenshots/pharmacy-hub-overview.png)
