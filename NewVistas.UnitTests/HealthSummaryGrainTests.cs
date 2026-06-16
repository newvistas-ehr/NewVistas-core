// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans.TestingHost;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.UnitTests;

/// <summary>
/// Unit tests for the Health Summary grain layer.
/// VistA HEALTH SUMMARY TYPE file (#142).
/// Tests HealthSummaryTypeGrain, HealthSummaryTypeIndexGrain,
/// HealthSummaryGrain, HealthSummaryIndexGrain, and PatientWorkflowGrain
/// Health Summary methods.
/// </summary>
[TestFixture]
public class HealthSummaryGrainTests
{
    private TestCluster _cluster = default!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    // ── HealthSummaryTypeGrain ─────────────────────────────────────────────────

    [Test]
    public async Task HealthSummaryTypeGrain_Create_PersistsAllFields()
    {
        string typeId = $"HS-TYPE:{Guid.NewGuid()}";
        IHealthSummaryTypeGrain grain = _cluster.GrainFactory.GetGrain<IHealthSummaryTypeGrain>(typeId);

        await grain.CreateAsync(typeId, "CPRS Summary", "Standard outpatient summary", "PROV-001", "Dr. Smith");

        HealthSummaryTypeState state = await grain.GetAsync();

        Assert.That(state.TypeId,         Is.EqualTo(typeId));
        Assert.That(state.Name,           Is.EqualTo("CPRS Summary"));
        Assert.That(state.Description,    Is.EqualTo("Standard outpatient summary"));
        Assert.That(state.Status,         Is.EqualTo(HealthSummaryTypeStatus.Active));
        Assert.That(state.CreatedById,    Is.EqualTo("PROV-001"));
        Assert.That(state.CreatedByName,  Is.EqualTo("Dr. Smith"));
        Assert.That(state.Components,     Is.Empty);
        Assert.That(state.CreatedDate,    Is.Not.EqualTo(default(DateTime)));
    }

    [Test]
    public async Task HealthSummaryTypeGrain_Update_ChangesNameAndDescription()
    {
        string typeId = $"HS-TYPE:{Guid.NewGuid()}";
        IHealthSummaryTypeGrain grain = _cluster.GrainFactory.GetGrain<IHealthSummaryTypeGrain>(typeId);

        await grain.CreateAsync(typeId, "Old Name", "Old desc", "PROV-001", "Dr. A");
        await grain.UpdateAsync("New Name", "New desc");

        HealthSummaryTypeState state = await grain.GetAsync();

        Assert.That(state.Name,        Is.EqualTo("New Name"));
        Assert.That(state.Description, Is.EqualTo("New desc"));
    }

    [Test]
    public async Task HealthSummaryTypeGrain_AddOrUpdateComponent_AppendsAndSortsByOrder()
    {
        string typeId = $"HS-TYPE:{Guid.NewGuid()}";
        IHealthSummaryTypeGrain grain = _cluster.GrainFactory.GetGrain<IHealthSummaryTypeGrain>(typeId);

        await grain.CreateAsync(typeId, "Test Type", null, "PROV-001", "Dr. A");

        await grain.AddOrUpdateComponentAsync(new HealthSummaryComponentConfig
        {
            ComponentType = HealthSummaryComponentType.Allergies,
            IsEnabled = true,
            DisplayOrder = 20,
            MaxOccurrences = 5,
            DaysBack = 0
        });

        await grain.AddOrUpdateComponentAsync(new HealthSummaryComponentConfig
        {
            ComponentType = HealthSummaryComponentType.Demographics,
            IsEnabled = true,
            DisplayOrder = 10,
            MaxOccurrences = 0,
            DaysBack = 0
        });

        await grain.AddOrUpdateComponentAsync(new HealthSummaryComponentConfig
        {
            ComponentType = HealthSummaryComponentType.VitalSigns,
            IsEnabled = true,
            DisplayOrder = 30,
            MaxOccurrences = 3,
            DaysBack = 30
        });

        HealthSummaryTypeState state = await grain.GetAsync();

        Assert.That(state.Components, Has.Count.EqualTo(3));
        // Sorted by display order
        Assert.That(state.Components[0].ComponentType, Is.EqualTo(HealthSummaryComponentType.Demographics));
        Assert.That(state.Components[1].ComponentType, Is.EqualTo(HealthSummaryComponentType.Allergies));
        Assert.That(state.Components[2].ComponentType, Is.EqualTo(HealthSummaryComponentType.VitalSigns));
    }

