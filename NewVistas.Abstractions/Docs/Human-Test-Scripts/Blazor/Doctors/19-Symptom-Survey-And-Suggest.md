# Symptom Survey & Suggest (Provider/Nurse) Human Test Script

**Purpose:** Verify the front-door capture side of emerging-condition surveillance —
the structured Present / Absent / Unknown symptom survey and the "surface this patient
into a cluster" action — from a clinician's chair. The corresponding automated coverage
lives in [`SymptomCaptureTests`](../../../../../NewVistas.FunctionalTests/SymptomCaptureTests.cs)
and [`ProtoScreeningTests`](../../../../../NewVistas.FunctionalTests/ProtoScreeningTests.cs).

The point of the survey being **structured** (not free text): you can only close the net
by dropping symptoms that come back at background rate, and that only works if the survey
actually *asked*. Leaving an item **Unknown** means "not assessed" — the system will not
pretend it was absent.

---

## Prerequisites

- **Login (provider):** `DOCTOR1` / Password: `smythVista1` (holds `PROVIDER` — enough to
  record symptoms and to suggest a patient into a cluster). A nurse (`NURSE1`, `ORELSE`) works too.
- **Site profile:** any with `EMERGING_CONDITIONS` enabled (on by default).
- **Seeded data:** the active proto `outbreak-2019-resp` and the symptom-clean demo patient
  **P9214** (OUTBREAK,NINA N).

---

## Part A: Record a structured survey

1. As `DOCTOR1`, open **Surveillance → Symptom Survey**.
2. Enter patient **P9214** and click **Load**.
   - **Expected:** the survey renders, grouped by body system. The question set is the core "wide
     net" **union** the active cluster's symptom features (so fever, cough, loss of smell, hearing
     change, sore throat all appear). All items start **Unknown**.
3. Answer: **Fever = Present**, **Cough = Present**, **Loss of smell = Present**, **Hearing change =
   Absent**. Leave the rest **Unknown**.
4. Click **Save answers**.
   - **Expected:** a success message ("Recorded N symptom answers"). Only the non-Unknown answers are
     recorded (Unknown = not asked → nothing stored).

## Part B: Screen against the active clusters

5. Click **Screen against active clusters**.
   - **Expected:** a "Cluster match preview" table shows the novel respiratory cluster with a score
     and **✓ candidate** (P9214 now matches on fever + cough + anosmia).
6. Click **Suggest into cluster** for that row.
   - **Expected:** success message ("suggested for epidemiologist review"). P9214 is now a
     **human-sourced candidate** — it will persist for the epidemiologist even if a later evaluation
     stops matching.

## Part C: Confirm the "not asked" distinction

7. Re-open P9214's survey (Load again). Note that items you left **Unknown** are still **Unknown** —
   they were never recorded as Absent.
   - **Expected:** the loss-of-smell answer is retained as **Present**; unasked items remain Unknown,
     visibly distinct from the ones you answered Absent.

## Part D (hand-off): epidemiologist confirms

8. Log in as `QM3` (see Admin/17) → Emerging Conditions → the cluster → **Candidates** → find P9214 →
   **Confirm**.
9. Back as `DOCTOR1`, open **P9214's cover sheet**.
   - **Expected:** a non-suppressible **precaution banner** — "Emerging condition — Novel respiratory
     cluster: Droplet isolation recommended (confirmed cluster member)."

---

## Pass criteria

- The survey renders the core screen ∪ the active cluster's symptom features, three-state.
- Only non-Unknown answers are recorded; Unknown stays Unknown on reload.
- Screening surfaces the match; Suggest creates a persistent candidate.
- After the epidemiologist confirms, the patient's cover sheet shows the Droplet precaution banner.
