// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NUnit.Framework;
using Orleans.TestingHost;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.UnitTests;

[TestFixture]
public class MassCasualtyGrainTests
{
    private TestCluster _cluster = default!;
    [OneTimeSetUp] public void OneTimeSetup() { _cluster = SharedCluster.Instance; }

    private IMassCasualtyIncidentGrain GetIncident(string id) => _cluster.GrainFactory.GetGrain<IMassCasualtyIncidentGrain>(id);
    private IMassCasualtyCasualtyGrain GetCasualty(string id) => _cluster.GrainFactory.GetGrain<IMassCasualtyCasualtyGrain>(id);
    private IMassCasualtyIncidentIndexGrain GetIncidentIndex() => _cluster.GrainFactory.GetGrain<IMassCasualtyIncidentIndexGrain>("MCI-IDX");
    private IMassCasualtyCasualtyIndexGrain GetCasualtyIndex() => _cluster.GrainFactory.GetGrain<IMassCasualtyCasualtyIndexGrain>("MCI-CASUALTY-IDX");

    [Test]
    public async Task Incident_Activates()
    {
        string id = $"MCI:{Guid.NewGuid()}";
        var result = await GetIncident(id).ActivateAsync("Highway 10 MVC", "MVC", "LEVEL_2", "IC Smith", "Multi-vehicle on I-10", 30);

        Assert.That(result.IncidentId, Is.EqualTo(id));
        Assert.That(result.IncidentName, Is.EqualTo("Highway 10 MVC"));
        Assert.That(result.IncidentType, Is.EqualTo("MVC"));
        Assert.That(result.Severity, Is.EqualTo("LEVEL_2"));
        Assert.That(result.Status, Is.EqualTo("ACTIVE"));
        Assert.That(result.EstimatedCasualties, Is.EqualTo(30));
        Assert.That(result.StatusUpdates, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task Incident_Deactivates()
    {
        string id = $"MCI:{Guid.NewGuid()}";
        await GetIncident(id).ActivateAsync("Test", "EXPLOSION", "LEVEL_1", "IC", null, 10);
        await GetIncident(id).DeactivateAsync("IC", "All casualties treated");

        var state = await GetIncident(id).GetIncidentAsync();
        Assert.That(state.Status, Is.EqualTo("DEACTIVATED"));
        Assert.That(state.AfterActionNotes, Is.EqualTo("All casualties treated"));
        Assert.That(state.DeactivatedDate, Is.Not.Null);
    }

    [Test]
    public async Task Incident_UpdatesSeverity()
    {
        string id = $"MCI:{Guid.NewGuid()}";
        await GetIncident(id).ActivateAsync("Esc", "SHOOTING", "LEVEL_1", "IC", null, 15);
        await GetIncident(id).UpdateSeverityAsync("LEVEL_3", "IC");

        var state = await GetIncident(id).GetIncidentAsync();
        Assert.That(state.Severity, Is.EqualTo("LEVEL_3"));
        Assert.That(state.StatusUpdates, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task Casualty_Registers()
    {
        string incId = $"MCI:{Guid.NewGuid()}";
        await GetIncident(incId).ActivateAsync("Test", "MVC", "LEVEL_1", "IC", null, 10);

        string casId = $"MCI-CASUALTY:{Guid.NewGuid()}";
        var result = await GetCasualty(casId).RegisterCasualtyAsync(incId, "T-001", "IMMEDIATE", null, null, "Multiple trauma", "AMBULANCE", "Triage Nurse");

        Assert.That(result.CasualtyId, Is.EqualTo(casId));
        Assert.That(result.TriageTag, Is.EqualTo("T-001"));
        Assert.That(result.TriageCategory, Is.EqualTo("IMMEDIATE"));
        Assert.That(result.PatientName, Is.EqualTo("UNIDENTIFIED"));
        Assert.That(result.Disposition, Is.EqualTo("PENDING"));
    }

    [Test]
    public async Task Casualty_UpdatesTriage()
    {
        string incId = $"MCI:{Guid.NewGuid()}";
        await GetIncident(incId).ActivateAsync("T", "MVC", "LEVEL_1", "IC", null, 5);
        string casId = $"MCI-CASUALTY:{Guid.NewGuid()}";
        await GetCasualty(casId).RegisterCasualtyAsync(incId, "T-002", "DELAYED", null, null, "Fracture", "WALK_IN", "Nurse");

        await GetCasualty(casId).UpdateTriageCategoryAsync("IMMEDIATE", "Dr. Jones");

        var state = await GetCasualty(casId).GetCasualtyAsync();
        Assert.That(state.TriageCategory, Is.EqualTo("IMMEDIATE"));
    }

    [Test]
    public async Task Casualty_AssignsArea()
    {
        string incId = $"MCI:{Guid.NewGuid()}";
        await GetIncident(incId).ActivateAsync("T", "MVC", "LEVEL_1", "IC", null, 5);
        string casId = $"MCI-CASUALTY:{Guid.NewGuid()}";
        await GetCasualty(casId).RegisterCasualtyAsync(incId, "T-003", "IMMEDIATE", null, null, "Burns", "AMBULANCE", "Nurse");

        await GetCasualty(casId).AssignToAreaAsync("TRAUMA_BAY", "Charge Nurse");

        var state = await GetCasualty(casId).GetCasualtyAsync();
        Assert.That(state.TreatmentArea, Is.EqualTo("TRAUMA_BAY"));
    }

    [Test]
    public async Task Casualty_UpdatesDisposition()
    {
        string incId = $"MCI:{Guid.NewGuid()}";
        await GetIncident(incId).ActivateAsync("T", "MVC", "LEVEL_1", "IC", null, 5);
        string casId = $"MCI-CASUALTY:{Guid.NewGuid()}";
        await GetCasualty(casId).RegisterCasualtyAsync(incId, "T-004", "MINOR", null, null, "Laceration", "WALK_IN", "Nurse");

        await GetCasualty(casId).UpdateDispositionAsync("DISCHARGED", "Dr. Adams");

        var state = await GetCasualty(casId).GetCasualtyAsync();
        Assert.That(state.Disposition, Is.EqualTo("DISCHARGED"));
    }

    [Test]
    public async Task Casualty_LinksPatient()
    {
        string incId = $"MCI:{Guid.NewGuid()}";
        await GetIncident(incId).ActivateAsync("T", "MVC", "LEVEL_1", "IC", null, 5);
        string casId = $"MCI-CASUALTY:{Guid.NewGuid()}";
        await GetCasualty(casId).RegisterCasualtyAsync(incId, "T-005", "DELAYED", null, null, "Head injury", "AMBULANCE", "Nurse");

        await GetCasualty(casId).LinkPatientAsync("PATIENT-123", "DOE,JOHN");

        var state = await GetCasualty(casId).GetCasualtyAsync();
        Assert.That(state.PatientId, Is.EqualTo("PATIENT-123"));
        Assert.That(state.PatientName, Is.EqualTo("DOE,JOHN"));
    }

    [Test]
    public async Task IncidentIndex_ActiveQuery()
    {
        string id = $"MCI:{Guid.NewGuid()}";
        await GetIncident(id).ActivateAsync("Active Test", "HAZMAT", "LEVEL_1", "IC", null, 5);

        var active = await GetIncidentIndex().GetActiveAsync();
        Assert.That(active.Any(e => e.IncidentId == id), Is.True);
    }

    [Test]
    public async Task CasualtyIndex_ByIncident()
    {
        string incId = $"MCI:{Guid.NewGuid()}";
        await GetIncident(incId).ActivateAsync("Idx Test", "MVC", "LEVEL_1", "IC", null, 5);

        string cas1 = $"MCI-CASUALTY:{Guid.NewGuid()}";
        string cas2 = $"MCI-CASUALTY:{Guid.NewGuid()}";
        await GetCasualty(cas1).RegisterCasualtyAsync(incId, "T-010", "IMMEDIATE", null, null, "Trauma", "AMBULANCE", "Nurse");
        await GetCasualty(cas2).RegisterCasualtyAsync(incId, "T-011", "MINOR", null, null, "Abrasion", "WALK_IN", "Nurse");

        var casualties = await GetCasualtyIndex().GetByIncidentAsync(incId);
        Assert.That(casualties, Has.Count.EqualTo(2));
    }
}
