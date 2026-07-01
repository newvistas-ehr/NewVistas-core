// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.WebServer.Infrastructure;

/// <summary>
/// Seeds the demo data the specialty cover-sheet PROTOTYPE needs on P9001 (SICK,EXTREME LEE): an
/// upcoming right rotator-cuff repair (Dr. Yew, ~2 weeks out) and a recent right-shoulder MRI. P9001
/// already carries Stage IIIB melanoma (ExtremeLeeSickSeed), so the same chart exercises the Oncology
/// and Procedural layouts. Runs under SYSTEM-SEED (XUPROG); idempotent.
/// </summary>
public static class SpecialtyCoverSheetSeed
{
    private const string Pid = "P9001";
    private const string SurgeonId = "PROV-YEW", Surgeon = "Dr. Yew";

    public static async Task SeedAsync(IGrainFactory grainFactory, ILogger logger)
    {
        var saved = DemoSeedHelper.SetSystemContext();
        try
        {
            IPatientWorkflowGrain wf = grainFactory.GetGrain<IPatientWorkflowGrain>(Pid);

            PatientState patient = await wf.GetPatientAsync();
            if (string.IsNullOrEmpty(patient.Name))
            {
                logger.LogInformation("Demo patient {Id} not present — skipping specialty cover-sheet seed", Pid);
                return;
            }

            List<SurgerySummary> surgeries = await wf.GetSurgeriesAsync(20);
            if (surgeries.Any(s => s.PrincipalProcedure.Contains("rotator", StringComparison.OrdinalIgnoreCase)))
            {
                logger.LogInformation("Specialty cover-sheet demo for {Id} already seeded — skipping", Pid);
                return;
            }

            logger.LogInformation("Seeding specialty cover-sheet demo data for {Id} (rotator-cuff surgery + shoulder MRI)...", Pid);

            DateTime surgeryDate = DateTime.UtcNow.Date.AddDays(14).AddHours(7); // ~2 weeks out, 7 AM
            await wf.ScheduleSurgeryAsync(
                principalProcedure: "Rotator cuff repair, right shoulder (arthroscopic)",
                cptCode: "29827",
                dateOfOperation: surgeryDate,
                surgeonId: SurgeonId, surgeonName: Surgeon,
                anesthesiaTechnique: "General",
                surgicalSpecialty: "Orthopedic Surgery",
                preOpDiagnosis: "Full-thickness tear, right supraspinatus tendon",
                locationId: null, locationName: "Main OR",
                comments: "Elective. Pre-op anesthesia and cardiac clearance pending; reconcile with active oncology treatment plan.");

            // Recent right-shoulder MRI (resulted) — the "latest image" the surgeon leads with.
            string radId = await wf.OrderRadiologyStudyAsync(
                procedureName: "MRI Right Shoulder without contrast",
                procedureId: null, cptCode: "73221", imagingType: "MRI",
                requestingProviderId: SurgeonId, requestingProviderName: Surgeon,
                urgency: "Routine",
                clinicalHistory: "Right shoulder pain and weakness, several months; positive impingement signs.",
                reasonForStudy: "Evaluate for rotator cuff tear.",
                orderId: null, locationId: null, locationName: "Radiology");

            await wf.CompleteRadiologyAsync(radId,
                reportText: "MRI of the right shoulder without contrast. Full-thickness tear of the supraspinatus "
                    + "tendon at the footprint with approximately 2 cm of retraction. Moderate subacromial-subdeltoid "
                    + "bursitis. Mild AC joint osteoarthritis. Glenoid labrum intact.",
                impression: "Full-thickness supraspinatus tear (~2 cm retraction) with subacromial bursitis.",
                interpretingPhysicianId: "PROV-RAY", interpretingPhysicianName: "Dr. Ray (Radiology)");

            logger.LogInformation("  + specialty cover-sheet: {Id} rotator-cuff repair scheduled {Date:d} + shoulder MRI resulted", Pid, surgeryDate);
            logger.LogInformation("Specialty cover-sheet demo data for {Id} seeded successfully", Pid);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Error seeding specialty cover-sheet demo for {Id} (non-fatal)", Pid);
        }
        finally
        {
            DemoSeedHelper.RestoreContext(saved);
        }
    }
}
