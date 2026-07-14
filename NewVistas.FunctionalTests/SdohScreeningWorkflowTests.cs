// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using Orleans.TestingHost;

namespace NewVistas.FunctionalTests;

/// <summary>
/// Phase 2 (Whole-Person Social Care) — the coded SDOH screening closed loop: a positive screen
/// computes findings, then one-click applies the mapped Z-code to the problem list and opens a Social
/// Work referral in the EXISTING referral machinery, and the SDOH cohort index reflects the positive
/// domain. NonParallelizable — toggles the SOCIAL_CARE feature.
/// </summary>
[TestFixture, NonParallelizable]
public class SdohScreeningWorkflowTests
{
    private TestCluster _cluster = null!;
    private const string Feature = "SOCIAL_CARE";
    private const string By = "SW1";

    [OneTimeSetUp]
    public void OneTimeSetup() => _cluster = SharedCluster.Instance;

    private ISiteParametersGrain SiteParams() => _cluster.GrainFactory.GetGrain<ISiteParametersGrain>("SITE:DEFAULT");

    [SetUp]
    public async Task SetUp() => await SiteParams().EnableFeatureAsync(Feature);

    [TearDown]
    public async Task TearDown() => await SiteParams().EnableFeatureAsync(Feature);

    private IPatientWorkflowGrain Wf(string patientId) => _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

    private static SdohScreeningResponse R(SdohDomain d, SdohResponse r) => new() { Domain = d, Response = r };

    [Test]
    public async Task Screen_ComputesPositiveFindings_AndIndexesTheScreening()
    {
        string patient = $"SDOH-{Guid.NewGuid()}";
        string screeningId = await Wf(patient).RecordSdohScreeningAsync("AHC-HRSN", new()
        {
            R(SdohDomain.FoodInsecurity, SdohResponse.Positive),
            R(SdohDomain.HousingInstability, SdohResponse.Positive),
            R(SdohDomain.Employment, SdohResponse.Negative)
        }, By);

        SdohScreeningState state = await Wf(patient).GetSdohScreeningAsync(screeningId);
        Assert.That(state.Findings.Select(f => f.Domain),
            Is.EquivalentTo(new[] { SdohDomain.FoodInsecurity, SdohDomain.HousingInstability }));

        List<SdohScreeningSummary> history = await Wf(patient).GetSdohScreeningsAsync();
        Assert.That(history.Single(h => h.ScreeningId == screeningId).PositiveDomainCount, Is.EqualTo(2));
    }

    [Test]
    public async Task ApplyZCode_LandsOnProblemList_WithCitation()
    {
        string patient = $"SDOH-{Guid.NewGuid()}";
        string screeningId = await Wf(patient).RecordSdohScreeningAsync("AHC-HRSN", new()
        {
            R(SdohDomain.FoodInsecurity, SdohResponse.Positive)
        }, By);

        string problemId = await Wf(patient).AddSdohProblemAsync(screeningId, SdohDomain.FoodInsecurity, By);
        Assert.That(problemId, Is.Not.Empty);

        List<ProblemSummary> problems = await Wf(patient).GetActiveProblemsAsync();
        ProblemSummary zcode = problems.Single(p => p.DiagnosisCode == "Z59.41");
        Assert.That(zcode.Diagnosis, Does.Contain("Food insecurity"));

        // The screening records the closed-loop action.
        SdohScreeningState state = await Wf(patient).GetSdohScreeningAsync(screeningId);
        Assert.That(state.Actions.Any(a => a.ActionType == SdohActionType.ProblemAdded && a.TargetId == problemId), Is.True);
    }

    [Test]
    public async Task CreateReferral_LandsInExistingSocialWorkQueue()
    {
        string patient = $"SDOH-{Guid.NewGuid()}";
        string screeningId = await Wf(patient).RecordSdohScreeningAsync("AHC-HRSN", new()
        {
            R(SdohDomain.HousingInstability, SdohResponse.Positive)
        }, By);

        string referralId = await Wf(patient).CreateSdohReferralAsync(screeningId, SdohDomain.HousingInstability, By);
        Assert.That(referralId, Is.Not.Empty);

        List<SocialWorkReferralIndexEntry> referrals = await Wf(patient).GetSocialWorkReferralsAsync();
        Assert.That(referrals.Any(r => r.ReferralId == referralId), Is.True);

        SocialWorkReferralState referral = await Wf(patient).GetSocialWorkReferralAsync(referralId);
        Assert.That(referral.ServiceType, Is.EqualTo(SocialWorkReferralServiceType.Housing));
    }

    [Test]
    public async Task PositiveScreen_AddsPatientToDomainCohort()
    {
        string patient = $"SDOH-{Guid.NewGuid()}";
        await Wf(patient).RecordSdohScreeningAsync("AHC-HRSN", new()
        {
            R(SdohDomain.FoodInsecurity, SdohResponse.Positive)
        }, By);

        ISdohCohortIndexGrain cohort = _cluster.GrainFactory.GetGrain<ISdohCohortIndexGrain>($"SDOH-COHORT:{SdohDomain.FoodInsecurity}");
        Assert.That(await cohort.ContainsAsync(patient), Is.True);
        Assert.That(await cohort.GetCountAsync(), Is.GreaterThanOrEqualTo(1));
    }

    [Test]
    public async Task FlagOff_ScreeningHistoryEmpty()
    {
        string patient = $"SDOH-{Guid.NewGuid()}";
        await Wf(patient).RecordSdohScreeningAsync("AHC-HRSN", new() { R(SdohDomain.FoodInsecurity, SdohResponse.Positive) }, By);

        await SiteParams().DisableFeatureAsync(Feature);
        try
        {
            Assert.That(await Wf(patient).GetSdohScreeningsAsync(), Is.Empty);
        }
        finally
        {
            await SiteParams().EnableFeatureAsync(Feature);
        }
    }
}
