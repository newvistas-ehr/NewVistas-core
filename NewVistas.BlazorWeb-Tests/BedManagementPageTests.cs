// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Bunit;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.BlazorWeb.Components.Pages;
using NSubstitute;

namespace NewVistas.BlazorWeb_Tests;

[TestFixture]
public class BedManagementPageTests : BlazorTestBase
{
    private IBedBoardGrain _mockBedBoard = null!;

    public override void Setup()
    {
        base.Setup();
        _mockBedBoard = Substitute.For<IBedBoardGrain>();
        MockGrainFactory.GetGrain<IBedBoardGrain>(Arg.Any<string>(), Arg.Any<string?>())
            .Returns(_mockBedBoard);
        _mockBedBoard.GetAllBedsAsync().Returns(new List<BedSummaryEntry>());
    }

    [Test]
    public void BedManagement_RendersPageTitle()
    {
        var cut = Ctx.Render<BedManagement>();
        Assert.That(cut.Markup, Does.Contain("Bed Management"));
    }

    [Test]
    public void BedManagement_RendersTabBar()
    {
        var cut = Ctx.Render<BedManagement>();
        Assert.That(cut.Markup, Does.Contain("Bed Board"));
        Assert.That(cut.Markup, Does.Contain("Statistics"));
    }

    [Test]
    public async Task BedManagement_ShowsErrorOnFailure()
    {
        _mockBedBoard.GetAllBedsAsync().Returns<List<BedSummaryEntry>>(
            _ => throw new Exception("Board unavailable"));

        var cut = Ctx.Render<BedManagement>();

        Assert.That(cut.Markup, Does.Contain("Board unavailable"));
    }
}
