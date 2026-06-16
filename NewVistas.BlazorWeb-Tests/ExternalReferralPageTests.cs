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
public class ExternalReferralPageTests : BlazorTestBase
{
    [Test]
    public void ExternalReferral_RendersPageTitle()
    {
        var mockSite = Substitute.For<ISiteParametersGrain>();
        mockSite.IsFeatureEnabledAsync(Arg.Any<string>()).Returns(true);
        MockGrainFactory.GetGrain<ISiteParametersGrain>(Arg.Any<string>(), Arg.Any<string?>()).Returns(mockSite);

        var cut = Ctx.Render<ExternalReferral>();
        Assert.That(cut.Markup, Does.Contain("External Referral Tracking"));
    }

    [Test]
    public void ExternalReferral_ShowsWarningWhenDisabled()
    {
        var mockSite = Substitute.For<ISiteParametersGrain>();
        mockSite.IsFeatureEnabledAsync(Arg.Any<string>()).Returns(false);
        MockGrainFactory.GetGrain<ISiteParametersGrain>(Arg.Any<string>(), Arg.Any<string?>()).Returns(mockSite);

        var cut = Ctx.Render<ExternalReferral>();
        Assert.That(cut.Markup, Does.Contain("EXTERNAL_REFERRAL"));
    }

    [Test]
    public void ExternalReferral_ShowsErrorOnFeatureCheckFailure()
    {
        var mockSite = Substitute.For<ISiteParametersGrain>();
        mockSite.IsFeatureEnabledAsync(Arg.Any<string>()).Returns<bool>(x => throw new Exception("connection error"));
        MockGrainFactory.GetGrain<ISiteParametersGrain>(Arg.Any<string>(), Arg.Any<string?>()).Returns(mockSite);

        var cut = Ctx.Render<ExternalReferral>();
        Assert.That(cut.Markup, Does.Contain("Error checking feature status"));
    }
}
