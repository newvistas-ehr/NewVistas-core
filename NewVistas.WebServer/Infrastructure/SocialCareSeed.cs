// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.WebServer.Infrastructure;

/// <summary>
/// Seeds the Whole-Person Social Care demo (ADR-005): a demo patient (P9301) in a Person-anchored
/// household with a non-patient child, plus a positive AHC-HRSN SDOH screen whose loop is closed —
/// the mapped Z-codes (food + housing) are on the problem list and matching Social Work referrals are
/// open. Demonstrates the whole thesis: screen → coded need → intervention. Runs under SYSTEM-SEED
/// (XUPROG); idempotent (keyed off the patient's household existing).
/// </summary>
public static class SocialCareSeed
{
    private const string Pid = "P9301";

    public static async Task SeedAsync(IGrainFactory grainFactory, ILogger logger)
    {
        var saved = DemoSeedHelper.SetSystemContext();
        try
        {
            // Ensure the feature is on for the seed path (it is on by default).
            await grainFactory.GetGrain<ISiteParametersGrain>("SITE:DEFAULT").EnableFeatureAsync("SOCIAL_CARE");

            IPatientWorkflowGrain wf = grainFactory.GetGrain<IPatientWorkflowGrain>(Pid);
            if ((await wf.GetPatientHouseholdAsync()) is not null)
            {
                logger.LogInformation("Social Care demo already seeded — skipping");
                return;
            }

            logger.LogInformation("Seeding Whole-Person Social Care demo (P9301 household + closed-loop SDOH screen)...");

            // ── Patient + household ──────────────────────────────────────────
            if (string.IsNullOrEmpty((await wf.GetPatientAsync()).Name))
                await wf.UpdateDemographicsAsync("SOCIAL,SAM", "M", new DateTime(1979, 9, 12), null);

            string householdId = await wf.CreateHouseholdForPatientAsync("Social Household", "Self", "500", "SEED");
            await wf.AddNonPatientMemberToHouseholdAsync(
                householdId, "SOCIAL,SUSIE", new DateTime(2015, 4, 3), "F", "", "Daughter", HouseholdMemberRole.Child, "SEED");
            await grainFactory.GetGrain<IHouseholdGrain>(householdId)
                .SetHousingAsync(HouseholdHousingType.Rented, "14 Maple St", "Salem", "MA", "01970", "SEED");

            // ── Positive SDOH screen → close the loop ────────────────────────
            string screeningId = await wf.RecordSdohScreeningAsync("AHC-HRSN", new()
            {
                new SdohScreeningResponse { Domain = SdohDomain.FoodInsecurity, Response = SdohResponse.Positive },
                new SdohScreeningResponse { Domain = SdohDomain.HousingInstability, Response = SdohResponse.Positive },
                new SdohScreeningResponse { Domain = SdohDomain.TransportationInsecurity, Response = SdohResponse.Negative },
                new SdohScreeningResponse { Domain = SdohDomain.Employment, Response = SdohResponse.Negative }
            }, "SEED");

            // Add the mapped Z-codes to the problem list and open matching referrals.
            await wf.AddSdohProblemAsync(screeningId, SdohDomain.FoodInsecurity, "SEED");
            await wf.AddSdohProblemAsync(screeningId, SdohDomain.HousingInstability, "SEED");
            await wf.CreateSdohReferralAsync(screeningId, SdohDomain.FoodInsecurity, "SEED");
            await wf.CreateSdohReferralAsync(screeningId, SdohDomain.HousingInstability, "SEED");

            logger.LogInformation("Social Care demo seeded: {Pid} household {Hh} + closed-loop SDOH screen (Z59.41, Z59.811 + 2 referrals)",
                Pid, householdId);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Error seeding Social Care demo (non-fatal)");
        }
        finally
        {
            DemoSeedHelper.RestoreContext(saved);
        }
    }
}
