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
public class IntegratedBillingPageTests : BlazorTestBase
{
    [Test]
    public void IntegratedBilling_RendersPageTitle()
    {
        var cut = Ctx.Render<IntegratedBilling>();
        Assert.That(cut.Markup, Does.Contain("Integrated Billing"));
    }

    [Test]
    public void IntegratedBilling_RendersLookupBar()
    {
        var cut = Ctx.Render<IntegratedBilling>();
        var input = cut.Find("input.form-control");
        Assert.That(input, Is.Not.Null);
        Assert.That(input.GetAttribute("placeholder"), Is.EqualTo("Patient ID"));
    }

    [Test]
    public async Task IntegratedBilling_ShowsErrorOnGrainFailure()
    {
        var mockPatient = Substitute.For<IIBillingPatientGrain>();
        mockPatient.EnsureInitializedAsync(Arg.Any<string>()).Returns<Task>(_ => throw new Exception("Grain failure"));
        MockGrainFactory.GetGrain<IIBillingPatientGrain>(Arg.Any<string>(), Arg.Any<string?>()).Returns(mockPatient);

        var cut = Ctx.Render<IntegratedBilling>();
        cut.Find("input.form-control").Input("PAT-001");
        await cut.Find("button.btn-primary").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        Assert.That(cut.Markup, Does.Contain("Grain failure"));
    }
}
