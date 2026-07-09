# Security Keys & Page Privileges

NewVistas uses **VistA-style security keys** (modeled on VistA File #19.1) to authorize
sensitive actions. This document lists which key each page/action needs, which demo logins
already have them, and how to grant a key.

## How access control works

- Grain methods that **write or modify** clinical data are annotated with
  `[RequiresSecurityKey("…")]` (see [`SecurityKeys.cs`](../NewVistas.Abstractions/Security/SecurityKeys.cs)).
- Before such a method runs, the silo's **`AuthorizationCallFilter`** checks the signed-in
  user's keys, which live in that user's **AccessControl grain** (`ACL:{userId}`). If the user
  holds **any one** of the required keys, the call proceeds; otherwise it is refused.
- **Most pages are viewable without any key** — only the write actions are gated. Reading a
  patient's problems, orders, labs, vitals, etc. needs no key.
- **Two things happen when a key is missing**, and neither crashes the app:
  - **Action pages** (you click a button without the key) show a friendly notice naming the
    required key, e.g. *"You don't have permission for this action — it needs the 'GMPL PROBLEM'
    security key."*
  - **Mental Health is gated end-to-end** (even viewing requires `YS MH INSTRUMENT`): its nav
    item is **hidden** for users without the key, and opening it directly shows an
    *"Additional access required"* notice instead of an error.

## What each page needs

A user needs **any one** of the listed keys for that action. Viewing a page (loading data)
needs no key unless noted.

| Page (route) | Action that needs a key | Required key (any of) |
| --- | --- | --- |
| Cover Sheet (`/cover-sheet`) | Schedule a follow-up appointment | `SD SCHEDULING` |
| Problems (`/problems`) | Add a problem | `GMPL PROBLEM` |
| Orders (`/orders`) | Place an order | `ORES` or `ORELSE` |
| Orders (`/orders`) | Sign an order | `ORES` |
| Orders (`/orders`) | Hold / Release / Discontinue an order | `ORES` or `ORELSE` |
| Notes (`/notes`) | Create a note | `PROVIDER` |
| Notes (`/notes`) | Sign a note | `TIU SIGN` |
| Notes (`/notes`) | Cosign a note | `TIU COSIGN` |
| Labs (`/labs`) | Place a lab order | `ORES` or `ORELSE` |
| Labs (`/labs`) | Collect specimen / record result | `LRLAB` |
| Labs (`/labs`) | Verify a result | `LRVERIFY` |
| Vitals (`/vitals`) | Record vitals | `GMRV VITALS` |
| Allergies (`/allergies`) | Record an allergy | `GMRA ALLERGY` |
| Scheduling (`/scheduling`) | Schedule an appointment | `SD SCHEDULING` |
| ADT (`/adt`) | Admit / Transfer / Discharge | `DG ADMIT` |
| Bed Board (`/beds`) | Block/out-of-service, EVS turnover (mark dirty/clean) | `DG BED CONTROL` (EVS flips also satisfied by `ORELSE`) |
| Transfer Center (`/transfer-center`) | Request / accept / decline / complete inter-facility transfers | `DG BED CONTROL` |
| **Mental Health (`/mental-health`)** | **View *and* record screenings (whole page)** | `YS MH INSTRUMENT` |

> Administrative back-office flows enforce keys too but aren't reached from the clinician UI
> (e.g. patient **registration** → `CanRegisterPatients`, patient **merge** → `CanMergePatients`,
> **GPRA/NDW** submission, **CHS** authorization, **diabetes registry** mutations). They live on
> API/operator surfaces, not the Blazor pages above.

## Demo logins and their keys

All demo users share the password **`smythVista1`**. Each user is seeded with the keys for its
**role(s)** (see the `roleKeyMap` in [`NewVistas.WebServer/Program.cs`](../NewVistas.WebServer/Program.cs)).

**Role → keys:**

| Role | Keys granted |
| --- | --- |
| Provider | `PROVIDER`, `ORES`, `TIU SIGN`, `GMRA ALLERGY`, `GMRV VITALS`, `GMPL PROBLEM`, `HBHC MANAGER` |
| Nurse | `ORELSE`, `GMRV VITALS`, `GMRA ALLERGY`, `GMPL PROBLEM`, `SD SCHEDULING`, `HBHC MANAGER`, `DG BED CONTROL` |
| Pharmacist | `PSO PHARMACY`, `PSJ RPHARM`, `PSA ORDERS`, `PSB MANAGER` |
| LabTechnician | `LRLAB`, `LRVERIFY` |
| Radiologist | `RA VERIFY`, `PROVIDER`, `TIU SIGN` |
| Surgeon | `PROVIDER`, `ORES`, `TIU SIGN`, `SR SURGERY` |
| Administrator | `XUMGR`, `XUAUDIT`, `DG SENSITIVITY`, `DG BED CONTROL` |
| RegistrationClerk | `SD SCHEDULING`, `DG ADMIT`, `DG BED CONTROL` |
| MentalHealth | `PROVIDER`, `TIU SIGN`, `YS MH INSTRUMENT` |

**Common demo users:**

| Login | Role(s) | Effective keys |
| --- | --- | --- |
| `DOCTOR1`–`DOCTOR5` | Provider | `PROVIDER`, `ORES`, `TIU SIGN`, `GMRA ALLERGY`, `GMRV VITALS`, `GMPL PROBLEM` |
| `NURSE1`–`NURSE5` | Nurse | `ORELSE`, `GMRV VITALS`, `GMRA ALLERGY`, `GMPL PROBLEM`, `SD SCHEDULING` |
| `NP1`, `NP2` | Provider + Nurse | union of Provider and Nurse keys |
| `PHARM1`–`PHARM5` | Pharmacist | `PSO PHARMACY`, `PSJ RPHARM`, `PSA ORDERS`, `PSB MANAGER` |

> **Why `DOCTOR1` is denied some actions:** as a *Provider*, `DOCTOR1` can place/sign orders,
> add problems, record vitals/allergies, and write & sign notes — but **does not** hold
> `YS MH INSTRUMENT` (Mental Health), `SD SCHEDULING` (scheduling), `DG ADMIT` (ADT),
> `LRLAB`/`LRVERIFY` (lab collection/verification), or `TIU COSIGN` (cosigning). Those actions
> show the access notice as `DOCTOR1` — by design. To demo them, sign in as a role that holds
> the key (e.g. `NURSE1` for scheduling, a LabTechnician for lab verification) or grant the key
> (below).

## How to grant a key

**Via the admin UI (Security Key Management):**

1. Sign in as an administrator and go to **`/security-keys`** (Security Key Management).
2. Enter the target **User ID** and click **Load User**.
3. Open the **Grant Key** tab, find the key, and click **Grant**. (Use **Revoke** on the Security
   Keys tab to remove one.) Grants/revocations are written to the user's AccessControl grain and
   recorded in the **Key Audit Log** tab.

**Via role (seed time):** assign the user a role whose `roleKeyMap` entry includes the key
(see `Program.cs`). New keys take effect on the user's next sign-in (the key set is cached per
login session).

