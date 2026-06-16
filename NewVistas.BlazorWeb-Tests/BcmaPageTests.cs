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
public class BcmaPageTests : BlazorTestBase
{
    [Test]
    public void Bcma_RendersPageTitle()
    {
        var cut = Ctx.Render<Bcma>();

        Assert.That(cut.Markup, Does.Contain("BCMA"));
    }

    [Test]
    public void Bcma_RendersLookupBar()
    {
        var cut = Ctx.Render<Bcma>();

        var input = cut.Find("input.lookup-input");
        Assert.That(input, Is.Not.Null);
        Assert.That(input.GetAttribute("placeholder"), Does.Contain("Patient ID"));
    }

    [Test]
    public void Bcma_LoadButton_DisabledWhenEmpty()
    {
        var cut = Ctx.Render<Bcma>();

        var button = cut.Find("button.btn-primary");
        Assert.That(button.HasAttribute("disabled"), Is.True);
    }

    [Test]
    public async Task Bcma_LoadsDataFromGrain()
    {
        var marEntries = new List<MarEntry>
        {
            new() { OrderId = "ORD-001", DrugName = "Metoprolol 25mg", Dosage = "25mg",
                     Route = "PO", Schedule = "BID", OrderType = "UNIT_DOSE", Priority = "ROUTINE",
                     WardId = "3B", IsActive = true }
        };
        var history = new List<BcmaSummary>
        {
            new() { BcmaId = "BCMA-001", DrugName = "Aspirin 81mg", Dosage = "81mg",
                     ActionStatus = "GIVEN", AdministrationDateTime = DateTime.UtcNow,
                     AdministeredByName = "Nurse Jones" }
        };
        MockWorkflowGrain.GetPatientMARAsync().Returns(marEntries);
        MockWorkflowGrain.GetMedicationAdministrationsAsync(Arg.Any<int>()).Returns(history);

        var cut = Ctx.Render<Bcma>();

        cut.Find("input.lookup-input").Input("PATIENT-001");
        await cut.Find("button.btn-primary").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        await MockWorkflowGrain.Received(1).GetPatientMARAsync();

        Assert.That(cut.Markup, Does.Contain("Metoprolol 25mg"));
    }

    [Test]
    public async Task Bcma_ShowsEmptyState()
    {
        MockWorkflowGrain.GetPatientMARAsync().Returns(new List<MarEntry>());
        MockWorkflowGrain.GetMedicationAdministrationsAsync(Arg.Any<int>()).Returns(new List<BcmaSummary>());

        var cut = Ctx.Render<Bcma>();

        cut.Find("input.lookup-input").Input("PATIENT-002");
        await cut.Find("button.btn-primary").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        Assert.That(cut.Markup, Does.Contain("No active medication orders"));
    }

    [Test]
    public async Task Bcma_ShowsErrorOnGrainFailure()
    {
        MockWorkflowGrain.GetPatientMARAsync().Returns<List<MarEntry>>(
            _ => throw new Exception("Silo unreachable"));

        var cut = Ctx.Render<Bcma>();

        cut.Find("input.lookup-input").Input("PATIENT-003");
        await cut.Find("button.btn-primary").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        Assert.That(cut.Markup, Does.Contain("Silo unreachable"));
    }
}
