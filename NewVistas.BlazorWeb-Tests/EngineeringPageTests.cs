// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Bunit;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.BlazorWeb.Components.Pages;
using NSubstitute;

namespace NewVistas.BlazorWeb_Tests;

[TestFixture]
public class EngineeringPageTests : BlazorTestBase
{
    private IEngineeringWorkOrderIndexGrain _mockWoIndex = null!;
    private IFacilityIndexGrain _mockFacIndex = null!;

    public override void Setup()
    {
        base.Setup();
        _mockWoIndex = Substitute.For<IEngineeringWorkOrderIndexGrain>();
        _mockFacIndex = Substitute.For<IFacilityIndexGrain>();
        MockGrainFactory.GetGrain<IEngineeringWorkOrderIndexGrain>(Arg.Any<string>(), Arg.Any<string?>()).Returns(_mockWoIndex);
        MockGrainFactory.GetGrain<IFacilityIndexGrain>(Arg.Any<string>(), Arg.Any<string?>()).Returns(_mockFacIndex);
        _mockWoIndex.GetActiveAsync(Arg.Any<int>()).Returns(new List<WorkOrderIndexEntry>());
    }

    [Test]
    public void Engineering_RendersPageTitle()
    {
        var cut = Ctx.Render<Engineering>();
        Assert.That(cut.Markup, Does.Contain("Engineering"));
    }

    [Test]
    public void Engineering_RendersTabs()
    {
        var cut = Ctx.Render<Engineering>();
        Assert.That(cut.Markup, Does.Contain("Work Orders"));
        Assert.That(cut.Markup, Does.Contain("Facilities"));
    }

    [Test]
    public async Task Engineering_LoadsActiveWorkOrders()
    {
        var orders = new List<WorkOrderIndexEntry>
        {
            new() { WorkOrderId = "WO-1", WorkOrderNumber = "WO-2026-001",
                     FacilityName = "Building A", Status = WorkOrderStatus.Open,
                     Priority = WorkOrderPriority.Routine, Shop = EngineeringShop.General }
        };
        _mockWoIndex.GetActiveAsync(Arg.Any<int>()).Returns(orders);

        var cut = Ctx.Render<Engineering>();
        cut.WaitForState(() => cut.Markup.Contains("WO-2026-001"));

        Assert.That(cut.Markup, Does.Contain("Building A"));
    }

    [Test]
    public async Task Engineering_ShowsErrorOnFailure()
    {
        _mockWoIndex.GetActiveAsync(Arg.Any<int>()).Returns<List<WorkOrderIndexEntry>>(
            _ => throw new Exception("Silo down"));

        var cut = Ctx.Render<Engineering>();
        cut.WaitForState(() => cut.Markup.Contains("Failed to load"));

        Assert.That(cut.Markup, Does.Contain("Failed to load active work orders"));
    }
}
