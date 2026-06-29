# NewVistas — Oncology & Precision Medicine Demo

> **UI:** Blazor Web (`NewVistas.BlazorWeb`)
> **Target length:** ≈ 8:00
> **Audience:** Oncology stakeholder / clinician / prospective site
> **Goal:** Show that NewVistas has a *complete* oncology module (registry → staging → treatment →
> radiation → cancer-registry abstract) **and** a precision-oncology layer that turns molecular
> results into matched, guideline-grounded therapy options — without becoming a privacy silo.
> Not goal: a line-by-line tour of every oncology field.

Theme in one line: **"VistA, brought into the precision-medicine era."**

Pairs with `DemoScripts/Blazor/01-Product-Overview-4min.md` (show that first for a cold audience).

---

## Before You Record

### Services to start (three terminals, in this order)
1. `dotnet run --project NewVistas.SiloHost`  — wait for "Application started".
2. `dotnet run --project NewVistas.WebServer` — seeds the demo patient automatically (see below).
3. `dotnet run --project NewVistas.BlazorWeb`

### Browser
- Open **https://localhost:7137** (VS / https launch profile) — or **http://localhost:5196** if you started Blazor with the plain `http` profile. Use a fresh InPrivate/Incognito window.
- Zoom 110–125% so the molecular-profile table and match cards read cleanly on a 1080p capture.
- Close dev tools and notification panels. (If you run from Visual Studio, turn off **Browser Link** — it can inject into pages and throw a 500 on static manual content.)

### Demo data — nothing to load by hand
Unlike the product-overview demo, **the oncology data seeds automatically** on WebServer startup
(the marquee patient **P9001 — SICK, EXTREME LEE**). No `demo/load` curls needed.

Confirm it took: the WebServer console should print
`+ oncology: melanoma ONC-TUMOR:… staged IIIB with molecular profile (BRAF V600E, PD-L1 35%)`
followed by `Rich demo patient P9001 (SICK,EXTREME LEE) seeded successfully`.

### Credentials & patient
| Field | Value |
|---|---|
| Access Code (primary driver) | `ONC1` — Oncologist (holds the `ONCO MANAGER` key → full edit) |
| Access Code (for the access-model beat) | `DOCTOR1` — primary-care physician (read-only oncology) |
| Verify Code | `smythVista1` |
| ONC1 header name | BENNETT,SARAH J — Oncology |
| Demo patient | **P9001** (SICK, EXTREME LEE) — pre-seeded stage IIIB melanoma |

Have **`P9001`** on the clipboard before you start.

> **Dry run before the real take:** sign in as ONC1 → Oncology → open the melanoma → confirm the
> Molecular Profile and two match cards render. That's the whole demo de-risked in 20 seconds.

---

## Script (target 8:00)

Each block lists **[TIME]**, what the viewer sees, and the narrator line. Narrator lines are
conversational — tweak to your cadence, hold the timing.

---

### 0:00 – 0:25 — Title / framing *(still card or blurred login)*

**On screen:** Title card — "NewVistas — Oncology & Precision Medicine".

> "NewVistas began as a modern rebuild of the VA's VistA EHR. Oncology is where we show what
> 'modern' really means: not just a tumor registry, but the precision-medicine workflow that
> defines cancer care today. Eight minutes, one patient, the whole arc."

---

### 0:25 – 0:45 — Sign in as the oncologist

**Action:** On `/login`, enter **`ONC1`** / **`smythVista1`**, click **Sign In**.

**On screen:** Login card → header shows "BENNETT,SARAH J".

> "We're signing in as Dr. Bennett, an oncologist. Her role carries the oncology-management
> security key — remember that, it matters later."

---

### 0:45 – 1:05 — The oncology footprint

**Action:** Let the home page render. Point to the **ONCOLOGY** section in the left rail —
**Tumor Registry · Radiation Therapy · Cancer Registry**. Don't click yet.

