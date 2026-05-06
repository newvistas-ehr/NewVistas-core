// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using System.Net;
using System.Text.Json;
using NewVistas.PatientPortal.Components.Pages;
using RichardSzalay.MockHttp;

namespace NewVistas.PatientPortal_Tests;

[TestFixture]
public class MyAllergiesTests : PortalTestBase
{
    [Test]
    public void RendersPageTitle()
    {
        MockHttp.When("https://localhost:5001/api/my/health/allergies")
            .Respond("application/json", "[]");
        var cut = Ctx.Render<MyAllergies>();
        Assert.That(cut.Markup, Does.Contain("My Allergies"));
    }

    [Test]
    public void LoadsAllergiesFromApi()
    {
        var allergies = new[]
        {
            new { AllergenName = "Penicillin", Reaction = "Hives", Severity = "Severe", AllergyType = "Drug" },
            new { AllergenName = "Peanuts", Reaction = "Anaphylaxis", Severity = "Severe", AllergyType = "Food" }
        };
        MockHttp.When("https://localhost:5001/api/my/health/allergies")
            .Respond("application/json", JsonSerializer.Serialize(allergies));
        var cut = Ctx.Render<MyAllergies>();
        cut.WaitForState(() => !cut.Markup.Contains("Loading"));
        Assert.That(cut.Markup, Does.Contain("Penicillin"));
        Assert.That(cut.Markup, Does.Contain("Hives"));
        Assert.That(cut.Markup, Does.Contain("Peanuts"));
        Assert.That(cut.Markup, Does.Contain("Drug"));
    }

    [Test]
    public void ShowsEmptyState()
    {
        MockHttp.When("https://localhost:5001/api/my/health/allergies")
            .Respond("application/json", "[]");
        var cut = Ctx.Render<MyAllergies>();
        cut.WaitForState(() => !cut.Markup.Contains("Loading"));
        Assert.That(cut.Markup, Does.Contain("No allergies on record."));
    }

    [Test]
    public void ShowsErrorOnApiFailure()
    {
        MockHttp.When("https://localhost:5001/api/my/health/allergies")
            .Respond(HttpStatusCode.InternalServerError);
        var cut = Ctx.Render<MyAllergies>();
        cut.WaitForState(() => !cut.Markup.Contains("Loading"));
        Assert.That(cut.Markup, Does.Contain("Error"));
    }

    [Test]
    public void RendersTableHeaders()
    {
        var allergies = new[]
        {
            new { AllergenName = "Dust", Reaction = "Sneezing", Severity = "Mild", AllergyType = "Environmental" }
        };
        MockHttp.When("https://localhost:5001/api/my/health/allergies")
            .Respond("application/json", JsonSerializer.Serialize(allergies));
        var cut = Ctx.Render<MyAllergies>();
        cut.WaitForState(() => !cut.Markup.Contains("Loading"));
        Assert.That(cut.Markup, Does.Contain("Allergen"));
        Assert.That(cut.Markup, Does.Contain("Reaction"));
        Assert.That(cut.Markup, Does.Contain("Severity"));
        Assert.That(cut.Markup, Does.Contain("Type"));
    }

    [Test]
    public void RendersSeverityBadge()
    {
        var allergies = new[]
        {
            new { AllergenName = "Latex", Reaction = "Rash", Severity = "Moderate", AllergyType = "Other" }
        };
        MockHttp.When("https://localhost:5001/api/my/health/allergies")
            .Respond("application/json", JsonSerializer.Serialize(allergies));
        var cut = Ctx.Render<MyAllergies>();
        cut.WaitForState(() => !cut.Markup.Contains("Loading"));
        Assert.That(cut.Markup, Does.Contain("severity-moderate"));
        Assert.That(cut.Markup, Does.Contain("Moderate"));
    }
}
