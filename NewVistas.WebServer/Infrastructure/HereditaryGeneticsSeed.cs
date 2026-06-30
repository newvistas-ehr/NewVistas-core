// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.WebServer.Infrastructure;

/// <summary>
/// Seeds a hereditary-cancer demo patient — HEREDITARY,HOPE (grain key P9004) — exercising the whole
/// genetics module: an interpreted germline genetic test report (BRCA1 pathogenic + an ATM VUS) and a
/// structured 3-generation family history (early-onset breast, ovarian, and pancreatic cancer) that
/// together drive the hereditary-risk assessment (HBOC finding) and the family-history referral
/// red-flags. Runs under SYSTEM-SEED (XUPROG); idempotent.
/// </summary>
public static class HereditaryGeneticsSeed
{
    private const string Pid = "P9004";
    private const string GeneticistId = "PROV-GENETICS", Geneticist = "Dr. Helix (Genetics)";
    private const string Lab = "Invitae";

    public static async Task SeedAsync(IGrainFactory grainFactory, ILogger logger)
    {
        var saved = DemoSeedHelper.SetSystemContext();
        try
        {
            IPatientWorkflowGrain wf = grainFactory.GetGrain<IPatientWorkflowGrain>(Pid);

            PatientState existing = await wf.GetPatientAsync();
            if (!string.IsNullOrEmpty(existing.Name))
            {
                logger.LogInformation("Demo patient {Id} ({Name}) already exists — skipping hereditary-genetics seed", Pid, existing.Name);
                return;
            }

            logger.LogInformation("Seeding hereditary-genetics demo patient {Id} (HEREDITARY,HOPE)...", Pid);

            // ── Demographics ────────────────────────────────────────────────────────
            await wf.UpdateDemographicsAsync("HEREDITARY,HOPE", "F", new DateTime(1984, 3, 14), "666009004");
            await wf.UpdateAddressAsync("7 Birch Lane", null, null, "Andover", "MA", "01810");
            await wf.UpdateContactInfoAsync("978-555-0144", null, "hope.hereditary@newvistas.demo");
            await wf.UpdateMaritalStatusAsync("MARRIED");

            DateTime reportDate = DateTime.UtcNow.Date.AddMonths(-3);
            DateTime collected = reportDate.AddDays(-12);

            // ── Genetic test report (germline hereditary-cancer panel) ──────────────
            string reportId = await wf.RecordGeneticTestReportAsync(
                testName: "Hereditary Cancer Panel (84 genes)",
                lab: Lab,
                method: GeneticTestMethod.NextGenSequencing,
                indication: "Strong family history of early-onset breast and ovarian cancer.",
                collectionDate: collected,
                reportDate: reportDate,
                overallResult: GeneticReportResult.PositivePathogenic,
                orderingProvider: Geneticist,
                notes: "Germline panel; cascade testing offered to at-risk relatives.",
                recordedBy: Geneticist);

            // Pathogenic BRCA1 founder variant → HBOC.
            await wf.AddGeneticVariantAsync(reportId, "BRCA1", "c.68_69delAG", "p.Glu23ValfsTer17", "NM_007294.4",
                VariantClassification.Pathogenic, VariantZygosity.Heterozygous, VariantOrigin.Germline,
                "VCV000017661", "rs80357914", "Founder frameshift; loss of function.");
            // An incidental VUS — recorded but not actionable (no hereditary finding).
            await wf.AddGeneticVariantAsync(reportId, "ATM", "c.3161C>G", "p.Pro1054Arg", "NM_000051.4",
                VariantClassification.UncertainSignificance, VariantZygosity.Heterozygous, VariantOrigin.Germline,
                "", "", "VUS — no management change; reclassification follow-up.");

            // ── Family history (3 generations, maternal cancer clustering) ──────────
            string mother = await wf.AddFamilyMemberAsync(FamilyRelationship.Mother, "—", "F",
                FamilyVitalStatus.Deceased, null, 52, "Breast cancer", "Diagnosed at 44.");
            await wf.AddFamilyConditionAsync(mother, "Breast cancer", "C50.9", 44, "ER+; bilateral.");

            string aunt = await wf.AddFamilyMemberAsync(FamilyRelationship.MaternalAunt, "—", "F",
                FamilyVitalStatus.Alive, 67, null, string.Empty, "Maternal side.");
            await wf.AddFamilyConditionAsync(aunt, "Ovarian cancer", "C56.9", 58, "High-grade serous.");

            string grandmother = await wf.AddFamilyMemberAsync(FamilyRelationship.MaternalGrandmother, "—", "F",
                FamilyVitalStatus.Deceased, null, 71, "Breast cancer", string.Empty);
            await wf.AddFamilyConditionAsync(grandmother, "Breast cancer", "C50.9", 60, string.Empty);

            string uncle = await wf.AddFamilyMemberAsync(FamilyRelationship.MaternalUncle, "—", "M",
                FamilyVitalStatus.Deceased, null, 66, "Pancreatic cancer", string.Empty);
            await wf.AddFamilyConditionAsync(uncle, "Pancreatic cancer", "C25.9", 65, string.Empty);

            logger.LogInformation("  + hereditary-genetics: {Id} BRCA1 c.68_69delAG pathogenic (HBOC) + ATM VUS; maternal breast/ovarian/pancreatic family history (referral red-flags)", Pid);
            logger.LogInformation("Hereditary-genetics demo patient {Id} (HEREDITARY,HOPE) seeded successfully", Pid);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Error seeding hereditary-genetics demo patient {Id} (non-fatal)", Pid);
        }
        finally
        {
            DemoSeedHelper.RestoreContext(saved);
        }
    }
}
