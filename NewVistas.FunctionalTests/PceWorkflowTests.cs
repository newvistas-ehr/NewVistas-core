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
public class PceWorkflowTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    private IVisitGrain GetVisitGrain(string visitId) =>
        _cluster.GrainFactory.GetGrain<IVisitGrain>($"PCE-VISIT:{visitId}");

    private IPatientVisitIndexGrain GetIndexGrain(string patientId) =>
        _cluster.GrainFactory.GetGrain<IPatientVisitIndexGrain>($"PCE-VISITS:{patientId}");

    // ── 1 ────────────────────────────────────────────────────────────────────
    [Test]
    public async Task VisitGrain_CreateVisit_PersistsCore()
    {
        string visitId = $"VISIT-{Guid.NewGuid()}";
        IVisitGrain grain = GetVisitGrain(visitId);

        await grain.CreateVisitAsync("PAT-001", new DateTime(2025, 6, 15, 9, 0, 0, DateTimeKind.Utc),
            "A", "LOC-001", "Primary Care", "ESTABLISHED", "323", null,
            "PROV-001", "Dr. Jones", null, "Routine visit");

        VisitState state = await grain.GetVisitAsync();
        Assert.That(state.PatientId, Is.EqualTo("PAT-001"));
        Assert.That(state.ServiceCategory, Is.EqualTo("A"));
        Assert.That(state.LocationName, Is.EqualTo("Primary Care"));
        Assert.That(state.Status, Is.EqualTo("OPEN"));
    }

    // ── 2 ────────────────────────────────────────────────────────────────────
    [Test]
    public async Task VisitGrain_CreateVisit_AutoAddsPrimaryProvider()
    {
        string visitId = $"VISIT-{Guid.NewGuid()}";
        IVisitGrain grain = GetVisitGrain(visitId);

        await grain.CreateVisitAsync("PAT-002", DateTime.UtcNow, "A",
            null, null, null, null, null, "PROV-002", "Dr. Smith", null, null);

        VisitState state = await grain.GetVisitAsync();
        Assert.That(state.Providers, Has.Count.EqualTo(1));
        Assert.That(state.Providers[0].ProviderId, Is.EqualTo("PROV-002"));
        Assert.That(state.Providers[0].IsPrimary, Is.True);
    }

    // ── 3 ────────────────────────────────────────────────────────────────────
    [Test]
    public async Task VisitGrain_CheckOut_SetsStatusAndTimestamp()
    {
        string visitId = $"VISIT-{Guid.NewGuid()}";
        IVisitGrain grain = GetVisitGrain(visitId);
        await grain.CreateVisitAsync("PAT-003", DateTime.UtcNow, "A",
            null, null, null, null, null, null, null, null, null);

        DateTime coTime = DateTime.UtcNow.AddHours(1);
        await grain.CheckOutAsync(coTime);

        VisitState state = await grain.GetVisitAsync();
        Assert.That(state.Status, Is.EqualTo("CHECKED OUT"));
        Assert.That(state.CheckOutDateTime, Is.EqualTo(coTime));
    }

    // ── 4 ────────────────────────────────────────────────────────────────────
    [Test]
    public async Task VisitGrain_AddEncounterDiagnosis_AppendsToPovList()
    {
        string visitId = $"VISIT-{Guid.NewGuid()}";
        IVisitGrain grain = GetVisitGrain(visitId);
        await grain.CreateVisitAsync("PAT-004", DateTime.UtcNow, "A",
            null, null, null, null, null, null, null, null, null);

        await grain.AddEncounterDiagnosisAsync("I10", "Essential hypertension", true, null, null);
        await grain.AddEncounterDiagnosisAsync("E11.9", "Type 2 diabetes", false, null, null);

        VisitState state = await grain.GetVisitAsync();
        Assert.That(state.Diagnoses, Has.Count.EqualTo(2));
        Assert.That(state.Diagnoses[0].Icd10Code, Is.EqualTo("I10"));
        Assert.That(state.Diagnoses[0].IsPrimary, Is.True);
        Assert.That(state.Diagnoses[1].IsPrimary, Is.False);
    }

    // ── 5 ────────────────────────────────────────────────────────────────────
    [Test]
    public async Task VisitGrain_AddDiagnosis_PrimaryDemotesPrevious()
    {
        string visitId = $"VISIT-{Guid.NewGuid()}";
        IVisitGrain grain = GetVisitGrain(visitId);
        await grain.CreateVisitAsync("PAT-005", DateTime.UtcNow, "A",
            null, null, null, null, null, null, null, null, null);

        await grain.AddEncounterDiagnosisAsync("I10", "Hypertension", true, null, null);
        await grain.AddEncounterDiagnosisAsync("E11.9", "Diabetes", true, null, null);  // new primary

        VisitState state = await grain.GetVisitAsync();
        Assert.That(state.Diagnoses[0].IsPrimary, Is.False);
        Assert.That(state.Diagnoses[1].IsPrimary, Is.True);
    }

    // ── 6 ────────────────────────────────────────────────────────────────────
    [Test]
    public async Task VisitGrain_AddProcedure_AppendsToCptList()
    {
        string visitId = $"VISIT-{Guid.NewGuid()}";
        IVisitGrain grain = GetVisitGrain(visitId);
        await grain.CreateVisitAsync("PAT-006", DateTime.UtcNow, "A",
            null, null, null, null, null, null, null, null, null);

        await grain.AddProcedureAsync("99213", "Office visit, established, low complexity", 1,
            null, "PROV-001", "Dr. Jones");
        await grain.AddProcedureAsync("85025", "Complete CBC with differential", 1,
            null, "PROV-001", "Dr. Jones");

        VisitState state = await grain.GetVisitAsync();
        Assert.That(state.Procedures, Has.Count.EqualTo(2));
        Assert.That(state.Procedures[0].CptCode, Is.EqualTo("99213"));
        Assert.That(state.Procedures[1].CptCode, Is.EqualTo("85025"));
    }

    // ── 7 ────────────────────────────────────────────────────────────────────
    [Test]
    public async Task VisitGrain_AddProcedure_StoresModifiers()
    {
        string visitId = $"VISIT-{Guid.NewGuid()}";
        IVisitGrain grain = GetVisitGrain(visitId);
        await grain.CreateVisitAsync("PAT-007", DateTime.UtcNow, "T",
            null, null, null, null, null, null, null, null, null);

        await grain.AddProcedureAsync("99213", "Telehealth visit", 1, "95", null, null);

        VisitState state = await grain.GetVisitAsync();
        Assert.That(state.Procedures[0].Modifiers, Is.EqualTo("95"));
    }

    // ── 8 ────────────────────────────────────────────────────────────────────
    [Test]
    public async Task VisitGrain_AddProvider_AvoidsDuplicates()
    {
        string visitId = $"VISIT-{Guid.NewGuid()}";
        IVisitGrain grain = GetVisitGrain(visitId);
        await grain.CreateVisitAsync("PAT-008", DateTime.UtcNow, "A",
            null, null, null, null, null, "PROV-010", "Dr. Doe", null, null);

        // Try to add the same provider again
        await grain.AddProviderAsync("PROV-010", "Dr. Doe", "PRIMARY", true);

        VisitState state = await grain.GetVisitAsync();
        Assert.That(state.Providers, Has.Count.EqualTo(1));
    }

    // ── 9 ────────────────────────────────────────────────────────────────────
    [Test]
    public async Task VisitGrain_CancelVisit_SetsStatusAndReason()
    {
        string visitId = $"VISIT-{Guid.NewGuid()}";
        IVisitGrain grain = GetVisitGrain(visitId);
        await grain.CreateVisitAsync("PAT-009", DateTime.UtcNow, "A",
            null, null, null, null, null, null, null, null, null);

        await grain.CancelVisitAsync("Patient no-show");

        VisitState state = await grain.GetVisitAsync();
        Assert.That(state.Status, Is.EqualTo("CANCELLED"));
        Assert.That(state.CancellationReason, Is.EqualTo("Patient no-show"));
    }

    // ── 10 ───────────────────────────────────────────────────────────────────
    [Test]
    public async Task VisitGrain_LinkNote_AppendedToList()
    {
        string visitId = $"VISIT-{Guid.NewGuid()}";
        IVisitGrain grain = GetVisitGrain(visitId);
        await grain.CreateVisitAsync("PAT-010", DateTime.UtcNow, "A",
            null, null, null, null, null, null, null, null, null);

        await grain.LinkNoteAsync("NOTE-001");
        await grain.LinkNoteAsync("NOTE-002");

        VisitState state = await grain.GetVisitAsync();
        Assert.That(state.LinkedNoteIds, Has.Count.EqualTo(2));
        Assert.That(state.LinkedNoteIds, Contains.Item("NOTE-001"));
    }

    // ── 11 ───────────────────────────────────────────────────────────────────
    [Test]
    public async Task VisitGrain_LinkNote_NoDuplicates()
    {
        string visitId = $"VISIT-{Guid.NewGuid()}";
        IVisitGrain grain = GetVisitGrain(visitId);
        await grain.CreateVisitAsync("PAT-011", DateTime.UtcNow, "A",
            null, null, null, null, null, null, null, null, null);

        await grain.LinkNoteAsync("NOTE-DUP");
        await grain.LinkNoteAsync("NOTE-DUP");

        VisitState state = await grain.GetVisitAsync();
        Assert.That(state.LinkedNoteIds, Has.Count.EqualTo(1));
    }

    // ── 12 ───────────────────────────────────────────────────────────────────
    [Test]
    public async Task PatientVisitIndexGrain_AddOrUpdate_PersistsEntry()
    {
        string patientId = $"PAT-{Guid.NewGuid()}";
        IPatientVisitIndexGrain indexGrain = GetIndexGrain(patientId);

        await indexGrain.AddOrUpdateVisitAsync(new PceVisitEntry
        {
            VisitId = "V-001",
            VisitDateTime = DateTime.UtcNow.AddDays(-5),
            ServiceCategory = "A",
            LocationName = "Primary Care",
            Status = "CHECKED OUT",
            DiagnosisCount = 2,
            ProcedureCount = 1
        });

        List<PceVisitEntry> visits = await indexGrain.GetVisitsAsync(10);
        Assert.That(visits, Has.Count.EqualTo(1));
        Assert.That(visits[0].VisitId, Is.EqualTo("V-001"));
    }

    // ── 13 ───────────────────────────────────────────────────────────────────
    [Test]
    public async Task PatientVisitIndexGrain_AddOrUpdate_ReplacesExisting()
    {
        string patientId = $"PAT-{Guid.NewGuid()}";
        IPatientVisitIndexGrain indexGrain = GetIndexGrain(patientId);

        await indexGrain.AddOrUpdateVisitAsync(new PceVisitEntry
            { VisitId = "V-002", Status = "OPEN", VisitDateTime = DateTime.UtcNow, ServiceCategory = "A" });
        await indexGrain.AddOrUpdateVisitAsync(new PceVisitEntry
            { VisitId = "V-002", Status = "CHECKED OUT", VisitDateTime = DateTime.UtcNow, ServiceCategory = "A" });

        List<PceVisitEntry> visits = await indexGrain.GetVisitsAsync(10);
        Assert.That(visits, Has.Count.EqualTo(1));
        Assert.That(visits[0].Status, Is.EqualTo("CHECKED OUT"));
    }

    // ── 14 ───────────────────────────────────────────────────────────────────
    [Test]
    public async Task PatientVisitIndexGrain_GetVisits_ReturnsDescendingByDate()
    {
        string patientId = $"PAT-{Guid.NewGuid()}";
        IPatientVisitIndexGrain indexGrain = GetIndexGrain(patientId);

        await indexGrain.AddOrUpdateVisitAsync(new PceVisitEntry
            { VisitId = "V-OLD", VisitDateTime = DateTime.UtcNow.AddDays(-10), ServiceCategory = "A", Status = "CHECKED OUT" });
        await indexGrain.AddOrUpdateVisitAsync(new PceVisitEntry
            { VisitId = "V-NEW", VisitDateTime = DateTime.UtcNow.AddDays(-1), ServiceCategory = "A", Status = "OPEN" });

        List<PceVisitEntry> visits = await indexGrain.GetVisitsAsync(10);
        Assert.That(visits[0].VisitId, Is.EqualTo("V-NEW"));
        Assert.That(visits[1].VisitId, Is.EqualTo("V-OLD"));
    }

    // ── 15 ───────────────────────────────────────────────────────────────────
    [Test]
    public async Task PatientVisitIndexGrain_RemoveVisit_DeletesEntry()
    {
        string patientId = $"PAT-{Guid.NewGuid()}";
        IPatientVisitIndexGrain indexGrain = GetIndexGrain(patientId);

        await indexGrain.AddOrUpdateVisitAsync(new PceVisitEntry
            { VisitId = "V-REM", VisitDateTime = DateTime.UtcNow, ServiceCategory = "A", Status = "CANCELLED" });
        await indexGrain.RemoveVisitAsync("V-REM");

        List<PceVisitEntry> visits = await indexGrain.GetVisitsAsync(10);
        Assert.That(visits, Is.Empty);
    }

    // ── 16 ───────────────────────────────────────────────────────────────────
    [Test]
    public async Task VisitGrain_EndToEnd_FullEncounterLifecycle()
    {
        // Arrange — create visit
        string patientId = $"PAT-{Guid.NewGuid()}";
        string visitId = $"VISIT-{Guid.NewGuid()}";
        IVisitGrain visitGrain = GetVisitGrain(visitId);
        IPatientVisitIndexGrain indexGrain = GetIndexGrain(patientId);

        await visitGrain.CreateVisitAsync(patientId, DateTime.UtcNow, "A",
            "LOC-PCARE", "Primary Care Clinic", "ESTABLISHED", "323", null,
            "PROV-001", "Dr. Jones", null, "Wellness visit");

        VisitState created = await visitGrain.GetVisitAsync();
        await indexGrain.AddOrUpdateVisitAsync(new PceVisitEntry
        {
            VisitId = visitId,
            VisitDateTime = created.VisitDateTime,
            ServiceCategory = created.ServiceCategory,
            LocationName = created.LocationName,
            Status = created.Status
        });

        // Act — document diagnoses and procedures
        await visitGrain.AddEncounterDiagnosisAsync("Z00.00",
            "General adult medical exam", true, "PROV-001", "Dr. Jones");
        await visitGrain.AddEncounterDiagnosisAsync("I10", "Hypertension", false, "PROV-001", "Dr. Jones");
        await visitGrain.AddProcedureAsync("99395",
            "Preventive medicine E/M, established 18-39", 1, null, "PROV-001", "Dr. Jones");

        // Act — check out
        await visitGrain.CheckOutAsync(DateTime.UtcNow.AddHours(1));

        VisitState final = await visitGrain.GetVisitAsync();

        // Assert
        Assert.That(final.Status, Is.EqualTo("CHECKED OUT"));
        Assert.That(final.CheckOutDateTime, Is.Not.Null);
        Assert.That(final.Diagnoses, Has.Count.EqualTo(2));
        Assert.That(final.Diagnoses.First(d => d.IsPrimary).Icd10Code, Is.EqualTo("Z00.00"));
        Assert.That(final.Procedures, Has.Count.EqualTo(1));
        Assert.That(final.Procedures[0].CptCode, Is.EqualTo("99395"));

        // Assert index updated correctly
        await indexGrain.AddOrUpdateVisitAsync(new PceVisitEntry
        {
            VisitId = visitId,
            VisitDateTime = final.VisitDateTime,
            ServiceCategory = final.ServiceCategory,
            LocationName = final.LocationName,
            Status = final.Status,
            DiagnosisCount = final.Diagnoses.Count,
            ProcedureCount = final.Procedures.Count
        });

        List<PceVisitEntry> indexedVisits = await indexGrain.GetVisitsAsync(10);
        Assert.That(indexedVisits, Has.Count.EqualTo(1));
        Assert.That(indexedVisits[0].DiagnosisCount, Is.EqualTo(2));
        Assert.That(indexedVisits[0].ProcedureCount, Is.EqualTo(1));
        Assert.That(indexedVisits[0].Status, Is.EqualTo("CHECKED OUT"));
    }
}
