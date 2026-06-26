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
/// Tests the singleton provider directory used for "look up a provider by name"
/// (e.g. a nurse selecting the physician an order is for). The directory is a
/// shared singleton, so each test uses unique names to stay deterministic.
/// </summary>
[TestFixture]
public class ProviderDirectoryGrainTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup() => _cluster = SharedCluster.Instance;

    private IProviderDirectoryGrain Directory()
        => _cluster.GrainFactory.GetGrain<IProviderDirectoryGrain>("PROVIDER-DIRECTORY");

    private static string Unique(string stem) => stem + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();

    [Test]
    public async Task ProviderDirectory_SearchFindsByLastNamePrefix()
    {
        string id = Guid.NewGuid().ToString();
        string last = Unique("ZZLAST");
        await Directory().AddOrUpdateAsync(new ProviderDirectoryEntry
        {
            UserId = id, Name = $"{last},ALICE", Title = "Staff Physician",
            ProviderType = "PHYSICIAN", Specialty = "Cardiology", IsActive = true
        });

        List<ProviderDirectoryEntry> results = await Directory().SearchAsync(last[..6]);

        Assert.That(results.Select(r => r.UserId), Contains.Item(id));
    }

    [Test]
    public async Task ProviderDirectory_SearchMatchesFirstNameFragment()
    {
        string id = Guid.NewGuid().ToString();
        string first = Unique("QFIRST");
        await Directory().AddOrUpdateAsync(new ProviderDirectoryEntry
        {
            UserId = id, Name = $"JONES,{first}", IsActive = true
        });

        // Substring match means a first-name fragment also finds the provider.
        List<ProviderDirectoryEntry> results = await Directory().SearchAsync(first);

        Assert.That(results.Select(r => r.UserId), Contains.Item(id));
    }

    [Test]
    public async Task ProviderDirectory_SetActiveFalse_ExcludesFromSearch()
    {
        string id = Guid.NewGuid().ToString();
        string last = Unique("INACT");
        await Directory().AddOrUpdateAsync(new ProviderDirectoryEntry
        {
            UserId = id, Name = $"{last},BOB", IsActive = true
        });
        Assert.That((await Directory().SearchAsync(last)).Select(r => r.UserId), Contains.Item(id));

        await Directory().SetActiveAsync(id, false);

        Assert.That((await Directory().SearchAsync(last)).Select(r => r.UserId), Does.Not.Contain(id));
    }

    [Test]
    public async Task ProviderDirectory_GetByExactUserId()
    {
        string id = Guid.NewGuid().ToString();
        await Directory().AddOrUpdateAsync(new ProviderDirectoryEntry
        {
            UserId = id, Name = $"{Unique("GETBY")},CARL", IsActive = true
        });

        ProviderDirectoryEntry? entry = await Directory().GetAsync(id);

        Assert.That(entry, Is.Not.Null);
        Assert.That(entry!.UserId, Is.EqualTo(id));
    }

    [Test]
    public async Task NewPersonGrain_UpdateProfile_SyncsToDirectory_KeyedByUserIdWithoutPrefix()
    {
        string userId = Guid.NewGuid().ToString();
        string last = Unique("DIRSYNC");
        INewPersonGrain person = _cluster.GrainFactory.GetGrain<INewPersonGrain>($"USER:{userId}");

        await person.UpdateProfileAsync(
            $"{last},DELTA", "Staff Physician", "MD", "MEDICINE",
            "PHYSICIAN", "STAFF", "Neurology", null, null, null, null);

        // The directory entry is keyed by the bare userId (no "USER:" prefix), so a
        // picked provider slots straight into providerId fields / PROV-PAT-IDX keys.
        List<ProviderDirectoryEntry> results = await Directory().SearchAsync(last);
        Assert.That(results.Select(r => r.UserId), Contains.Item(userId));
    }

    [Test]
    public async Task NewPersonGrain_SetInactive_RemovesFromDirectorySearch()
    {
        string userId = Guid.NewGuid().ToString();
        string last = Unique("TERMED");
        INewPersonGrain person = _cluster.GrainFactory.GetGrain<INewPersonGrain>($"USER:{userId}");
        await person.UpdateProfileAsync(
            $"{last},ECHO", "Staff Physician", "MD", "MEDICINE",
            "PHYSICIAN", "STAFF", "Neurology", null, null, null, null);
        Assert.That((await Directory().SearchAsync(last)).Select(r => r.UserId), Contains.Item(userId));

        await person.SetActiveStatusAsync(false, DateTime.UtcNow);

        Assert.That((await Directory().SearchAsync(last)).Select(r => r.UserId), Does.Not.Contain(userId));
    }
}
