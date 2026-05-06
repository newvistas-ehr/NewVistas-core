// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Bunit;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.BlazorWeb.Components.Pages;
using NSubstitute;

namespace NewVistas.BlazorWeb_Tests;

[TestFixture]
public class SuicidePreventionPageTests : BlazorTestBase
{
    private ISuicidePreventionIndexGrain _mockSpIndex = null!;

    public override void Setup()
    {
        base.Setup();
        _mockSpIndex = Substitute.For<ISuicidePreventionIndexGrain>();
        MockGrainFactory.GetGrain<ISuicidePreventionIndexGrain>(Arg.Any<string>(), Arg.Any<string?>()).Returns(_mockSpIndex);
        _mockSpIndex.GetAllPatientsAsync().Returns(new List<PatientHighRiskSummary>());
    }

    [Test]
    public void SuicidePrevention_RendersTitle()
    {
        var cut = Ctx.Render<SuicidePrevention>();
        Assert.That(cut.Markup, Does.Contain("Suicide Prevention"));
    }

    [Test]
    public void SuicidePrevention_RendersTabs()
    {
        var cut = Ctx.Render<SuicidePrevention>();
        Assert.That(cut.Markup, Does.Contain("High-Risk Roster"));
        Assert.That(cut.Markup, Does.Contain("Safety Plans"));
        Assert.That(cut.Markup, Does.Contain("Follow-Up Tracking"));
    }

    [Test]
    public async Task SuicidePrevention_LoadsRoster()
    {
        var patients = new List<PatientHighRiskSummary>
        {
            new() { PatientId = "P1", PatientName = "Test Patient",
                     CurrentRiskLevel = RiskLevel.High, IsHighRiskFlagged = true,
                     ActivePlanCount = 1 }
        };
        _mockSpIndex.GetAllPatientsAsync().Returns(patients);

        var cut = Ctx.Render<SuicidePrevention>();
        cut.WaitForState(() => cut.Markup.Contains("Test Patient"));

        Assert.That(cut.Markup, Does.Contain("High"));
        Assert.That(cut.Markup, Does.Contain("YES"));
    }

    [Test]
    public async Task SuicidePrevention_ShowsErrorOnFailure()
    {
        _mockSpIndex.GetAllPatientsAsync().Returns<List<PatientHighRiskSummary>>(
            _ => throw new Exception("Network error"));

        var cut = Ctx.Render<SuicidePrevention>();
        cut.WaitForState(() => cut.Markup.Contains("Failed to load"));

        Assert.That(cut.Markup, Does.Contain("Failed to load patient roster"));
    }
}
