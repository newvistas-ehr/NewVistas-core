// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using System.Net;
using System.Text.Json;
using NewVistas.PatientPortal.Components.Pages;
using RichardSzalay.MockHttp;

namespace NewVistas.PatientPortal_Tests;

[TestFixture]
public class MyMedicationsTests : PortalTestBase
{
    [Test]
    public void RendersPageTitle()
    {
        MockHttp.When("https://localhost:5001/api/my/health/medications")
            .Respond("application/json", "[]");
        var cut = Ctx.Render<MyMedications>();
        Assert.That(cut.Markup, Does.Contain("My Medications"));
    }

    [Test]
    public void LoadsMedicationsFromApi()
    {
        var items = new[]
        {
            new { DrugName = (string?)"Lisinopril", OrderableItemName = (string?)null, Dosage = "10mg", Route = "Oral", Schedule = "Daily", Status = "Active" },
            new { DrugName = (string?)null, OrderableItemName = (string?)"Metformin HCl", Dosage = "500mg", Route = "Oral", Schedule = "BID", Status = "Active" }
        };
        MockHttp.When("https://localhost:5001/api/my/health/medications")
            .Respond("application/json", JsonSerializer.Serialize(items));
        var cut = Ctx.Render<MyMedications>();
        cut.WaitForState(() => !cut.Markup.Contains("Loading"));
        Assert.That(cut.Markup, Does.Contain("Lisinopril"));
        Assert.That(cut.Markup, Does.Contain("Metformin HCl"));
        Assert.That(cut.Markup, Does.Contain("10mg"));
        Assert.That(cut.Markup, Does.Contain("Oral"));
    }

    [Test]
    public void ShowsEmptyState()
    {
        MockHttp.When("https://localhost:5001/api/my/health/medications")
            .Respond("application/json", "[]");
        var cut = Ctx.Render<MyMedications>();
        cut.WaitForState(() => !cut.Markup.Contains("Loading"));
        Assert.That(cut.Markup, Does.Contain("No medications on record."));
    }

    [Test]
    public void ShowsErrorOnApiFailure()
    {
        MockHttp.When("https://localhost:5001/api/my/health/medications")
            .Respond(HttpStatusCode.InternalServerError);
        var cut = Ctx.Render<MyMedications>();
        cut.WaitForState(() => !cut.Markup.Contains("Loading"));
        Assert.That(cut.Markup, Does.Contain("Error"));
    }

    [Test]
    public void RendersTableHeaders()
    {
        var items = new[]
        {
            new { DrugName = "Aspirin", Dosage = "81mg", Route = "Oral", Schedule = "Daily", Status = "Active" }
        };
        MockHttp.When("https://localhost:5001/api/my/health/medications")
            .Respond("application/json", JsonSerializer.Serialize(items));
        var cut = Ctx.Render<MyMedications>();
        cut.WaitForState(() => !cut.Markup.Contains("Loading"));
        Assert.That(cut.Markup, Does.Contain("Medication"));
        Assert.That(cut.Markup, Does.Contain("Dosage"));
        Assert.That(cut.Markup, Does.Contain("Route"));
        Assert.That(cut.Markup, Does.Contain("Schedule"));
        Assert.That(cut.Markup, Does.Contain("Status"));
    }

    [Test]
    public void FallsBackToOrderableItemName()
    {
        var items = new[]
        {
            new { DrugName = (string?)null, OrderableItemName = "Amoxicillin Cap", Dosage = "250mg", Route = "Oral", Schedule = "TID", Status = "Active" }
        };
        MockHttp.When("https://localhost:5001/api/my/health/medications")
            .Respond("application/json", JsonSerializer.Serialize(items));
        var cut = Ctx.Render<MyMedications>();
        cut.WaitForState(() => !cut.Markup.Contains("Loading"));
        Assert.That(cut.Markup, Does.Contain("Amoxicillin Cap"));
    }
}
