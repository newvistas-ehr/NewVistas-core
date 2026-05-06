// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Bunit;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.BlazorWeb.Components.Pages;
using NSubstitute;

namespace NewVistas.BlazorWeb_Tests;

[TestFixture]
public class InfectionControlPageTests : BlazorTestBase
{
    private IHAICaseIndexGrain _mockCaseIndex = null!;
    private IOutbreakIndexGrain _mockOutbreakIndex = null!;

    public override void Setup()
    {
        base.Setup();
        _mockCaseIndex = Substitute.For<IHAICaseIndexGrain>();
        _mockOutbreakIndex = Substitute.For<IOutbreakIndexGrain>();
        MockGrainFactory.GetGrain<IHAICaseIndexGrain>(Arg.Any<string>(), Arg.Any<string?>()).Returns(_mockCaseIndex);
        MockGrainFactory.GetGrain<IOutbreakIndexGrain>(Arg.Any<string>(), Arg.Any<string?>()).Returns(_mockOutbreakIndex);
        _mockCaseIndex.GetAllCasesAsync().Returns(new List<HAICaseSummary>());
        _mockOutbreakIndex.GetAllOutbreaksAsync().Returns(new List<OutbreakSummary>());
    }

    [Test]
    public void InfectionControl_RendersTitle()
    {
        var cut = Ctx.Render<InfectionControl>();
        Assert.That(cut.Markup, Does.Contain("Infection Control"));
    }

    [Test]
    public void InfectionControl_RendersTabs()
    {
        var cut = Ctx.Render<InfectionControl>();
        Assert.That(cut.Markup, Does.Contain("HAI Cases"));
        Assert.That(cut.Markup, Does.Contain("Outbreaks"));
        Assert.That(cut.Markup, Does.Contain("Antibiogram"));
    }

    [Test]
    public async Task InfectionControl_LoadsCases()
    {
        var cases = new List<HAICaseSummary>
        {
            new() { CaseId = "C1", PatientId = "P1", PatientName = "John Doe",
                     HAIType = HAIType.CLABSI, Status = HAICaseStatus.Confirmed,
                     Pathogen = "MRSA", LocationName = "ICU" }
        };
        _mockCaseIndex.GetAllCasesAsync().Returns(cases);

        var cut = Ctx.Render<InfectionControl>();
        cut.WaitForState(() => cut.Markup.Contains("John Doe"));

        Assert.That(cut.Markup, Does.Contain("CLABSI"));
        Assert.That(cut.Markup, Does.Contain("MRSA"));
    }

    [Test]
    public async Task InfectionControl_ShowsErrorOnFailure()
    {
        _mockCaseIndex.GetAllCasesAsync().Returns<List<HAICaseSummary>>(
            _ => throw new Exception("Connection refused"));
        _mockOutbreakIndex.GetAllOutbreaksAsync().Returns<List<OutbreakSummary>>(
            _ => throw new Exception("Connection refused"));

        var cut = Ctx.Render<InfectionControl>();
        cut.WaitForState(() => cut.Markup.Contains("Failed to load"), TimeSpan.FromSeconds(3));

        Assert.That(cut.Markup, Does.Contain("Failed to load outbreaks"));
    }
}
