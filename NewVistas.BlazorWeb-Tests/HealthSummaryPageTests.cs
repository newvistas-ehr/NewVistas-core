// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Bunit;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.BlazorWeb.Components.Pages;
using NSubstitute;

namespace NewVistas.BlazorWeb_Tests;

[TestFixture]
public class HealthSummaryPageTests : BlazorTestBase
{
    private IHealthSummaryTypeIndexGrain _mockTypeIndex = null!;

    public override void Setup()
    {
        base.Setup();
        _mockTypeIndex = Substitute.For<IHealthSummaryTypeIndexGrain>();
        MockGrainFactory.GetGrain<IHealthSummaryTypeIndexGrain>(Arg.Any<string>(), Arg.Any<string?>())
            .Returns(_mockTypeIndex);
        _mockTypeIndex.GetAllAsync().Returns(new List<HealthSummaryTypeIndexEntry>());
    }

    [Test]
    public void HealthSummary_RendersPageTitle()
    {
        var cut = Ctx.Render<HealthSummary>();
        Assert.That(cut.Markup, Does.Contain("Health Summary"));
    }

    [Test]
    public void HealthSummary_RendersTabBar()
    {
        var cut = Ctx.Render<HealthSummary>();
        Assert.That(cut.Markup, Does.Contain("Generate Summary"));
        Assert.That(cut.Markup, Does.Contain("Summary History"));
        Assert.That(cut.Markup, Does.Contain("Summary Types"));
    }

    [Test]
    public async Task HealthSummary_LoadsPatientHistoryFromGrain()
    {
        var entries = new List<HealthSummaryIndexEntry>
        {
            new() { ReportId = "RPT-001", PatientId = "PAT-001", TypeId = "T1",
                     TypeName = "CPRS Summary", GeneratedDate = DateTime.UtcNow,
                     GeneratedById = "PROV-001", GeneratedByName = "Dr. Smith", SectionCount = 5 }
        };
        MockWorkflowGrain.GetHealthSummaryListAsync().Returns(entries);

        var cut = Ctx.Render<HealthSummary>();
        cut.Find("input.patient-input").Change("PAT-001");
        await cut.Find("button.btn-primary").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        await MockWorkflowGrain.Received(1).GetHealthSummaryListAsync();
    }

    [Test]
    public async Task HealthSummary_ShowsErrorOnGrainFailure()
    {
        MockWorkflowGrain.GetHealthSummaryListAsync().Returns<List<HealthSummaryIndexEntry>>(
            _ => throw new Exception("Connection refused"));

        var cut = Ctx.Render<HealthSummary>();
        cut.Find("input.patient-input").Change("PAT-002");
        await cut.Find("button.btn-primary").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        Assert.That(cut.Markup, Does.Contain("Error loading data"));
    }
}
