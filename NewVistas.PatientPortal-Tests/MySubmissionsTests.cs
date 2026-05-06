// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using System.Net;
using System.Text.Json;
using Bunit;
using NewVistas.PatientPortal.Components.Pages;
using RichardSzalay.MockHttp;

namespace NewVistas.PatientPortal_Tests;

[TestFixture]
public class MySubmissionsTests : PortalTestBase
{
    [Test]
    public void RendersPageTitle()
    {
        MockHttp.When("https://localhost:5001/api/my/submissions")
            .Respond("application/json", "[]");
        var cut = Ctx.Render<MySubmissions>();
        Assert.That(cut.Markup, Does.Contain("Health Information Submissions"));
    }

    [Test]
    public void LoadsSubmissionsFromApi()
    {
        var items = new[]
        {
            new { SubmissionId = "SUB-1", SubmittedDate = new DateTime(2026, 3, 15, 10, 0, 0), Status = "submitted", SectionCount = 3 },
            new { SubmissionId = "SUB-2", SubmittedDate = new DateTime(2026, 3, 10, 9, 0, 0), Status = "accepted", SectionCount = 2 }
        };
        MockHttp.When("https://localhost:5001/api/my/submissions")
            .Respond("application/json", JsonSerializer.Serialize(items));
        var cut = Ctx.Render<MySubmissions>();
        cut.WaitForState(() => !cut.Markup.Contains("Loading submissions"));
        Assert.That(cut.Markup, Does.Contain("03/15/2026 10:00"));
        Assert.That(cut.Markup, Does.Contain("3 sections"));
        Assert.That(cut.Markup, Does.Contain("2 sections"));
    }

    [Test]
    public void ShowsEmptyState()
    {
        MockHttp.When("https://localhost:5001/api/my/submissions")
            .Respond("application/json", "[]");
        var cut = Ctx.Render<MySubmissions>();
        cut.WaitForState(() => !cut.Markup.Contains("Loading submissions"));
        Assert.That(cut.Markup, Does.Contain("No submissions yet"));
    }

    [Test]
    public void ShowsErrorOnApiFailure()
    {
        MockHttp.When("https://localhost:5001/api/my/submissions")
            .Respond(HttpStatusCode.InternalServerError);
        var cut = Ctx.Render<MySubmissions>();
        cut.WaitForState(() => !cut.Markup.Contains("Loading submissions"));
        Assert.That(cut.Markup, Does.Contain("Error"));
    }

    [Test]
    public void ShowsNewSubmissionButton()
    {
        MockHttp.When("https://localhost:5001/api/my/submissions")
            .Respond("application/json", "[]");
        var cut = Ctx.Render<MySubmissions>();
        Assert.That(cut.Markup, Does.Contain("+ New Submission"));
    }

    [Test]
    public void ShowsSubmissionFormOnClick()
    {
        MockHttp.When("https://localhost:5001/api/my/submissions")
            .Respond("application/json", "[]");
        var cut = Ctx.Render<MySubmissions>();
        cut.WaitForState(() => !cut.Markup.Contains("Loading submissions"));
        cut.Find(".btn-primary").Click();
        Assert.That(cut.Markup, Does.Contain("Submit Health Information"));
        Assert.That(cut.Markup, Does.Contain("Demographics"));
        Assert.That(cut.Markup, Does.Contain("Health Concerns"));
        Assert.That(cut.Markup, Does.Contain("Medications"));
        Assert.That(cut.Markup, Does.Contain("Allergies"));
    }

    [Test]
    public void ShowsSubmissionFormSections()
    {
        MockHttp.When("https://localhost:5001/api/my/submissions")
            .Respond("application/json", "[]");
        var cut = Ctx.Render<MySubmissions>();
        cut.WaitForState(() => !cut.Markup.Contains("Loading submissions"));
        cut.Find(".btn-primary").Click();
        Assert.That(cut.Markup, Does.Contain("Health Goals"));
        Assert.That(cut.Markup, Does.Contain("Additional Notes"));
        Assert.That(cut.Markup, Does.Contain("Submit"));
        Assert.That(cut.Markup, Does.Contain("Cancel"));
    }

    [Test]
    public void SubmitsFormSuccessfully()
    {
        // First load
        MockHttp.When(HttpMethod.Get, "https://localhost:5001/api/my/submissions")
            .Respond("application/json", "[]");
        // Submit
        MockHttp.When(HttpMethod.Post, "https://localhost:5001/api/my/submissions")
            .Respond(HttpStatusCode.Created);

        var cut = Ctx.Render<MySubmissions>();
        cut.WaitForState(() => !cut.Markup.Contains("Loading submissions"));
        cut.Find(".btn-primary").Click();

        // Fill in some demographics fields (the Demographics section is open by default)
        var inputs = cut.FindAll(".section-body input");
        if (inputs.Count > 0)
        {
            inputs[0].Change("123 Main St");
        }

        // Click Submit button (second .btn-primary after the "+ New Submission" button)
        var submitBtn = cut.FindAll(".btn-primary")[1];
        submitBtn.Click();
        cut.WaitForState(() => cut.Markup.Contains("Submitted successfully") || cut.Markup.Contains("Error"));
        Assert.That(cut.Markup, Does.Contain("Submitted successfully"));
    }

    [Test]
    public void ViewsSubmissionDetail()
    {
        var items = new[]
        {
            new { SubmissionId = "SUB-1", SubmittedDate = new DateTime(2026, 3, 15), Status = "accepted", SectionCount = 2 }
        };
        var detail = new
        {
            SubmissionId = "SUB-1",
            SubmittedDate = new DateTime(2026, 3, 15),
            Status = "accepted",
            PatientNotes = "Please review",
            ReviewedBy = "Dr. Smith",
            ReviewNotes = "All looks good",
            AcceptedSections = new[] { "Demographics", "Medications" },
            RejectedSections = new List<string>()
        };
        MockHttp.When(HttpMethod.Get, "https://localhost:5001/api/my/submissions")
            .Respond("application/json", JsonSerializer.Serialize(items));
        MockHttp.When(HttpMethod.Get, "https://localhost:5001/api/my/submissions/SUB-1")
            .Respond("application/json", JsonSerializer.Serialize(detail));

        var cut = Ctx.Render<MySubmissions>();
        cut.WaitForState(() => !cut.Markup.Contains("Loading submissions"));
        cut.Find(".btn-sm").Click();
        cut.WaitForState(() => cut.Markup.Contains("Dr. Smith"));
        Assert.That(cut.Markup, Does.Contain("Dr. Smith"));
        Assert.That(cut.Markup, Does.Contain("All looks good"));
        Assert.That(cut.Markup, Does.Contain("Please review"));
    }

    [Test]
    public void ShowsStatusBadges()
    {
        var items = new[]
        {
            new { SubmissionId = "SUB-1", SubmittedDate = new DateTime(2026, 3, 15), Status = "submitted", SectionCount = 1 },
            new { SubmissionId = "SUB-2", SubmittedDate = new DateTime(2026, 3, 10), Status = "accepted", SectionCount = 2 }
        };
        MockHttp.When("https://localhost:5001/api/my/submissions")
            .Respond("application/json", JsonSerializer.Serialize(items));
        var cut = Ctx.Render<MySubmissions>();
        cut.WaitForState(() => !cut.Markup.Contains("Loading submissions"));
        Assert.That(cut.Markup, Does.Contain("status-submitted"));
        Assert.That(cut.Markup, Does.Contain("status-accepted"));
    }
}
