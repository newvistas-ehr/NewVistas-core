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
/// Functional tests for Health Summary — VistA File #142.
/// Tests end-to-end workflows via <see cref="IPatientWorkflowGrain"/>
/// with <see cref="IHealthSummaryTypeGrain"/> for type setup.
/// </summary>
[TestFixture]
public class HealthSummaryWorkflowTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    private IPatientWorkflowGrain Workflow(string patientId)
        => _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

    /// <summary>
    /// Helper: creates a health summary type grain with a single Demographics component.
    /// The workflow grain prepends "HS-TYPE:" internally, so we must create the type grain
    /// at "HS-TYPE:{rawTypeId}" and pass the rawTypeId to workflow methods.
    /// </summary>
    private async Task<string> CreateTypeWithDemographicsAsync(string typeName)
    {
        string rawTypeId = Guid.NewGuid().ToString();
        string typeGrainKey = $"HS-TYPE:{rawTypeId}";
        IHealthSummaryTypeGrain typeGrain =
            _cluster.GrainFactory.GetGrain<IHealthSummaryTypeGrain>(typeGrainKey);

        await typeGrain.CreateAsync(rawTypeId, typeName, null, "PROV-001", "Dr. Smith");

        await typeGrain.AddOrUpdateComponentAsync(new HealthSummaryComponentConfig
        {
            ComponentType = HealthSummaryComponentType.Demographics,
            IsEnabled = true,
            DisplayOrder = 10,
            MaxOccurrences = 0,
            DaysBack = 0
        });

        return rawTypeId;
    }

    // ── Generate health summary ────────────────────────────────────────────────

    [Test]
    public async Task GenerateHealthSummary_ReturnsReportId()
    {
        // Arrange
        string rawTypeId = await CreateTypeWithDemographicsAsync("Basic Summary");
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        // Act
        string reportId = await wf.GenerateHealthSummaryAsync(rawTypeId, "PROV-001", "Dr. Smith");

        // Assert
        Assert.That(reportId, Does.StartWith("HS-REPORT:"));
    }

    [Test]
    public async Task GenerateHealthSummary_PersistsReportState()
    {
        // Arrange
        string rawTypeId = await CreateTypeWithDemographicsAsync("Full Report Test");
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        // Act
        string reportId = await wf.GenerateHealthSummaryAsync(rawTypeId, "PROV-002", "Dr. Jones");
        HealthSummaryState report = await wf.GetHealthSummaryAsync(reportId);

        // Assert
        Assert.That(report.ReportId, Is.EqualTo(reportId));
        Assert.That(report.PatientId, Is.EqualTo(patientId));
        Assert.That(report.TypeId, Is.EqualTo(rawTypeId));
        Assert.That(report.GeneratedByName, Is.EqualTo("Dr. Jones"));
        Assert.That(report.Sections, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task GenerateHealthSummary_TypeNameStored()
    {
        // Arrange
        string rawTypeId = await CreateTypeWithDemographicsAsync("Outpatient Summary");
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        // Act
        string reportId = await wf.GenerateHealthSummaryAsync(rawTypeId, "PROV-001", "Dr. A");
        HealthSummaryState report = await wf.GetHealthSummaryAsync(reportId);

        // Assert
        Assert.That(report.TypeName, Is.EqualTo("Outpatient Summary"));
    }

    [Test]
    public async Task GetHealthSummaryList_ReturnsEmptyByDefault()
    {
        // Arrange
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        // Act
        List<HealthSummaryIndexEntry> list = await wf.GetHealthSummaryListAsync();

        // Assert
        Assert.That(list, Is.Empty);
    }

    [Test]
    public async Task GenerateHealthSummary_AppearsInList()
    {
        // Arrange
        string rawTypeId = await CreateTypeWithDemographicsAsync("Index Test");
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        // Act
        string reportId = await wf.GenerateHealthSummaryAsync(rawTypeId, "PROV-001", "Dr. B");
        List<HealthSummaryIndexEntry> list = await wf.GetHealthSummaryListAsync();

        // Assert
        Assert.That(list, Has.Count.EqualTo(1));
        Assert.That(list[0].ReportId, Is.EqualTo(reportId));
        Assert.That(list[0].TypeId, Is.EqualTo(rawTypeId));
    }

    [Test]
    public async Task MultipleReports_AllAppearNewestFirst()
    {
        // Arrange
        string rawTypeId = await CreateTypeWithDemographicsAsync("Multi Report Type");
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        // Act
        string reportId1 = await wf.GenerateHealthSummaryAsync(rawTypeId, "PROV-001", "Dr. C");
        string reportId2 = await wf.GenerateHealthSummaryAsync(rawTypeId, "PROV-002", "Dr. D");

        List<HealthSummaryIndexEntry> list = await wf.GetHealthSummaryListAsync();

        // Assert
        Assert.That(list, Has.Count.EqualTo(2));
        // Newest first
        Assert.That(list[0].ReportId, Is.EqualTo(reportId2));
        Assert.That(list[1].ReportId, Is.EqualTo(reportId1));
    }

    [Test]
    public async Task GetHealthSummaryByType_FiltersCorrectly()
    {
        // Arrange
        string rawTypeId1 = await CreateTypeWithDemographicsAsync("Type Alpha");
        string rawTypeId2 = await CreateTypeWithDemographicsAsync("Type Beta");
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        // Act
        await wf.GenerateHealthSummaryAsync(rawTypeId1, "PROV-001", "Dr. E");
        await wf.GenerateHealthSummaryAsync(rawTypeId2, "PROV-001", "Dr. E");
        await wf.GenerateHealthSummaryAsync(rawTypeId1, "PROV-001", "Dr. E");

        List<HealthSummaryIndexEntry> type1Reports = await wf.GetHealthSummaryByTypeAsync(rawTypeId1);
        List<HealthSummaryIndexEntry> type2Reports = await wf.GetHealthSummaryByTypeAsync(rawTypeId2);

        // Assert
        Assert.That(type1Reports, Has.Count.EqualTo(2));
        Assert.That(type2Reports, Has.Count.EqualTo(1));
        Assert.That(type1Reports.All(r => r.TypeId == rawTypeId1), Is.True);
    }

    [Test]
    public async Task GenerateHealthSummary_MultipleComponents_AllSectionsIncluded()
    {
        // Arrange
        string rawTypeId = Guid.NewGuid().ToString();
        string typeGrainKey = $"HS-TYPE:{rawTypeId}";
        IHealthSummaryTypeGrain typeGrain =
            _cluster.GrainFactory.GetGrain<IHealthSummaryTypeGrain>(typeGrainKey);

        await typeGrain.CreateAsync(rawTypeId, "Multi Component Summary", null, "PROV-001", "Dr. F");

        await typeGrain.AddOrUpdateComponentAsync(new HealthSummaryComponentConfig
        {
            ComponentType = HealthSummaryComponentType.Demographics,
            IsEnabled = true, DisplayOrder = 10, MaxOccurrences = 0, DaysBack = 0
        });
        await typeGrain.AddOrUpdateComponentAsync(new HealthSummaryComponentConfig
        {
            ComponentType = HealthSummaryComponentType.ActiveProblems,
            IsEnabled = true, DisplayOrder = 20, MaxOccurrences = 10, DaysBack = 0
        });
        await typeGrain.AddOrUpdateComponentAsync(new HealthSummaryComponentConfig
        {
            ComponentType = HealthSummaryComponentType.Allergies,
            IsEnabled = true, DisplayOrder = 30, MaxOccurrences = 5, DaysBack = 0
        });

        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        // Act
        string reportId = await wf.GenerateHealthSummaryAsync(rawTypeId, "PROV-001", "Dr. F");
        HealthSummaryState report = await wf.GetHealthSummaryAsync(reportId);

        // Assert
        Assert.That(report.Sections, Has.Count.EqualTo(3));
        List<string> headers = report.Sections.Select(s => s.SectionHeader).ToList();
        Assert.That(headers, Does.Contain("PATIENT DEMOGRAPHICS"));
        Assert.That(headers, Does.Contain("ACTIVE PROBLEMS"));
        Assert.That(headers, Does.Contain("ALLERGIES/ADVERSE REACTIONS"));
    }

    [Test]
    public async Task GenerateHealthSummary_EmptyTemplate_ProducesEmptyReport()
    {
        // Arrange
        string rawTypeId = Guid.NewGuid().ToString();
        string typeGrainKey = $"HS-TYPE:{rawTypeId}";
        IHealthSummaryTypeGrain typeGrain =
            _cluster.GrainFactory.GetGrain<IHealthSummaryTypeGrain>(typeGrainKey);

        await typeGrain.CreateAsync(rawTypeId, "Empty Template", null, "PROV-001", "Dr. G");
        // No components added

        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        // Act
        string reportId = await wf.GenerateHealthSummaryAsync(rawTypeId, "PROV-001", "Dr. G");
        HealthSummaryState report = await wf.GetHealthSummaryAsync(reportId);

        // Assert
        Assert.That(report.ReportId, Does.StartWith("HS-REPORT:"));
        Assert.That(report.Sections, Is.Empty);
    }

    [Test]
    public async Task IndependentPatients_SeparateReportLists()
    {
        // Arrange
        string rawTypeId = await CreateTypeWithDemographicsAsync("Patient Isolation Test");
        string p1 = $"PATIENT-{Guid.NewGuid()}";
        string p2 = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf1 = Workflow(p1);
        IPatientWorkflowGrain wf2 = Workflow(p2);

        // Act
        await wf1.GenerateHealthSummaryAsync(rawTypeId, "PROV-001", "Dr. H");
        await wf2.GenerateHealthSummaryAsync(rawTypeId, "PROV-001", "Dr. H");
        await wf2.GenerateHealthSummaryAsync(rawTypeId, "PROV-001", "Dr. H");

        List<HealthSummaryIndexEntry> p1List = await wf1.GetHealthSummaryListAsync();
        List<HealthSummaryIndexEntry> p2List = await wf2.GetHealthSummaryListAsync();

        // Assert
        Assert.That(p1List, Has.Count.EqualTo(1));
        Assert.That(p2List, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task GenerateHealthSummary_SectionCountInIndex()
    {
        // Arrange
        string rawTypeId = Guid.NewGuid().ToString();
        string typeGrainKey = $"HS-TYPE:{rawTypeId}";
        IHealthSummaryTypeGrain typeGrain =
            _cluster.GrainFactory.GetGrain<IHealthSummaryTypeGrain>(typeGrainKey);

        await typeGrain.CreateAsync(rawTypeId, "Section Count Type", null, "PROV-001", "Dr. I");
        await typeGrain.AddOrUpdateComponentAsync(new HealthSummaryComponentConfig
        {
            ComponentType = HealthSummaryComponentType.Demographics,
            IsEnabled = true, DisplayOrder = 10, MaxOccurrences = 0, DaysBack = 0
        });
        await typeGrain.AddOrUpdateComponentAsync(new HealthSummaryComponentConfig
        {
            ComponentType = HealthSummaryComponentType.VitalSigns,
            IsEnabled = true, DisplayOrder = 20, MaxOccurrences = 3, DaysBack = 30
        });

        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        // Act
        string reportId = await wf.GenerateHealthSummaryAsync(rawTypeId, "PROV-001", "Dr. I");
        List<HealthSummaryIndexEntry> list = await wf.GetHealthSummaryListAsync();

        // Assert
        Assert.That(list, Has.Count.EqualTo(1));
        Assert.That(list[0].SectionCount, Is.EqualTo(2));
    }
}