> "Oncology is a first-class area, not a bolt-on: a tumor registry, radiation-therapy course
> tracking, and a NAACCR cancer-registry abstract — the reportable record."

---

### 1:05 – 1:45 — Open the patient; the registry is real

**Action:**
1. Click **Tumor Registry** (or **Patient Lookup → P9001** first).
2. Enter **`P9001`** in the patient box and **Load**.
3. Point to the summary tiles (1 Active · 0 Remission · 0 Recurrence · 1 Total Primary) and the
   registry row: *Malignant melanoma of trunk · C43.5 · stage IIIB · Active · Dr. Cure*.
4. Click the row → **Tumor Detail**.

> "One registered primary — a melanoma. ICD-O-3 histology, clinical and pathologic TNM, SEER
> summary stage, multiple-primary and recurrence tracking. The same FileMan-rooted data model as
> VistA's oncology files, with a modern surface."

---

### 1:45 – 3:00 — Precision oncology *(the money shot — linger here)*

**Action:** Scroll to **Molecular Profile**, then to **Precision-Oncology Matches**.

**On screen — Molecular Profile:**
- **BRAF — Positive — V600E — NGS — FoundationOne CDx**
- **PD-L1 — Positive — TPS 35% — IHC**
- **KIT — Negative — wild type — NGS**

**On screen — Precision-Oncology Matches** (two cards, each FDA-approved-tagged):
- **BRAF + MEK inhibitor** — *e.g. dabrafenib + trametinib* — BRAF: V600E
- **PD-1/PD-L1 checkpoint inhibitor** — *e.g. pembrolizumab* — PD-L1: TPS 35%

> "Here's the tumor's molecular profile — gene, result, method, lab, the way a pathology report
> reads. And here's the payoff: the system reads the *positive* biomarkers and surfaces the
> matched targeted and immunotherapy options, with context and the level of evidence.
>
> Two things to notice. KIT is wild-type, so it produces **no** match — only real findings drive
> suggestions. And this is **decision support, not auto-ordering**: it never writes an order. The
> matcher is a curated, transparent rule set" — *(point to the disclaimer)* — "not a black-box
> model, so there's nothing to hallucinate. Confirm against guidelines and the tumor board, always."

---

### 3:00 – 3:45 — Live: add a biomarker, watch a new match appear

**Action:** In **Add Biomarker** (visible because Dr. Bennett can edit):
1. Gene **NTRK**, Status **Positive**, Result `NTRK1 fusion`, Method **NGS**.
2. Click **Add Biomarker**.
3. A third profile row appears, and a **new** match card — *TRK inhibitor (larotrectinib / entrectinib)* — drops into the Matches panel.

> "Watch what happens when new molecular data lands. I add an NTRK fusion… and a tissue-agnostic
> TRK-inhibitor match appears instantly. As guidelines evolve, we extend the rule base in one
> knowledge file and every tumor benefits — no UI rework."

---

### 3:45 – 4:45 — Close the loop: treatment & radiation

**Action:**
1. Click the **Treatments** tab. Register a treatment off a match (e.g. *Immunotherapy — Pembrolizumab*), **Start** it, set a cycle count, record a **RECIST** response.
2. (Optional) **Radiation Therapy** → show a course (modality, fraction tracking).
3. (Optional) **Cancer Registry** → the NAACCR abstract.

> "Biomarker, to matched therapy, to a treatment episode with RECIST response assessment, to the
> reportable registry abstract — one continuous chart, not four disconnected systems."

---

### 4:45 – 6:00 — Access model: open to read, gated to write

**Action:**
1. **Sign Out.** Sign in as **`DOCTOR1`** / **`smythVista1`** (the primary-care physician).
2. Re-open **Patient Lookup → P9001 → Oncology**, select the melanoma.
3. Point out: DOCTOR1 **sees the full molecular profile and the matches** — but a **read-only
   banner** is shown, and there's **no Add Biomarker form and no action buttons**.