    [Test]
    public async Task HealthSummaryTypeGrain_AddOrUpdateComponent_ReplacesExisting()
    {
        string typeId = $"HS-TYPE:{Guid.NewGuid()}";
        IHealthSummaryTypeGrain grain = _cluster.GrainFactory.GetGrain<IHealthSummaryTypeGrain>(typeId);

        await grain.CreateAsync(typeId, "Test Type", null, "PROV-001", "Dr. A");

        await grain.AddOrUpdateComponentAsync(new HealthSummaryComponentConfig
        {
            ComponentType = HealthSummaryComponentType.ActiveProblems,
            IsEnabled = true,
            DisplayOrder = 10,
            MaxOccurrences = 10,
            DaysBack = 0
        });

        // Update with new max occurrences
        await grain.AddOrUpdateComponentAsync(new HealthSummaryComponentConfig
        {
            ComponentType = HealthSummaryComponentType.ActiveProblems,
            IsEnabled = true,
            DisplayOrder = 10,
            MaxOccurrences = 5,  // changed
            DaysBack = 0
        });

        HealthSummaryTypeState state = await grain.GetAsync();

        Assert.That(state.Components,                      Has.Count.EqualTo(1));
        Assert.That(state.Components[0].MaxOccurrences,    Is.EqualTo(5));
    }

    [Test]
    public async Task HealthSummaryTypeGrain_RemoveComponent_RemovesMatchingType()
    {
        string typeId = $"HS-TYPE:{Guid.NewGuid()}";
        IHealthSummaryTypeGrain grain = _cluster.GrainFactory.GetGrain<IHealthSummaryTypeGrain>(typeId);

        await grain.CreateAsync(typeId, "Test Type", null, "PROV-001", "Dr. A");

        await grain.AddOrUpdateComponentAsync(new HealthSummaryComponentConfig
        {
            ComponentType = HealthSummaryComponentType.Allergies,
            IsEnabled = true, DisplayOrder = 10, MaxOccurrences = 5, DaysBack = 0
        });

        await grain.AddOrUpdateComponentAsync(new HealthSummaryComponentConfig
        {
            ComponentType = HealthSummaryComponentType.ActiveProblems,
            IsEnabled = true, DisplayOrder = 20, MaxOccurrences = 10, DaysBack = 0
        });

        await grain.RemoveComponentAsync(HealthSummaryComponentType.Allergies);

        HealthSummaryTypeState state = await grain.GetAsync();

        Assert.That(state.Components, Has.Count.EqualTo(1));
        Assert.That(state.Components[0].ComponentType, Is.EqualTo(HealthSummaryComponentType.ActiveProblems));
    }

    [Test]
    public async Task HealthSummaryTypeGrain_SetActive_False_SetsInactive()
    {
        string typeId = $"HS-TYPE:{Guid.NewGuid()}";
        IHealthSummaryTypeGrain grain = _cluster.GrainFactory.GetGrain<IHealthSummaryTypeGrain>(typeId);

        await grain.CreateAsync(typeId, "Test Type", null, "PROV-001", "Dr. A");
        Assert.That((await grain.GetAsync()).Status, Is.EqualTo(HealthSummaryTypeStatus.Active));

        await grain.SetActiveAsync(false);
        Assert.That((await grain.GetAsync()).Status, Is.EqualTo(HealthSummaryTypeStatus.Inactive));

        await grain.SetActiveAsync(true);
        Assert.That((await grain.GetAsync()).Status, Is.EqualTo(HealthSummaryTypeStatus.Active));
    }

