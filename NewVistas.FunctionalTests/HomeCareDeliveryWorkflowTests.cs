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
/// The Home-Based Care delivery-model dimension (who delivers): hospital-provided vs. external agency,
/// plus the Hospital-at-Home acute program and its handoff link. The facility census is a shared
/// singleton across the run, so assertions use this test's unique ids — never exact totals.
/// </summary>
[TestFixture]
public class HomeCareDeliveryWorkflowTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup() => _cluster = SharedCluster.Instance;

    private IPatientWorkflowGrain Workflow(string patientId) => _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);
    private IHomeCareCensusGrain Census() => _cluster.GrainFactory.GetGrain<IHomeCareCensusGrain>("HHC-CENSUS:DEFAULT");

    private Task<string> Admit(IPatientWorkflowGrain wf, HomeCareProgramType program = HomeCareProgramType.HomeBasedPrimaryCare,
        HomeCareDeliveryModel delivery = HomeCareDeliveryModel.HospitalProvided)
        => wf.AdmitToHomeCareAsync(program, new DateTime(2026, 3, 1), HomeCareAdmissionSource.AcuteHospital,
            "REF-001", "Dr. Holt", "I50.9", "Congestive heart failure", HomeCareLevelOfCare.Enhanced,
            "Post-discharge skilled need.", "Spouse", "14 Maple St", delivery);

    [Test]
    public async Task Admit_DefaultsToHospitalProvided()
    {
        // Back-compat lock: the existing admit path (no delivery model) yields HospitalProvided.
        string patient = $"HHCDLV-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patient);
        string episodeId = await wf.AdmitToHomeCareAsync(
            HomeCareProgramType.HomeBasedPrimaryCare, new DateTime(2026, 3, 1), HomeCareAdmissionSource.Community,
            "REF-001", "Dr. Holt", "I50.9", "CHF", HomeCareLevelOfCare.Basic, "need", "Spouse", "14 Maple St");

        HomeCareEpisodeState e = await wf.GetHomeCareEpisodeAsync(episodeId);
        Assert.That(e.DeliveryModel, Is.EqualTo(HomeCareDeliveryModel.HospitalProvided));
        Assert.That(e.AgencyCoordination, Is.Null);
        Assert.That(e.HospitalAtHome, Is.Null);
    }

    [Test]
    public async Task ExternalAgency_MirrorsToEpisodeAndCensus()
    {
        string patient = $"HHCDLV-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patient);
        string episodeId = await Admit(wf, delivery: HomeCareDeliveryModel.ExternalAgency);

        HomeCareEpisodeState e = await wf.GetHomeCareEpisodeAsync(episodeId);
        Assert.That(e.DeliveryModel, Is.EqualTo(HomeCareDeliveryModel.ExternalAgency));

        List<HomeCareCensusEntry> agencyCaseload = await Census().GetByDeliveryModelAsync(HomeCareDeliveryModel.ExternalAgency);
        Assert.That(agencyCaseload.Select(c => c.EpisodeId), Contains.Item(episodeId));
    }

    [Test]
    public async Task LinkAgency_DenormalizesFromDirectory_AndForcesExternalAgency()
    {
        string patient = $"HHCDLV-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patient);
        // Admit hospital-provided, then refer out to a seeded directory agency.
        string episodeId = await Admit(wf);
        await wf.LinkHomeCareAgencyAsync(episodeId, "HHA-VALLEY-VNA", "COORD-1", "Case Mgr", "EXT-REF:abc");

        HomeCareEpisodeState e = await wf.GetHomeCareEpisodeAsync(episodeId);
        Assert.That(e.DeliveryModel, Is.EqualTo(HomeCareDeliveryModel.ExternalAgency));
        Assert.That(e.AgencyCoordination, Is.Not.Null);
        Assert.That(e.AgencyCoordination!.AgencyName, Is.EqualTo("VALLEY VNA HOME HEALTH"));
        Assert.That(e.AgencyCoordination.AgencyCcn, Is.EqualTo("227312"));
        Assert.That(e.AgencyCoordination.ExternalReferralId, Is.EqualTo("EXT-REF:abc"));

        // Census carries the denormalized agency name.
        List<HomeCareCensusEntry> mine = (await Census().GetAllAsync()).Where(c => c.EpisodeId == episodeId).ToList();
        Assert.That(mine.Single().AgencyName, Is.EqualTo("VALLEY VNA HOME HEALTH"));
    }

    [Test]
    public async Task AgencyMilestones_AppendInOrder()
    {
        string patient = $"HHCDLV-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patient);
        string episodeId = await Admit(wf, delivery: HomeCareDeliveryModel.ExternalAgency);
        await wf.LinkHomeCareAgencyAsync(episodeId, "HHA-VALLEY-VNA", "COORD-1", "Case Mgr", null);

        await wf.AddAgencyCareMilestoneAsync(episodeId, AgencyMilestoneType.ReferralSent, new DateTime(2026, 3, 1), "sent", "U1", "User One");
        await wf.AddAgencyCareMilestoneAsync(episodeId, AgencyMilestoneType.StartOfCare, new DateTime(2026, 3, 3), "soc", "U1", "User One");

        HomeCareEpisodeState e = await wf.GetHomeCareEpisodeAsync(episodeId);
        Assert.That(e.AgencyCoordination!.Milestones, Has.Count.EqualTo(2));
        Assert.That(e.AgencyCoordination.Milestones[0].Type, Is.EqualTo(AgencyMilestoneType.ReferralSent));
        Assert.That(e.AgencyCoordination.Milestones[1].Type, Is.EqualTo(AgencyMilestoneType.StartOfCare));
        Assert.That(e.AgencyCoordination.Milestones.All(m => !string.IsNullOrEmpty(m.MilestoneId)), Is.True);
    }

    [Test]
    public async Task HospitalAtHome_ForcesHospitalProvided_AndSetsHandoff()
    {
        string patient = $"HHCDLV-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patient);
        // Even if ExternalAgency is passed, a HospitalAtHome episode is forced to HospitalProvided.
        string episodeId = await Admit(wf, HomeCareProgramType.HospitalAtHome, HomeCareDeliveryModel.ExternalAgency);

        HomeCareEpisodeState e = await wf.GetHomeCareEpisodeAsync(episodeId);
        Assert.That(e.ProgramType, Is.EqualTo(HomeCareProgramType.HospitalAtHome));
        Assert.That(e.DeliveryModel, Is.EqualTo(HomeCareDeliveryModel.HospitalProvided));

        await wf.SetHospitalAtHomeContextAsync(episodeId, "ADT-77", "500", "Springfield Medical Center",
            "MED-3A", "3A-12", new DateTime(2026, 6, 15), "Acute cellulitis at home.");
        e = await wf.GetHomeCareEpisodeAsync(episodeId);
        Assert.That(e.HospitalAtHome, Is.Not.Null);
        Assert.That(e.HospitalAtHome!.SourceAdmissionId, Is.EqualTo("ADT-77"));
        Assert.That(e.HospitalAtHome.SourceBedId, Is.EqualTo("3A-12"));
    }

    [Test]
    public async Task SetDeliveryModel_CannotMakeHospitalAtHomeExternal()
    {
        string patient = $"HHCDLV-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patient);
        string episodeId = await Admit(wf, HomeCareProgramType.HospitalAtHome);

        await wf.SetHomeCareDeliveryModelAsync(episodeId, HomeCareDeliveryModel.ExternalAgency);

        HomeCareEpisodeState e = await wf.GetHomeCareEpisodeAsync(episodeId);
        Assert.That(e.DeliveryModel, Is.EqualTo(HomeCareDeliveryModel.HospitalProvided));
    }
}
