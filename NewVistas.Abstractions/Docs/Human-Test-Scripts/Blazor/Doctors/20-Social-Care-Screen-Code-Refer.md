# Social Care — Screen, Code, Refer (Provider/Nurse) Human Test Script

**Purpose:** Verify the Whole-Person Social Care first increment end to end — the Person-anchored
household and the coded SDOH screening that closes the loop from a positive social need to a billable
Z-code on the problem list and a referral in the existing Social Work queue. Automated coverage lives in
[`SdohScreeningCatalogTests`](../../../../../NewVistas.UnitTests/SdohScreeningCatalogTests.cs),
[`SdohScreeningWorkflowTests`](../../../../../NewVistas.FunctionalTests/SdohScreeningWorkflowTests.cs),
[`HouseholdWorkflowTests`](../../../../../NewVistas.FunctionalTests/HouseholdWorkflowTests.cs), and
[`SocialCareDemoSeedTests`](../../../../../NewVistas.FunctionalTests/SocialCareDemoSeedTests.cs).

---

## Prerequisites

- **Login (provider):** `DOCTOR1` / Password: `smythVista1` (holds `GMPL PROBLEM`, needed to place a
  Z-code on the problem list). A nurse (`NURSE1`, `ORELSE`) can screen and refer but not place the Z-code.
- **Site profile:** any with the `SOCIAL_CARE` feature enabled (on by default; needs `PERSON_IDENTITY`
  on for household resolution — also on by default).
- **Seeded data:** `SocialCareSeed` — patient **P9301** (SOCIAL,SAM) already in a household with a
  non-patient child, and a positive food + housing screen with the loop closed.

---

## Part A: The coded SDOH screen (fresh patient)

1. As `DOCTOR1`, open **Social Care → SDOH Screening**. Enter any registered patient and **Load**.
2. Answer: **Food insecurity = Yes**, **Housing instability = Yes**, **Transportation = No**, leave the
   rest **Not asked**. Click **Record screening**.
   - **Expected:** a "Positive findings" table shows **Food insecurity → Z59.41 → Food** and **Housing
     instability → Z59.811 → Housing**. Negative/Not-asked domains do not appear.
3. On the Food insecurity row, click **Add Z-code**.
   - **Expected:** success; the row shows "✓ Z-code added". Open the patient's **Problems** page —
     **Z59.41 Food insecurity** is on the list with a citation back to the screening.
4. On the Housing instability row, click **Create referral**.
   - **Expected:** success ("Referral created in the Social Work queue"). Open the patient's **Social
     Work** referrals — a **Housing** referral is present, sourced "SDOH screening".

## Part B: The "not asked" distinction

5. Re-open the screening from **Screening history**. The domains you left **Not asked** are not counted
   as negatives (they produce no finding and no false Z-code).

## Part C: Household (P9301 demo)

6. Open **Social Care → Household**, enter **P9301**, **Load**.
   - **Expected:** the "Social Household" shows **SOCIAL,SAM** (Head) and **SOCIAL,SUSIE** (Daughter, a
     non-patient member). Susie has no patient chart but is a member.
7. **Add a non-patient family member** (name + relationship) → they appear in the member list.
8. **Remove** a non-head member → they show as "Left {date}" (history kept), not deleted.

## Part D: Key gating

9. Log in as `NURSE1` and repeat Part A steps 1–2 and the **Create referral** action — both work. The
   **Add Z-code** button is replaced by a "Z-code (no key)" note, because placing a diagnosis code needs
   `GMPL PROBLEM`. A provider closes that half.

---

## Pass criteria

- A positive screen yields the correct billable Z-code + referral-type per domain; Not-asked yields nothing.
- Add Z-code lands the code on the problem list with a citation; Create referral lands in the existing
  Social Work queue.
- The household shows Person-anchored members incl. a non-patient; leaving keeps history.
- The Z-code action is gated by `GMPL PROBLEM`; referral is open.
