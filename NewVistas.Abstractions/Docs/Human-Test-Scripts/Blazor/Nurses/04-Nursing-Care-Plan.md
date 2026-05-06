# Nursing Care Plan (NANDA) -- Human Test Script

## Prerequisites

- **Login:** NURSE1 / Password: `smythVista1`
- **Patient:** 16
- **Pre-conditions:**
  1. The SiloHost, WebServer, and BlazorWeb projects must all be running.
  2. Navigate to `/nursing-careplan` in the browser.
  3. Enter Patient ID `16` in the Patient ID field and click **Load**.
  4. The Care Plan tab should load. If no diagnoses exist, it shows "No nursing diagnoses on care plan. Use the Add Diagnosis tab to start."

---

## Scenario 1: Add a Nursing Diagnosis, Goals, and Interventions (Happy Path)

### Steps

1. Navigate to `/nursing-careplan`.
2. Enter Patient ID: `16`
3. Click **Load**.
4. Click the **Add Diagnosis** tab.
5. Fill in:
   - Nursing Diagnosis: `Acute Pain`
   - Related To: `Surgical incision, tissue trauma`
   - As Evidenced By: `Verbal report of pain 7/10, guarding behavior, facial grimacing, elevated HR 98 bpm`
6. Click **Add to Care Plan**.
7. Wait for the green success banner (e.g., "Diagnosis added: NURS-PROB-...").
8. Click the **Care Plan** tab. The new diagnosis card should appear.
9. In the **Acute Pain** card, locate the Goals section.
10. In the "New goal..." input field, type: `Patient will report pain at or below 3/10 within 24 hours of intervention`
11. Click **Add** next to the goal input.
12. Add a second goal: `Patient will demonstrate use of non-pharmacologic pain management techniques by discharge`
13. Click **Add**.
14. In the **Interventions** section, enter:
    - Intervention: `Assess pain using DVPRS scale and document location, quality, and intensity`
    - Freq: `Q4H`
15. Click **Add** next to the intervention input.
16. Add a second intervention:
    - Intervention: `Administer prescribed analgesics per MD order and evaluate effectiveness within 30 minutes`
    - Freq: `PRN`
17. Click **Add**.

### Expected Result

- The Care Plan tab shows the **Acute Pain** diagnosis card with:
  - Header: **Acute Pain** R/T Surgical incision, tissue trauma
  - Status badge: **Active** (green)
  - AEB: Verbal report of pain 7/10, guarding behavior, facial grimacing, elevated HR 98 bpm
  - **Goals** section lists 2 goals:
    1. "Patient will report pain at or below 3/10 within 24 hours of intervention" -- Status: Pending
    2. "Patient will demonstrate use of non-pharmacologic pain management techniques by discharge" -- Status: Pending
  - **Interventions** section lists 2 interventions:
    1. "Assess pain using DVPRS scale and document location, quality, and intensity (Q4H)"
    2. "Administer prescribed analgesics per MD order and evaluate effectiveness within 30 minutes (PRN)"
  - A **Resolve Diagnosis** button is visible at the bottom of the card.

---

## Scenario 2: Add a Second Nursing Diagnosis -- Risk for Falls

### Steps

1. Click the **Add Diagnosis** tab.
2. Fill in:
   - Nursing Diagnosis: `Risk for Falls`
   - Related To: `Post-operative status, opioid use, Morse score 55`
   - As Evidenced By: `History of fall 3 months ago, use of assistive device, IV access`
3. Click **Add to Care Plan**.
4. Click the **Care Plan** tab.
5. In the **Risk for Falls** card, add a goal:
   - `Patient will remain free from falls during hospitalization`
6. Click **Add**.
7. Add interventions:
   - Intervention: `Maintain bed in lowest position with side rails up x2`
   - Freq: `CONTINUOUS`
   - Click **Add**.
   - Intervention: `Ensure call light within reach and patient instructed on use`
   - Freq: `Q shift`
   - Click **Add**.
   - Intervention: `Assist patient with ambulation using rolling walker`
   - Freq: `PRN`
   - Click **Add**.

### Expected Result

- The Care Plan tab now shows 2 diagnosis cards:
  1. **Acute Pain** (from Scenario 1)
  2. **Risk for Falls** with:
     - Status: Active
     - 1 goal (Pending)
     - 3 interventions listed
- Both cards have Active status badges.

---