    [Test]
    public async Task HealthSummaryTypeGrain_Component_SectionHeaderOverride_Persists()
    {
        string typeId = $"HS-TYPE:{Guid.NewGuid()}";
        IHealthSummaryTypeGrain grain = _cluster.GrainFactory.GetGrain<IHealthSummaryTypeGrain>(typeId);

        await grain.CreateAsync(typeId, "Test Type", null, "PROV-001", "Dr. A");

        await grain.AddOrUpdateComponentAsync(new HealthSummaryComponentConfig
        {
            ComponentType = HealthSummaryComponentType.CurrentMedications,
            IsEnabled = true,
            DisplayOrder = 10,
            MaxOccurrences = 5,
            DaysBack = 0,
            SectionHeader = "ACTIVE PRESCRIPTIONS"
        });

        HealthSummaryTypeState state = await grain.GetAsync();
        Assert.That(state.Components[0].SectionHeader, Is.EqualTo("ACTIVE PRESCRIPTIONS"));
    }

    // ── HealthSummaryTypeIndexGrain ────────────────────────────────────────────

    [Test]
    public async Task HealthSummaryTypeIndexGrain_UpsertEntry_AddsToIndex()
    {
        IHealthSummaryTypeIndexGrain index =
            _cluster.GrainFactory.GetGrain<IHealthSummaryTypeIndexGrain>($"HS-IDX-TEST:{Guid.NewGuid()}");

        HealthSummaryTypeIndexEntry entry = new()
        {
            TypeId = "TYPE-001",
            Name = "Basic Summary",
            Status = HealthSummaryTypeStatus.Active,
            ComponentCount = 3,
            CreatedDate = DateTime.UtcNow
        };

        await index.UpsertEntryAsync(entry);

        List<HealthSummaryTypeIndexEntry> all = await index.GetAllAsync();
        Assert.That(all, Has.Count.EqualTo(1));
        Assert.That(all[0].TypeId, Is.EqualTo("TYPE-001"));
        Assert.That(all[0].Name,   Is.EqualTo("Basic Summary"));
    }

    [Test]
    public async Task HealthSummaryTypeIndexGrain_UpsertEntry_UpdatesExisting()
    {
        IHealthSummaryTypeIndexGrain index =
            _cluster.GrainFactory.GetGrain<IHealthSummaryTypeIndexGrain>($"HS-IDX-TEST:{Guid.NewGuid()}");

        await index.UpsertEntryAsync(new HealthSummaryTypeIndexEntry
        {
            TypeId = "TYPE-002", Name = "Old Name", Status = HealthSummaryTypeStatus.Active,
            ComponentCount = 2, CreatedDate = DateTime.UtcNow
        });

        await index.UpsertEntryAsync(new HealthSummaryTypeIndexEntry
        {
            TypeId = "TYPE-002", Name = "New Name", Status = HealthSummaryTypeStatus.Inactive,
            ComponentCount = 5, CreatedDate = DateTime.UtcNow
        });

        List<HealthSummaryTypeIndexEntry> all = await index.GetAllAsync();
        Assert.That(all, Has.Count.EqualTo(1));
        Assert.That(all[0].Name,           Is.EqualTo("New Name"));
        Assert.That(all[0].ComponentCount, Is.EqualTo(5));
        Assert.That(all[0].Status,         Is.EqualTo(HealthSummaryTypeStatus.Inactive));
    }

