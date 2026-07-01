# Specialty Cover Sheet — Prototype (DECISIONS + design)

> **Status: IMPLEMENTED as a flagged feature, 2026-06-30 on branch `Pharmacogenomics`.** Started as a
> spike ("see how it looks"), now promoted to a real feature: the cover sheet as a **composition of
> sections driven by a layout** (General / Oncology / Procedural), resolved **per-patient-then-viewer**,
> over a non-suppressible safety spine. Gated by **`SPECIALTY_COVERSHEET`** (Modern, on by default) at
> `/coversheet-preview` ("Cover Sheet (Specialty)"). The legacy `/cover-sheet` is **untouched** (augment,
> not replace — see *Promotion* below). **13 unit + 7 functional tests**; full suite green. Demo P9001
> (Stage IIIB melanoma + seeded rotator-cuff repair + shoulder MRI).
>
> **Sequencing note:** the `PersonGrain`/personId identity work (triplication fix) is **NOT** a
> prerequisite. Confirmed from code: the two resolution inputs — patient clinical context (patient
> grains) and viewer role (provider `NewPersonState` File #200 `ServiceSection`/`Specialty`) — are both
> independent of the unified-person layer. The resolver takes viewer-role as an **input parameter**
> (dependency-inverted), so a future PersonGrain only changes *where the viewer's specialty is fetched*,
> not the cover-sheet logic.

## Decisions I made autonomously (James was at lunch — here are the options I chose among)

**D1 — Layout binding axis.** *Options:* (a) viewer-role-led — Dr. Yew always gets the surgical view;
(b) patient-context-led — an onco patient leads with cancer whoever opens it; (c) manual switch only.
**Chosen: (b) patient-context-led as the auto-default, PLUS a manual switcher** so all three layouts
can be compared on one patient. Rationale: patient-context-led matches "the chart should tell you what's
clinically loudest first," and the manual switch is what makes a *prototype* useful for eyeballing.
Production would fold in viewer role as a reorderer/tiebreaker.

**D2 — Tiebreak when a patient has BOTH cancer and an upcoming surgery (P9001 does).** *Options:*
(a) oncology wins; (b) procedural wins; (c) most-recent-activity wins. **Chosen: (a) oncology wins** —
serious active cancer outranks an elective procedure clinically (and, per James, a serious cancer dx
usually *defers* elective surgery). The procedural layout is still one click away. See D6 (the banner).

**D3 — Layout representation.** *Options:* (a) N hardcoded cover-sheet methods; (b) layout-as-data in a
static registry; (c) layout-as-persisted-site-parameter. **Chosen: (b)** — a static `CoverSheetLayouts`
registry (ordered section specs + prominence + item cap). Production → (c) site parameter with
system→division→service→user precedence. Explicitly NOT (a) (combinatorial, duplicates fan-out).

**D4 — Safety spine.** Every layout, regardless of what it declares, is prepended a **non-suppressible
spine: Demographics + CWAD + Allergies.** No layout can hide these. This is the guardrail against lean
views dropping safety-critical info.

**D5 — Selective fan-out.** The assembler reads **only the sections a layout declares** (+ spine). This
is the same lever as the "90% of the fan-out is useless" conversation — a surgeon's layout simply doesn't
pull the eight sections he never opens. The page shows a "sections loaded: N" readout to make it visible.
*(Prototype caveat: default-resolution + the context banner still read oncology/surgery indexes eagerly;
production would resolve the default from a cheap patient-context flag.)*

**D6 — Context banner ("loudest problem").** On the **Procedural** layout, if the patient has active
cancer, show a caution: *"Active oncology (…) — confirm this elective procedure is not deferred."* This
surfaces James's clinical point that serious cancer usually reconsiders elective surgery.

**D7 — Scope/isolation.** *Options:* (a) replace `/coversheet`; (b) new preview page. **Chosen: (b)** —
new page `/coversheet-preview`, new model `SpecialtyCoverSheet`, new workflow method. The legacy
`GetCoverSheetAsync` and `/coversheet` are untouched. **No feature flag, no new grain, no new store, no
tests** — it's a read-only composition prototype. All of that would come if we promote it.

**D8 — Demo data.** P9001 already has **Stage IIIB melanoma** (BRAF V600E / PD-L1, from
`ExtremeLeeSickSeed`). I added an **upcoming right rotator-cuff repair** (Dr. Yew, ~2 wks out) and a
**right-shoulder MRI** (impression: full-thickness supraspinatus tear). So P9001 exercises oncology
*and* procedural on the same chart.

## The three layouts (ordered sections; ★ = prominent)

| Section | General (PCP) | Oncology | Procedural (surgeon) |
|---|---|---|---|
| Oncology summary (tumor/stage/tx/biomarkers→therapy) | — | ★ 1st | compact |
| Upcoming procedures | — | — | ★ 1st |
| Latest imaging | — | ✓ (staging) | ★ 2nd |
| Pharmacogenomics alerts | — | ✓ (DPYD etc.) | ✓ |
| Active problems | ★ | compact | compact |
| Active medications | ✓ | ✓ | ✓ (anticoag) |
| Clinical reminders (health maint.) | ✓ | — | — |
| Recent labs | ✓ | ✓ (chemo) | ✓ (pre-op) |
| Recent vitals | ✓ | ✓ | ✓ |
| Recent visits | ✓ | — | — |
| Active orders | ✓ | — | — |
| Active consults | ✓ | — | — |
| **Safety spine (demographics/CWAD/allergies)** | always | always | always |

## Architecture

- **Model** `GrainStates/SpecialtyCoverSheetState.cs` — `SpecialtyCoverSheet` (LayoutId/Name/Reason,
  ContextBanner, ordered `List<CoverSheetSectionSpec>`, + the section payloads reusing existing summary
  DTOs and a small `OncologyTumorCard`). `CoverSheetSectionSpec` = {SectionKey, Title, Prominent, MaxItems}.
- **Layout registry** `Clinical/CoverSheetLayouts.cs` — the 3 layouts as data + `Resolve(id)` +
  `ResolveDefault(hasCancer, hasUpcomingSurgery)` + `All`.
- **Workflow** `Grains/PatientWorkflowGrain.SpecialtyCoverSheet.cs` —
  `GetSpecialtyCoverSheetAsync(string? layoutId)`: resolve layout, assemble only declared sections in
  parallel, build the banner, return.
- **Blazor** `Components/Pages/CoverSheetPreview.razor` at `/coversheet-preview` — layout switcher
  (Auto/General/Oncology/Procedural), fixed safety spine, prominence-styled sections, "sections loaded" readout.
- **Seed** `Infrastructure/SpecialtyCoverSheetSeed.cs` — P9001 rotator-cuff surgery + shoulder MRI.

## Promotion — done / remaining

**Done (this pass):**
- **Per-patient-then-viewer resolution** — `CoverSheetLayouts.ResolveDefault(hasCancer, hasSurgery, viewerRole)`
  + `MapViewerRole(serviceOrSpecialty)`. The patient's context sets what's *relevant* + the loudest default;
  the viewer's provider role picks the *lens* — but **only among the patient's own SPECIALTY concerns**
  (a surgeon viewing a patient with no surgery falls back to patient-loudest). **General is the baseline,
  never a viewer override**, so a generalist lens can't bury a specialty concern — a PCP viewing a cancer
  patient still leads with Oncology. The context banner surfaces the patient's loudest concern regardless
  of lens. Viewer role is an **input param** to the workflow (`GetSpecialtyCoverSheetAsync(layoutId,
  viewerRole)`); the page sources it from the logged-in provider's `NewPersonState` (File #200,
  `ServiceSection`/`Specialty`), read via the grain keyed **`USER:{userId}`** (per AuthController /
  SeedDemoUsersAsync) — the PersonGrain-independent seam.
