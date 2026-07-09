# Bed Board & EVS Turnover -- Human Test Script

Covers the institution-aware **Bed Board** (`/beds`): the unit capacity overview, the
per-unit room/bed grid with lifecycle colors, the EVS (Environmental Services) turnover
cycle, bed blocking / out-of-service, the institution-wide EVS queue, the institution
picker, and the small-site (no-rooms) shape. Feature flag `BED_MANAGEMENT` (on by
default); write actions require the `DG BED CONTROL` security key.

## Prerequisites

- **Login:** NURSE1 / Password: `smythVista1` (holds `DG BED CONTROL`; `ORELSE` also
  satisfies the EVS clean/dirty flips).
- **Pre-conditions:**
  1. The SiloHost, WebServer, and BlazorWeb projects must all be running (WebServer
     started with a dataset so the demo institutions/units seed).
  2. The demo seeds must have run: institution **500** (NEW VISTAS MEDICAL CENTER) has
     units MED-3A, MED-4B, SURG-2C, ICU-1, TELE-4B, PSYCH-5A, OBS-1; the BILH health
     system has LAHEY-BURLINGTON, LAWRENCE-GENERAL, and the 4-bed BILH-CLINIC-ANDOVER.
  3. Navigate to `/beds`.

> **Terminology.** A bed is *placeable* only when its state is **Available**. Dirty,
> Cleaning, Reserved, Blocked, and Out-of-Service beds are NOT free. The stats strip
> labels Available as the only "placeable" number on purpose.

---

## Scenario 1: Institution capacity overview

### Steps

1. Navigate to `/beds`. The **Institution** picker at the top defaults to
   **NEW VISTAS MEDICAL CENTER** (institution 500).
2. Read the stats strip across the top.
3. Read the unit cards below the **Bed Board** tab.

### Expected Result

- A stats strip shows **Total Beds**, **Available (placeable)** (green),
  **Occupied**, **Dirty**, **Blocked**, and **Out of Service** for the whole
  institution.
- One card per unit (MED-3A, MED-4B, SURG-2C, ICU-1, TELE-4B, PSYCH-5A, OBS-1), each
  showing its name, type, and colored counts: available (green), occupied (blue),
  reserved (amber), dirty (orange), cleaning (teal), blocked/out-of-service (gray),
  and boarders (purple) when present.
- Because of the seeded lifecycle variety, the institution total shows at least one
  **Dirty**, one **Cleaning**, one **Blocked**, and one **Out of Service** bed
  (they are NOT counted in Available).

---

## Scenario 2: Drill into a unit and read the bed grid

### Steps

1. On the **Bed Board** tab, click the **MED-4B** unit card.
2. Observe the room-grouped bed grid.
3. Click **<- All Units** to return to the overview.

### Expected Result

- The unit board opens showing beds grouped by room (Room 401, 402, ...), each bed a
  colored card with its bed id, type, and a state badge.
- Bed **403-A** shows **Dirty** (orange) and bed **404-B** shows **Cleaning** (teal) --
  the seeded EVS-in-progress state.
- Available beds are green with **Mark dirty**, **Block...**, and **Out of service...**
  actions; the dirty/cleaning beds show EVS actions (below).
- **<- All Units** returns to the capacity overview.

---

## Scenario 3: EVS turnover -- clean a dirty bed (the happy path)

### Steps

1. Open the **MED-4B** unit board.
2. Find bed **403-A** (state **Dirty**).
3. Click **Start cleaning**.
4. Observe the state change, then click **Mark clean**.

### Expected Result

- After **Start cleaning**, bed 403-A moves from **Dirty** to **Cleaning** (teal).
- After **Mark clean**, bed 403-A moves to **Available** (green) and now offers the
  Mark dirty / Block / Out of service actions.
- The unit card counts and the institution stats strip update (Dirty -1, Available +1)
  on the next refresh -- capacity is a live projection of the beds, never a separate
  number to sync.

> **Small-site shortcut.** **Mark clean** works straight from **Dirty** too -- a tiny
> site can turn a bed in one click without a separate Start-cleaning step. Try it on
> bed **404-B** (already Cleaning) or mark another bed dirty first.

---

## Scenario 4: Mark a bed dirty, then clean it

### Steps

