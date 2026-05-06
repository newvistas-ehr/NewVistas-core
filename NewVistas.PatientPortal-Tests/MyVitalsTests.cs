// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using System.Net;
using System.Text.Json;
using NewVistas.PatientPortal.Components.Pages;
using RichardSzalay.MockHttp;

namespace NewVistas.PatientPortal_Tests;

[TestFixture]
public class MyVitalsTests : PortalTestBase
{
    [Test]
    public void RendersPageTitle()
    {
        MockHttp.When("https://localhost:5001/api/my/health/vitals")
            .Respond("application/json", "[]");
        var cut = Ctx.Render<MyVitals>();
        Assert.That(cut.Markup, Does.Contain("My Vitals"));
    }

    [Test]
    public void LoadsVitalsFromApi()
    {
        var items = new[]
        {
            new { VitalType = "Blood Pressure", Value = "120/80", Units = "mmHg", DateTaken = new DateTime(2026, 3, 15, 10, 30, 0) },
            new { VitalType = "Temperature", Value = "98.6", Units = "F", DateTaken = new DateTime(2026, 3, 15, 10, 30, 0) }
        };
        MockHttp.When("https://localhost:5001/api/my/health/vitals")
            .Respond("application/json", JsonSerializer.Serialize(items));
        var cut = Ctx.Render<MyVitals>();
        cut.WaitForState(() => !cut.Markup.Contains("Loading"));
        Assert.That(cut.Markup, Does.Contain("Blood Pressure"));
        Assert.That(cut.Markup, Does.Contain("120/80"));
        Assert.That(cut.Markup, Does.Contain("mmHg"));
        Assert.That(cut.Markup, Does.Contain("Temperature"));
        Assert.That(cut.Markup, Does.Contain("98.6"));
    }

    [Test]
    public void ShowsEmptyState()
    {
        MockHttp.When("https://localhost:5001/api/my/health/vitals")
            .Respond("application/json", "[]");
        var cut = Ctx.Render<MyVitals>();
        cut.WaitForState(() => !cut.Markup.Contains("Loading"));
        Assert.That(cut.Markup, Does.Contain("No vitals on record."));
    }

    [Test]
    public void ShowsErrorOnApiFailure()
    {
        MockHttp.When("https://localhost:5001/api/my/health/vitals")
            .Respond(HttpStatusCode.InternalServerError);
        var cut = Ctx.Render<MyVitals>();
        cut.WaitForState(() => !cut.Markup.Contains("Loading"));
        Assert.That(cut.Markup, Does.Contain("Error"));
    }

    [Test]
    public void RendersTableHeaders()
    {
        var items = new[]
        {
            new { VitalType = "Pulse", Value = "72", Units = "bpm", DateTaken = DateTime.UtcNow }
        };
        MockHttp.When("https://localhost:5001/api/my/health/vitals")
            .Respond("application/json", JsonSerializer.Serialize(items));
        var cut = Ctx.Render<MyVitals>();
        cut.WaitForState(() => !cut.Markup.Contains("Loading"));
        Assert.That(cut.Markup, Does.Contain("Type"));
        Assert.That(cut.Markup, Does.Contain("Value"));
        Assert.That(cut.Markup, Does.Contain("Date/Time"));
    }

    [Test]
    public void FormatsDateCorrectly()
    {
        var items = new[]
        {
            new { VitalType = "Weight", Value = "180", Units = "lbs", DateTaken = new DateTime(2026, 3, 15, 14, 45, 0) }
        };
        MockHttp.When("https://localhost:5001/api/my/health/vitals")
            .Respond("application/json", JsonSerializer.Serialize(items));
        var cut = Ctx.Render<MyVitals>();
        cut.WaitForState(() => !cut.Markup.Contains("Loading"));
        Assert.That(cut.Markup, Does.Contain("03/15/2026 14:45"));
    }
}
