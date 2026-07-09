// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.WebServer.Infrastructure;

/// <summary>
/// Seeds the demo inpatient units for the home institution ("500") — the explicit
/// replacement for the retired self-seeding BedBoard/WardLocationIndex grains
/// (self-seeding index grains were part of the four-sources-of-truth disease).
/// Each unit owns its rooms and beds; the capacity board fills in automatically
/// from the units' first pushes. Idempotent; runs under SYSTEM-SEED (XUPROG).
/// </summary>
public static class InpatientUnitSeed
{
    private const string Institution = "500";

    public static async Task SeedAsync(IGrainFactory grainFactory, ILogger logger)
    {
        var saved = DemoSeedHelper.SetSystemContext();
        try
        {
            IInpatientUnitGrain med3a = Unit(grainFactory, "MED-3A");
            InpatientUnitState existing = await med3a.GetAsync();
            if (existing.Beds.Count > 0)
            {
                logger.LogInformation("Inpatient units for institution {Inst} already seeded — skipping", Institution);
                return;
            }

            logger.LogInformation("Seeding inpatient units for institution {Inst}...", Institution);

            // Med-surg floors — semi-private rooms, two beds each.
            await SeedRoomedUnitAsync(grainFactory, "MED-3A", "Medical Ward 3A", "MedSurg", "INTERNAL MEDICINE",
                roomStart: 301, roomCount: 6, bedsPerRoom: 2, BedType.Regular);
            await SeedRoomedUnitAsync(grainFactory, "MED-4B", "Medical Ward 4B", "MedSurg", "INTERNAL MEDICINE",
                roomStart: 401, roomCount: 5, bedsPerRoom: 2, BedType.Regular);
            await SeedRoomedUnitAsync(grainFactory, "SURG-2C", "Surgical Ward 2C", "MedSurg", "GENERAL SURGERY",
                roomStart: 201, roomCount: 5, bedsPerRoom: 2, BedType.Regular);

            // ICU — private rooms, one bed each.
            await SeedRoomedUnitAsync(grainFactory, "ICU-1", "Intensive Care Unit", "ICU", "CRITICAL CARE",
                roomStart: 1, roomCount: 8, bedsPerRoom: 1, BedType.Icu, roomPrefix: "ICU-");

            // Telemetry — private rooms.
            await SeedRoomedUnitAsync(grainFactory, "TELE-4B", "Telemetry Unit 4B", "Telemetry", "CARDIOLOGY",
                roomStart: 451, roomCount: 8, bedsPerRoom: 1, BedType.Telemetry);

            // Psych — bed-only (no room modeling), the simplest unit shape.
            IInpatientUnitGrain psych = Unit(grainFactory, "PSYCH-5A");
            await psych.ConfigureUnitAsync("Psychiatry Unit 5A", "Psych", "PSYCHIATRY");
            for (int i = 1; i <= 10; i++)
                await psych.AddBedAsync(i.ToString(), null, BedType.Regular);

            // Observation — bed-only.
            IInpatientUnitGrain obs = Unit(grainFactory, "OBS-1");
            await obs.ConfigureUnitAsync("Observation Unit", "Observation", "EMERGENCY MEDICINE");
            for (int i = 1; i <= 6; i++)
                await obs.AddBedAsync($"OBS-{i}", null, BedType.Observation);

            // A little lifecycle variety so the bed board isn't a wall of green:
            // one bed dirty (awaiting EVS), one mid-clean, one blocked, one out of service.
            IInpatientUnitGrain med4b = Unit(grainFactory, "MED-4B");
            await med4b.MarkBedDirtyAsync("403-A");
            await med4b.MarkBedDirtyAsync("404-B");
            await med4b.StartCleaningAsync("404-B", "EVS,DEMO");
            await med3a.BlockBedAsync("306-B", "Isolation buffer for 306-A");
            await Unit(grainFactory, "TELE-4B").SetOutOfServiceAsync("458", "Telemetry monitor awaiting repair");
            await Unit(grainFactory, "ICU-1").SetBedIsolationAsync("ICU-3", BedIsolationType.Airborne);

            logger.LogInformation("Inpatient units seeded: MED-3A, MED-4B, SURG-2C, ICU-1, TELE-4B, PSYCH-5A, OBS-1 (institution {Inst})", Institution);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Error seeding inpatient units (non-fatal)");
        }
        finally
        {
            DemoSeedHelper.RestoreContext(saved);
        }
    }

    private static IInpatientUnitGrain Unit(IGrainFactory gf, string unitId)
        => gf.GetGrain<IInpatientUnitGrain>($"UNIT:{Institution}:{unitId}");

    /// <summary>Configure a unit with numbered rooms and lettered bed positions ("301-A", "301-B"...).</summary>
    private static async Task SeedRoomedUnitAsync(IGrainFactory gf, string unitId, string name,
        string unitType, string specialty, int roomStart, int roomCount, int bedsPerRoom,
        BedType bedType, string roomPrefix = "")
    {
        IInpatientUnitGrain unit = Unit(gf, unitId);
        await unit.ConfigureUnitAsync(name, unitType, specialty);
        for (int r = 0; r < roomCount; r++)
        {
            string roomId = $"{roomPrefix}{roomStart + r}";
            await unit.AddOrUpdateRoomAsync(new InpatientRoom { RoomId = roomId });
            if (bedsPerRoom == 1)
            {
                await unit.AddBedAsync(roomId, roomId, bedType);
            }
            else
            {
                for (int b = 0; b < bedsPerRoom; b++)
                    await unit.AddBedAsync($"{roomId}-{(char)('A' + b)}", roomId, bedType);
            }
        }
    }
}
