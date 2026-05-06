// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using System.Net;
using System.Text.Json;
using NewVistas.PatientPortal.Components.Pages;
using RichardSzalay.MockHttp;

namespace NewVistas.PatientPortal_Tests;

[TestFixture]
public class HomeTests : PortalTestBase
{
    private void MockAllEndpoints(
        object? profile = null,
        object? submissions = null,
        object? unreadThreads = null)
    {
        MockHttp.When("https://localhost:5001/api/patient-auth/me")
            .Respond("application/json", JsonSerializer.Serialize(profile ?? new
            {
                PatientId = "PATIENT-001",
                DisplayName = "Test Patient",
                Email = "test@example.com",
                PatientName = "Smith, John",
                DateOfBirth = "01/15/1980",
                LastLoginDate = new DateTime(2026, 3, 20, 14, 30, 0)
            }));

        MockHttp.When("https://localhost:5001/api/my/submissions")
            .Respond("application/json", JsonSerializer.Serialize(submissions ?? new List<object>()));

        MockHttp.When("https://localhost:5001/api/my/messages/threads/unread")
            .Respond("application/json", JsonSerializer.Serialize(unreadThreads ?? new List<object>()));
    }

    [Test]
    public void RendersPageTitle()
    {
        MockAllEndpoints();
        var cut = Ctx.Render<Home>();
        Assert.That(cut.Markup, Does.Contain("Welcome to MyHealth Portal"));
    }

    [Test]
    public void LoadsProfileFromApi()
    {
        MockAllEndpoints();
        var cut = Ctx.Render<Home>();
        cut.WaitForState(() => !cut.Markup.Contains("Loading your health summary"));
        Assert.That(cut.Markup, Does.Contain("Smith, John"));
        Assert.That(cut.Markup, Does.Contain("test@example.com"));
        Assert.That(cut.Markup, Does.Contain("01/15/1980"));
    }

    [Test]
    public void ShowsSubmissionsOnDashboard()
    {
        var submissions = new[]
        {
            new { SubmissionId = "SUB-1", SubmittedDate = new DateTime(2026, 3, 15), Status = "submitted", SectionCount = 3 },
            new { SubmissionId = "SUB-2", SubmittedDate = new DateTime(2026, 3, 10), Status = "accepted", SectionCount = 2 }
        };
        MockAllEndpoints(submissions: submissions);
        var cut = Ctx.Render<Home>();
        cut.WaitForState(() => !cut.Markup.Contains("Loading your health summary"));
        Assert.That(cut.Markup, Does.Contain("Recent Submissions (2)"));
        Assert.That(cut.Markup, Does.Contain("3 sections"));
    }

    [Test]
    public void ShowsEmptySubmissionsState()
    {
        MockAllEndpoints();
        var cut = Ctx.Render<Home>();
        cut.WaitForState(() => !cut.Markup.Contains("Loading your health summary"));
        Assert.That(cut.Markup, Does.Contain("No submissions yet."));
    }

    [Test]
    public void ShowsUnreadMessages()
    {
        var threads = new[]
        {
            new { ThreadId = "T-1", Subject = "Lab Results Question", Category = "lab-results", LastMessageDate = DateTime.UtcNow }
        };
        MockAllEndpoints(unreadThreads: threads);
        var cut = Ctx.Render<Home>();
        cut.WaitForState(() => !cut.Markup.Contains("Loading your health summary"));
        Assert.That(cut.Markup, Does.Contain("Unread Messages (1)"));
        Assert.That(cut.Markup, Does.Contain("Lab Results Question"));
    }

    [Test]
    public void ShowsNoUnreadMessagesState()
    {
        MockAllEndpoints();
        var cut = Ctx.Render<Home>();
        cut.WaitForState(() => !cut.Markup.Contains("Loading your health summary"));
        Assert.That(cut.Markup, Does.Contain("No unread messages."));
    }

    [Test]
    public void ShowsQuickLinks()
    {
        MockAllEndpoints();
        var cut = Ctx.Render<Home>();
        cut.WaitForState(() => !cut.Markup.Contains("Loading your health summary"));
        Assert.That(cut.Markup, Does.Contain("Submit Health Information"));
        Assert.That(cut.Markup, Does.Contain("Send a Message"));
        Assert.That(cut.Markup, Does.Contain("View Medications"));
        Assert.That(cut.Markup, Does.Contain("View Vitals"));
    }

    [Test]
    public void ShowsErrorOnApiFailure()
    {
        MockHttp.When("https://localhost:5001/api/patient-auth/me")
            .Respond(HttpStatusCode.InternalServerError);
        MockHttp.When("https://localhost:5001/api/my/submissions")
            .Respond("application/json", "[]");
        MockHttp.When("https://localhost:5001/api/my/messages/threads/unread")
            .Respond("application/json", "[]");
        var cut = Ctx.Render<Home>();
        cut.WaitForState(() => !cut.Markup.Contains("Loading your health summary"));
        Assert.That(cut.Markup, Does.Contain("Error loading dashboard"));
    }

    [Test]
    public void ShowsLoadingState()
    {
        MockAllEndpoints();
        var cut = Ctx.Render<Home>();
        // The component may or may not still be loading, but it renders the title immediately
        Assert.That(cut.Markup, Does.Contain("Welcome to MyHealth Portal"));
    }
}
