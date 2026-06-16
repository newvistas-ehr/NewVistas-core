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
public class ImmunizationForecastPageTests : BlazorTestBase
{
    [Test]
    public void ImmunizationForecast_RendersPageTitle()
    {
        var mockSite = Substitute.For<ISiteParametersGrain>();
        mockSite.IsFeatureEnabledAsync(Arg.Any<string>()).Returns(true);
        MockGrainFactory.GetGrain<ISiteParametersGrain>(Arg.Any<string>(), Arg.Any<string?>()).Returns(mockSite);

        var cut = Ctx.Render<ImmunizationForecast>();
        Assert.That(cut.Markup, Does.Contain("Immunization Forecast"));
    }

    [Test]
    public void ImmunizationForecast_ShowsWarningWhenDisabled()
    {
        var mockSite = Substitute.For<ISiteParametersGrain>();
        mockSite.IsFeatureEnabledAsync(Arg.Any<string>()).Returns(false);
        MockGrainFactory.GetGrain<ISiteParametersGrain>(Arg.Any<string>(), Arg.Any<string?>()).Returns(mockSite);

        var cut = Ctx.Render<ImmunizationForecast>();
        Assert.That(cut.Markup, Does.Contain("IMMUNIZATION_FORECAST"));
    }

    [Test]
    public void ImmunizationForecast_ShowsErrorOnFeatureCheckFailure()
    {
        var mockSite = Substitute.For<ISiteParametersGrain>();
        mockSite.IsFeatureEnabledAsync(Arg.Any<string>()).Returns<bool>(x => throw new Exception("fail"));
        MockGrainFactory.GetGrain<ISiteParametersGrain>(Arg.Any<string>(), Arg.Any<string?>()).Returns(mockSite);

        var cut = Ctx.Render<ImmunizationForecast>();
        Assert.That(cut.Markup, Does.Contain("Error checking feature status"));
    }
}
