// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using System.Net;
using System.Text.Json;
using NewVistas.PatientPortal.Components.Pages;
using RichardSzalay.MockHttp;

namespace NewVistas.PatientPortal_Tests;

[TestFixture]
public class MyDemographicsTests : PortalTestBase
{
    [Test]
    public void RendersPageTitle()
    {
        MockHttp.When("https://localhost:5001/api/my/health/demographics")
            .Respond("application/json", "null");
        var cut = Ctx.Render<MyDemographics>();
        Assert.That(cut.Markup, Does.Contain("My Demographics"));
    }

    [Test]
    public void LoadsDemographicsFromApi()
    {
        var demographics = new
        {
            Name = "Smith, John",
            DateOfBirth = "01/15/1980",
            Sex = "Male",
            Address = "123 Main St",
            City = "Anytown",
            State = "VA",
            ZipCode = "22030",
            PhoneNumber = "555-0100",
            MaritalStatus = "Married",
            Ethnicity = "Not Hispanic",
            Race = "White",
            PreferredLanguage = "English",
            EmergencyContactName = "Jane Smith",
            EmergencyContactPhone = "555-0101"
        };
        MockHttp.When("https://localhost:5001/api/my/health/demographics")
            .Respond("application/json", JsonSerializer.Serialize(demographics));
        var cut = Ctx.Render<MyDemographics>();
        cut.WaitForState(() => !cut.Markup.Contains("Loading"));
        Assert.That(cut.Markup, Does.Contain("Smith, John"));
        Assert.That(cut.Markup, Does.Contain("01/15/1980"));
        Assert.That(cut.Markup, Does.Contain("Male"));
        Assert.That(cut.Markup, Does.Contain("123 Main St"));
        Assert.That(cut.Markup, Does.Contain("555-0100"));
        Assert.That(cut.Markup, Does.Contain("Jane Smith"));
    }

    [Test]
    public void ShowsContactUpdateNote()
    {
        var demographics = new { Name = "Test", DateOfBirth = (string?)null, Sex = (string?)null };
        MockHttp.When("https://localhost:5001/api/my/health/demographics")
            .Respond("application/json", JsonSerializer.Serialize(demographics));
        var cut = Ctx.Render<MyDemographics>();
        cut.WaitForState(() => !cut.Markup.Contains("Loading"));
        Assert.That(cut.Markup, Does.Contain("To update your information"));
    }

    [Test]
    public void ShowsErrorOnApiFailure()
    {
        MockHttp.When("https://localhost:5001/api/my/health/demographics")
            .Respond(HttpStatusCode.InternalServerError);
        var cut = Ctx.Render<MyDemographics>();
        cut.WaitForState(() => !cut.Markup.Contains("Loading"));
        Assert.That(cut.Markup, Does.Contain("Error"));
    }

    [Test]
    public void ShowsDashForMissingFields()
    {
        var demographics = new
        {
            Name = "Smith, John",
            DateOfBirth = (string?)null,
            Sex = (string?)null,
            PhoneNumber = (string?)null,
            MaritalStatus = (string?)null
        };
        MockHttp.When("https://localhost:5001/api/my/health/demographics")
            .Respond("application/json", JsonSerializer.Serialize(demographics));
        var cut = Ctx.Render<MyDemographics>();
        cut.WaitForState(() => !cut.Markup.Contains("Loading"));
        Assert.That(cut.Markup, Does.Contain("Smith, John"));
        // Null fields render as em-dash
        var dashCount = cut.Markup.Split("\u2014").Length - 1;
        Assert.That(dashCount, Is.GreaterThanOrEqualTo(3));
    }

    [Test]
    public void RendersAllFieldLabels()
    {
        var demographics = new { Name = "Test" };
        MockHttp.When("https://localhost:5001/api/my/health/demographics")
            .Respond("application/json", JsonSerializer.Serialize(demographics));
        var cut = Ctx.Render<MyDemographics>();
        cut.WaitForState(() => !cut.Markup.Contains("Loading"));
        Assert.That(cut.Markup, Does.Contain("Name:"));
        Assert.That(cut.Markup, Does.Contain("Date of Birth:"));
        Assert.That(cut.Markup, Does.Contain("Sex:"));
        Assert.That(cut.Markup, Does.Contain("Phone:"));
        Assert.That(cut.Markup, Does.Contain("Address:"));
    }
}
