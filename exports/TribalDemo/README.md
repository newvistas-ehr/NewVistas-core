# Tribal Demo Dataset

A small, self-contained dataset for demonstrating NewVistas in an IHS / tribal
deployment context. Loaded via [`ITribalDemoSeederGrain`](../../NewVistas.Abstractions/GrainInterfaces/ITribalDemoSeederGrain.cs),
which calls the same registration / referral / GPRA workflows a live operator
would, so the demo exercises the full pipeline (including `IhsTribalEligibilityPolicy`,
the CHS authorization workflow, and the GPRA submission packager).

## Files

| File | Contents |
|---|---|
| `patients.json` | 50 tribal patients with realistic eligibility distribution |
| `chs-referrals.json` | 8 CHS referrals at various lifecycle stages (approved, denied, mixed priority classes) |
| `gpra-report.json` | One completed FY2026-Q1 GPRA report with 11 indicators across diabetes, CV, immunizations, behavioral health, women's health, and preventive care |

## Patient Eligibility Distribution

Generated with a fixed random seed so the manifest is byte-stable.
Of the 50 patients:

| Tier | Count | Description |
|---|---|---|
| **CHS-eligible** | 28 | Tribal member, resides in CHSDA, ≥180 days residency — `IhsTribalEligibilityPolicy` stamps `IHS CHS` |
| **Direct-care only** | 12 | Tribal member but either outside CHSDA or below 180-day threshold — stamps `IHS DIRECT` |
| **Eligible by category** | 3 | Non-Indian patients eligible per 25 CFR § 136.12 (e.g., pregnant by an eligible Indian) — stamps `IHS DIRECT` |
| **Walk-in / no IHS hints** | 7 | Self-pay or private-insurance walk-ins — registered with no enrollment record |

Common tribal affiliations seeded: Cherokee Nation, Navajo Nation, Choctaw,
Chickasaw, Creek, Lakota Sioux, White Mountain Apache, Tohono O'odham,
Pascua Yaqui, Cheyenne River Sioux.

## CHS Referrals

| # | Patient | Type | Priority | Outcome |
|---|---|---|---|---|
| 1 | #1 | Cardiology consult | II (acute) | **Approved** $2,200 |
| 2 | #3 | MRI lumbar spine | III (non-emergent acute) | **Approved** $1,800 |
| 3 | #7 | Cataract surgery | III | **Approved** $4,500 (with Medicare secondary) |
| 4 | #12 | Acute MI ER admit | I (emergent) | **Approved** $35,000 (post-emergency) |
| 5 | #18 | Cosmetic procedure | V (excluded) | **Denied** — Class V excluded per 25 CFR Part 136 |
| 6 | #24 | Screening colonoscopy | IV | **Denied** — alternate resources not verified |
| 7 | #30 | SUD evaluation | II | **Approved** $950 |
| 8 | #35 | Diabetes endocrinology | II | **Approved** $600 |

Total approved CHS dollars: ~$45,050. Mix of urgency levels and one each of
the IHS Medical Priority Classes I-V.

## GPRA Report

`gpra-report.json` is a complete `IGpraReportGrain` payload representing
FY2026 Q1 for the demo facility. 11 indicators spanning 6 GPRA clinical
categories. Realistic IHS performance numbers (diabetes HbA1c testing at
79%, depression screening at 70%, BP control at 70%) with year-over-3-years
baseline comparison and improvement flags.

After loading, the report is in `Completed` status and ready for the
[GPRA submission workflow](../../NewVistas.Abstractions/Docs/Human-Test-Scripts/Blazor/Admin/12-GPRA-Submission.md).

## Loading

See [`Blazor/Admin/13-Tribal-Demo-Data.md`](../../NewVistas.Abstractions/Docs/Human-Test-Scripts/Blazor/Admin/13-Tribal-Demo-Data.md)
for the operator workflow. Programmatically:

```csharp
ITribalDemoSeederGrain seeder = grainFactory.GetGrain<ITribalDemoSeederGrain>("TRIBAL-DEMO-SEEDER");
TribalDemoSeedResult result = await seeder.LoadAsync(
    manifestDirectory: "exports/TribalDemo",
    seededByUserId: "ADMIN1",
    seededByUserName: "System Administrator");
```

The seeder is idempotent on patient identity (uses externally-supplied ICNs
derived from each patient's index), so re-running produces no duplicates.

## Site Profile Requirements

To exercise the full pipeline, the loading silo should have these features
enabled (the `IhsTribalSiteProfile` pre-enables all of them):

- `EXTERNAL_REFERRAL_TRACKING` (for CHS referrals)
- `GPRA_REPORTING` (for the GPRA report)
- `PATIENT_MERGE` (not used by the seed itself but commonly enabled in tribal demos)
- `IhsTribalEligibilityPolicy` registered as the `IRegistrationEligibilityPolicy` (so patient registration applies tribal eligibility rules)

A non-tribal site can still load the seed; in that case patients register
without enrollment records (the `NoOpRegistrationEligibilityPolicy` ignores
the IHS hints), but CHS referrals and GPRA report still load normally.
