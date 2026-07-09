// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.WebServer.Infrastructure;

/// <summary>
/// Seeds the Transfer Center demo: P9008 "TRANSFERRE,TERRY" admitted to the Lahey
/// Burlington ICU with an in-flight REQUESTED transfer to Lawrence General telemetry —
/// so Lawrence's Transfer Center shows one actionable incoming request on first login
/// (accept with the bed picker → complete arrival). Requires InstitutionSeed.
/// Idempotent; runs under SYSTEM-SEED (XUPROG).
/// </summary>
public static class InterfacilityTransferSeed
{
    private const string Pid = "P9008";

    public static async Task SeedAsync(IGrainFactory grainFactory, ILogger logger)
    {
        var saved = DemoSeedHelper.SetSystemContext();
        try
        {
            IPatientWorkflowGrain wf = grainFactory.GetGrain<IPatientWorkflowGrain>(Pid);
            PatientState existing = await wf.GetPatientAsync();
            if (!string.IsNullOrEmpty(existing.Name))
            {
                logger.LogInformation("Transfer demo patient {Id} already seeded — skipping", Pid);
                return;
            }

            logger.LogInformation("Seeding inter-facility transfer demo ({Id} TRANSFERRE,TERRY, Burlington → Lawrence)...", Pid);

            await wf.UpdateDemographicsAsync("TRANSFERRE,TERRY", "M", new DateTime(1958, 4, 2), "666009008");
            await wf.UpdateAddressAsync("77 Winn Street", null, null, "Burlington", "MA", "01803");

            // Admitted to the Lahey Burlington ICU two days ago (NSTEMI, now stable).
            string admissionId = await wf.RecordAdmissionAsync(
                DateTime.UtcNow.AddDays(-2), "LAHEY-BURLINGTON", "ICU-1", "ICU-2",
                "CRITICAL CARE", "STAFF-LB-INTENSIVIST", "HALSTED,WILLIAM MD",
                "Non-ST-elevation myocardial infarction",
                "Admitted via the ED with NSTEMI; treated medically, now hemodynamically stable.");

            // The in-flight ask: Burlington wants to reposition him to Lawrence General
            // telemetry (closer to home, ICU bed pressure). Left in REQUESTED so the
            // Lawrence Transfer Center has an actionable incoming request.
            string transferId = await wf.RequestInterfacilityTransferAsync(
                "LAHEY-BURLINGTON", "ICU-1", admissionId,
                "STAFF-LB-INTENSIVIST", "HALSTED,WILLIAM MD",
                "LAWRENCE-GENERAL",
                "TELEMETRY", BedType.Telemetry, BedIsolationType.None,
                "URGENT",
                "68M NSTEMI day 2, medically managed, hemodynamically stable off drips for 24h. "
                + "Needs continued telemetry monitoring and cardiology follow-up; family is in Lawrence.",
                "ICU bed pressure at Burlington; step-down telemetry appropriate and closer to home.");

            logger.LogInformation("  + {Id} admitted at LAHEY-BURLINGTON ICU-1/ICU-2; transfer {Xfer} REQUESTED → LAWRENCE-GENERAL (telemetry)",
                Pid, transferId);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Error seeding inter-facility transfer demo (non-fatal)");
        }
        finally
        {
            DemoSeedHelper.RestoreContext(saved);
        }
    }
}
