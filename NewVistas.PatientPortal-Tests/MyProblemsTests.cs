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
public class MyProblemsTests : PortalTestBase
{
    [Test]
    public void RendersPageTitle()
    {
        MockHttp.When("https://localhost:5001/api/my/health/problems")
            .Respond("application/json", "[]");
        var cut = Ctx.Render<MyProblems>();
        Assert.That(cut.Markup, Does.Contain("My Health Conditions"));
    }

    [Test]
    public void LoadsProblemsFromApi()
    {
        var items = new[]
        {
            new { ProblemName = "Essential Hypertension", IcdCode = "I10", Status = "Active", OnsetDate = "01/01/2020" },
            new { ProblemName = "Type 2 Diabetes", IcdCode = "E11.9", Status = "Active", OnsetDate = "06/15/2018" }
        };
        MockHttp.When("https://localhost:5001/api/my/health/problems")
            .Respond("application/json", JsonSerializer.Serialize(items));
        var cut = Ctx.Render<MyProblems>();
        cut.WaitForState(() => !cut.Markup.Contains("Loading"));
        Assert.That(cut.Markup, Does.Contain("Essential Hypertension"));
        Assert.That(cut.Markup, Does.Contain("I10"));
        Assert.That(cut.Markup, Does.Contain("Type 2 Diabetes"));
        Assert.That(cut.Markup, Does.Contain("E11.9"));
    }

    [Test]
    public void ShowsEmptyState()
    {
        MockHttp.When("https://localhost:5001/api/my/health/problems")
            .Respond("application/json", "[]");
        var cut = Ctx.Render<MyProblems>();
        cut.WaitForState(() => !cut.Markup.Contains("Loading"));
        Assert.That(cut.Markup, Does.Contain("No health conditions on record."));
    }

    [Test]
    public void ShowsErrorOnApiFailure()
    {
        MockHttp.When("https://localhost:5001/api/my/health/problems")
            .Respond(HttpStatusCode.InternalServerError);
        var cut = Ctx.Render<MyProblems>();
        cut.WaitForState(() => !cut.Markup.Contains("Loading"));
        Assert.That(cut.Markup, Does.Contain("Error"));
    }

    [Test]
    public void RendersTableHeaders()
    {
        var items = new[]
        {
            new { ProblemName = "Asthma", IcdCode = "J45", Status = "Active", OnsetDate = "2015" }
        };
        MockHttp.When("https://localhost:5001/api/my/health/problems")
            .Respond("application/json", JsonSerializer.Serialize(items));
        var cut = Ctx.Render<MyProblems>();
        cut.WaitForState(() => !cut.Markup.Contains("Loading"));
        Assert.That(cut.Markup, Does.Contain("Condition"));
        Assert.That(cut.Markup, Does.Contain("ICD Code"));
        Assert.That(cut.Markup, Does.Contain("Status"));
        Assert.That(cut.Markup, Does.Contain("Onset Date"));
    }

    [Test]
    public void RendersStatusBadge()
    {
        var items = new[]
        {
            new { ProblemName = "Resolved Issue", IcdCode = "Z00", Status = "Inactive", OnsetDate = "2020" }
        };
        MockHttp.When("https://localhost:5001/api/my/health/problems")
            .Respond("application/json", JsonSerializer.Serialize(items));
        var cut = Ctx.Render<MyProblems>();
        cut.WaitForState(() => !cut.Markup.Contains("Loading"));
        Assert.That(cut.Markup, Does.Contain("status-inactive"));
        Assert.That(cut.Markup, Does.Contain("Inactive"));
    }
}
