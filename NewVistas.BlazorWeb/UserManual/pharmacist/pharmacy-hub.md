# Pharmacy Hub

**Route:** `/pharmacy`

The Pharmacy Hub is the central pharmacy operations dashboard in NewVistas. It provides a consolidated view of all pharmacy activities requiring attention, including pending verification queues, dispensing queues, alerts, and navigation to all pharmacy modules.

> **Tip:** Start each shift on the Pharmacy Hub. The Pending Verification Queue should be your first priority -- unverified orders delay patient care.

---

## Dashboard Sections

The Pharmacy Hub is organized into several sections, each providing real-time status information for a specific area of pharmacy operations.

### Pending Verification Queue

The Pending Verification Queue displays all medication orders awaiting pharmacist review and verification. Orders are listed in priority order, with stat and urgent orders appearing at the top.

- **Columns:** Patient Name, Order Type (Outpatient/Inpatient/IV), Drug, Dose, Route, Frequency, Priority, Provider, Order Date/Time
- **Color coding:** Stat orders display with a red priority badge, urgent orders with an orange badge, and routine orders with a gray badge
- **Actions:** Click any order to open the appropriate verification workflow (outpatient, inpatient, or IV)
- **Count badge:** The total number of pending orders is displayed as a count badge on the section header

> **Warning:** Stat orders have a red priority badge and must be processed immediately. Delays in verification directly affect patient safety and medication administration timing.

![Pending Verification Queue](screenshots/pharmacy-hub-pending-queue.png)

### Dispensing Queue

The Dispensing Queue lists all verified orders that are ready for dispensing. These orders have completed pharmacist verification and are waiting for physical preparation and labeling.

- **Columns:** Patient Name, Drug, Quantity, Refills, Fill Type (New/Refill/Partial), Priority, Verified By, Verified Date/Time
- **Actions:** Click to open the dispensing workflow, print labels, or generate patient information leaflets

### Auto-Refill Alerts

The Auto-Refill Alerts section displays prescriptions enrolled in the auto-refill program that are due or overdue for processing.

- **Due prescriptions:** Prescriptions approaching their refill due date, based on days supply and last fill date
- **Overdue prescriptions:** Prescriptions that have passed their expected refill date without processing
- **Actions:** Click to navigate to the Auto-Refill Dashboard (`/auto-refill`) for processing

### Controlled Substance Alerts

This section surfaces controlled substance compliance items requiring attention.

- **Upcoming inspections:** Scheduled inspection dates and assigned inspectors
- **Count discrepancies:** Unresolved discrepancies from recent counts or inspections
- **Expiring credentials:** EPCS provider credentials nearing expiration
- **Actions:** Click to navigate to Controlled Substances (`/controlled-substances`) or EPCS (`/epcs`)

> **Warning:** Unresolved controlled substance count discrepancies are a compliance issue. Investigate immediately and report to the Pharmacy Supervisor and facility security as required by policy.

### CMOP Status

The CMOP Status section shows the current state of Centralized Mail-Out Pharmacy operations.

- **Suspense queue count:** Number of prescriptions queued for CMOP transmission
- **Active transmissions:** Current transmissions with status (Transmitted, Acknowledged, Dispensed, Shipped)
- **Errors:** Any failed transmissions or rejected items requiring attention
- **Actions:** Click to navigate to the CMOP module (`/cmop`)

### Inventory Alerts

The Inventory Alerts section highlights medication inventory issues across pharmacy locations.

- **Low stock items:** Medications at or below their reorder point
- **Critical stock items:** Medications at critically low levels requiring immediate attention
- **Expiring medications:** Items approaching or past their expiration date
- **Actions:** Click to navigate to Drug Accountability (`/drugaccountability`) or Ward Stock (`/ward-stock`)

---

## Module Cards

Below the dashboard alert sections, the Pharmacy Hub displays module navigation cards. Each card provides a brief description and a link to the corresponding pharmacy module.

| Card | Module | Route | Description |
|------|--------|-------|-------------|
| Outpatient Pharmacy | Outpatient | `/outpatientpharmacy` | Outpatient prescription processing and dispensing |
| Inpatient Pharmacy | Inpatient | `/inpatientpharmacy` | Inpatient medication orders and unit dose dispensing |
| IV Pharmacy | IV | `/iv-pharmacy` | IV admixture compounding and management |
| BCMA | Bar Code Medication Administration | `/bcma` | Medication administration support and history |
| Drug Accountability | Accountability | `/drugaccountability` | Inventory, transactions, and physical inventory |
| Controlled Substances | Controlled Substances | `/controlled-substances` | DEA schedule management, inspections, and counts |
| Pharmacy Benefits | Benefits | `/pharmacybenefits` | Patient pharmacy benefits and prior authorization |
| Pharmacy POS | Point of Sale | `/pharmacy-pos` | Claims processing and payment transactions |
| National Drug File | Formulary | `/drugformulary` | National Drug File formulary search |
| Drug File | Drug File | `/drugfile` | Facility drug file maintenance |
| Auto-Refill | Auto-Refill | `/auto-refill` | Automatic refill enrollment and processing |
| CMOP | Centralized Mail-Out | `/cmop` | Mail-out pharmacy transmissions and tracking |
| EPCS | Electronic Prescribing | `/epcs` | Electronic prescribing for controlled substances |
| Ward Stock | Ward Stock | `/ward-stock` | Ward stock inventory and replenishment |
| Data Management | Data Management | -- | Pharmacy data management and reporting |

![Pharmacy Hub module cards](screenshots/pharmacy-hub-module-cards.png)

---

## Demo Data Loader

In development environments, the Pharmacy Hub includes a **Load Demo Data** button that populates the system with sample pharmacy data for testing and training purposes. This button is only available when the system is running in development mode.

Demo data includes:

- Sample patients with active prescriptions
- Pending outpatient and inpatient orders for verification practice
- IV admixture orders in various workflow states
- Controlled substance dispense log entries and inspection records
- Auto-refill enrollments and CMOP suspense queue items
- Drug formulary and drug file entries

> **Note:** Demo data is intended for development and training environments only. It is not available in production configurations.

---

## Refreshing the Dashboard

The Pharmacy Hub refreshes its data when you navigate to the page. To manually refresh the dashboard data without a full page reload, use the refresh controls provided on each section. Individual sections can be refreshed independently to update their data without affecting other sections.

> **Tip:** If the dashboard appears stale or shows unexpected data, a full page refresh (F5 or Ctrl+R) will reload all sections simultaneously.

![Pharmacy Hub dashboard with all sections](screenshots/pharmacy-hub-dashboard-full.png)
