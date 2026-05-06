// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.BlazorWeb.Components.Pages;
using NSubstitute;

namespace NewVistas.BlazorWeb_Tests;

[TestFixture]
public class IfcapPageTests : BlazorTestBase
{
    private IControlPointIndexGrain _mockCpIdx = null!;

    public override void Setup()
    {
        base.Setup();
        _mockCpIdx = Substitute.For<IControlPointIndexGrain>();
        _mockCpIdx.GetByFiscalYearAsync(Arg.Any<int>()).Returns(new List<ControlPointIndexEntry>());
        _mockCpIdx.GetAllAsync().Returns(new List<ControlPointIndexEntry>());
        MockGrainFactory.GetGrain<IControlPointIndexGrain>(Arg.Any<string>(), Arg.Any<string?>()).Returns(_mockCpIdx);
    }

    [Test]
    public void Ifcap_RendersPageTitle()
    {
        var cut = Ctx.Render<Ifcap>();
        Assert.That(cut.Markup, Does.Contain("Integrated Funds Distribution"));
    }

    [Test]
    public void Ifcap_RendersTabs()
    {
        var cut = Ctx.Render<Ifcap>();
        Assert.That(cut.Markup, Does.Contain("Control Points"));
        Assert.That(cut.Markup, Does.Contain("Purchase Requests"));
        Assert.That(cut.Markup, Does.Contain("Purchase Orders"));
        Assert.That(cut.Markup, Does.Contain("Vendors"));
        Assert.That(cut.Markup, Does.Contain("Site Parameters"));
    }

    [Test]
    public async Task Ifcap_ShowsErrorOnGrainFailure()
    {
        _mockCpIdx.GetByFiscalYearAsync(Arg.Any<int>()).Returns<List<ControlPointIndexEntry>>(
            _ => throw new Exception("CP index error"));

        var cut = Ctx.Render<Ifcap>();
        // The OnInitializedAsync calls LoadControlPoints which should fail
        // Wait for the state change
        cut.WaitForState(() => cut.Markup.Contains("Error loading control points"), TimeSpan.FromSeconds(2));

        Assert.That(cut.Markup, Does.Contain("Error loading control points"));
    }
}
