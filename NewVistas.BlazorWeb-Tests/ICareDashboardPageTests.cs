// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Bunit;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.BlazorWeb.Components.Pages;
using NSubstitute;

namespace NewVistas.BlazorWeb_Tests;

[TestFixture]
public class ICareDashboardPageTests : BlazorTestBase
{
    [Test]
    public void ICareDashboard_RendersPageTitle()
    {
        var mockSite = Substitute.For<ISiteParametersGrain>();
        mockSite.IsFeatureEnabledAsync(Arg.Any<string>()).Returns(true);
        MockGrainFactory.GetGrain<ISiteParametersGrain>(Arg.Any<string>(), Arg.Any<string?>()).Returns(mockSite);

        var cut = Ctx.Render<ICareDashboard>();
        Assert.That(cut.Markup, Does.Contain("iCare Dashboard"));
    }

    [Test]
    public void ICareDashboard_ShowsWarningWhenDisabled()
    {
        var mockSite = Substitute.For<ISiteParametersGrain>();
        mockSite.IsFeatureEnabledAsync(Arg.Any<string>()).Returns(false);
        MockGrainFactory.GetGrain<ISiteParametersGrain>(Arg.Any<string>(), Arg.Any<string?>()).Returns(mockSite);

        var cut = Ctx.Render<ICareDashboard>();
        Assert.That(cut.Markup, Does.Contain("ICARE_DASHBOARD"));
    }

    [Test]
    public void ICareDashboard_RendersTabs()
    {
        var mockSite = Substitute.For<ISiteParametersGrain>();
        mockSite.IsFeatureEnabledAsync(Arg.Any<string>()).Returns(true);
        MockGrainFactory.GetGrain<ISiteParametersGrain>(Arg.Any<string>(), Arg.Any<string?>()).Returns(mockSite);

        var cut = Ctx.Render<ICareDashboard>();
        Assert.That(cut.Markup, Does.Contain("Dashboard"));
        Assert.That(cut.Markup, Does.Contain("Panel Management"));
    }
}
