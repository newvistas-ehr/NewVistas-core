# Drug Interaction Dataset Management -- Pharmacist Human Test Script -- WPF UI

## Prerequisites

- **Login:** PHARM1 (WILLIAMS,ROBERT L -- Clinical Pharmacy) / Password: `smythVista1`
- **Pre-conditions:**
  1. The SiloHost, WebServer, and WPF Application (NewVistas.WpfClient) must all be running.
  2. In the Navigation Panel, select **Drug Interactions**.

---

## Scenario 1: Check Dataset Status

### Steps

1. In the Navigation Panel, select **Drug Interactions**.
2. The **Dataset Status** TabItem should be active by default.
3. Click **Refresh Status**.

### Expected Result

- The Dataset Status panel displays four status cards:
  - **Dataset:** "LOADED" (green) or "EMPTY" (gray)
  - **Cache:** "READY" (green) or "NOT READY" (gray)
  - **Total Pairs:** number of interaction pairs loaded (e.g., 0 if empty, or the demo count)
  - **Last Loaded:** date/time of last load, or "Never"
- If the dataset is empty, all subsequent interaction checks will return no results.

---

## Scenario 2: Load Demo Interaction Pairs

### Steps

1. On the **Dataset Status** TabItem, click **Load Demo Dataset**.
2. A busy indicator appears while loading. Wait for the load to complete (this loads sample drug-drug interaction pairs from VistA Files #56-1 through #56-6).

### Expected Result

- A success toast notification appears indicating the demo dataset was loaded.
- Click **Refresh Status** to verify:
  - Dataset: **LOADED** (green card)
  - Total Pairs: a positive number (e.g., 10-50 demo pairs)
  - Last Loaded: current date/time
- The cache may need a moment to populate. If Cache shows "NOT READY", wait a few seconds and refresh again.

---

## Scenario 3: Check Cache Readiness

### Steps

1. After loading the dataset, verify the cache status.
2. Click **Refresh Status**.
3. Observe the Cache status card.

### Expected Result

- Cache: **READY** (green card) -- the DrugInteractionCacheService silo singleton has loaded the dataset into volatile memory for fast lookup.
- If the cache is not ready:
  - The IDrugInteractionCheckerGrain (StatelessWorker) will not find interactions.
  - Wait 5-10 seconds and refresh. The cache populates asynchronously via volatile-swap.
- The cache is used by the Interaction Blocking workflow (Script 03) for real-time drug interaction checking.

---

## Scenario 4: Look Up Specific Interaction Pair

### Steps

1. Click the **Browse Interactions** TabItem.
2. In the lookup fields:
   - Ingredient IEN 1: `1190` (WARFARIN)
   - Ingredient IEN 2: `3345` (ASPIRIN)
3. Click **Lookup Pair**.

### Expected Result

- If this pair exists in the dataset, the Interaction Detail panel shows:
  - Ingredient 1: WARFARIN (name resolved from IEN)
  - Ingredient 2: ASPIRIN (name resolved from IEN)
  - Severity: status indicator showing one of: Minor, Moderate, Significant, Contraindicated
  - Description: clinical description of the interaction (e.g., "Increased risk of bleeding")
  - Clinical Effects: detailed clinical effects text
  - Management recommendation
- The pair key is canonically sorted: DrugInteractionKeyHelper.MakePairKey ensures "1190:3345" regardless of input order.
- If the pair does not exist in the dataset, a message indicates no interaction found.

### Load All Pairs

4. Click **Load All Pairs** to see the full dataset.

### Expected Result

- A DataGrid of all interaction pairs loaded from the dataset.
- Each row shows: Ingredient 1, Ingredient 2, Severity, Description.
- The list may be large depending on demo data size.

---

## Scenario 5: Check a List of Ingredients for Interactions

### Steps

1. Click the **Check Interactions** TabItem.
2. This TabItem provides a way to check multiple ingredients against each other for interactions.
3. Enter ingredient IENs to check. The exact UI depends on the implementation, but typically:
   - Enter IEN list: `1190,3345,6809,1898` (WARFARIN, ASPIRIN, METFORMIN, LISINOPRIL)
   - Click **Check** or **Run Check**.

### Expected Result

- The system checks all pairwise combinations:
  - 1190 vs 3345 (WARFARIN vs ASPIRIN) -- may find interaction
  - 1190 vs 6809 (WARFARIN vs METFORMIN) -- may find interaction
  - 1190 vs 1898 (WARFARIN vs LISINOPRIL) -- may find interaction
  - 3345 vs 6809 (ASPIRIN vs METFORMIN) -- likely no interaction
  - 3345 vs 1898 (ASPIRIN vs LISINOPRIL) -- likely no interaction
  - 6809 vs 1898 (METFORMIN vs LISINOPRIL) -- likely no interaction
- Results show which pairs have interactions and their severity.
- Pairs with no interaction in the dataset show as "No interaction found."
- This is the same mechanism used by the ScreenPrescriptionForInteractionsAsync workflow in Script 03, but allows bulk checking without being tied to a specific prescription.

### Clear Dataset

4. To clean up, click **Clear Dataset** on the Dataset Status TabItem.
5. Click **Refresh Status**.

### Expected Result

- Dataset: **EMPTY**
- Total Pairs: 0
- Cache: **NOT READY** (cache cleared)
- All subsequent interaction lookups will return no results until the dataset is reloaded.
