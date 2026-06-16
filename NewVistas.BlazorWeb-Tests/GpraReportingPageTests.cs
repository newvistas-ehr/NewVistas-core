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
public class GpraReportingPageTests : BlazorTestBase
{
    [Test]
    public void GpraReporting_RendersPageTitle()
    {
        var mockIndex = Substitute.For<IGpraReportIndexGrain>();
        mockIndex.GetAllAsync().Returns(new List<GpraReportIndexEntry>());
        MockGrainFactory.GetGrain<IGpraReportIndexGrain>(Arg.Any<string>(), Arg.Any<string?>()).Returns(mockIndex);

        var cut = Ctx.Render<GpraReporting>();
        Assert.That(cut.Markup, Does.Contain("GPRA Population Health Reporting"));
    }

    [Test]
    public void GpraReporting_RendersTabs()
    {
        var mockIndex = Substitute.For<IGpraReportIndexGrain>();
        mockIndex.GetAllAsync().Returns(new List<GpraReportIndexEntry>());
        MockGrainFactory.GetGrain<IGpraReportIndexGrain>(Arg.Any<string>(), Arg.Any<string?>()).Returns(mockIndex);

        var cut = Ctx.Render<GpraReporting>();
        Assert.That(cut.Markup, Does.Contain("Reports"));
        Assert.That(cut.Markup, Does.Contain("Report Detail"));
    }

    [Test]
    public void GpraReporting_ShowsEmptyState()
    {
        var mockIndex = Substitute.For<IGpraReportIndexGrain>();
        mockIndex.GetAllAsync().Returns(new List<GpraReportIndexEntry>());
        MockGrainFactory.GetGrain<IGpraReportIndexGrain>(Arg.Any<string>(), Arg.Any<string?>()).Returns(mockIndex);

        var cut = Ctx.Render<GpraReporting>();
        Assert.That(cut.Markup, Does.Contain("No GPRA reports"));
    }
}
