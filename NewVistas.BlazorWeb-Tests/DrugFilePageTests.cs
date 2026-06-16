// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Bunit;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.BlazorWeb.Components.Pages;
using NSubstitute;

namespace NewVistas.BlazorWeb_Tests;

[TestFixture]
public class DrugFilePageTests : BlazorTestBase
{
    private IDrugIndexGrain _mockDrugIndex = null!;
    private IOrderableItemIndexGrain _mockOiIndex = null!;
    private IMedicationRouteIndexGrain _mockRouteIndex = null!;
    private IDoseUnitIndexGrain _mockDoseUnitIndex = null!;

    public override void Setup()
    {
        base.Setup();
        _mockDrugIndex = Substitute.For<IDrugIndexGrain>();
        _mockOiIndex = Substitute.For<IOrderableItemIndexGrain>();
        _mockRouteIndex = Substitute.For<IMedicationRouteIndexGrain>();
        _mockDoseUnitIndex = Substitute.For<IDoseUnitIndexGrain>();

        MockGrainFactory.GetGrain<IDrugIndexGrain>(Arg.Any<string>(), Arg.Any<string?>()).Returns(_mockDrugIndex);
        MockGrainFactory.GetGrain<IOrderableItemIndexGrain>(Arg.Any<string>(), Arg.Any<string?>()).Returns(_mockOiIndex);
        MockGrainFactory.GetGrain<IMedicationRouteIndexGrain>(Arg.Any<string>(), Arg.Any<string?>()).Returns(_mockRouteIndex);
        MockGrainFactory.GetGrain<IDoseUnitIndexGrain>(Arg.Any<string>(), Arg.Any<string?>()).Returns(_mockDoseUnitIndex);

        _mockDrugIndex.GetStatusAsync().Returns(new DrugIndexStatus { IsLoaded = false, TotalDrugs = 0 });
        _mockOiIndex.GetStatusAsync().Returns(new OrderableItemIndexStatus { IsLoaded = false, TotalItems = 0 });
        _mockRouteIndex.IsLoadedAsync().Returns(false);
        _mockDoseUnitIndex.IsLoadedAsync().Returns(false);
    }

    [Test]
    public void DrugFile_RendersPageTitle()
    {
        var cut = Ctx.Render<DrugFile>();
        Assert.That(cut.Markup, Does.Contain("Drug File"));
    }

    [Test]
    public void DrugFile_RendersTabs()
    {
        var cut = Ctx.Render<DrugFile>();
        Assert.That(cut.Markup, Does.Contain("Drugs"));
        Assert.That(cut.Markup, Does.Contain("Orderable Items"));
        Assert.That(cut.Markup, Does.Contain("Routes"));
        Assert.That(cut.Markup, Does.Contain("Dose Units"));
    }

    [Test]
    public void DrugFile_ShowsNotLoadedBanner()
    {
        var cut = Ctx.Render<DrugFile>();
        Assert.That(cut.Markup, Does.Contain("Drug file data has not been loaded"));
    }
}
