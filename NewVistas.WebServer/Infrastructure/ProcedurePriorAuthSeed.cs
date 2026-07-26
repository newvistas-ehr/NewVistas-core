// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.WebServer.Infrastructure;

/// <summary>
/// Seeds the procedure prior-authorization demo: Blue Cross Blue Shield of Florida in the payer
/// directory, and on P9001 a TKA (CPT 27447) PA that was DENIED for missing conservative-therapy +
/// imaging documentation, then resubmitted with the docs and APPROVED. That denial teaches the learned
/// KB, so <c>/prior-auth</c> "check requirements" for 27447 + BCBS-FL ranks those two categories to the
/// top — the "fill these boxes" tool demos live. Runs under SYSTEM-SEED; idempotent.
/// </summary>
public static class ProcedurePriorAuthSeed
{
    private const string Pid = "P9001";

    public static async Task SeedAsync(IGrainFactory grainFactory, ILogger logger)
    {
        var saved = DemoSeedHelper.SetSystemContext();
        try
        {
            await grainFactory.GetGrain<ISiteParametersGrain>("SITE:DEFAULT").EnableFeatureAsync("PROCEDURE_PRIOR_AUTH");

            // Blue Cross Blue Shield of Florida in the payer directory (idempotent upsert).
            await grainFactory.GetGrain<IPayerConfigIndexGrain>("PAYER-CFG-INDEX").AddOrUpdateAsync(
                new PayerConfigIndexEntry { PayerId = "PAYER-BCBS-FL", PayerName = "Blue Cross Blue Shield of Florida", SupportsRealTimeEligibility = true, IsActive = true });

            IPatientWorkflowGrain wf = grainFactory.GetGrain<IPatientWorkflowGrain>(Pid);
            if ((await wf.GetProcedureAuthsAsync()).Count > 0)
            {
                logger.LogInformation("Procedure prior-auth demo already seeded — skipping");
                return;
            }

            logger.LogInformation("Seeding procedure prior-auth demo (P9001 TKA → BCBS-FL: denied then approved)...");

            // 1) TKA phoned in to BCBS-FL, DENIED for missing conservative-therapy + imaging docs.
            string deniedId = await wf.SubmitProcedureAuthAsync(
                "27447", "Total knee arthroplasty", "PAYER-BCBS-FL", "Blue Cross Blue Shield of Florida",
                "PROV-007", "Dr. Sarah Lee", new List<string> { "M17.11" },
                "Severe primary osteoarthritis, right knee; failing function.", null, null,
                ProcedureAuthSubmissionChannel.Phone, null, null, null);
            await wf.DenyProcedureAuthAsync(deniedId, "UM-1", "UM Nurse", new List<ProcedureDenialReason>
            {
                new() { Category = PriorAuthRequirementCategory.ConservativeTherapyTrial, ReasonText = "No documented 3-month conservative therapy." },
                new() { Category = PriorAuthRequirementCategory.ImagingEvidence, ReasonText = "Weight-bearing radiographs not attached." }
            });

            // 2) Resubmitted with the missing docs → APPROVED.
            string approvedId = await wf.SubmitProcedureAuthAsync(
                "27447", "Total knee arthroplasty", "PAYER-BCBS-FL", "Blue Cross Blue Shield of Florida",
                "PROV-007", "Dr. Sarah Lee", new List<string> { "M17.11" },
                "OA right knee; 4 months PT + NSAIDs + injections failed; weight-bearing films show grade-4 JSN.", null, null,
                ProcedureAuthSubmissionChannel.PayerPortal, null, null, null);
            await wf.ApproveProcedureAuthAsync(approvedId, "UM-1", "UM Nurse", "AUTH-BCBSFL-88213",
                new DateTime(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc),
                new List<PriorAuthRequirementCategory> { PriorAuthRequirementCategory.ConservativeTherapyTrial, PriorAuthRequirementCategory.ImagingEvidence });

            logger.LogInformation("Procedure prior-auth demo seeded: P9001 27447→BCBS-FL (denied {D} + approved {A}); checklist now learned", deniedId, approvedId);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Error seeding procedure prior-auth demo (non-fatal)");
        }
        finally
        {
            DemoSeedHelper.RestoreContext(saved);
        }
    }
}
