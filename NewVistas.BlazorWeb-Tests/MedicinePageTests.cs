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
public class MedicinePageTests : BlazorTestBase
{
    [Test]
    public void Medicine_RendersPageTitle()
    {
        var cut = Ctx.Render<Medicine>();

        Assert.That(cut.Markup, Does.Contain("Medicine"));
    }

    [Test]
    public void Medicine_RendersLookupBar()
    {
        var cut = Ctx.Render<Medicine>();

        var input = cut.Find("input.lookup-input");
        Assert.That(input, Is.Not.Null);
        Assert.That(input.GetAttribute("placeholder"), Is.EqualTo("Patient ID"));
    }

    [Test]
    public void Medicine_LoadButton_Present()
    {
        var cut = Ctx.Render<Medicine>();

        var button = cut.Find("button.btn-primary");
        Assert.That(button, Is.Not.Null);
    }

    [Test]
    public async Task Medicine_LoadsDataFromGrain()
    {
        var procedures = new List<MedProcedureIndexEntry>
        {
            new()
            {
                ProcedureId = "MED-001",
                Category = MedProcedureCategory.Electrocardiogram,
                ProcedureCode = "93000",
                ProcedureDescription = "Routine ECG",
                Status = MedProcedureStatus.Completed,
                OrderedDate = DateTime.Today,
                ProviderName = "Dr. Cardio"
            }
        };
        MockWorkflowGrain.GetMedProceduresAsync().Returns(procedures);
        MockWorkflowGrain.GetMedProcedureAsync(Arg.Any<string>()).Returns(new MedProcedureState
        {
            ProcedureId = "MED-001",
            Category = MedProcedureCategory.Electrocardiogram,
            ProcedureCode = "93000",
            ProcedureDescription = "Routine ECG",
            Status = MedProcedureStatus.Completed,
            OrderedDate = DateTime.Today,
            ProviderName = "Dr. Cardio"
        });

        var cut = Ctx.Render<Medicine>();

        cut.Find("input.lookup-input").Change("PAT-001");
        await cut.Find("button.btn-primary").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        await MockWorkflowGrain.Received(1).GetMedProceduresAsync();
        Assert.That(cut.Markup, Does.Contain("Dr. Cardio"));
        Assert.That(cut.Markup, Does.Contain("93000"));
    }

    [Test]
    public async Task Medicine_ShowsEmptyState()
    {
        MockWorkflowGrain.GetMedProceduresAsync().Returns(new List<MedProcedureIndexEntry>());

        var cut = Ctx.Render<Medicine>();

        cut.Find("input.lookup-input").Change("PAT-002");
        await cut.Find("button.btn-primary").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        // When procedures is empty, the stat cards show 0
        Assert.That(cut.Markup, Does.Contain("0"));
    }

    [Test]
    public async Task Medicine_ShowsErrorOnGrainFailure()
    {
        MockWorkflowGrain.GetMedProceduresAsync().Returns<List<MedProcedureIndexEntry>>(
            _ => throw new Exception("Grain timeout"));

        var cut = Ctx.Render<Medicine>();

        cut.Find("input.lookup-input").Change("PAT-003");
        await cut.Find("button.btn-primary").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        Assert.That(cut.Markup, Does.Contain("Grain timeout"));
    }
}