    [Test]
    public async Task HealthSummaryTypeIndexGrain_GetActive_FiltersInactiveEntries()
    {
        IHealthSummaryTypeIndexGrain index =
            _cluster.GrainFactory.GetGrain<IHealthSummaryTypeIndexGrain>($"HS-IDX-TEST:{Guid.NewGuid()}");

        await index.UpsertEntryAsync(new HealthSummaryTypeIndexEntry
        {
            TypeId = "TYPE-A", Name = "Active Type", Status = HealthSummaryTypeStatus.Active,
            ComponentCount = 2, CreatedDate = DateTime.UtcNow
        });

        await index.UpsertEntryAsync(new HealthSummaryTypeIndexEntry
        {
            TypeId = "TYPE-B", Name = "Inactive Type", Status = HealthSummaryTypeStatus.Inactive,
            ComponentCount = 3, CreatedDate = DateTime.UtcNow
        });

        List<HealthSummaryTypeIndexEntry> active = await index.GetActiveAsync();
        Assert.That(active, Has.Count.EqualTo(1));
        Assert.That(active[0].TypeId, Is.EqualTo("TYPE-A"));
    }

    [Test]
    public async Task HealthSummaryTypeIndexGrain_RemoveEntry_RemovesFromIndex()
    {
        IHealthSummaryTypeIndexGrain index =
            _cluster.GrainFactory.GetGrain<IHealthSummaryTypeIndexGrain>($"HS-IDX-TEST:{Guid.NewGuid()}");

        await index.UpsertEntryAsync(new HealthSummaryTypeIndexEntry
        {
            TypeId = "TYPE-C", Name = "To Remove", Status = HealthSummaryTypeStatus.Active,
            ComponentCount = 0, CreatedDate = DateTime.UtcNow
        });

        await index.RemoveEntryAsync("TYPE-C");

        List<HealthSummaryTypeIndexEntry> all = await index.GetAllAsync();
        Assert.That(all, Is.Empty);
    }

    // ── HealthSummaryGrain ─────────────────────────────────────────────────────

    [Test]
    public async Task HealthSummaryGrain_Save_PersistsAllFields()
    {
        string reportId = $"HS-REPORT:{Guid.NewGuid()}";
        IHealthSummaryGrain grain = _cluster.GrainFactory.GetGrain<IHealthSummaryGrain>(reportId);

        HealthSummaryState report = new()
        {
            ReportId = reportId,
            PatientId = "PATIENT-001",
            TypeId = "TYPE-001",
            TypeName = "CPRS Summary",
            GeneratedDate = new DateTime(2025, 4, 10, 9, 0, 0),
            GeneratedById = "PROV-001",
            GeneratedByName = "Dr. Jones",
            Sections = new()
            {
                new HealthSummarySectionResult
                {
                    ComponentType = HealthSummaryComponentType.Allergies,
                    SectionHeader = "ALLERGIES/ADVERSE REACTIONS",
                    ContentLines = new() { "Penicillin [Drug] — Severe: Anaphylaxis" },
                    EntryCount = 1
                }
            }
        };

        await grain.SaveAsync(report);
        HealthSummaryState persisted = await grain.GetAsync();

        Assert.That(persisted.ReportId,        Is.EqualTo(reportId));
        Assert.That(persisted.PatientId,       Is.EqualTo("PATIENT-001"));
        Assert.That(persisted.TypeId,          Is.EqualTo("TYPE-001"));
        Assert.That(persisted.TypeName,        Is.EqualTo("CPRS Summary"));
        Assert.That(persisted.GeneratedByName, Is.EqualTo("Dr. Jones"));
        Assert.That(persisted.Sections,        Has.Count.EqualTo(1));
        Assert.That(persisted.Sections[0].SectionHeader, Is.EqualTo("ALLERGIES/ADVERSE REACTIONS"));
        Assert.That(persisted.Sections[0].ContentLines,  Has.Count.EqualTo(1));
        Assert.That(persisted.Sections[0].ContentLines[0], Does.Contain("Penicillin"));
    }

    // ── HealthSummaryIndexGrain ────────────────────────────────────────────────

