# Controlled Substance Management -- Pharmacist Human Test Script -- WPF UI

## Prerequisites

- **Login:** PHARM1 (WILLIAMS,ROBERT L -- Clinical Pharmacy) / Password: `smythVista1`
- **Patient:** 30 (for dispense scenarios)
- **Pre-conditions:**
  1. The SiloHost, WebServer, and WPF Application (NewVistas.WpfClient) must all be running.
  2. In the Navigation Panel, select **Controlled Substances**.
  3. Enter Location ID: `VAULT-1A` in the Location ID field.
  4. Click **Load** to initialize the location.

---

## Scenario 1: Record Controlled Substance Dispense with Witness

### Steps

1. In the Navigation Panel, select **Controlled Substances**.
2. Enter Location ID: `VAULT-1A` and click **Load**.
3. Click the **Record** TabItem (tab 2).
4. Click the **Record Dispense** sub-tab (sub-tab 1).
5. Fill in the CS Dispense form:
   - Patient ID: `30`
   - Patient Name: `SMITH,JOHN A`
   - Drug ID: `50-OXYCODONE`
   - Drug Name: `OXYCODONE 5MG TAB`
   - DEA Schedule: **Schedule II**
   - Qty Dispensed: `60`
   - Unit: `tablets`
   - Running Balance: `940`
   - Dispense Type: **Routine**
   - Prescriber ID: `PROV-001`
   - Prescriber Name: `DR. JANE SMITH`
   - Prescriber DEA #: `AS1234567`
   - Dispensed By ID: `PHARM1`
   - Dispensed By Name: `WILLIAMS,ROBERT L`
   - Witness Name: `KIM,JENNY H`
   - Date/Time: (current date/time)
   - Rx/Order Number: `RX-CS-001`
   - Notes: `Routine monthly fill. Patient ID verified.`
6. Click **Record Dispense**.

### Expected Result

- A success toast notification appears: "Dispense recorded successfully."
- Switch to the **Dispense Log** TabItem (tab 0). The new dispense entry appears at the top of the DataGrid:
  - Date/Time: current date/time
  - Patient: SMITH,JOHN A
  - Drug: OXYCODONE 5MG TAB
  - Schedule: CII
  - Qty: 60
  - Unit: tablets
  - Running Balance: 940
  - Dispensed By: WILLIAMS,ROBERT L

---

## Scenario 2: Create Routine Inspection, Count Drugs, Finalize as PASSED

### Steps

1. Click the **Record** TabItem (tab 2).
2. Click the **Create Inspection** sub-tab (sub-tab 0).
3. Fill in the New Vault Inspection form:
   - Inspection Type: **Scheduled**
   - Date/Time: (current date/time)
   - Inspector ID: `PHARM1`
   - Inspector Name: `WILLIAMS,ROBERT L`
   - Witness ID: `PHARM4`
   - Witness Name: `KIM,JENNY H`
   - Notes: `Monthly scheduled vault inspection.`
4. Under **Add Drug Counts**, add the following counts:
   - First count:
     - Drug Name: `OXYCODONE 5MG TAB`
     - DEA Schedule: **Schedule II**
     - System Count: `940`
     - Physical Count: `940`
     - Unit: `tablets`
     - Click **+ Add Count**
   - Second count:
     - Drug Name: `ALPRAZOLAM 0.5MG TAB`
     - DEA Schedule: **Schedule IV**
     - System Count: `500`
     - Physical Count: `500`
     - Unit: `tablets`
     - Click **+ Add Count**
   - Third count:
     - Drug Name: `MORPHINE SULFATE 15MG TAB`
     - DEA Schedule: **Schedule II**
     - System Count: `200`
     - Physical Count: `200`
     - Unit: `tablets`
     - Click **+ Add Count**
5. Verify the pending counts DataGrid shows all 3 drugs with matching System and Physical counts (no highlighted rows).
6. Click **Create Inspection**.

### Expected Result

- A success toast notification appears: "Inspection created: CS-INSPECTION:XXXXXXXX"
- Switch to the **Inspections** TabItem (tab 1). The new inspection appears in the DataGrid:
  - Date/Time: current date/time
  - Type: Scheduled
  - Inspector: WILLIAMS,ROBERT L
  - Result: **Passed** (displayed with green foreground)
  - Discrepancies: 0
