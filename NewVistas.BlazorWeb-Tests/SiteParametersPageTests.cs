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
public class SiteParametersPageTests : BlazorTestBase
{
    [Test]
    public void SiteParameters_RendersPageTitle()
    {
        var mockGrain = Substitute.For<ISiteParametersGrain>();
        mockGrain.GetVitalsDisplayCountAsync().Returns(10);
        mockGrain.GetOrdersDisplayCountAsync().Returns(5);
        mockGrain.GetNotesDisplayCountAsync().Returns(10);
        MockGrainFactory.GetGrain<ISiteParametersGrain>(Arg.Any<string>(), Arg.Any<string?>()).Returns(mockGrain);

        var cut = Ctx.Render<SiteParameters>();
        Assert.That(cut.Markup, Does.Contain("Site Parameters"));
    }

    [Test]
    public void SiteParameters_LoadsDisplaySettings()
    {
        var mockGrain = Substitute.For<ISiteParametersGrain>();
        mockGrain.GetVitalsDisplayCountAsync().Returns(15);
        mockGrain.GetOrdersDisplayCountAsync().Returns(8);
        mockGrain.GetNotesDisplayCountAsync().Returns(12);
        MockGrainFactory.GetGrain<ISiteParametersGrain>(Arg.Any<string>(), Arg.Any<string?>()).Returns(mockGrain);

        var cut = Ctx.Render<SiteParameters>();
        Assert.That(cut.Markup, Does.Contain("Display Settings"));
    }

    [Test]
    public void SiteParameters_ShowsErrorOnLoadFailure()
    {
        var mockGrain = Substitute.For<ISiteParametersGrain>();
        mockGrain.GetVitalsDisplayCountAsync().Returns<int>(x => throw new Exception("fail"));
        MockGrainFactory.GetGrain<ISiteParametersGrain>(Arg.Any<string>(), Arg.Any<string?>()).Returns(mockGrain);

        var cut = Ctx.Render<SiteParameters>();
        Assert.That(cut.Markup, Does.Contain("Failed to load display settings"));
    }
}