1. On the **MED-4B** board, pick any **Available** bed (e.g. **402-A**).
2. Click **Mark dirty**.
3. Click **Mark clean**.

### Expected Result

- **Mark dirty** moves the bed Available -> **Dirty**.
- **Mark clean** moves it back Dirty -> **Available** (the skip-Start-cleaning path).
- At no point can a *discharge* leave a bed silently "free": vacating an occupied bed
  always routes it through Dirty first (verified in the ADT and Transfer scripts).

---

## Scenario 5: Block a bed and unblock it

### Steps

1. On any unit board, pick an **Available** bed.
2. Click **Block...**; an inline reason field appears.
3. Type a reason (e.g. `Isolation buffer`) and confirm.
4. On the now-**Blocked** bed, click **Unblock**.

### Expected Result

- A block requires a reason (the confirm button is disabled until you type one).
- The bed shows **Blocked** (gray) with the reason; it is NOT counted in Available.
- **Unblock** returns it to **Available**.
- Blocking an **Occupied** or **Reserved** bed is rejected with a message -- release the
  patient / clear the reservation first.

---

## Scenario 6: Out of service and return to service

### Steps

1. Pick an **Available** bed and click **Out of service...**; enter a reason
   (e.g. `Monitor repair`) and confirm.
2. On the now-**Out of Service** bed, click **Return to service**.

### Expected Result

- The bed shows **Out of Service** (gray) with the reason; excluded from Available and
  from operational capacity.
- **Return to service** moves it to **Dirty** -- NOT straight to Available. Physical
  work happened, so the bed must be cleaned before it is placeable again (honest
  capacity). Finish with **Mark clean**.
- Seeded example: TELE-4B bed **458** starts Out of Service ("Telemetry monitor
  awaiting repair").

---

## Scenario 7: Institution-wide EVS queue

### Steps

1. Return to the **Bed Board** overview (**<- All Units**).
2. Click the **EVS Queue** tab.

### Expected Result

- A single table lists every Dirty/Cleaning bed across the WHOLE institution --
  Unit, Bed, Room, State, Dirty Since, and Isolation precautions -- oldest-dirty first.
- This is one grain read (the per-institution capacity rollup), not a per-unit scan,
  so it can never disagree with the unit boards for more than a moment.
- Each row offers the same clean actions; clearing a bed here removes it from the queue.

---

## Scenario 8: Switch institutions -- the small 4-bed clinic

### Steps

1. In the **Institution** picker, choose **BILH PRIMARY CARE -- ANDOVER**.
2. Observe the board.

### Expected Result

- The board reloads for the clinic: a single unit with **4 beds** and **no rooms** --
  the beds render as a flat list, not grouped by room.
- This proves the small-site collapse: one institution, one unit, four beds, zero room
  ceremony -- strictly less setup than a full hospital, same page.
- Switching back to **NEW VISTAS MEDICAL CENTER** restores the multi-unit board.

---

## Scenario 9: Read-only for a user without the key (negative)

### Steps

1. Sign out and log in as **DOCTOR1 / `smythVista1`** (a Provider -- holds neither
   `DG BED CONTROL` nor `ORELSE`).
2. Navigate to `/beds` and open a unit board.

### Expected Result

- The Bed Board and unit grid render (Providers have Clinical access, so they can SEE
  the board), but the EVS / Block / Out-of-service action buttons are hidden or
  disabled.
- Attempting a gated action surfaces a friendly access notice naming the required key
  (via `SecurityKeyError.Describe`) -- never a crash.

---

## Reference: bed lifecycle states

| State           | Color  | Placeable? | Reached by |
|-----------------|--------|------------|------------|
| Available       | green  | **Yes**    | Mark clean; Unblock |
| Reserved        | amber  | No         | Transfer accept / pending admission |
| Occupied        | blue   | No         | Admission / bed assignment |
| Dirty           | orange | No         | Discharge/transfer-out; Mark dirty; Return to service |
| Cleaning        | teal   | No         | Start cleaning (from Dirty) |
| Blocked         | gray   | No         | Block (reason required) |
| Out of Service  | gray   | No         | Out of service (reason required) |

Legal transitions are enforced server-side; an illegal move (e.g. Block an Occupied
bed, Reserve a non-Available bed, Mark clean a bed that isn't Dirty/Cleaning) is
rejected with a clear message rather than silently applied.
