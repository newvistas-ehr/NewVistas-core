// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NUnit.Framework;
using Orleans.TestingHost;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.FunctionalTests;

/// <summary>
/// ADR-003 institutions + system-wide capacity: the File #4 directory (health-system
/// grouping, legacy alias resolution) and the SYSTEM-CAPACITY fan-out (aggregation +
/// placement-target filtering by capability/bed-type/AcceptsInboundTransfers).
/// </summary>
[TestFixture]
public class SystemCapacityTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup() => _cluster = SharedCluster.Instance;

    private IInstitutionIndexGrain Index()
        => _cluster.GrainFactory.GetGrain<IInstitutionIndexGrain>("INSTITUTION-INDEX");

    private ISystemCapacityGrain System()
        => _cluster.GrainFactory.GetGrain<ISystemCapacityGrain>("SYSTEM-CAPACITY");

    private IInpatientUnitGrain Unit(string inst, string unit)
        => _cluster.GrainFactory.GetGrain<IInpatientUnitGrain>($"UNIT:{inst}:{unit}");

    private async Task<string> RegisterAsync(string prefix, string healthSystemId,
        string[] capabilities, bool acceptsTransfers = true, string[]? aliases = null)
    {
        string id = $"{prefix}-{Guid.NewGuid().ToString("N")[..8]}";
        IInstitutionGrain grain = _cluster.GrainFactory.GetGrain<IInstitutionGrain>($"INST:{id}");
        await grain.RegisterAsync($"{prefix} HOSPITAL {id}", InstitutionType.Hospital, null,
            healthSystemId, $"{healthSystemId} HEALTH", null, "Boston", "MA", null, null,
            capabilities, aliases);
        if (!acceptsTransfers)
            await grain.SetAcceptsInboundTransfersAsync(false);
        return id;
    }

    [Test]
    public async Task Institution_Register_IndexesAndResolvesLegacyAliases()
    {
        string tag = Guid.NewGuid().ToString("N")[..8];
        string id = await RegisterAsync("ALIAS", $"HS-{tag}",
            new[] { InstitutionCapabilities.Icu }, aliases: new[] { $"OLD-{tag}", $"LEGACY-{tag}" });

        // Canonical id resolves to itself; both aliases resolve to the canonical id.
        Assert.That(await Index().ResolveLegacyFacilityIdAsync(id), Is.EqualTo(id));
        Assert.That(await Index().ResolveLegacyFacilityIdAsync($"OLD-{tag}"), Is.EqualTo(id));
        Assert.That(await Index().ResolveLegacyFacilityIdAsync($"LEGACY-{tag}"), Is.EqualTo(id));
        Assert.That(await Index().ResolveLegacyFacilityIdAsync($"UNKNOWN-{tag}"), Is.Null);
    }

    [Test]
    public async Task Institution_HealthSystemGrouping()
    {
        string hs = $"HS-{Guid.NewGuid():N}"[..12];
        string a = await RegisterAsync("GRP-A", hs, new[] { InstitutionCapabilities.Icu });
        string b = await RegisterAsync("GRP-B", hs, new[] { InstitutionCapabilities.Telemetry });
        await RegisterAsync("GRP-C", $"OTHER-{Guid.NewGuid():N}"[..12], Array.Empty<string>());

        List<InstitutionIndexEntry> members = await Index().GetByHealthSystemAsync(hs);

        Assert.That(members.Select(m => m.InstitutionId), Is.EquivalentTo(new[] { a, b }));
    }

    [Test]
    public async Task SystemCapacity_AggregatesAcrossInstitutions()
    {
        string hs = $"HS-{Guid.NewGuid():N}"[..12];
        string a = await RegisterAsync("CAP-A", hs, new[] { InstitutionCapabilities.Icu });
        string b = await RegisterAsync("CAP-B", hs, new[] { InstitutionCapabilities.Telemetry });

        IInpatientUnitGrain unitA = Unit(a, "ICU");
        await unitA.ConfigureUnitAsync("ICU", "ICU", null);
        await unitA.AddBedAsync("1", null, BedType.Icu);
        await unitA.AddBedAsync("2", null, BedType.Icu);

        IInpatientUnitGrain unitB = Unit(b, "TELE");
        await unitB.ConfigureUnitAsync("Telemetry", "Telemetry", null);
        await unitB.AddBedAsync("1", null, BedType.Telemetry);
        await unitB.MarkBedDirtyAsync("1"); // not placeable

        SystemCapacitySnapshot snapshot = await System().GetSystemCapacityAsync(hs);

        Assert.That(snapshot.Institutions.Select(i => i.InstitutionId), Is.EquivalentTo(new[] { a, b }));
        InstitutionCapacitySummary instA = snapshot.Institutions.Single(i => i.InstitutionId == a);
        InstitutionCapacitySummary instB = snapshot.Institutions.Single(i => i.InstitutionId == b);
        Assert.That(instA.TotalBeds, Is.EqualTo(2));
        Assert.That(instA.Available, Is.EqualTo(2));
        Assert.That(instB.TotalBeds, Is.EqualTo(1));
        Assert.That(instB.Available, Is.EqualTo(0));   // dirty ≠ placeable
        Assert.That(instB.Dirty, Is.EqualTo(1));
    }

    [Test]
    public async Task PlacementTargets_FilterByBedType_Capability_AndAcceptance()
    {
        // Unique health system per test run isn't available on FindPlacementTargetsAsync
        // (it searches ALL institutions), so use unique ids and assert membership only.
        string hs = $"HS-{Guid.NewGuid():N}"[..12];
        string icuHospital = await RegisterAsync("PT-ICU", hs, new[] { InstitutionCapabilities.Icu });
        string teleHospital = await RegisterAsync("PT-TELE", hs, new[] { InstitutionCapabilities.Telemetry });
        string divertedHospital = await RegisterAsync("PT-DIVERT", hs,
            new[] { InstitutionCapabilities.Icu }, acceptsTransfers: false);

        IInpatientUnitGrain icuUnit = Unit(icuHospital, "ICU");
        await icuUnit.ConfigureUnitAsync("ICU", "ICU", null);
        await icuUnit.AddBedAsync("1", null, BedType.Icu);

        IInpatientUnitGrain teleUnit = Unit(teleHospital, "TELE");
        await teleUnit.ConfigureUnitAsync("Telemetry", "Telemetry", null);
        await teleUnit.AddBedAsync("1", null, BedType.Telemetry);

        IInpatientUnitGrain divertedUnit = Unit(divertedHospital, "ICU");
        await divertedUnit.ConfigureUnitAsync("ICU", "ICU", null);
        await divertedUnit.AddBedAsync("1", null, BedType.Icu);

        // By bed type: only the hospital with a placeable ICU bed (diverted one excluded).
        List<InstitutionCapacitySummary> icuTargets = await System().FindPlacementTargetsAsync(BedType.Icu, null);
        Assert.That(icuTargets.Select(t => t.InstitutionId), Contains.Item(icuHospital));
        Assert.That(icuTargets.Select(t => t.InstitutionId), Does.Not.Contain(teleHospital));
        Assert.That(icuTargets.Select(t => t.InstitutionId), Does.Not.Contain(divertedHospital));

        // By capability.
        List<InstitutionCapacitySummary> teleCapable = await System()
            .FindPlacementTargetsAsync(null, InstitutionCapabilities.Telemetry);
        Assert.That(teleCapable.Select(t => t.InstitutionId), Contains.Item(teleHospital));
        Assert.That(teleCapable.Select(t => t.InstitutionId), Does.Not.Contain(icuHospital));

        // A placeable bed is required: dirty the tele bed and the hospital drops out.
        await teleUnit.MarkBedDirtyAsync("1");
        List<InstitutionCapacitySummary> afterDirty = await System().FindPlacementTargetsAsync(BedType.Telemetry, null);
        Assert.That(afterDirty.Select(t => t.InstitutionId), Does.Not.Contain(teleHospital));
    }

    [Test]
    public async Task TransferCenter_SelfHideSignal_ActiveCount()
    {
        // The index counts every active institution — at least the ones this test adds.
        string hs = $"HS-{Guid.NewGuid():N}"[..12];
        await RegisterAsync("CNT-A", hs, Array.Empty<string>());
        await RegisterAsync("CNT-B", hs, Array.Empty<string>());

        Assert.That(await Index().GetActiveCountAsync(), Is.GreaterThanOrEqualTo(2));
    }
}