    [Test]
    public async Task HealthSummaryIndexGrain_AddEntry_NewestFirst()
    {
        IHealthSummaryIndexGrain index =
            _cluster.GrainFactory.GetGrain<IHealthSummaryIndexGrain>($"HS-IDX:{Guid.NewGuid()}");

        HealthSummaryIndexEntry first = new()
        {
            ReportId = "RPT-001", PatientId = "PAT-001", TypeId = "T1", TypeName = "Type A",
            GeneratedDate = new DateTime(2025, 1, 1), GeneratedById = "P1",
            GeneratedByName = "Dr. A", SectionCount = 3
        };

        HealthSummaryIndexEntry second = new()
        {
            ReportId = "RPT-002", PatientId = "PAT-001", TypeId = "T1", TypeName = "Type A",
            GeneratedDate = new DateTime(2025, 2, 1), GeneratedById = "P1",
            GeneratedByName = "Dr. A", SectionCount = 4
        };

        await index.AddEntryAsync(first);
        await index.AddEntryAsync(second);

        List<HealthSummaryIndexEntry> all = await index.GetAllAsync();
        Assert.That(all, Has.Count.EqualTo(2));
        // Newest first
        Assert.That(all[0].ReportId, Is.EqualTo("RPT-002"));
        Assert.That(all[1].ReportId, Is.EqualTo("RPT-001"));
    }

    [Test]
    public async Task HealthSummaryIndexGrain_GetByType_FiltersCorrectly()
    {
        IHealthSummaryIndexGrain index =
            _cluster.GrainFactory.GetGrain<IHealthSummaryIndexGrain>($"HS-IDX:{Guid.NewGuid()}");

        await index.AddEntryAsync(new HealthSummaryIndexEntry
        {
            ReportId = "RPT-A", PatientId = "PAT-002", TypeId = "TYPE-1", TypeName = "Type 1",
            GeneratedDate = DateTime.UtcNow, GeneratedById = "P1", GeneratedByName = "Dr. B", SectionCount = 2
        });

        await index.AddEntryAsync(new HealthSummaryIndexEntry
        {
            ReportId = "RPT-B", PatientId = "PAT-002", TypeId = "TYPE-2", TypeName = "Type 2",
            GeneratedDate = DateTime.UtcNow, GeneratedById = "P1", GeneratedByName = "Dr. B", SectionCount = 3
        });

        await index.AddEntryAsync(new HealthSummaryIndexEntry
        {
            ReportId = "RPT-C", PatientId = "PAT-002", TypeId = "TYPE-1", TypeName = "Type 1",
            GeneratedDate = DateTime.UtcNow, GeneratedById = "P1", GeneratedByName = "Dr. B", SectionCount = 2
        });

        List<HealthSummaryIndexEntry> type1 = await index.GetByTypeAsync("TYPE-1");
        Assert.That(type1, Has.Count.EqualTo(2));
        Assert.That(type1.Select(e => e.ReportId), Does.Contain("RPT-A"));
        Assert.That(type1.Select(e => e.ReportId), Does.Contain("RPT-C"));

        List<HealthSummaryIndexEntry> type2 = await index.GetByTypeAsync("TYPE-2");
        Assert.That(type2, Has.Count.EqualTo(1));
        Assert.That(type2[0].ReportId, Is.EqualTo("RPT-B"));
    }

    // ── Workflow grain integration ─────────────────────────────────────────────

