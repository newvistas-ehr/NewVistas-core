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
public class PatientMergePageTests : BlazorTestBase
{
    [Test]
    public void PatientMerge_RendersPageTitle()
    {
        var mockSite = Substitute.For<ISiteParametersGrain>();
        mockSite.IsFeatureEnabledAsync(Arg.Any<string>()).Returns(true);
        MockGrainFactory.GetGrain<ISiteParametersGrain>(Arg.Any<string>(), Arg.Any<string?>()).Returns(mockSite);

        var cut = Ctx.Render<PatientMerge>();
        Assert.That(cut.Markup, Does.Contain("Patient Merge"));
    }

    [Test]
    public void PatientMerge_ShowsWarningWhenDisabled()
    {
        var mockSite = Substitute.For<ISiteParametersGrain>();
        mockSite.IsFeatureEnabledAsync(Arg.Any<string>()).Returns(false);
        MockGrainFactory.GetGrain<ISiteParametersGrain>(Arg.Any<string>(), Arg.Any<string?>()).Returns(mockSite);

        var cut = Ctx.Render<PatientMerge>();
        Assert.That(cut.Markup, Does.Contain("PATIENT_MERGE"));
    }

    [Test]
    public void PatientMerge_RendersHelpTab()
    {
        var mockSite = Substitute.For<ISiteParametersGrain>();
        mockSite.IsFeatureEnabledAsync(Arg.Any<string>()).Returns(true);
        MockGrainFactory.GetGrain<ISiteParametersGrain>(Arg.Any<string>(), Arg.Any<string?>()).Returns(mockSite);

        var cut = Ctx.Render<PatientMerge>();
        Assert.That(cut.Markup, Does.Contain("Merge Patients"));
        Assert.That(cut.Markup, Does.Contain("Help"));
    }
}
