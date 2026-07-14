# ADR-005 — Whole-Person Social Care (HITS harvest, first increment)

**Status:** Accepted — implemented (2026-07)

## Context

An internal analysis (`HITS8_DOMAIN_HARVEST.md`) of a social-services case-management app identified a
social/whole-person domain worth re-expressing as NewVistas grains — the non-clinical mission areas
(homeless veterans, behavioral health, SDOH) that a VA/VistA-inspired system, and any community-health
partner, actually run on.

Exploration found NewVistas **already implements most of the social surface**: a Social Work module
(`SocialWorkAssessmentGrain` with HomelessRisk/SubstanceUse/DV types and housing/employment/financial
fields, `SocialWorkReferralGrain` with a full SDOH referral catalog), insurance/coverage, a financial
means-test income household, care plans + goals, and Person identity (ADR-002). So this is **compose +
fill genuine gaps**, not greenfield. The two genuine gaps this increment fills:

1. There is no **Person-anchored, longitudinal general household** — the existing income household is
   financial and patient-anchored; nothing associates *people* into a family unit that outlives any
   one member.
2. There is no **coded SDOH screening that closes the loop.** Health Factors are free-form/uncoded, and
   the Social Work assessment has SDOH *fields* but nothing computes positive social-need domains, drops
   a billable Z55–Z65 code onto the problem list, and opens the matching referral.

## Decision

Add a flag-gated module (`SOCIAL_CARE`, Modern, on by default) with two capabilities, composing over the
existing Social Work + problem-list machinery rather than forking it.

### 1. Person-anchored Household

- `IHouseholdGrain` (`HOUSEHOLD:{guid}`): a family/residential unit whose members are **Persons**
  (ADR-002 `PERSON:{guid}`), not patients — so a non-patient family member (a child, an unregistered
  spouse) still belongs, and a member who is also staff or a relative on another chart resolves to the
  same human. Membership is time-bounded (`LeftDate`): people move between households, and the household
  outlives any one member.
- `IPersonHouseholdIndexGrain` (`PERSON-HOUSEHOLD-IDX:{personId}`) is the reverse index; the workflow
  resolves a patient → their Person (`PatientState.PersonId`) → current household. A non-patient member
  gets a bare Person via `RegisterIdentityAsync`.
- **Distinct from `IncomeHouseholdGrain`** (financial means-test, patient-anchored) — complementary,
  not a replacement.

### 2. Coded SDOH screening + closed loop

- `SdohScreeningCatalog` (`Clinical/`): the AHC-HRSN core social-need domains, each mapped to a **billable
  FY2026 ICD-10 Z-code** (validated: Z59.00 homelessness, Z59.811 housing instability, Z59.41 food
  insecurity, Z59.82 transportation, Z59.12 utilities, Z59.869 financial insecurity, Z56.0 unemployment,
  Z55.9 education, Z65.4 interpersonal safety) and a `SocialWorkReferralServiceType`.
- `ISdohScreeningGrain` (`SDOH:{guid}`) + per-patient index (`SDOH-IDX:{patientId}`) + a per-domain
  reverse-index cohort (`SDOH-COHORT:{domain}`, count for population reporting). A screening records
  trinary answers (Positive / Negative / **Unknown** — "not asked" never becomes a false positive);
  the catalog computes the positive-domain findings.
- **The closed loop** (workflow façade, suggest-and-confirm — nothing auto-fires): a positive domain can
  be one-click applied as its Z-code onto the problem list (via the existing `AddProblemAsync`, gated by
  `GMPL PROBLEM`) and opened as a referral (via the existing `CreateSocialWorkReferralAsync`). The screen
  becomes a tracked intervention, not just a note.

## Security & gating

Flag-gated (`SOCIAL_CARE`); household and SDOH reads are open. The Z-code apply requires `GMPL PROBLEM`
(it writes the problem list); referral creation is open like Social Work referrals. Household resolution
additionally depends on `PERSON_IDENTITY` — with it off or a patient unlinked, the household is empty by
design (it degrades, never guesses).

## Consequences

- New: `IHouseholdGrain` + `IPersonHouseholdIndexGrain`, `ISdohScreeningGrain` + index + cohort, one
  curated `Clinical/` catalog, two workflow façade partials, a REST controller, two Blazor pages, a seed.
  Everything downstream of a positive screen (problem list, referrals) reuses existing grains.
- Deliberately NOT built (specified as a roadmap): income-source typing, a program-agnostic case-management
  goal/outcome spine, a community-resource directory, veteran psychosocial enrichment, behavioral-health
  treatment planning (gated on a MentalHealth overlap analysis), and a shelter/bed subsystem (gated on a
  concrete homeless-veteran use case). The agency back-office (volunteer mgmt, ledger, multi-tenant
  sharing, reporting subsystem) is explicitly out of scope.