> "This is deliberate. Oncology is **not** a sealed silo like behavioral health — the primary-care
> doctor needs to see the cancer picture to coordinate care, so reads are open to any clinician.
> But only the oncology team — the ones holding that key from earlier — can edit. And that's
> enforced down in the data layer, not just hidden in the UI."

---

### 6:00 – 7:00 — Editions: VistA → RPMS → Modern

**Action:** (No clicks required; optionally open `/api/site/features` or `/manual`.)

> "Every capability is classified — core VistA, the RPMS/IHS modules, or Modern enhancements.
> Oncology and the precision layer are Modern, and they're on by default so every demo has them —
> but they're independent feature flags. A traditional VistA or RPMS site can run the classic
> tumor registry and switch the precision layer off; the nav and panels simply disappear — no dead
> buttons. The in-app user manual even reads the site's flags and dims modules that aren't enabled."

---

### 7:00 – 7:40 — Architecture & grounded-AI pitch *(no clicks)*

**Action:** Return to the tumor detail so the profile + matches sit on screen.

> "Under the hood, every patient, every tumor, every lab batch is its own stateful Orleans
> virtual actor — that's what lets this scale horizontally without a single-database bottleneck.
> And every suggestion you saw traces to a recorded biomarker and a named rule: auditable,
> reproducible, and never an autonomous action. That's how clinical AI has to work."

---

### 7:40 – 8:00 — Close

**On screen:** Tumor detail with matches, or a closing card.

> "That's oncology in NewVistas — a complete registry, precision-medicine decision support, a
> sensible access model, and an architecture built to scale. VistA's heritage, today's medicine."

---

## Recording Notes

- **Record per section, not one take.** The eight blocks above cut cleanly.
- **The NTRK add persists in memory.** If you re-run the demo, that marker (and its match) are
  already there from the last take — **restart the SiloHost** to return to the clean BRAF/PD-L1
  baseline. (WebServer re-seeds P9001 only on a fresh, empty silo.)
- After signing in as DOCTOR1, **patient context resets** — re-enter `P9001`.
- Keep a subtle cursor highlight on; narrate over the capture for cleaner timing.

## Express version (≈2:00)
ONC1 → P9001 → Oncology → open the melanoma → *"BRAF V600E and PD-L1 positive, here are the two
matched therapies — decision support, never auto-ordered, KIT is negative so it doesn't match"* →
add **NTRK** live, watch the TRK-inhibitor match appear → sign in as **DOCTOR1** to show **read-only**
access → one line on the **Modern edition flag**.

## Fallbacks

| If this breaks… | Do this |
|---|---|
| No tumor on P9001 | Check the WebServer log for the `+ oncology: melanoma …` line. If P9001 already existed on a persisted silo, the seed no-ops — restart SiloHost (memory mode) so it re-seeds fresh. |
| ONC1 / DOCTOR1 login fails | Confirm WebServer is up; verify code is `smythVista1` exactly (case-sensitive). |
| ONCOLOGY section missing from the nav | The `ONCOLOGY` site flag is off — it's on by default, so confirm you're on a default site (`SITE:DEFAULT`). |
| Molecular Profile / Add Biomarker not shown | The `PRECISION_ONCOLOGY` flag gates that whole section (on by default). The Add form also requires the `ONCO MANAGER` key — expected to be hidden for DOCTOR1. |
| `/manual` throws a 500 in Visual Studio | Disable **Browser Link** in VS, or open the app from a terminal. |

## Knowledge base
The biomarker → therapy rules live in
`NewVistas.Abstractions/Clinical/PrecisionOncology.cs` (EGFR, ALK, ROS1, BRAF, KRAS, HER2, PD-L1,
MSI, TMB, NTRK, BRCA1/2, RET). Add a rule there and it shows up in the Matches panel automatically —
a good answer to "can it cover *our* markers?"
