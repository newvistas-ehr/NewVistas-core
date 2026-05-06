// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Bunit;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.BlazorWeb.Components.Pages;
using NSubstitute;

namespace NewVistas.BlazorWeb_Tests;

[TestFixture]
public class RecordTrackingPageTests : BlazorTestBase
{
    private IChartIndexGrain _mockChartIndex = null!;
    private IChartRequestIndexGrain _mockReqIndex = null!;

    public override void Setup()
    {
        base.Setup();
        _mockChartIndex = Substitute.For<IChartIndexGrain>();
        _mockReqIndex = Substitute.For<IChartRequestIndexGrain>();
        MockGrainFactory.GetGrain<IChartIndexGrain>(Arg.Any<string>(), Arg.Any<string?>()).Returns(_mockChartIndex);
        MockGrainFactory.GetGrain<IChartRequestIndexGrain>(Arg.Any<string>(), Arg.Any<string?>()).Returns(_mockReqIndex);
        _mockChartIndex.GetAllChartsAsync().Returns(new List<ChartIndexEntry>());
        _mockReqIndex.GetPendingRequestsAsync().Returns(new List<ChartRequestIndexEntry>());
        _mockReqIndex.GetUrgentRequestsAsync().Returns(new List<ChartRequestIndexEntry>());
    }

    [Test]
    public void RecordTracking_RendersTitle()
    {
        var cut = Ctx.Render<RecordTracking>();
        Assert.That(cut.Markup, Does.Contain("Record Tracking"));
    }

    [Test]
    public void RecordTracking_RendersTabs()
    {
        var cut = Ctx.Render<RecordTracking>();
        Assert.That(cut.Markup, Does.Contain("Chart Locator"));
        Assert.That(cut.Markup, Does.Contain("Request Queue"));
        Assert.That(cut.Markup, Does.Contain("Dashboard"));
    }

    [Test]
    public async Task RecordTracking_LoadsDashboard()
    {
        var charts = new List<ChartIndexEntry>
        {
            new() { PatientId = "P1", PatientName = "Smith, John", ChartNumber = "CH-001",
                     IsCheckedOut = true, IsLost = false, IsOnRequest = false }
        };
        _mockChartIndex.GetAllChartsAsync().Returns(charts);

        var cut = Ctx.Render<RecordTracking>();
        cut.WaitForState(() => cut.Markup.Contains("Dashboard") && cut.FindAll(".stat-value").Count > 0, TimeSpan.FromSeconds(3));

        // Dashboard loads on init; verify it rendered something
        Assert.That(cut.Markup, Does.Contain("Dashboard"));
    }

    [Test]
    public async Task RecordTracking_ShowsErrorOnFailure()
    {
        _mockChartIndex.GetAllChartsAsync().Returns<List<ChartIndexEntry>>(
            _ => throw new Exception("DB down"));

        var cut = Ctx.Render<RecordTracking>();
        cut.WaitForState(() => cut.Markup.Contains("Failed to load"), TimeSpan.FromSeconds(3));

        Assert.That(cut.Markup, Does.Contain("Failed to load dashboard"));
    }
}
