# Whole-Person Social Care — Design

Feature flag: `SOCIAL_CARE` (Modern, on by default). ADR: ADR-005. First increment of the HITS harvest.

## Grain / engine map

| Type | Key | Store | Role |
| --- | --- | --- | --- |
| `IHouseholdGrain` | `HOUSEHOLD:{guid}` | `householdStore` | Person-anchored household; members join/leave over time |
| `IPersonHouseholdIndexGrain` | `PERSON-HOUSEHOLD-IDX:{personId}` | `personHouseholdIndexStore` | Reverse index: a Person's households (current + history) |
| `SdohScreeningCatalog` (static) | — | — | AHC-HRSN domain → billable Z-code → referral service type + Evaluate |
| `ISdohScreeningGrain` | `SDOH:{guid}` | `sdohScreeningStore` | One screening: answers, computed findings, closed-loop actions |
| `ISdohScreeningIndexGrain` | `SDOH-IDX:{patientId}` | `sdohScreeningIndexStore` | Per-patient screening history |
| `ISdohCohortIndexGrain` | `SDOH-COHORT:{domain}` | `sdohCohortStore` | Per-domain positive-screen cohort (GetCountAsync) |

Workflow façade partials: `PatientWorkflowGrain.Household.cs` (patient→Person→household resolution,
create/join, non-patient member) and `PatientWorkflowGrain.Sdoh.cs` (record screening + the closed-loop
apply helpers). REST: `SocialCareController` (`api/social-care`). UI: `/household` and `/sdoh-screening`,
"Social Care" nav.

## Reuse (compose, don't duplicate)

- Household member identity → `PersonGrain` (ADR-002); `CreateOrGetPersonForPatientAsync` bootstraps a
  patient's Person, `RegisterIdentityAsync` mints a bare Person for a non-patient member.
- Z-code onto the problem list → the existing `AddProblemAsync` (`GMPL PROBLEM`).
- Referral → the existing `SocialWorkReferralGrain` via `CreateSocialWorkReferralAsync`
  (`SocialWorkReferralServiceType`: Food, Housing, Transportation, FinancialAssistance, …).
- Cohort/reporting → the `DrugClassCohortIndexGrain` reverse-index shape.

## Data flow (the closed loop)

1. **Screen** — a clinician records the AHC-HRSN answers (Positive / Negative / Unknown per domain);
   `SdohScreeningCatalog.Evaluate` computes one finding (Z-code + suggested referral) per positive domain;
   the patient is added to each positive domain's cohort shard.
2. **Code** — one-click applies a finding's Z-code to the problem list, citing the screening (a real,
   billable Z55–Z65 code — the coded SDOH capture value-based care wants).
3. **Refer** — one-click opens a Social Work referral to the matching service type — the intervention,
   in the queue the social-work team already uses.
4. **Report** — `SDOH-COHORT:{domain}` counts answer "how many patients screen positive for food
   insecurity" across the population.

## Z-code crosswalk (validated FY2026 ICD-10-CM, all billable)

| Domain | Z-code | Referral |
| --- | --- | --- |
| Homelessness | Z59.00 | HomelessServices |
| Housing instability | Z59.811 | Housing |
| Food insecurity | Z59.41 | Food |
| Transportation insecurity | Z59.82 | Transportation |
| Utility needs | Z59.12 | FinancialAssistance |
| Financial strain | Z59.869 | FinancialAssistance |
| Employment | Z56.0 | VocationalRehabilitation |
| Education | Z55.9 | VocationalRehabilitation |
| Interpersonal safety | Z65.4 | AdultProtectiveServices |

## Demo (`SocialCareSeed`)

P9301 (SOCIAL,SAM) in "Social Household" as head + a non-patient child (SOCIAL,SUSIE); a positive
AHC-HRSN screen (food + housing) with the loop closed — Z59.41 + Z59.811 on the problem list and two
matching Social Work referrals open.

## Out of scope (v1) — roadmap in ADR-005 / the plan

Income-source typing; program-agnostic case-management goal/outcome spine; community-resource directory;
veteran psychosocial enrichment; behavioral-health treatment planning (gated on MentalHealth overlap);
shelter/bed subsystem (gated on a homeless-veteran use case). The agency back-office is not harvested.
