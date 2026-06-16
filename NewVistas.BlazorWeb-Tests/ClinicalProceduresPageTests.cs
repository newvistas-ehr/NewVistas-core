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
public class ClinicalProceduresPageTests : BlazorTestBase
{
    [Test]
    public void ClinicalProcedures_RendersPageTitle()
    {
        var cut = Ctx.Render<ClinicalProcedures>();

        Assert.That(cut.Markup, Does.Contain("Clinical Procedures"));
    }

    [Test]
    public void ClinicalProcedures_RendersLookupBar()
    {
        var cut = Ctx.Render<ClinicalProcedures>();

        var inputs = cut.FindAll("input");
        Assert.That(inputs.Count, Is.GreaterThan(0));
    }

    [Test]
    public void ClinicalProcedures_LoadButton_Present()
    {
        var cut = Ctx.Render<ClinicalProcedures>();

        var buttons = cut.FindAll("button");
        Assert.That(buttons.Any(b => b.TextContent.Contains("Load")), Is.True);
    }

    [Test]
    public async Task ClinicalProcedures_LoadsDataFromGrain()
    {
        var procs = new List<ClinicProcedureIndexEntry>
        {
            new()
            {
                ProcedureId = "CP-001",
                Category = ClinicProcedureCategory.EEG,
                ProcedureCode = "95816",
                ProcedureDescription = "EEG Recording",
                Status = ClinicProcedureStatus.Completed,
                OrderedDate = DateTime.Today,
                ProviderName = "Dr. Neuro"
            }
        };
        MockWorkflowGrain.GetClinicProceduresAsync().Returns(procs);

        var cut = Ctx.Render<ClinicalProcedures>();

        cut.Find("input").Change("PAT-001");
        await cut.FindAll("button").First(b => b.TextContent.Contains("Load"))
            .ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        await MockWorkflowGrain.Received(1).GetClinicProceduresAsync();
        Assert.That(cut.Markup, Does.Contain("Dr. Neuro"));
        Assert.That(cut.Markup, Does.Contain("95816"));
    }

    [Test]
    public async Task ClinicalProcedures_ShowsEmptyState()
    {
        MockWorkflowGrain.GetClinicProceduresAsync().Returns(new List<ClinicProcedureIndexEntry>());

        var cut = Ctx.Render<ClinicalProcedures>();

        cut.Find("input").Change("PAT-002");
        await cut.FindAll("button").First(b => b.TextContent.Contains("Load"))
            .ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        Assert.That(cut.Markup, Does.Contain("No clinical procedures on record"));
    }

    [Test]
    public async Task ClinicalProcedures_ShowsErrorOnGrainFailure()
    {
        MockWorkflowGrain.GetClinicProceduresAsync().Returns<List<ClinicProcedureIndexEntry>>(
            _ => throw new Exception("Connection refused"));

        var cut = Ctx.Render<ClinicalProcedures>();

        cut.Find("input").Change("PAT-003");
        await cut.FindAll("button").First(b => b.TextContent.Contains("Load"))
            .ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        Assert.That(cut.Markup, Does.Contain("Failed to load clinical procedures"));
    }
}
