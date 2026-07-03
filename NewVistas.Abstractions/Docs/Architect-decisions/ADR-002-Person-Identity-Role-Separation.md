# ADR-002 — Person Identity & Role Separation

**Status:** Accepted (Phases 0–3 approved for implementation 2026-07-02)
**Relates to:** [ADR-001 — Patient Identity Strategy](ADR-001-Patient-Identity-Strategy.md),
[genetics-and-family-modeling.md](../Domain/genetics-and-family-modeling.md) (the "triplication problem" + "identity vs role" open questions this ADR resolves)

---

## The distinction (read this first)

NewVistas already has a **patient‑identity** layer: the **ICN** (`IcnIssuerGrain`) + **MPI**
(`MpiCorrelationGrain`, File #985) give every *patient record* a national id correlated across
facilities, and `PatientMergeGrain` dedups duplicate *patient records*. That layer answers:

> **ICN: "is this the same _patient_ across facilities?"**

It does **not** know that a patient is also a nurse on staff, or a relative described on someone
else's chart. Those are three disconnected representations of one human (the *triplication problem*).
This ADR adds a layer that answers a **different** question:

> **Person: "is this patient also that _provider_, or that _relative_?"**

A `PersonGrain` is the identity anchor for a **human**. The patient‑role (a chart, keyed by ICN),
the staff‑role (a `NewPersonGrain`/File #200 record, keyed `USER:{userId}`), and relative‑appearances
(on others' charts) all *point at* one Person. **Person sits above the ICN/MPI; it does not replace or
modify it.**

Concrete cases this enables:
- A **nurse who gets care** at the hospital she works at → one Person, a staff‑role + a patient‑role.
- A **mother who is a patient** and appears as a relative / emergency contact / family‑history entry on
  her child's chart → one Person, a patient‑role + a relative‑appearance.
- A **relative who later becomes a patient** → the family‑history entry can be linked to the new Person.

## Decisions

1. **Person is an ANCHOR, not a mandatory indirection.** The hot clinical path still keys by ICN
   (patient) and `USER:{userId}` (staff), exactly as today. The Person is consulted **only** for
   cross‑role operations (does this nurse have a chart? is this relative a patient? cascade testing).
   Consequence: **nothing is rekeyed.** Unlike ADR‑001 (which rekeys every patient grain to ICN), the
   Person layer is a purely **additive overlay** — nullable back‑pointers + a new anchor grain.

2. **Links are deliberate, never automatic.** A human (registrar/clerk/clinician) *confirms* a link,
   with a confidence level — mirroring `PatientMergeGrain` and `LinkedExternalIdentity`. The system may
   *suggest* candidates (demographic match), but never auto‑merges humans. Auto‑merge is how you open
   the wrong chart.

3. **Privacy is the hard part (Phase 4).** Linking a nurse's staff record to her patient chart must not
   let coworkers open her chart from the staff directory, and the *existence* of the link must not leak.
   The cross‑role view is a privileged, **audited, break‑the‑glass** operation. A Person with both a
   patient‑role and a staff‑role is flagged **employee‑patient** (sensitive). *(Phases 0–3 build the
   plumbing and the flag; the enforcement lands in Phase 4.)*

4. **Graceful when absent.** `PersonId` is nullable everywhere. Null = behaves exactly like today.
   Adoption is incremental: new registrations create/link a Person; existing records back‑fill on
   demand. (Same dependency‑inverted seam used for the specialty cover sheet's viewer role.)

## Design

- **`PersonGrain`** (`PERSON:{guid}`) — identity spine (name, DOB, sex, SSN last‑4, aliases) + three
  role‑reference lists: `PatientRoles` (patientId/ICN + facility), `StaffRoles` (File #200 userId),
  `RelativeAppearances` (onPatientId + relationship + source). `IsEmployeePatient` = has both a
  patient‑role and a staff‑role. The Person **orchestrates** linking (calls the patient/staff grains to
  set their back‑pointers) so the anchor and the pointers never drift.
- **Nullable back‑pointers:** `PatientState.PersonId`, `NewPersonState.PersonId`, and an optional
  `LinkedPersonId` on `FamilyMemberHistoryEntry` (set only when a relative is confirmed to be a Person).
- **`PersonIndexGrain`** (`PERSON-INDEX:DEFAULT`) — a searchable directory of Persons (name/DOB), for
  candidate suggestion. Populated on Person registration. (Fuzzy match against *existing unlinked*
  patients/staff reuses `MpiSearchGrain` + the provider directory — Phase 6.)

## Phasing (0–3 approved; 4–6 revisited after review)

| Phase | Scope | Status |
|---|---|---|
| **0** | This ADR. | ✅ |
| **1** | `PersonGrain` + `PersonState` + `PersonIndexGrain` + stores. Standalone. | approved |
| **2** | Nullable back‑pointers + `SetPersonIdAsync` on patient/staff + link/unlink workflow + family‑member link. | approved |
| **3** | Bootstrap (create‑Person‑from‑record) + demo (nurse‑patient, mother‑patient‑relative) + tests. | approved |
| **4** | Cross‑role "Person view" + employee‑patient privacy guard (break‑the‑glass, audited). | ✅ (2026‑07‑03) |
| 5 | Cascade / genetics integration (confirmed relative→Person feeds hereditary‑risk). | after review |
| 6 | UI (Person panel + link UI mirroring External‑Identities/merge) + candidate suggestion + docs. | after review |

## Phase 4 — implemented (2026‑07‑03)

The privacy guard, built on the existing `PatientAccessControlGrain` (PAC), enforces the rules above:

- **Employee‑patient auto‑flag.** When a Person gains both a patient‑role and a staff‑role, the
  `PersonGrain` auto‑calls `PAC.SetEmployeePatientAsync(true)` on each linked chart (adds an "EMPLOYEE"
  sensitivity category); losing either role clears it (only if no other reason remains).
- **`PAC.DecideAccessAsync(viewer, btgAttested, justification)`** — one place that decides *and* audits:
  - patient chose **open sharing** → `AllowedByOpenConsent` (the patient's own record, their call);
  - not sensitive → `Allowed`;
  - sensitive **+ treatment relationship** (viewer in the authorized list) → `AllowedByRelationship`
    — **the team is NEVER gated, no BTG** (James's rule: *"if you want BTG for your team, you should be
    at a different hospital — you could have your privacy and be dead"*);
  - sensitive, no relationship, **attested** → `AllowedByBreakTheGlass`;
  - sensitive, no relationship, not attested → `RequiresBreakTheGlass` — a **soft** signal (attest to
    proceed); **BTG never hard‑blocks**. Every outcome (including a pending‑BTG attempt) is written to
    the audit log.
- **Maximal openness is first‑class.** `PatientSharePreference.OpenForTeachingAndResearch` makes access
  frictionless (still audited) regardless of sensitivity — the mirror of the restrictive end, and the
  "next Jim Smyth" stance encoded as a deliberate patient choice.
- **Non‑leaking cross‑role read.** `GetPatientPersonForViewerAsync(...)` runs the decision and returns
  the Person detail **only when granted**; otherwise `Person` is null — so an unauthorized viewer can't
  even learn that the patient is also on staff. (The raw `GetPatientPersonAsync` is documented
  system‑only.)
- **Tests:** 10 functional (`PersonAccessControlTests`) — the full decision matrix, auto‑flag on/off,
  the gated view hiding cross‑role status until BTG, the openness override, and the audit trail.
- **Deliberately NOT built (per James):** any "require‑BTG‑even‑for‑my‑team" option. The team is
  sacrosanct for access; strong privacy is don't‑leak‑status + audit‑and‑notify + confidential/alias
  registration or a different facility — never friction on your own caregivers.

**Follow‑on (still Phase‑4‑adjacent, not built):** auto‑*establishing* the treatment relationship from
encounters/orders/OR‑case/unit‑admission (today the authorized list is populated manually / by care
team) — so the surgical cast and floor coverage are authorized without a hand‑curated list; plus
anomaly detection and a patient‑facing access report.

## Non‑goals
- Not auto‑merging humans. Not replacing/modifying the ICN/MPI rekey machinery (Person sits above it).
- Not a GA4GH pedigree (family history stays flat; Person just enables optional relative→patient links).
- Not rekeying any grains (Person is additive — the whole point).

## Risks
| Risk | Mitigation |
|---|---|
| **Person↔ICN conceptual confusion** (the #1 risk) | This ADR + naming discipline ("Person" = human, "ICN" = patient record). |
| **Privacy leakage** (employee‑patient, VIP) | Phase 4: cross‑role view gated + audited; link existence non‑leaking. `IsEmployeePatient` flag set in Phase 1–3. |
| **Wrong link** (two different humans) | Deliberate confirmation + confidence + unlink + audit (mirror `PatientMerge`). |
| **Scope creep into ICN territory** | Person is strictly additive; ADR‑001's rekey machinery is untouched. |
| **Performance** | Negligible — Person is an anchor consulted only for cross‑role ops; hot path unchanged. |
