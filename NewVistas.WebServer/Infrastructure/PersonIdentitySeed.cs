// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.WebServer.Infrastructure;

/// <summary>
/// Seeds the Person-identity (ADR-002) demo — the two cross-role cases:
///   1. NURSE-WHO-IS-A-PATIENT: NIGHTINGALE,NORA — patient chart P9005 + a staff record
///      (USER:STAFF-NORA, Nursing) linked to one Person → flagged employee-patient (sensitive).
///   2. PATIENT-WHO-IS-A-RELATIVE: KINDRED,KAY — patient chart P9006 who is also the "Mother"
///      family-history entry on her daughter KINDRED,KIM's chart (P9007), linked to one Person.
/// Demonstrates that one human's patient / staff / relative representations resolve to a single Person.
/// Runs under SYSTEM-SEED (XUPROG); idempotent.
/// </summary>
public static class PersonIdentitySeed
{
    private const string Facility = "500";
    private const string By = "SYSTEM-SEED";

    public static async Task SeedAsync(IGrainFactory grainFactory, ILogger logger)
    {
        var saved = DemoSeedHelper.SetSystemContext();
        try
        {
            IPatientWorkflowGrain nora = grainFactory.GetGrain<IPatientWorkflowGrain>("P9005");
            PatientState existing = await nora.GetPatientAsync();
            if (!string.IsNullOrEmpty(existing.Name))
            {
                logger.LogInformation("Person-identity demo (P9005) already seeded — skipping");
                return;
            }

            logger.LogInformation("Seeding Person-identity (ADR-002) demo — nurse-patient + mother-patient-relative...");

            // ── Case 1: nurse who is also a patient ──────────────────────────────
            await nora.UpdateDemographicsAsync("NIGHTINGALE,NORA", "F", new DateTime(1988, 6, 12), "666009005");
            await nora.UpdateAddressAsync("3 Lamp Lane", null, null, "Lowell", "MA", "01852");

            // Her staff/provider record (File #200) — she works here as a med-surg nurse.
            await grainFactory.GetGrain<INewPersonGrain>("USER:STAFF-NORA").UpdateProfileAsync(
                name: "NIGHTINGALE,NORA", title: "Registered Nurse", degree: "RN",
                serviceSection: "NURSING", userClass: "NURSE", providerType: "NURSE",
                specialty: "Medical-Surgical", institutionId: "INST-500", institutionName: "VA MEDICAL CENTER",
                divisionId: "DIV-500", divisionName: "MAIN DIVISION");

            // One Person = her chart + her staff record → employee-patient.
            string personNora = await nora.CreateOrGetPersonForPatientAsync(Facility, PersonLinkConfidence.ConfirmedByRegistration, By);
            await grainFactory.GetGrain<IPersonGrain>(personNora)
                .LinkStaffAsync("STAFF-NORA", PersonLinkConfidence.ConfirmedByRegistration, By);

            // Phase 4: linking both roles auto-flagged her chart sensitive (EMPLOYEE). Give her a
            // treating provider so the frictionless-team vs break-the-glass contrast is demonstrable.
            await grainFactory.GetGrain<IPatientAccessControlGrain>("PAC:P9005").AddAuthorizedProviderAsync("STAFF-NORA-DOC");
            PatientAccessControlState noraPac = await grainFactory.GetGrain<IPatientAccessControlGrain>("PAC:P9005").GetAccessControlAsync();
            logger.LogInformation("  + nurse-patient: Person {P} = patient P9005 + staff USER:STAFF-NORA (employee-patient; chart sensitive={S}, categories=[{C}])",
                personNora, noraPac.IsSensitive, string.Join(",", noraPac.SensitivityCategories));

            // ── Case 2: mother who is a patient AND a relative on her child's chart ─
            IPatientWorkflowGrain kay = grainFactory.GetGrain<IPatientWorkflowGrain>("P9006");
            await kay.UpdateDemographicsAsync("KINDRED,KAY", "F", new DateTime(1962, 2, 3), "666009006");

            IPatientWorkflowGrain kim = grainFactory.GetGrain<IPatientWorkflowGrain>("P9007");
            await kim.UpdateDemographicsAsync("KINDRED,KIM", "F", new DateTime(1990, 9, 21), "666009007");

            // KIM's chart records her mother (KAY) in structured family history.
            string motherEntryId = await kim.AddFamilyMemberAsync(
                FamilyRelationship.Mother, "KINDRED,KAY", "F", FamilyVitalStatus.Alive, 62, null, string.Empty,
                "Also a patient at this facility.");
            await kim.AddFamilyConditionAsync(motherEntryId, "Breast cancer", "C50.9", 48, "ER+.");

            // KAY gets her own Person (from her chart), then KIM's family entry is linked to it.
            string personKay = await kay.CreateOrGetPersonForPatientAsync(Facility, PersonLinkConfidence.ConfirmedByRegistration, By);
            await kim.LinkFamilyMemberToPersonAsync(motherEntryId, personKay, By);
            logger.LogInformation("  + mother-patient-relative: Person {P} = patient P9006 (KAY) + 'Mother' relative on P9007 (KIM)'s chart", personKay);

            // Phase 4: the mirror of sensitivity — a patient who opts INTO open sharing for
            // teaching/research (the "next Jim Smyth" stance: maximal openness as a first-class choice).
            await grainFactory.GetGrain<IPatientWorkflowGrain>("P9001")
                .SetPatientSharePreferenceAsync(PatientSharePreference.OpenForTeachingAndResearch);
            logger.LogInformation("  + open-sharing: P9001 opted into open sharing (teaching/research) — access allowed without break-the-glass, still audited");

            logger.LogInformation("Person-identity (ADR-002) demo seeded successfully");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Error seeding Person-identity demo (non-fatal)");
        }
        finally
        {
            DemoSeedHelper.RestoreContext(saved);
        }
    }
}
