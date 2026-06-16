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
public class MyImmunizationsTests : PortalTestBase
{
    [Test]
    public void RendersPageTitle()
    {
        MockHttp.When("https://localhost:5001/api/my/health/immunizations")
            .Respond("application/json", "[]");
        var cut = Ctx.Render<MyImmunizations>();
        Assert.That(cut.Markup, Does.Contain("My Immunizations"));
    }

    [Test]
    public void LoadsImmunizationsFromApi()
    {
        var items = new[]
        {
            new { ImmunizationName = "COVID-19 Vaccine", AdministeredDate = "03/01/2026", Series = "Booster", Reaction = "None" },
            new { ImmunizationName = "Influenza", AdministeredDate = "10/15/2025", Series = "Annual", Reaction = "Sore arm" }
        };
        MockHttp.When("https://localhost:5001/api/my/health/immunizations")
            .Respond("application/json", JsonSerializer.Serialize(items));
        var cut = Ctx.Render<MyImmunizations>();
        cut.WaitForState(() => !cut.Markup.Contains("Loading"));
        Assert.That(cut.Markup, Does.Contain("COVID-19 Vaccine"));
        Assert.That(cut.Markup, Does.Contain("Influenza"));
        Assert.That(cut.Markup, Does.Contain("Booster"));
        Assert.That(cut.Markup, Does.Contain("Sore arm"));
    }

    [Test]
    public void ShowsEmptyState()
    {
        MockHttp.When("https://localhost:5001/api/my/health/immunizations")
            .Respond("application/json", "[]");
        var cut = Ctx.Render<MyImmunizations>();
        cut.WaitForState(() => !cut.Markup.Contains("Loading"));
        Assert.That(cut.Markup, Does.Contain("No immunizations on record."));
    }

    [Test]
    public void ShowsErrorOnApiFailure()
    {
        MockHttp.When("https://localhost:5001/api/my/health/immunizations")
            .Respond(HttpStatusCode.InternalServerError);
        var cut = Ctx.Render<MyImmunizations>();
        cut.WaitForState(() => !cut.Markup.Contains("Loading"));
        Assert.That(cut.Markup, Does.Contain("Error"));
    }

    [Test]
    public void RendersTableHeaders()
    {
        var items = new[]
        {
            new { ImmunizationName = "Tetanus", AdministeredDate = "06/01/2024", Series = "Primary", Reaction = (string?)null }
        };
        MockHttp.When("https://localhost:5001/api/my/health/immunizations")
            .Respond("application/json", JsonSerializer.Serialize(items));
        var cut = Ctx.Render<MyImmunizations>();
        cut.WaitForState(() => !cut.Markup.Contains("Loading"));
        Assert.That(cut.Markup, Does.Contain("Vaccine"));
        Assert.That(cut.Markup, Does.Contain("Date"));
        Assert.That(cut.Markup, Does.Contain("Series"));
        Assert.That(cut.Markup, Does.Contain("Reaction"));
    }

    [Test]
    public void ShowsNoneForNullReaction()
    {
        var items = new[]
        {
            new { ImmunizationName = "Hepatitis B", AdministeredDate = "01/01/2025", Series = "1 of 3", Reaction = (string?)null }
        };
        MockHttp.When("https://localhost:5001/api/my/health/immunizations")
            .Respond("application/json", JsonSerializer.Serialize(items));
        var cut = Ctx.Render<MyImmunizations>();
        cut.WaitForState(() => !cut.Markup.Contains("Loading"));
        Assert.That(cut.Markup, Does.Contain("None"));
    }
}