## Scenario 3: Add a Third Diagnosis -- Impaired Gas Exchange

### Steps

1. Click the **Add Diagnosis** tab.
2. Fill in:
   - Nursing Diagnosis: `Impaired Gas Exchange`
   - Related To: `Alveolar-capillary membrane changes, ventilation-perfusion imbalance`
   - As Evidenced By: `SpO2 92% on room air, dyspnea on exertion, abnormal ABGs`
3. Click **Add to Care Plan**.
4. Return to the **Care Plan** tab and add:
   - Goal: `SpO2 will be maintained at or above 95% on supplemental O2 within 4 hours`
   - Intervention: `Monitor SpO2 continuously and titrate O2 per protocol` / Freq: `CONTINUOUS`
   - Intervention: `Elevate HOB to 30-45 degrees` / Freq: `CONTINUOUS`

### Expected Result

- The Care Plan now shows 3 active nursing diagnosis cards.
- Each card has its own goals and interventions.

---

## Scenario 4: Resolve a Nursing Diagnosis

### Steps

1. On the **Care Plan** tab, locate the **Acute Pain** card.
2. Click the **Resolve Diagnosis** button at the bottom of the card.

### Expected Result

- The **Acute Pain** card status badge changes from **Active** (green) to **Resolved** (gray/secondary).
- The card may appear with a different visual style (resolved styling).
- The other two diagnoses (Risk for Falls, Impaired Gas Exchange) remain Active.
- The Resolve Diagnosis button is no longer visible on the resolved card.

---

## Scenario 5: Verify Empty Care Plan Display

### Steps

1. Navigate to `/nursing-careplan`.
2. Enter a patient ID with no existing care plan, e.g., `50`
3. Click **Load**.
4. View the **Care Plan** tab.

### Expected Result

- The message "No nursing diagnoses on care plan. Use the Add Diagnosis tab to start." is displayed.
- No diagnosis cards are shown.

---

## Scenario 6: Outcome Evaluations

### Steps

1. Use the API to record an outcome evaluation for the **Risk for Falls** diagnosis (the Blazor page displays outcomes but adding them requires the API or the `/nursing` page):
   - `POST /api/nursing/16/careplan/diagnoses/{problemId}/outcomes`
   - Body:
     ```json
     {
       "rating": "GoalMet",
       "evaluatedById": "NURSE1",
       "evaluatedByName": "JOHNSON,MARY R",
       "notes": "Patient has remained fall-free for 72 hours. Continues to use call light and walker for ambulation."
     }
     ```
   - (The `rating` field accepts: `GoalMet`, `GoalPartiallyMet`, `GoalNotMet`, `Improved`, `Declined`, `Unchanged`)
2. Reload the care plan on the Blazor page (click **Load** again).
3. Check the **Risk for Falls** card for the Outcome Evaluations section.

### Expected Result

- The Risk for Falls card shows an "Outcome Evaluations" section at the bottom.
- The evaluation table shows:
  - Date: current date/time
  - By: JOHNSON,MARY R
  - Rating: GoalMet (green badge)
  - Notes: "Patient has remained fall-free for 72 hours. Continues to use call light and walker for ambulation."

---

## Reference: Common NANDA Nursing Diagnoses

| NANDA Diagnosis               | Related To (Example)                         |
|-------------------------------|---------------------------------------------|
| Acute Pain                    | Surgical incision, tissue trauma            |
| Risk for Falls                | Post-op status, opioid use, Morse > 50      |
| Impaired Gas Exchange         | V/Q mismatch, alveolar membrane changes     |
| Risk for Infection            | Invasive lines, surgical wound              |
| Impaired Skin Integrity       | Braden < 14, immobility, incontinence       |
| Anxiety                       | Change in health status, unfamiliar setting  |
| Deficient Knowledge           | New diagnosis, medication regimen            |
| Impaired Physical Mobility    | Pain, surgical restrictions                  |

### Goal Status Values
- **Pending** -- goal not yet evaluated
- **Achieved** -- goal fully met
- **PartiallyAchieved** -- some progress toward goal
- **NotAchieved** -- goal not met

### Outcome Rating Values
- **GoalMet** -- desired outcome achieved
- **GoalPartiallyMet** -- partial progress
- **GoalNotMet** -- no progress toward goal
- **Improved** -- patient condition improved
- **Declined** -- patient condition declined
- **Unchanged** -- no change in condition
