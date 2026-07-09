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
public class BedManagementPageTests : BlazorTestBase
{
    private IBedCapacityGrain _mockCapacity = null!;
    private IInstitutionIndexGrain _mockInstitutionIndex = null!;

    public override void Setup()
    {
        base.Setup();
        _mockCapacity = Substitute.For<IBedCapacityGrain>();
        MockGrainFactory.GetGrain<IBedCapacityGrain>(Arg.Any<string>(), Arg.Any<string?>())
            .Returns(_mockCapacity);
        _mockCapacity.GetUnitsAsync(Arg.Any<bool>()).Returns(new List<UnitCapacitySummary>());
        _mockCapacity.GetInstitutionTotalsAsync().Returns((0, 0, 0, 0, 0, 0));

        _mockInstitutionIndex = Substitute.For<IInstitutionIndexGrain>();
        MockGrainFactory.GetGrain<IInstitutionIndexGrain>(Arg.Any<string>(), Arg.Any<string?>())
            .Returns(_mockInstitutionIndex);
        _mockInstitutionIndex.GetAllAsync(Arg.Any<bool>()).Returns(new List<InstitutionIndexEntry>());
    }

    [Test]
    public void BedManagement_RendersPageTitle()
    {
        var cut = Ctx.Render<BedManagement>();
        Assert.That(cut.Markup, Does.Contain("Bed Board"));
    }

    [Test]
    public void BedManagement_RendersTabBar()
    {
        var cut = Ctx.Render<BedManagement>();
        Assert.That(cut.Markup, Does.Contain("Bed Board"));
        Assert.That(cut.Markup, Does.Contain("EVS Queue"));
    }

    [Test]
    public void BedManagement_ShowsErrorOnFailure()
    {
        _mockCapacity.GetUnitsAsync(Arg.Any<bool>()).Returns<List<UnitCapacitySummary>>(
            _ => throw new Exception("Board unavailable"));

        var cut = Ctx.Render<BedManagement>();

        Assert.That(cut.Markup, Does.Contain("Board unavailable"));
    }
}