    [Test]
    public async Task WorkflowGrain_GenerateHealthSummary_ReturnsReportIdAndPersistsReport()
    {
        // WorkflowGrain.HsType(rawTypeId) prepends "HS-TYPE:" internally.
        // Tests must: (1) create the type grain at the full key "HS-TYPE:{raw}",
        // (2) pass only the raw id to GenerateHealthSummaryAsync.
        string rawTypeId  = Guid.NewGuid().ToString();
        string typeGrainKey = $"HS-TYPE:{rawTypeId}";
        IHealthSummaryTypeGrain typeGrain = _cluster.GrainFactory.GetGrain<IHealthSummaryTypeGrain>(typeGrainKey);

        await typeGrain.CreateAsync(rawTypeId, "Workflow Test Summary", null, "PROV-001", "Dr. A");

        await typeGrain.AddOrUpdateComponentAsync(new HealthSummaryComponentConfig
        {
            ComponentType = HealthSummaryComponentType.Demographics,
            IsEnabled = true,
            DisplayOrder = 10,
            MaxOccurrences = 0,
            DaysBack = 0
        });

        await typeGrain.AddOrUpdateComponentAsync(new HealthSummaryComponentConfig
        {
            ComponentType = HealthSummaryComponentType.ActiveProblems,
            IsEnabled = true,
            DisplayOrder = 20,
            MaxOccurrences = 10,
            DaysBack = 0
        });

        await typeGrain.AddOrUpdateComponentAsync(new HealthSummaryComponentConfig
        {
            ComponentType = HealthSummaryComponentType.Allergies,
            IsEnabled = true,
            DisplayOrder = 30,
            MaxOccurrences = 5,
            DaysBack = 0
        });

        string patientId = $"PATIENT-HS-{Guid.NewGuid()}";
        IPatientWorkflowGrain workflow = _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

        string reportId = await workflow.GenerateHealthSummaryAsync(rawTypeId, "PROV-001", "Dr. A");

        Assert.That(reportId, Does.StartWith("HS-REPORT:"));

        HealthSummaryState report = await workflow.GetHealthSummaryAsync(reportId);
        Assert.That(report.ReportId,        Is.EqualTo(reportId));
        Assert.That(report.PatientId,       Is.EqualTo(patientId));
        Assert.That(report.TypeId,          Is.EqualTo(rawTypeId));
        Assert.That(report.TypeName,        Is.EqualTo("Workflow Test Summary"));
        Assert.That(report.GeneratedByName, Is.EqualTo("Dr. A"));
        Assert.That(report.Sections,        Has.Count.EqualTo(3));

        // Section headers should match defaults or config
        List<string> headers = report.Sections.Select(s => s.SectionHeader).ToList();
        Assert.That(headers, Does.Contain("PATIENT DEMOGRAPHICS"));
        Assert.That(headers, Does.Contain("ACTIVE PROBLEMS"));
        Assert.That(headers, Does.Contain("ALLERGIES/ADVERSE REACTIONS"));
    }

    [Test]
    public async Task WorkflowGrain_GenerateHealthSummary_UsesCustomSectionHeader()
    {
        string rawTypeId    = Guid.NewGuid().ToString();
        string typeGrainKey = $"HS-TYPE:{rawTypeId}";
        IHealthSummaryTypeGrain typeGrain = _cluster.GrainFactory.GetGrain<IHealthSummaryTypeGrain>(typeGrainKey);

        await typeGrain.CreateAsync(rawTypeId, "Custom Header Test", null, "PROV-001", "Dr. B");

        await typeGrain.AddOrUpdateComponentAsync(new HealthSummaryComponentConfig
        {
            ComponentType = HealthSummaryComponentType.CurrentMedications,
            IsEnabled = true,
            DisplayOrder = 10,
            MaxOccurrences = 5,
            DaysBack = 0,
            SectionHeader = "MY ACTIVE MEDS"
        });

        string patientId = $"PATIENT-HS-{Guid.NewGuid()}";
        IPatientWorkflowGrain workflow = _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

        string reportId = await workflow.GenerateHealthSummaryAsync(rawTypeId, "PROV-001", "Dr. B");
        HealthSummaryState report = await workflow.GetHealthSummaryAsync(reportId);

        Assert.That(report.Sections, Has.Count.EqualTo(1));
        Assert.That(report.Sections[0].SectionHeader, Is.EqualTo("MY ACTIVE MEDS"));
    }

