// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.BlazorWeb.Components.Pages;
using NSubstitute;

namespace NewVistas.BlazorWeb_Tests;

[TestFixture]
public class FeeBasisPageTests : BlazorTestBase
{
    [Test]
    public void FeeBasis_RendersPageTitle()
    {
        var cut = Ctx.Render<FeeBasis>();
        Assert.That(cut.Markup, Does.Contain("Fee Basis"));
    }

    [Test]
    public void FeeBasis_RendersPatientLookup()
    {
        var cut = Ctx.Render<FeeBasis>();
        Assert.That(cut.Markup, Does.Contain("Patient ID"));
    }

    [Test]
    public async Task FeeBasis_ShowsErrorOnGrainFailure()
    {
        var mockFeePatient = Substitute.For<IFeePatientGrain>();
        mockFeePatient.EnsureInitializedAsync(Arg.Any<string>()).Returns<Task>(_ => throw new Exception("Fee grain error"));
        MockGrainFactory.GetGrain<IFeePatientGrain>(Arg.Any<string>(), Arg.Any<string?>()).Returns(mockFeePatient);

        var cut = Ctx.Render<FeeBasis>();
        cut.Find("input.lookup-input").Change("PAT-001");
        await cut.Find("button.btn-primary").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        Assert.That(cut.Markup, Does.Contain("Error loading patient"));
    }
}
