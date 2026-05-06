// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Bunit;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.BlazorWeb.Components.Pages;
using NSubstitute;

namespace NewVistas.BlazorWeb_Tests;

[TestFixture]
public class WardStockPageTests : BlazorTestBase
{
    private IWardStockIndexGrain _mockIndex = null!;
    private IWardReplenishmentLogGrain _mockReplenish = null!;

    public override void Setup()
    {
        base.Setup();
        _mockIndex = Substitute.For<IWardStockIndexGrain>();
        _mockReplenish = Substitute.For<IWardReplenishmentLogGrain>();
        MockGrainFactory.GetGrain<IWardStockIndexGrain>(Arg.Any<string>(), Arg.Any<string?>()).Returns(_mockIndex);
        MockGrainFactory.GetGrain<IWardReplenishmentLogGrain>(Arg.Any<string>(), Arg.Any<string?>()).Returns(_mockReplenish);

        _mockIndex.GetAllItemsAsync().Returns(new List<WardStockSummaryEntry>());
    }

    [Test]
    public void WardStock_RendersPageTitle()
    {
        var cut = Ctx.Render<WardStock>();
        Assert.That(cut.Markup, Does.Contain("Ward Stock"));
    }

    [Test]
    public void WardStock_ShowsEmptyState()
    {
        var cut = Ctx.Render<WardStock>();
        Assert.That(cut.Markup, Does.Contain("No ward stock items configured"));
    }

    [Test]
    public void WardStock_LoadsInventoryFromGrain()
    {
        _mockIndex.GetAllItemsAsync().Returns(new List<WardStockSummaryEntry>
        {
            new() { DrugId = "D-001", DrugName = "Acetaminophen 500mg", QuantityOnHand = 100, ParLevel = 150, ReorderPoint = 50, UnitOfMeasure = "tablets", NeedsReplenishment = false }
        });

        var cut = Ctx.Render<WardStock>();
        Assert.That(cut.Markup, Does.Contain("Acetaminophen 500mg"));
        Assert.That(cut.Markup, Does.Contain("OK"));
    }
}