**Quickest for a demo:** just log in as a user whose role already holds the key — e.g. a
`MentalHealth`-role user for Mental Health, a LabTechnician for lab verification, a
RegistrationClerk for ADT/scheduling.

## Key catalog (reached from the clinician UI)

| Key | Description |
| --- | --- |
| `ORES` | Order Entry/Results Reporting (physician order entry). Place, sign, and discontinue orders. |
| `ORELSE` | Order Entry nurse/clerk mode. Enter orders on behalf of a provider (flagged for cosignature). |
| `PROVIDER` | General provider key. Clinical documentation, signing notes, recording assessments. |
| `TIU SIGN` | Apply an electronic signature to TIU documents/notes. |
| `TIU COSIGN` | Cosign documents authored by trainees/residents. |
| `GMPL PROBLEM` | Problem list management — add, inactivate, modify problems. |
| `GMRV VITALS` | Record and edit vital signs. |
| `GMRA ALLERGY` | Record, verify, and mark allergies/ADRs. |
| `LRLAB` | General lab access — specimen collection, result entry, lab order management. |
| `LRVERIFY` | Lab result verification — verify and release results (lab supervisors). |
| `SD SCHEDULING` | Appointment scheduling — schedule, reschedule, cancel. |
| `DG ADMIT` | Admission/discharge/transfer operations (patient placement into unit/bed). |
| `DG BED CONTROL` | Bed/room/unit structure, bed blocking & out-of-service, EVS turnover, and the inter-facility Transfer Center. Nurses' `ORELSE` also satisfies the EVS clean/dirty flips. |
| `YS MH INSTRUMENT` | Administer and score mental-health screening instruments. |

The full key list (including pharmacy, radiology, surgery, system-admin, and interoperability
keys not reached from these pages) is in
[`SecurityKeys.cs`](../NewVistas.Abstractions/Security/SecurityKeys.cs).
