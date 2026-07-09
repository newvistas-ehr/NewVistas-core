# Emerging Conditions — Full Arc (Epidemiologist) Human Test Script

**Purpose:** Walk the whole early-COVID story on the seeded "novel respiratory
cluster" — from a queue of candidates, through confirming the member that trips
the outbreak alert, closing the net with the signal/noise analytics, refining the
definition, sweeping, and finally promoting the cluster to a real code (U07.1) and
recoding the members' problem lists. The corresponding automated coverage lives in
[`ProtoConditionCoreTests`](../../../../../NewVistas.FunctionalTests/ProtoConditionCoreTests.cs),
[`ProtoConditionAnalyticsTests`](../../../../../NewVistas.UnitTests/ProtoConditionAnalyticsTests.cs),
and [`EmergingConditionDemoSeedTests`](../../../../../NewVistas.FunctionalTests/EmergingConditionDemoSeedTests.cs);
this script is the epidemiologist-SME counterpart.

---

## Prerequisites

- **Login (epidemiologist / admin):** `QM3` / Password: `smythVista1` — CAMPBELL,DIANE S,
  Infection Preventionist. Must hold the `EPI MANAGER` security key (granted via the
  Administrator role).
- **Site profile:** any profile with the `EMERGING_CONDITIONS` feature enabled (on by default).
- **Seeded demo data:** `EmergingConditionSeed` runs at startup — proto
  `outbreak-2019-resp` Active with **9 confirmed + 4 candidate** members, the alert armed at
  **10 confirmed → QM3**, patients P9201-P9214 + 30 controls.

---

## Part A: The candidate queue and the outbreak alert

1. As `QM3`, open **Surveillance → Emerging Conditions** from the nav.
2. Select **"Novel respiratory cluster (2019)"**. Confirm the badges show **Active**, ~**9 ✓**
   (confirmed) and **4 ?** (candidates).
3. Open the **Candidates** tab. You should see **4** candidates (P9210-P9213), each with a
   per-feature evidence row (✓ satisfied / ✗ assessed-not-satisfied / — not assessed).
4. Click **Confirm** on **P9210** (the 10th member).
   - **Expected:** success message; the confirmed count becomes **10**; the count-threshold
     alert **fires** (a notification is created for `QM3`). Confirming further members within the
     24-hour cooldown does **not** re-fire.

## Part B: Close the net (signal vs noise)

5. Open the **Analytics** tab and click **Compute signal analysis**.
   - **Expected:** **Loss of smell (anosmia)** shows a **Signal** verdict (cluster rate well above
     background). **Hearing change** shows a **Noise** verdict (cluster rate ≈ background). Each row
     shows the assessed denominator (present/assessed) and the background source (hover the ⓘ).
6. Under **Refinement suggestions**, find the **DropFeature** suggestion for hearing change and click
   **Apply**.
   - **Expected:** the hearing-change feature is removed and the definition version increments.

## Part C: Sweep

7. Return to the **Overview** tab and click **Run sweep**.
   - **Expected:** a sweep runs across the active patient population and re-evaluates members against
     the refined (v-bumped) definition; the message reports completion. (Members evaluated against the
     old version show a small "⟳" stale badge in the list until re-swept.)

## Part D: Promote to a code

8. Open the **Promotion** tab. Enter: Official name **COVID-19**, ICD-10 **U07.1**, SNOMED
   **840539006**, jurisdictions **US, MA**. Click **Promote**.
   - **Expected:** the proto becomes **Promoted** (definition frozen); remaining candidates expire to
     Excluded; an **eCR trigger** id is shown; a migration worklist appears with every confirmed member
     marked **Pending**.
9. In the migration table, click **Recode** on one member.
   - **Expected:** the member's problem list gains **U07.1 (COVID-19)** with a citation comment
     ("Recoded from emerging cluster…"), the migration row flips to **Migrated**, and the member's
     primary provider is notified.
10. Confirm the proto is now **read-only** — no feature edits, confirms, or promotes are offered.

---

## Pass criteria

- Confirming the 10th member fires the alert exactly once.
- Anosmia is a Signal, hearing change is Noise, with assessed denominators shown.
- Promotion freezes the definition, emits an eCR trigger, and produces a migration worklist.
- Recoding adds U07.1 with a source citation and marks the member Migrated.
