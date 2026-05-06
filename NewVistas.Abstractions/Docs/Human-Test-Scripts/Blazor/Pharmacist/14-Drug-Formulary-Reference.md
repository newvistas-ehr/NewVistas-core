# Drug Formulary and NDF Lookup -- Pharmacist Human Test Script

## Prerequisites

- **Login:** PHARM1 (WILLIAMS,ROBERT L -- Clinical Pharmacy) / Password: `smythVista1`
- **Pre-conditions:**
  1. The SiloHost, WebServer, and BlazorWeb projects must all be running.
  2. Navigate to `/drugformulary` in the browser.
  3. If the page shows a banner "Drug Formulary data has not been loaded yet", you must load demo data first (Scenario 5).

---

## Scenario 1: Search VA Product by Name

### Steps

1. Navigate to `/drugformulary`.
2. If data is not loaded, click **Load Demo Data** first (see Scenario 5).
3. In the search input, type: `LISINOPRIL`
4. Leave "Formulary only" unchecked and "All drug classes" selected.
5. Click **Search**.

### Expected Result

- The search results table populates with VA Product entries matching "LISINOPRIL".
- Results include entries such as:
  - LISINOPRIL 5MG TAB
  - LISINOPRIL 10MG TAB
  - LISINOPRIL 20MG TAB
  - LISINOPRIL 40MG TAB
- Each row shows product details including generic name, formulary status, and drug class.
- Results are returned from the VA Product Index grain.

---

## Scenario 2: View Product Details (Ingredients, NDCs, Formulary Status)

### Steps

1. From the search results in Scenario 1, click on a specific product (e.g., LISINOPRIL 10MG TAB).
2. The detail panel or view opens showing comprehensive product information.

### Expected Result

- Product detail shows:
  - VA Product Name
  - Generic Name
  - Drug Class code and name (e.g., CV800 -- ACE INHIBITORS)
  - Formulary status (Formulary / Non-Formulary)
  - Route of administration
  - Dosage Form
  - Strength
  - Unit of measure
  - National Drug Codes (NDCs) if available
  - Ingredients list
  - VA Product IEN (VistA identifier)

---

## Scenario 3: Browse Drug Classes

### Steps

1. On the formulary page, locate the drug class dropdown (labeled "All drug classes").
2. Click the dropdown to see the list of available drug classes.
3. Browse the available classes, which include codes like:
   - AM100 -- AMINOGLYCOSIDES
   - CV800 -- ACE INHIBITORS
   - CN302 -- BENZODIAZEPINES
   - HS502 -- ANTIDIABETIC AGENTS, BIGUANIDE
   - CV350 -- HMG-COA REDUCTASE INHIBITORS

### Expected Result

- The dropdown lists all drug classes loaded from the NDF.
- Each option shows the class code followed by the class name.
- Selecting a class filters the search results to that class only.

---

## Scenario 4: Search by Drug Class Code

### Steps

1. In the drug class dropdown, select a specific class: e.g., `CV800 -- ACE INHIBITORS`.
2. Clear the search text (or leave it blank).
3. Click **Search**.

### Expected Result

- The results show only products classified under CV800 (ACE Inhibitors).
- Products include lisinopril, enalapril, ramipril, captopril, etc. (depending on demo data loaded).
- Each result shows the matching drug class code.
- Combining a text search with a class filter narrows results further (e.g., typing "LISINOPRIL" with CV800 selected).

---

## Scenario 5: Load Demo Formulary Data

### Steps

1. Navigate to `/drugformulary`.
2. If the load banner is visible ("Drug Formulary data has not been loaded yet"), click **Load Demo Drug Data**.
3. Wait for the load to complete (this may take a few seconds).

### Expected Result

- A success banner appears showing the count of loaded data:
  - "Loaded XX products, XX generics, XX drug classes"
- The status bar at the top shows:
  - Products: checkmark with count (e.g., "50")
  - Formulary: count of formulary products
  - Generics: count
  - Classes: count
  - Last loaded: current date/time
- The load banner disappears.
- The search functionality is now available.

### Status Check

4. Click the **Status** button in the toolbar.

### Expected Result

- The status display shows:
  - Products: "Loaded" with total count and formulary count
  - Generics: count or "Not loaded"
  - Classes: count or "Not loaded"
  - Last loaded date/time
- If the "Formulary only" checkbox is checked during search, only formulary-listed products appear in results.
