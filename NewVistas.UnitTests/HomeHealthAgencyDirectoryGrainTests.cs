// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NUnit.Framework;
using Orleans.TestingHost;

namespace NewVistas.UnitTests;

/// <summary>
/// Tests the singleton home-health agency directory (the catalog an externally-delivered home-care
/// episode points at). The directory is a shared singleton that auto-seeds a demo set on first read,
/// so each test uses unique ids and asserts membership, never exact totals.
/// </summary>
[TestFixture]
public class HomeHealthAgencyDirectoryGrainTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup() => _cluster = SharedCluster.Instance;

    private IHomeHealthAgencyDirectoryGrain Directory()
        => _cluster.GrainFactory.GetGrain<IHomeHealthAgencyDirectoryGrain>("HHA-DIRECTORY");

    private static string Unique(string stem) => stem + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();

    [Test]
    public async Task AutoSeeds_InHouseAndExternalAgencies()
    {
        // First read triggers the demo seed; the in-house agency is present.
        HomeHealthAgencyEntry? inHouse = await Directory().GetAsync("HHA-NEWVISTAS");
        Assert.That(inHouse, Is.Not.Null);
        Assert.That(inHouse!.Kind, Is.EqualTo(HomeHealthAgencyKinds.InHouse));

        HomeHealthAgencyEntry? valley = await Directory().GetAsync("HHA-VALLEY-VNA");
        Assert.That(valley, Is.Not.Null);
        Assert.That(valley!.Kind, Is.EqualTo(HomeHealthAgencyKinds.External));
        Assert.That(valley.Ccn, Is.EqualTo("227312"));
    }

    [Test]
    public async Task ExternalOnly_ExcludesInHouseAgency()
    {
        List<HomeHealthAgencyEntry> external = await Directory().GetAllAsync(externalOnly: true);
        Assert.That(external.Any(a => a.Kind == HomeHealthAgencyKinds.InHouse), Is.False);
        Assert.That(external.Select(a => a.AgencyId), Contains.Item("HHA-VALLEY-VNA"));
    }

    [Test]
    public async Task AddSearchAndDeactivate_RoundTrips()
    {
        string id = Unique("HHA-");
        string name = Unique("ZZAGENCY ");
        await Directory().AddOrUpdateAsync(new HomeHealthAgencyEntry
        {
            AgencyId = id, Name = name, Kind = HomeHealthAgencyKinds.External, Npi = "1999999992",
            Disciplines = new() { HomeCareDiscipline.SkilledNursing }
        });

        List<HomeHealthAgencyEntry> hits = await Directory().SearchAsync(name[..8]);
        Assert.That(hits.Select(a => a.AgencyId), Contains.Item(id));

        // Deactivate → drops out of search/list.
        await Directory().SetActiveAsync(id, false);
        List<HomeHealthAgencyEntry> after = await Directory().SearchAsync(name[..8]);
        Assert.That(after.Select(a => a.AgencyId), Does.Not.Contain(id));
    }
}
