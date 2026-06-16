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
public class GeriatricsExtendedCarePageTests : BlazorTestBase
{
    private ICLCAdmissionIndexGrain _mockAdmissionIndex = null!;

    public override void Setup()
    {
        base.Setup();
        _mockAdmissionIndex = Substitute.For<ICLCAdmissionIndexGrain>();
        MockGrainFactory.GetGrain<ICLCAdmissionIndexGrain>(Arg.Any<string>(), Arg.Any<string?>())
            .Returns(_mockAdmissionIndex);
        _mockAdmissionIndex.GetActiveCensusAsync().Returns(new List<CLCAdmissionIndexEntry>());
        _mockAdmissionIndex.GetAnticipatedDischargesAsync(Arg.Any<int>()).Returns(new List<CLCAdmissionIndexEntry>());
    }

    [Test]
    public void GeriatricsExtendedCare_RendersPageTitle()
    {
        var cut = Ctx.Render<GeriatricsExtendedCare>();
        Assert.That(cut.Markup, Does.Contain("Geriatrics"));
    }

    [Test]
    public void GeriatricsExtendedCare_RendersTabBar()
    {
        var cut = Ctx.Render<GeriatricsExtendedCare>();
        Assert.That(cut.Markup, Does.Contain("CLC Census"));
        Assert.That(cut.Markup, Does.Contain("Assessments"));
        Assert.That(cut.Markup, Does.Contain("Dashboard"));
    }

    [Test]
    public async Task GeriatricsExtendedCare_ShowsErrorOnFailure()
    {
        _mockAdmissionIndex.GetActiveCensusAsync().Returns<List<CLCAdmissionIndexEntry>>(
            _ => throw new Exception("Grain error"));
        _mockAdmissionIndex.GetAnticipatedDischargesAsync(Arg.Any<int>()).Returns<List<CLCAdmissionIndexEntry>>(
            _ => throw new Exception("Grain error"));

        var cut = Ctx.Render<GeriatricsExtendedCare>();

        Assert.That(cut.Markup, Does.Contain("Failed to load dashboard"));
    }
}
