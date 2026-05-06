// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Bunit;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.BlazorWeb.Components.Pages;
using NSubstitute;

namespace NewVistas.BlazorWeb_Tests;

[TestFixture]
public class PolytraumaTBIPageTests : BlazorTestBase
{
    private IPolytraumaRegistryIndexGrain _mockRegIndex = null!;

    public override void Setup()
    {
        base.Setup();
        _mockRegIndex = Substitute.For<IPolytraumaRegistryIndexGrain>();
        MockGrainFactory.GetGrain<IPolytraumaRegistryIndexGrain>(Arg.Any<string>(), Arg.Any<string?>()).Returns(_mockRegIndex);
        _mockRegIndex.GetActivePatientAsync().Returns(new List<PolytraumaRegistrySummaryEntry>());
    }

    [Test]
    public void PolytraumaTBI_RendersTitle()
    {
        var cut = Ctx.Render<PolytraumaTBI>();
        Assert.That(cut.Markup, Does.Contain("Polytrauma / TBI"));
    }

    [Test]
    public void PolytraumaTBI_RendersTabs()
    {
        var cut = Ctx.Render<PolytraumaTBI>();
        Assert.That(cut.Markup, Does.Contain("Registry"));
        Assert.That(cut.Markup, Does.Contain("TBI Screenings"));
        Assert.That(cut.Markup, Does.Contain("Record"));
    }

    [Test]
    public async Task PolytraumaTBI_LoadsRegistry()
    {
        var entries = new List<PolytraumaRegistrySummaryEntry>
        {
            new() { PatientId = "P1", PatientName = "Veteran Smith",
                     Status = PolytraumaStatus.Active, InjuryCount = 3,
                     IssTotalScore = 25, RegistrationDate = DateTime.UtcNow.AddDays(-30) }
        };
        _mockRegIndex.GetActivePatientAsync().Returns(entries);

        var cut = Ctx.Render<PolytraumaTBI>();
        cut.WaitForState(() => cut.Markup.Contains("Veteran Smith"));

        Assert.That(cut.Markup, Does.Contain("Veteran Smith"));
    }

    [Test]
    public async Task PolytraumaTBI_ShowsEmptyState()
    {
        _mockRegIndex.GetActivePatientAsync().Returns(new List<PolytraumaRegistrySummaryEntry>());

        var cut = Ctx.Render<PolytraumaTBI>();
        cut.WaitForState(() => cut.Markup.Contains("No polytrauma patients"));

        Assert.That(cut.Markup, Does.Contain("No polytrauma patients found"));
    }
}