- **`SPECIALTY_COVERSHEET` feature flag** (Modern, on by default; wired into `/api/site/features` + `editions.js`).
- Page flag-gated + de-prototyped + nav relabeled "Cover Sheet (Specialty)".
- **Tests:** `CoverSheetLayoutsTests` (15 unit — resolution matrix incl. the generalist-never-buries case +
  viewer mapping) + `SpecialtyCoverSheetTests` (7 functional — section composition, the
  **non-suppressible-spine invariant**, auto/viewer/manual paths).
- **Live-verified** with the existing demo logins (no new seed needed) on **P9001** (cancer + surgery):
  **SURGEON2** (SURGERY) → *Procedural* over the patient's Oncology default, "viewing as SURGERY", with the
  cancer banner still firing; **DOCTOR1** (MEDICINE, PCP) → *Oncology* (generalist doesn't demote);
  **ONC1** (ONCOLOGY) → *Oncology* (matches loudest). Password `smythVista1`.

**Remaining:**
- Move layouts to **site parameters** (system→division→service→user precedence) + a layout editor. (Today
  the registry is data in `CoverSheetLayouts.cs` — the seam is there, the config surface isn't.)
- Decide **augment vs replace** the legacy `/cover-sheet` (currently *augment* — both exist). Replacing it
  lets `GetCoverSheetAsync`'s 10-way fan-out retire in favor of "General = a layout."
- Richer viewer→lens mapping (more specialties); an explicit per-user layout preference override.
