# NewVistas Data Stewardship Principles

**Status:** Living document — first issued 2026-07-14
**Audience:** NewVistas developers, prospective deployment partners (NGO and community health), and anyone evaluating whether a comprehensive clinical record can be operated without betraying the patient it describes.

## Why this document exists

NewVistas is deliberately comprehensive. It carries the clinical record, but also the financial, social, and household context that VistA carried before it — means tests, SDOH screenings, housing status, family composition. That breadth is a clinical asset: a home health nurse who can see a patient's financial constraints and social situation delivers better care than one who cannot. But breadth is also a liability, and the liability lands on two parties who are not the developer: the organization that must defend the data, and the patient whose life it describes.

The failure modes run in both directions. A system that is too open leaks, invites snooping, chills patient disclosure, and turns every breach into a catastrophe. A system that is too closed reproduces a failure that is just as dangerous and far more common: the clinician who cannot see — or does not bother to look at — the record in front of them. A specialist's assistant who asks a knee-fracture patient which *shoulder* is bothering them has not been protected by access control; the information was there and nobody read it. Both failure modes injure patients. This document states the principles NewVistas uses to steer between them, notes where the current implementation already embodies them, and names the gaps we have not yet closed — because a stewardship document that only describes the parts that work is marketing, not stewardship.

## Principle 1 — The patient is the principal, not the subject

Every design question about data access should be answerable in a sentence the patient would accept if read aloud to them. Data is collected for the patient's care, held in trust, and every other use — billing, operations, research, population reporting — is secondary and must be separately justifiable.

Two concrete consequences follow. First, the patient's own preference about openness is a first-class setting, not an administrative override: `PatientAccessControlGrain` records a per-patient share preference in which *maximal openness is itself a choice a patient can make*, alongside graduated sensitivity flags and 42 CFR Part 2 consent for substance-use records. A patient who wants every treating clinician to see everything can say so; a patient who wants a psychiatric history segmented can say that too. Second, the patient is entitled to see the record and to see who has looked at it. The audit trail (`PatientAccessLog`, `GetAccessLogAsync`) exists per patient today; surfacing it *to the patient* in the portal is a named gap below.

## Principle 2 — Never let security become a treatment error

The most seductive mistake in health IT security is the hard block. It demos well and audits well, and then one night a covering physician cannot open the chart of a crashing patient. NewVistas takes the VistA position: for a clinician, sensitive records are gated by **break-the-glass, never by refusal**. `DecideAccessAsync` implements attest-and-proceed — a user without a treatment relationship must attest and justify, and the access is recorded and surfaced for review (`GetSuspiciousAccessesAsync`), but the record opens. Deterrence is achieved by visibility, not by obstruction.

The complement is that the treating team should never face friction at all. Treatment relationships are **established automatically** from the write paths where care actually happens — encounters, orders, surgery, appointments, unit assignment (`EstablishRelationshipAsync`) — rather than by a hand-curated list that is stale the day it is written. The people caring for the patient get frictionless access because the system observed them caring for the patient.

## Principle 3 — Minimum necessary means role-shaped views, not less data