- Click **View Counts** on the inspection row (or right-click and select **View Counts**). The drug counts DataGrid shows:
  - All three drugs with System = Physical and Discrepancy = 0.

---

## Scenario 3: Inspection with Discrepancy -- Finalize as FAILED

### Steps

1. Click the **Record** TabItem, then **Create Inspection** sub-tab.
2. Fill in:
   - Inspection Type: **Unscheduled**
   - Date/Time: (current date/time)
   - Inspector ID: `PHARM1`
   - Inspector Name: `WILLIAMS,ROBERT L`
   - Witness ID: `PHARM3`
   - Witness Name: `MARTINEZ,CARLOS R`
   - Notes: `Unscheduled spot check triggered by anonymous report.`
3. Add drug counts with a discrepancy:
   - Drug Name: `OXYCODONE 5MG TAB`
   - DEA Schedule: **Schedule II**
   - System Count: `940`
   - Physical Count: `935`
   - Unit: `tablets`
   - Click **+ Add Count**
4. The pending counts DataGrid shows the OXYCODONE row highlighted in yellow (discrepancy detected: 935 - 940 = -5).
5. Click **Create Inspection**.

### Expected Result

- A success toast notification appears: "Inspection created: CS-INSPECTION:XXXXXXXX"
- Switch to the **Inspections** TabItem. The new inspection appears in the DataGrid:
  - Type: Unscheduled
  - Result: **Failed** or **DiscrepancyIdentified** (displayed with red/orange foreground)
  - Discrepancies: 1
  - The row is highlighted in yellow.
- Click **View Counts**. The drug counts detail shows:
  - OXYCODONE 5MG TAB: System=940, Physical=935, Discrepancy=-5 (displayed with red foreground)

---

## Scenario 4: Surprise Inspection

### Steps

1. Click the **Record** TabItem, then **Create Inspection** sub-tab.
2. Fill in:
   - Inspection Type: **Unscheduled** (value 1 -- this serves as surprise inspection)
   - Date/Time: (current date/time)
   - Inspector ID: `PHARM1`
   - Inspector Name: `WILLIAMS,ROBERT L`
   - Witness ID: `PHARM2`
   - Witness Name: `LEE,SANDRA K`
   - Notes: `Surprise inspection per DEA compliance protocol.`
3. Add drug counts for multiple controlled substances (all matching):
   - OXYCODONE 5MG TAB: System=935, Physical=935 (Schedule II)
   - ALPRAZOLAM 0.5MG TAB: System=500, Physical=500 (Schedule IV)
   - HYDROMORPHONE 2MG TAB: System=100, Physical=100 (Schedule II)
4. Click **Create Inspection**.

### Expected Result

- Inspection created with Result: Passed.
- The Inspections TabItem shows the new entry with Discrepancies: 0.

---

## Scenario 5: View Dispense History Filtered by DEA Schedule

### Steps

1. Click the **Dispense Log** TabItem (tab 0).
2. In the Filter by DEA Schedule ComboBox, select **Schedule II**.
3. Click **Filter**.
4. The DataGrid filters to show only Schedule II dispenses.
5. Change filter to **Schedule IV**.
6. Click **Filter**.
7. Change filter to **All Schedules** (empty value).
8. Click **Filter** to restore the full list.

### Expected Result

- When filtering by Schedule II: only CII drugs appear (e.g., OXYCODONE, MORPHINE, HYDROMORPHONE).
- When filtering by Schedule IV: only CIV drugs appear (e.g., ALPRAZOLAM).
- When set to All Schedules: all dispenses show regardless of schedule.
- The ComboBox options are: All Schedules, Schedule II, Schedule III, Schedule IV, Schedule V.

---

## Scenario 6: View Dispenses by Drug and Date Range

### Steps

1. On the **Dispense Log** TabItem, observe the full dispense log.
2. The DataGrid columns are: Date/Time, Patient, Drug, Schedule, Qty, Unit, Running Balance, Dispensed By.
3. Visually scan for specific drugs (e.g., OXYCODONE) across the log entries.
4. The entries are sorted by date (most recent first).
5. Note the Running Balance column tracks the cumulative balance after each dispense.

### Expected Result

- All dispense records are listed chronologically.
- Running Balance decrements correctly after each dispense.
- If Running Balance goes negative, it appears displayed with red foreground.
- Each row shows the dispensing pharmacist name.
