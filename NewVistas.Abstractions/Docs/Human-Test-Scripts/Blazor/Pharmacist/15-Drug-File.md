# Drug File Management -- Pharmacist Human Test Script

## Prerequisites

- **Login:** PHARM1 (WILLIAMS,ROBERT L -- Clinical Pharmacy) / Password: `smythVista1`
- **Pre-conditions:**
  1. The SiloHost, WebServer, and BlazorWeb projects must all be running.
  2. Navigate to `/drugfile` in the browser.
  3. If the page shows "Drug file data has not been loaded yet", load demo data first (Scenario 6).

---

## Scenario 1: Search Drug File Entries

### Steps

1. Navigate to `/drugfile`.
2. If data is not loaded, click **Load Demo Data** first (see Scenario 6).
3. The **Drugs** tab should be active by default.
4. In the search input, type: `METFORMIN`
5. Leave the type dropdown on "All types".
6. Click **Search**.

### Expected Result

- The drug file results table populates with matching entries:
  - METFORMIN 500MG TAB
  - METFORMIN 850MG TAB
  - METFORMIN 1000MG TAB
  - METFORMIN 500MG ER TAB (extended release)
- Each entry shows the drug name, type (Outpatient, Unit Dose, IV, etc.), and key attributes.
- Results come from the Drug Index grain (key: "DRUG-INDEX").

---

## Scenario 2: View Drug Detail (Ingredients, Routes, Dose Units)

### Steps

1. From the search results, click on a specific drug (e.g., METFORMIN 500MG TAB).
2. The detail view opens.

### Expected Result

- Drug detail shows:
  - Drug name and synonyms
  - VA Drug Class
  - Dosage Form (TAB, CAP, INJ, etc.)
  - Strength and unit
  - Route(s) of administration
  - National Drug Codes (NDCs)
  - Ingredients with IENs
  - Dispense type: Outpatient, Unit Dose, Both, IV, All

---

## Scenario 3: Browse Medication Routes

### Steps

1. Click the **Routes** tab button in the tab group.
2. The page switches to display all available medication routes.

### Expected Result

- A table or list of medication routes appears.
- The Medication Route Index grain (key: "MED-ROUTE-INDEX") self-seeds with 55 standard routes including:
  - ORAL
  - INTRAVENOUS (IV)
  - INTRAMUSCULAR (IM)
  - SUBCUTANEOUS (SC/SQ)
  - TOPICAL
  - OPHTHALMIC
  - OTIC
  - NASAL
  - INHALATION
  - RECTAL
  - SUBLINGUAL
  - TRANSDERMAL
  - VAGINAL
  - INTRADERMAL
  - INTRATHECAL
- Each route has a name and abbreviation.

---

## Scenario 4: Browse Dose Units

### Steps

1. Click the **Dose Units** tab button in the tab group.
2. The page switches to display all available dose units.

### Expected Result

- A table or list of dose units appears.
- The Dose Unit Index grain (key: "DOSE-UNIT-INDEX") self-seeds with 54 standard units including:
  - MG (milligrams)
  - GM (grams)
  - MCG (micrograms)
  - ML (milliliters)
  - MEQ (milliequivalents)
  - UNITS
  - TABLETS
  - CAPSULES
  - DROPS
  - PUFFS
  - PATCHES
  - MMOL (millimoles)
  - IU (international units)

---

## Scenario 5: Browse Orderable Items

### Steps

1. Click the **Orderable Items** tab button in the tab group.
2. The page switches to display pharmacy orderable items.

### Expected Result

- A list of orderable items appears from the Orderable Item Index grain (key: "OI-INDEX").
- Orderable items are the CPRS-facing drug names that map to specific drug file entries.
- Each item shows:
  - Orderable item name
  - Associated drug(s)
  - Dosage forms available
  - Default routes
- These are the items that appear in the CPRS order dialog when a provider places a medication order.

---

## Scenario 6: Load Demo Drug File Data

### Steps

1. Navigate to `/drugfile`.
2. Click **Load Demo Data**.
3. Wait for the load to complete.

### Expected Result

- A success banner appears:
  - "Loaded XX drugs and XX orderable items. Routes: Ready. Dose units: Ready."
- The search is now functional.
- Click the **Status** button to verify:
  - Drug count populated
  - Orderable item count populated
  - Routes: loaded (55)
  - Dose units: loaded (54)

### Type Filter

4. Use the type dropdown to filter drugs by dispense type:
   - **Outpatient** (value 1): drugs dispensed from outpatient pharmacy
   - **Unit Dose** (value 2): drugs dispensed as unit dose for inpatients
   - **Both** (value 3): drugs available in both settings
   - **IV** (value 4): IV preparations
   - **All** (value 5): all drug types

### Expected Result

- Each filter narrows the drug list to the selected type.
- "All types" shows every drug in the file.
