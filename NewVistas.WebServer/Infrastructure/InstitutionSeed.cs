// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.WebServer.Infrastructure;

/// <summary>
/// Seeds the institution directory (File #4) and the BILH-flavored multi-hospital
/// demo: the home institution "500" (absorbing the legacy "MAIN"/"INST-500" facility
/// strings), two BILH hospitals (Lahey Burlington + Lawrence General — the transfer-
/// center pair), and a 4-bed clinic proving the small-site collapse. Also creates the
/// BILH facilities' units/beds; institution 500's units come from InpatientUnitSeed.
/// Idempotent; runs under SYSTEM-SEED (XUPROG).
/// </summary>
public static class InstitutionSeed
{
    public static async Task SeedAsync(IGrainFactory grainFactory, ILogger logger)
    {
        var saved = DemoSeedHelper.SetSystemContext();
        try
        {
            IInstitutionGrain home = grainFactory.GetGrain<IInstitutionGrain>("INST:500");
            InstitutionState existing = await home.GetAsync();
            if (!string.IsNullOrEmpty(existing.Name))
            {
                logger.LogInformation("Institutions already seeded — skipping");
                return;
            }

            logger.LogInformation("Seeding institutions (File #4) + BILH multi-hospital demo...");

            // ── Home institution — absorbs all pre-institution facility strings ──
            await home.RegisterAsync("NEW VISTAS MEDICAL CENTER", InstitutionType.Hospital, "500",
                null, null, "1 Medical Center Drive", "Salem", "MA", "01970", "978-555-0100",
                new[]
                {
                    InstitutionCapabilities.Icu, InstitutionCapabilities.Telemetry,
                    InstitutionCapabilities.EmergencyDept, InstitutionCapabilities.Obstetrics,
                    InstitutionCapabilities.Nicu, InstitutionCapabilities.BehavioralHealth
                },
                new[] { "MAIN", "INST-500" });

            // ── The historical outside facilities from P9001's story (ExtremeLeeSickSeed) ──
            await Register(grainFactory, "LSH", "LITTLE SHOP OF HORRORS HOSPITAL", InstitutionType.Hospital,
                null, null, "Boston", new[] { InstitutionCapabilities.Icu, InstitutionCapabilities.EmergencyDept });
            await Register(grainFactory, "SRMC", "SUNSHINE REGIONAL MEDICAL CENTER", InstitutionType.Hospital,
                null, null, "Orlando", new[] { InstitutionCapabilities.Icu, InstitutionCapabilities.EmergencyDept });
            await Register(grainFactory, "REHAB-NSG", "NOT SO GOOD INPATIENT REHAB CENTER", InstitutionType.RehabilitationHospital,
                null, null, "Salem", new[] { InstitutionCapabilities.Rehab });

            // ── BILH — the multi-hospital health system (the transfer-center demo pair) ──
            await Register(grainFactory, "LAHEY-BURLINGTON", "LAHEY HOSPITAL & MEDICAL CENTER — BURLINGTON",
                InstitutionType.Hospital, "BILH", "BETH ISRAEL LAHEY HEALTH", "Burlington",
                new[] { InstitutionCapabilities.Icu, InstitutionCapabilities.Telemetry, InstitutionCapabilities.EmergencyDept });
            await Register(grainFactory, "LAWRENCE-GENERAL", "LAWRENCE GENERAL HOSPITAL",
                InstitutionType.Hospital, "BILH", "BETH ISRAEL LAHEY HEALTH", "Lawrence",
                new[] { InstitutionCapabilities.Telemetry, InstitutionCapabilities.EmergencyDept });
            await Register(grainFactory, "BILH-CLINIC-ANDOVER", "BILH PRIMARY CARE — ANDOVER",
                InstitutionType.Clinic, "BILH", "BETH ISRAEL LAHEY HEALTH", "Andover",
                Array.Empty<string>());

            // ── BILH units/beds ──────────────────────────────────────────────────
            // Lahey Burlington: an ICU + a med-surg floor.
            IInpatientUnitGrain lbIcu = Unit(grainFactory, "LAHEY-BURLINGTON", "ICU-1");
            await lbIcu.ConfigureUnitAsync("Medical ICU", "ICU", "CRITICAL CARE");
            for (int i = 1; i <= 8; i++)
                await lbIcu.AddBedAsync($"ICU-{i}", null, BedType.Icu);
            IInpatientUnitGrain lbMed = Unit(grainFactory, "LAHEY-BURLINGTON", "MED-4A");
            await lbMed.ConfigureUnitAsync("Medical Ward 4A", "MedSurg", "INTERNAL MEDICINE");
            for (int i = 401; i <= 406; i++)
            {
                await lbMed.AddOrUpdateRoomAsync(new InpatientRoom { RoomId = i.ToString() });
                await lbMed.AddBedAsync($"{i}-A", i.ToString(), BedType.Regular);
                await lbMed.AddBedAsync($"{i}-B", i.ToString(), BedType.Regular);
            }

            // Lawrence General: telemetry + a med-surg floor.
            IInpatientUnitGrain lgTele = Unit(grainFactory, "LAWRENCE-GENERAL", "TELE-2");
            await lgTele.ConfigureUnitAsync("Telemetry Unit 2", "Telemetry", "CARDIOLOGY");
            for (int i = 201; i <= 208; i++)
            {
                await lgTele.AddOrUpdateRoomAsync(new InpatientRoom { RoomId = i.ToString() });
                await lgTele.AddBedAsync(i.ToString(), i.ToString(), BedType.Telemetry);
            }
            IInpatientUnitGrain lgMed = Unit(grainFactory, "LAWRENCE-GENERAL", "MED-3B");
            await lgMed.ConfigureUnitAsync("Medical Ward 3B", "MedSurg", "INTERNAL MEDICINE");
            for (int i = 301; i <= 306; i++)
            {
                await lgMed.AddOrUpdateRoomAsync(new InpatientRoom { RoomId = i.ToString() });
                await lgMed.AddBedAsync($"{i}-A", i.ToString(), BedType.Regular);
                await lgMed.AddBedAsync($"{i}-B", i.ToString(), BedType.Regular);
            }

            // The 4-bed clinic — the small-site collapse, one unit, no rooms.
            IInpatientUnitGrain andover = Unit(grainFactory, "BILH-CLINIC-ANDOVER", "OBS");
            await andover.ConfigureUnitAsync("Observation", "Observation", null);
            for (int i = 1; i <= 4; i++)
                await andover.AddBedAsync(i.ToString(), null, BedType.Observation);

            // A little lifecycle variety on the BILH boards.
            await lbMed.MarkBedDirtyAsync("403-B");
            await lgTele.MarkBedDirtyAsync("206");
            await lgTele.StartCleaningAsync("206", "EVS,DEMO");
            await lgMed.BlockBedAsync("305-B", "Plumbing repair in room 305");

            logger.LogInformation("Institutions seeded: 500 (+aliases MAIN/INST-500), LSH, SRMC, REHAB-NSG, "
                + "BILH (LAHEY-BURLINGTON, LAWRENCE-GENERAL, BILH-CLINIC-ANDOVER) with units/beds");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Error seeding institutions (non-fatal)");
        }
        finally
        {
            DemoSeedHelper.RestoreContext(saved);
        }
    }

    private static IInpatientUnitGrain Unit(IGrainFactory gf, string institutionId, string unitId)
        => gf.GetGrain<IInpatientUnitGrain>($"UNIT:{institutionId}:{unitId}");

    private static Task Register(IGrainFactory gf, string id, string name, InstitutionType type,
        string? healthSystemId, string? healthSystemName, string city, string[] capabilities)
        => gf.GetGrain<IInstitutionGrain>($"INST:{id}")
            .RegisterAsync(name, type, null, healthSystemId, healthSystemName,
                null, city, "MA", null, null, capabilities, null);
}
