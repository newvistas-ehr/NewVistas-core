// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NUnit.Framework;
using Orleans.TestingHost;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.FunctionalTests;

[TestFixture]
public class MassCasualtyWorkflowTests
{
    private TestCluster _cluster = null!;
    [OneTimeSetUp] public void OneTimeSetup() { _cluster = SharedCluster.Instance; }

    private IPatientWorkflowGrain GetWorkflow(string patientId) => _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);
    private ISiteParametersGrain GetSiteParams() => _cluster.GrainFactory.GetGrain<ISiteParametersGrain>("SITE:DEFAULT");

    private async Task<string> CreatePatientAsync(string name, string sex, DateTime dob, string ssn)
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        var grain = _cluster.GrainFactory.GetGrain<IPatientGrain>(patientId);
        await grain.UpdateDemographicsAsync(name, sex, dob, ssn);
        await _cluster.GrainFactory.GetGrain<IPatientIndexGrain>("PATIENT-INDEX").AddOrUpdateAsync(new PatientIndexEntry
        {
            PatientId = patientId, Name = name, DateOfBirth = dob, Sex = sex,
            SsnLast4 = ssn.Length >= 4 ? ssn[^4..] : string.Empty, IsActive = true
        });
        return patientId;
    }

    [Test, Order(1)]
    public async Task WorkflowMci_FailsWhenDisabled()
    {
        await GetSiteParams().DisableFeatureAsync("MASS_CASUALTY");
        string patientId = await CreatePatientAsync("SMITH,ALICE", "F", new DateTime(1970, 5, 15), "111-22-3333");

        Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await GetWorkflow(patientId).RegisterAsMciCasualtyAsync("MCI:1", "T-001", "IMMEDIATE", "Trauma", "AMBULANCE", "Nurse"));
    }

    [Test, Order(2)]
    public async Task WorkflowMci_RegistersCasualtyWhenEnabled()
    {
        await GetSiteParams().EnableFeatureAsync("MASS_CASUALTY");

        // Create an incident first
        string incId = $"MCI:{Guid.NewGuid()}";
        var incGrain = _cluster.GrainFactory.GetGrain<IMassCasualtyIncidentGrain>(incId);
        await incGrain.ActivateAsync("Workflow Test MVC", "MVC", "LEVEL_2", "IC Commander", null, 20);

        string patientId = await CreatePatientAsync("DOE,JOHN", "M", new DateTime(1965, 8, 20), "444-55-6666");

        var casualty = await GetWorkflow(patientId).RegisterAsMciCasualtyAsync(
            incId, "T-100", "IMMEDIATE", "Chest trauma", "AMBULANCE", "Triage Nurse");

        Assert.That(casualty, Is.Not.Null);
        Assert.That(casualty.PatientId, Is.EqualTo(patientId));
        Assert.That(casualty.TriageTag, Is.EqualTo("T-100"));
        Assert.That(casualty.TriageCategory, Is.EqualTo("IMMEDIATE"));
        Assert.That(casualty.IncidentId, Is.EqualTo(incId));
    }

    [Test, Order(3)]
    public async Task WorkflowMci_FullIncidentLifecycle()
    {
        await GetSiteParams().EnableFeatureAsync("MASS_CASUALTY");

        // Activate incident
        string incId = $"MCI:{Guid.NewGuid()}";
        var incGrain = _cluster.GrainFactory.GetGrain<IMassCasualtyIncidentGrain>(incId);
        await incGrain.ActivateAsync("Full Lifecycle Test", "EXPLOSION", "LEVEL_1", "IC Commander", "Industrial explosion", 15);

        // Register casualties
        string cas1 = $"MCI-CASUALTY:{Guid.NewGuid()}";
        string cas2 = $"MCI-CASUALTY:{Guid.NewGuid()}";
        await _cluster.GrainFactory.GetGrain<IMassCasualtyCasualtyGrain>(cas1)
            .RegisterCasualtyAsync(incId, "T-201", "IMMEDIATE", null, null, "Burns 40% BSA", "AMBULANCE", "Nurse");
        await _cluster.GrainFactory.GetGrain<IMassCasualtyCasualtyGrain>(cas2)
            .RegisterCasualtyAsync(incId, "T-202", "MINOR", null, null, "Smoke inhalation", "WALK_IN", "Nurse");

        // Verify index
        var casualties = await _cluster.GrainFactory.GetGrain<IMassCasualtyCasualtyIndexGrain>("MCI-CASUALTY-IDX").GetByIncidentAsync(incId);
        Assert.That(casualties, Has.Count.EqualTo(2));

        // Deactivate
        await incGrain.DeactivateAsync("IC Commander", "All patients treated and dispositioned");
        var final = await incGrain.GetIncidentAsync();
        Assert.That(final.Status, Is.EqualTo("DEACTIVATED"));
    }

    [Test, Order(4)]
    public async Task WorkflowMci_TriageCategorySorting()
    {
        await GetSiteParams().EnableFeatureAsync("MASS_CASUALTY");

        string incId = $"MCI:{Guid.NewGuid()}";
        await _cluster.GrainFactory.GetGrain<IMassCasualtyIncidentGrain>(incId)
            .ActivateAsync("Triage Sort", "MVC", "LEVEL_1", "IC", null, 10);

        // Register with different triage categories
        var idx = _cluster.GrainFactory.GetGrain<IMassCasualtyCasualtyIndexGrain>("MCI-CASUALTY-IDX");

        string c1 = $"MCI-CASUALTY:{Guid.NewGuid()}";
        await _cluster.GrainFactory.GetGrain<IMassCasualtyCasualtyGrain>(c1)
            .RegisterCasualtyAsync(incId, "T-301", "IMMEDIATE", null, null, "Head injury", "AMBULANCE", "Nurse");

        string c2 = $"MCI-CASUALTY:{Guid.NewGuid()}";
        await _cluster.GrainFactory.GetGrain<IMassCasualtyCasualtyGrain>(c2)
            .RegisterCasualtyAsync(incId, "T-302", "MINOR", null, null, "Abrasion", "WALK_IN", "Nurse");

        var immediate = await idx.GetByTriageCategoryAsync(incId, "IMMEDIATE");
        var minor = await idx.GetByTriageCategoryAsync(incId, "MINOR");

        Assert.That(immediate.Any(e => e.CasualtyId == c1), Is.True);
        Assert.That(minor.Any(e => e.CasualtyId == c2), Is.True);
        Assert.That(immediate.Any(e => e.CasualtyId == c2), Is.False);
    }
}
