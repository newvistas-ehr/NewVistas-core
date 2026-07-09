# Interfacility Transfer Center -- Human Test Script

Covers the **Transfer Center** (`/transfer-center`): the request -> accept(reserve a
bed) -> complete(arrival) workflow that moves a patient between institutions in a health
system (the BILH demo: **Lahey Burlington -> Lawrence General**), plus decline, cancel,
the new-request form, and the single-institution self-hide. Feature flag
`BED_MANAGEMENT`; all transfer actions require the `DG BED CONTROL` key.

## Prerequisites

- **Login:** NURSE1 / Password: `smythVista1` (holds `DG BED CONTROL`).
- **Pre-conditions:**
  1. SiloHost, WebServer, and BlazorWeb running with demo seeds loaded.
  2. The seed creates patient **P9008 "TRANSFERRE,TERRY"**, admitted to
     **LAHEY-BURLINGTON** ICU-1 bed **ICU-2**, with an in-flight **REQUESTED** transfer
     to **LAWRENCE-GENERAL** for **TELEMETRY** (urgency URGENT). Lawrence's telemetry
     unit **TELE-2** has beds 201-208 available.
  3. Navigate to `/transfer-center`.

> **The receiving facility controls its own beds.** A transfer is a request the
> *sending* side submits; the *receiving* side accepts it by reserving one of ITS beds,
> or declines. Completion discharges the patient at the sender and admits them at the
> receiver in one step, on the same (ICN-keyed) chart.

---

## Scenario 1: Accept and complete the seeded transfer (happy path)

### Steps

1. Navigate to `/transfer-center`. In the **Institution** picker, choose
   **LAWRENCE GENERAL HOSPITAL** (the receiving side).
2. On the **Incoming** tab, find the row for **TRANSFERRE,TERRY (P9008)** --
   URGENT (amber badge), From **LAHEY HOSPITAL & MEDICAL CENTER -- BURLINGTON**,
   Level of Care **TELEMETRY**, Status **REQUESTED**.
3. Click the row to expand it and read the detail: clinical summary, reason, requested
   bed type, and the status **Timeline**.
4. In the row's Actions, click **Accept...**. An inline bed-picker panel opens.
5. In the panel: choose Unit **Telemetry Unit 2** (shows "7 available"), then choose a
   **Bed**. Telemetry beds are marked with a star and listed first because the request
   asked for TELEMETRY -- pick **201**.
6. Click **Confirm accept**.
7. The status becomes **ACCEPTED** and the row now shows the reserved bed. Click
   **Complete arrival...**.
8. In the complete panel, leave the arrival time at now (optionally add the receiving
   attending and a diagnosis) and click **Confirm arrival**.

### Expected Result

- After **Confirm accept**: the request shows **ACCEPTED** with "bed 201"; on the Bed
  Board, Lawrence's TELE-2 bed **201** is now **Reserved** (amber) for the patient.
- After **Confirm arrival**: a green banner confirms the arrival; the request leaves the
  active Incoming queue (Status **COMPLETED**).
- **Verify placement:** go to `/beds`, pick **LAWRENCE GENERAL HOSPITAL**, open
  **Telemetry Unit 2** -- bed **201** is now **Occupied** by **TRANSFERRE,TERRY**.
- Behind the scenes: the sending admission at Burlington is discharged with disposition
  **TRANSFER** (its old ICU bed goes to **Dirty**), a new admission movement is recorded
  at Lawrence, and Lawrence is added to the patient's treating-facility list.

---

## Scenario 2: Outgoing queue and cancel (from the sending side)

> This scenario needs an active (REQUESTED or ACCEPTED) transfer. If you already
> completed the seeded one, first create a fresh request with Scenario 4, then come
> back here.

### Steps

1. In the **Institution** picker, choose **LAHEY HOSPITAL & MEDICAL CENTER -- BURLINGTON**
   (the sending side).
2. Click the **Outgoing** tab -- the same request appears here (this institution is the
   sender).
3. On an active row, click **Cancel...**, optionally type a reason, and confirm.

### Expected Result

- The Outgoing tab lists transfers where this institution is the sender.
- **Cancel** on a REQUESTED transfer marks it CANCELLED. Cancelling an **ACCEPTED**
  transfer additionally releases the receiving facility's reserved bed (it returns to
  Available on Lawrence's board).

---

## Scenario 3: Decline an incoming request (receiving side)

> Needs a REQUESTED incoming transfer -- create one with Scenario 4 first if needed.

### Steps

1. As the **receiving** institution, on the **Incoming** tab find a **REQUESTED** row.
2. Click **Decline...**, type a reason (required, e.g. `No telemetry capacity tonight`),
   and confirm.

### Expected Result

- The request moves to **DECLINED** with the reason recorded in its Timeline.
- No bed was reserved, so nothing is released. A declined request cannot later be
  accepted.

---

## Scenario 4: Create a new transfer request (repeatable)

### Steps

1. Go to `/transfer-center` and pick a sending institution (e.g.
   **LAHEY HOSPITAL & MEDICAL CENTER -- BURLINGTON**).
2. Click the **New Request** tab.
3. Enter a patient id (e.g. `P9008`) and click **Load**. The patient's name appears and
   the form auto-fills the sending admission/unit/attending from their current
   admission.
4. Choose a **Receiving institution** from the dropdown -- it lists placement targets
   (institutions that accept transfers and have a matching placeable bed), each labeled
   "Name -- N placeable", excluding the sending institution.
5. Set Level of Care, Bed Type (optional), Isolation, Urgency, and fill the clinical
   summary and reason.
6. Click submit.

### Expected Result

- A new **REQUESTED** transfer is created; it appears in the sender's **Outgoing** tab
  and the receiver's **Incoming** tab immediately.
- The receiving-institution dropdown only offers institutions with real capacity, so you
  can't request a transfer to a full or non-accepting site.

---

## Scenario 5: Single-institution self-hide

### Steps

1. (Conceptual / config check.) On a deployment with only ONE active institution, open
   `/transfer-center`.

### Expected Result

- The Transfer Center renders only an informational card ("needs more than one
  institution"), and the **Transfer Center** nav item is hidden -- there is nowhere to
  transfer TO. In the BILH demo (multiple institutions) it is fully active. This is why
  there is no separate feature flag for transfers: the page hides itself when it can't
  be useful.

---

## Scenario 6: Read-only for a user without the key (negative)

### Steps

1. Sign in as **DOCTOR1 / `smythVista1`** (a Provider -- no `DG BED CONTROL`).
2. Open `/transfer-center` and pick an institution with queued transfers.

### Expected Result

- The Incoming and Outgoing queues render read-only -- the tester can SEE the requests
  and their detail, but the Accept / Decline / Complete / Cancel / New-request actions
  are hidden.
- No action can be taken without the key; there is no crash.

---

## Reference: transfer request lifecycle

| Status     | Set by | Next |
|------------|--------|------|
| REQUESTED  | sender submits | ACCEPTED (receiver reserves a bed) or DECLINED (receiver) or CANCELLED (sender) |
| ACCEPTED   | receiver, with a reserved bed | COMPLETED (arrival) or CANCELLED (releases the reservation) |
| COMPLETED  | receiver, on arrival | terminal -- discharge at sender + admission at receiver recorded |
| DECLINED   | receiver | terminal -- never reserved a bed |
| CANCELLED  | sender | terminal -- releases the reservation if one was held |

If the reserved bed becomes unavailable before arrival, the request stays **ACCEPTED**
and the coordinator uses **Reassign bed...** to reserve a different bed, then completes.
