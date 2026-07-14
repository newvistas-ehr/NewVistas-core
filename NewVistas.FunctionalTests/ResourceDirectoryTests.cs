// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using Orleans.TestingHost;

namespace NewVistas.FunctionalTests;

/// <summary>
/// R3 (Whole-Person Social Care roadmap) — the community-resource directory and its closed loop:
/// resources are searchable by service type and text, and referring a patient to a directory resource
/// opens a Social Work referral populated from that agency. NonParallelizable — toggles SOCIAL_CARE.
/// </summary>
[TestFixture, NonParallelizable]
public class ResourceDirectoryTests
{
    private TestCluster _cluster = null!;
    private const string Feature = "SOCIAL_CARE";
    private const string By = "SW1";

    [OneTimeSetUp]
    public void OneTimeSetup() => _cluster = SharedCluster.Instance;

    private ISiteParametersGrain SiteParams() => _cluster.GrainFactory.GetGrain<ISiteParametersGrain>("SITE:DEFAULT");
    private IPatientWorkflowGrain Wf(string patientId) => _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

    [SetUp]
    public async Task SetUp() => await SiteParams().EnableFeatureAsync(Feature);

    [TearDown]
    public async Task TearDown() => await SiteParams().EnableFeatureAsync(Feature);

    [Test]
    public async Task Directory_AddSearch_ThenReferPatient_OpensSocialWorkReferral()
    {
        // A directory keyed off a unique id so the shared cluster's seed can't collide.
        var dir = _cluster.GrainFactory.GetGrain<IResourceDirectoryGrain>($"RESOURCE-DIR-{Guid.NewGuid()}");

        string foodId = await dir.AddOrUpdateAsync(new CommunityResource
        {
            Name = "Harvest Food Bank", ServiceType = SocialWorkReferralServiceType.Food,
            Description = "Emergency food and SNAP help.", City = "Salem", ServiceArea = "Essex County",
            Phone = "978-555-0001", Website = "https://harvest.example.org"
        });
        await dir.AddOrUpdateAsync(new CommunityResource
        {
            Name = "Shelter House", ServiceType = SocialWorkReferralServiceType.Housing, City = "Lynn"
        });

        // Search by service type filters to the food resource.
        List<CommunityResource> food = await dir.SearchAsync(SocialWorkReferralServiceType.Food, null);
        Assert.That(food.Select(r => r.Name), Is.EquivalentTo(new[] { "Harvest Food Bank" }));

        // Free-text search matches description / area.
        List<CommunityResource> byText = await dir.SearchAsync(null, "essex");
        Assert.That(byText.Any(r => r.ResourceId == foodId), Is.True);

        Assert.That((await dir.GetAllAsync()).Count, Is.EqualTo(2));

        // Refer a patient to the food resource via the seeded (singleton) directory the workflow reads.
        var live = _cluster.GrainFactory.GetGrain<IResourceDirectoryGrain>("RESOURCE-DIRECTORY");
        string liveId = await live.AddOrUpdateAsync(new CommunityResource
        {
            Name = "Riverside Food Pantry", ServiceType = SocialWorkReferralServiceType.Food,
            Phone = "978-555-0009", Website = "https://riverside.example.org"
        });

        string patient = $"RES-{Guid.NewGuid()}";
        string referralId = await Wf(patient).ReferToResourceAsync(liveId, "Food insecurity", By);
        Assert.That(referralId, Is.Not.Empty);

        // The referral is in the Social Work queue, pointed at the resource's agency.
        List<SocialWorkReferralIndexEntry> referrals = await Wf(patient).GetSocialWorkReferralsAsync();
        SocialWorkReferralIndexEntry entry = referrals.Single(e => e.ReferralId == referralId);
        Assert.That(entry.ServiceType, Is.EqualTo(SocialWorkReferralServiceType.Food));
        Assert.That(entry.AgencyName, Is.EqualTo("Riverside Food Pantry"));
    }

    [Test]
    public async Task ReferToResource_UnknownResource_Throws()
    {
        string patient = $"RES-{Guid.NewGuid()}";
        Assert.ThrowsAsync<InvalidOperationException>(
            async () => await Wf(patient).ReferToResourceAsync($"missing-{Guid.NewGuid()}", "x", By));
    }
}