The answer to "we collect too much" is not to collect less of what care requires; it is to ensure no single role sees the whole of it by default. Authorization in NewVistas is three-layered: ASP.NET Core Identity answers *who you are*; the security-key layer (`AccessControlGrain`, `AuthorizationCallFilter`, VistA File #19.1 semantics) answers *what you may do*; and `PatientAccessControlGrain` answers *whether you may see this patient*. Keys are granted and revoked with recorded who-and-why, and enforcement is fail-closed: a call with no authenticated user in context is rejected, not waved through.

The principle this layering must grow into: **clinical and administrative sight-lines are different in kind.** A revenue-cycle user needs eligibility, coverage, and claims; they do not need the psychiatric note or the SDOH screening narrative. A clinician needs the medication list; they do not need the collection status of the patient's account. The security-key mechanism can express this today; the discipline is to define and maintain key sets per role such that financial operations, clinical care, and system administration are non-overlapping by default and every overlap is a decision someone made on the record.

## Principle 4 — Collection must close a loop, or it is surveillance

Every data element NewVistas asks a clinician or patient to provide should have a downstream consumer that acts on it. The SDOH module (ADR-005) is the template: a screening records positive, negative, or *unknown* — "not asked" is never inferred as an answer — and a positive domain flows, suggest-and-confirm, onto the problem list as a billable Z-code and into a referral. The screen becomes a tracked intervention, not a filed form. Data with no consumer is pure liability: it deepens breach exposure and patient unease while helping no one. When a proposed field has no loop to close, the correct amount to collect is none.

The same principle applies to results. A lab result nobody reads is a collection without a loop — and it is the classic EHR patient-safety failure, litigated for decades. The notification layer (`INotificationGrain`, mirroring CPRS/ORB alerts) delivers, forwards, and renews alerts today; what it does not yet do is *escalate on silence*. That gap is named below.

## Principle 5 — Watch the watchers, automatically

Auditing in NewVistas is infrastructural, not per-feature: `AuditCallFilter` runs cluster-wide on protected grain methods (ONC §170.315(d)(2), (d)(10)), capturing actor, patient, domain, and action; sessions time out per §170.315(d)(5); key grants, revocations, break-the-glass events, and relationship-less accesses are all recorded. The design position: **an audit log nobody reads is a test nobody reads.** Audit data must feed standing surfaces — the suspicious-access view per patient, and (gap, below) organization-level review of anomalous access patterns — rather than waiting for a subpoena to be opened for the first time.

Auditing is also the honest answer to a hard truth: role-based control cannot prevent misuse by people whose role legitimately includes access. The nurse who looks up a neighbor holds a valid key. Only visible, reviewed, patient-inspectable audit deters that — which is one more reason the access log belongs in the patient portal.

## Principle 6 — Enforcement lives at the gateway; protect that invariant

Authorization and audit are enforced at the workflow and API gateway grains, not on internal domain grains — mirroring VistA's menu/RPC-level key checks and keeping grain-to-grain orchestration fast. This is a sound design **with one standing obligation**: internal domain grains must remain unreachable except through the workflow layer. Any future controller, background job, federation endpoint, or AI service that takes a shortcut to a domain grain silently bypasses both filters. Every code review of a new external surface should ask one question first: does this path enter through a gateway grain?

## Principle 7 — AI reads the record so the record gets read

The failure in the examining room is rarely absent data; it is unread data. A clinician with ninety seconds does not want thirty documents — they want the three sentences that matter for *this* visit: what the PCP said last month, the pending result, the open referral. NewVistas already hosts clinical AI services (`NewVistas.AI`: narrative generation, radiology finding extraction); the natural next use is a **pre-visit brief** — a role- and context-shaped summary generated from the record the viewer is already authorized to see.

The guardrails are fixed in advance. AI summarization is a *view*, never a *write*: nothing it produces enters the chart unsigned. It must operate strictly within the viewer's existing authorization — a summary must never launder segmented content past an access rule (the psychiatric note a user cannot open must not surface in their summary either). Its output must cite the source documents it drew from, one click away. And its use is audited like any other read. Under those constraints, AI is not an added invasion of the record; it is the mechanism by which the invasive record finally gets *read* — which is the only thing that makes collecting it defensible.

## Named gaps (as of 2026-07-14)

Stated plainly, because a partner deploying this system deserves to know, and because they are the roadmap:

**Portal parity and transparency.** The patient portal today shows demographics, problems, medications, allergies, immunizations, vitals, pharmacy, and messaging — but not clinical notes, lab results, or documents, and not the access log. A patient of NewVistas currently has the same complaint a patient of any large commercial EHR has: they cannot see their full record, and they cannot see who has been looking at it or why. Both are already held per patient in grain state; the work is portal surfaces, not new collection. This is the highest-leverage trust feature the system can ship.

**Escalation on unacknowledged results.** Alerts deliver, forward, and renew, but nothing yet watches for the abnormal result that sits unprocessed and escalates it — to the ordering provider's backup, the service chief, a safety queue — after a defined interval. An ordered test whose result no one acknowledges should be structurally impossible to lose quietly. This is the single most important patient-safety gap named in this document.

**Read auditing scope.** The audit filter today concentrates on clinically significant actions — orders, signatures, problem changes, sensitive-record access decisions. Routine chart *views* inside an authorized relationship are not individually audited. That is a defensible performance trade-off, but the boundary should be a documented decision, and views of specially protected categories (Part 2, psychiatric, employee-patient) deserve per-view audit even within an authorized relationship.

**Role partition audit.** Verify, as a standing exercise, that the shipped security-key sets actually produce the clinical/financial/administrative separation Principle 3 requires — that no default role bundles both sides of that line.

**Consent granularity beyond Part 2.** Part 2 consent is modeled; a general patient-directed restriction ("do not share my PCP notes with my dental provider") is not. Note-level routing controls should follow the share-preference model: patient-settable, default-open, honored at the view layer.

## The position, in one paragraph

NewVistas collects broadly because fragmentary records injure patients, and it defends that breadth with layered authorization, automatic treatment relationships, attest-don't-block emergency access, infrastructural audit, and patient-held sharing preferences. The system's obligations run in a specific order: first to the patient's safety (the right clinician sees the right information at the moment of care), second to the patient's dignity (they can see, shape, and police their own record), and third to the organization's defensibility (every access is attributable and every collection has a purpose). When those three conflict, that is the priority order. When a proposed feature serves none of the three, the data should not be collected at all.
