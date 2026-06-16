// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.BlazorWeb.Components.Pages;
using NSubstitute;

namespace NewVistas.BlazorWeb_Tests;

[TestFixture]
public class PatientEditPageTests : BlazorTestBase
{
    [Test]
    public void PatientEdit_RendersPageTitle()
    {
        var cut = Ctx.Render<PatientEdit>();

        Assert.That(cut.Markup, Does.Contain("Edit Patient"));
    }

    [Test]
    public void PatientEdit_RendersLookupBar()
    {
        var cut = Ctx.Render<PatientEdit>();

        var input = cut.Find("input.lookup-input");
        Assert.That(input, Is.Not.Null);
        Assert.That(input.GetAttribute("placeholder"), Does.Contain("Patient ID"));
    }

    [Test]
    public void PatientEdit_LoadButton_DisabledWhenEmpty()
    {
        var cut = Ctx.Render<PatientEdit>();

        var button = cut.Find("button.btn-primary");
        Assert.That(button.HasAttribute("disabled"), Is.True);
    }

    [Test]
    public async Task PatientEdit_LoadsDataFromGrain()
    {
        var state = new PatientState
        {
            Name = "SMITH,JOHN A",
            Sex = "M",
            DateOfBirth = new DateTime(1960, 5, 15)
        };
        MockWorkflowGrain.GetPatientAsync().Returns(state);

        var cut = Ctx.Render<PatientEdit>();

        cut.Find("input.lookup-input").Input("PATIENT-001");
        await cut.Find("button.btn-primary").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        await MockWorkflowGrain.Received(1).GetPatientAsync();

        Assert.That(cut.Markup, Does.Contain("SMITH,JOHN A"));
        Assert.That(cut.Markup, Does.Contain("PATIENT-001"));
    }

    [Test]
    public async Task PatientEdit_ShowsEmptyState()
    {
        var state = new PatientState { Name = "" };
        MockWorkflowGrain.GetPatientAsync().Returns(state);

        var cut = Ctx.Render<PatientEdit>();

        cut.Find("input.lookup-input").Input("PATIENT-NONE");
        await cut.Find("button.btn-primary").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        Assert.That(cut.Markup, Does.Contain("not found"));
    }

    [Test]
    public async Task PatientEdit_ShowsErrorOnGrainFailure()
    {
        MockWorkflowGrain.GetPatientAsync().Returns<PatientState>(
            _ => throw new Exception("Silo offline"));

        var cut = Ctx.Render<PatientEdit>();

        cut.Find("input.lookup-input").Input("PATIENT-ERR");
        await cut.Find("button.btn-primary").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        Assert.That(cut.Markup, Does.Contain("Silo offline"));
    }
}