    [Test]
    public async Task WorkflowGrain_GetHealthSummaryList_ReturnsAllReportsNewestFirst()
    {
        string rawTypeId    = Guid.NewGuid().ToString();
        string typeGrainKey = $"HS-TYPE:{rawTypeId}";
        IHealthSummaryTypeGrain typeGrain = _cluster.GrainFactory.GetGrain<IHealthSummaryTypeGrain>(typeGrainKey);

        await typeGrain.CreateAsync(rawTypeId, "List Test Type", null, "PROV-001", "Dr. C");
        await typeGrain.AddOrUpdateComponentAsync(new HealthSummaryComponentConfig
        {
            ComponentType = HealthSummaryComponentType.ActiveProblems,
            IsEnabled = true, DisplayOrder = 10, MaxOccurrences = 5, DaysBack = 0
        });

        string patientId = $"PATIENT-HS-{Guid.NewGuid()}";
        IPatientWorkflowGrain workflow = _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

        string reportId1 = await workflow.GenerateHealthSummaryAsync(rawTypeId, "PROV-001", "Dr. C");
        string reportId2 = await workflow.GenerateHealthSummaryAsync(rawTypeId, "PROV-002", "Dr. D");

        List<HealthSummaryIndexEntry> list = await workflow.GetHealthSummaryListAsync();

        Assert.That(list, Has.Count.EqualTo(2));
        // Newest first — reportId2 was added last
        Assert.That(list[0].ReportId, Is.EqualTo(reportId2));
        Assert.That(list[1].ReportId, Is.EqualTo(reportId1));
    }

    [Test]
    public async Task WorkflowGrain_GetHealthSummaryByType_FiltersToCorrectType()
    {
        string rawTypeId1    = Guid.NewGuid().ToString();
        string rawTypeId2    = Guid.NewGuid().ToString();

        foreach ((string rawId, string label) in new[] { (rawTypeId1, "Type One"), (rawTypeId2, "Type Two") })
        {
            string key = $"HS-TYPE:{rawId}";
            IHealthSummaryTypeGrain tg = _cluster.GrainFactory.GetGrain<IHealthSummaryTypeGrain>(key);
            await tg.CreateAsync(rawId, label, null, "PROV-001", "Dr. E");
            await tg.AddOrUpdateComponentAsync(new HealthSummaryComponentConfig
            {
                ComponentType = HealthSummaryComponentType.Allergies,
                IsEnabled = true, DisplayOrder = 10, MaxOccurrences = 3, DaysBack = 0
            });
        }

        string patientId = $"PATIENT-HS-{Guid.NewGuid()}";
        IPatientWorkflowGrain workflow = _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

        await workflow.GenerateHealthSummaryAsync(rawTypeId1, "PROV-001", "Dr. E");
        await workflow.GenerateHealthSummaryAsync(rawTypeId2, "PROV-001", "Dr. E");
        await workflow.GenerateHealthSummaryAsync(rawTypeId1, "PROV-001", "Dr. E"); // second type1

        List<HealthSummaryIndexEntry> type1Reports = await workflow.GetHealthSummaryByTypeAsync(rawTypeId1);
        List<HealthSummaryIndexEntry> type2Reports = await workflow.GetHealthSummaryByTypeAsync(rawTypeId2);

        Assert.That(type1Reports, Has.Count.EqualTo(2));
        Assert.That(type2Reports, Has.Count.EqualTo(1));
        Assert.That(type1Reports.All(r => r.TypeId == rawTypeId1), Is.True);
    }

    [Test]
    public async Task WorkflowGrain_GenerateHealthSummary_EmptyTemplate_ProducesEmptyReport()
    {
        string rawTypeId    = Guid.NewGuid().ToString();
        string typeGrainKey = $"HS-TYPE:{rawTypeId}";
        IHealthSummaryTypeGrain typeGrain = _cluster.GrainFactory.GetGrain<IHealthSummaryTypeGrain>(typeGrainKey);

        await typeGrain.CreateAsync(rawTypeId, "Empty Template", null, "PROV-001", "Dr. F");
        // No components added

        string patientId = $"PATIENT-HS-{Guid.NewGuid()}";
        IPatientWorkflowGrain workflow = _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

        string reportId = await workflow.GenerateHealthSummaryAsync(rawTypeId, "PROV-001", "Dr. F");
        HealthSummaryState report = await workflow.GetHealthSummaryAsync(reportId);

        Assert.That(report.ReportId, Does.StartWith("HS-REPORT:"));
        Assert.That(report.Sections, Is.Empty);
    }
}
