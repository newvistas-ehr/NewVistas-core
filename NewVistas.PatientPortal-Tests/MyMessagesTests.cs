// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using System.Net;
using System.Text.Json;
using Bunit;
using NewVistas.PatientPortal.Components.Pages;
using RichardSzalay.MockHttp;

namespace NewVistas.PatientPortal_Tests;

[TestFixture]
public class MyMessagesTests : PortalTestBase
{
    [Test]
    public void RendersPageTitle()
    {
        MockHttp.When("https://localhost:5001/api/my/messages/threads")
            .Respond("application/json", "[]");
        var cut = Ctx.Render<MyMessages>();
        Assert.That(cut.Markup, Does.Contain("Secure Messages"));
    }

    [Test]
    public void LoadsThreadListFromApi()
    {
        var threads = new[]
        {
            new
            {
                ThreadId = "T-1", Subject = "Medication Question", Category = "medication",
                Status = "open", LastMessageDate = new DateTime(2026, 3, 15, 10, 0, 0),
                MessageCount = 3, HasUnreadPatient = true, HasUnreadProvider = false
            },
            new
            {
                ThreadId = "T-2", Subject = "Appointment Request", Category = "appointment",
                Status = "closed", LastMessageDate = new DateTime(2026, 3, 10, 9, 0, 0),
                MessageCount = 5, HasUnreadPatient = false, HasUnreadProvider = false
            }
        };
        MockHttp.When("https://localhost:5001/api/my/messages/threads")
            .Respond("application/json", JsonSerializer.Serialize(threads));
        var cut = Ctx.Render<MyMessages>();
        cut.WaitForState(() => !cut.Markup.Contains("Loading messages"));
        Assert.That(cut.Markup, Does.Contain("Medication Question"));
        Assert.That(cut.Markup, Does.Contain("Appointment Request"));
        Assert.That(cut.Markup, Does.Contain("medication"));
    }

    [Test]
    public void ShowsEmptyState()
    {
        MockHttp.When("https://localhost:5001/api/my/messages/threads")
            .Respond("application/json", "[]");
        var cut = Ctx.Render<MyMessages>();
        cut.WaitForState(() => !cut.Markup.Contains("Loading messages"));
        Assert.That(cut.Markup, Does.Contain("No messages yet"));
    }

    [Test]
    public void ShowsErrorOnApiFailure()
    {
        MockHttp.When("https://localhost:5001/api/my/messages/threads")
            .Respond(HttpStatusCode.InternalServerError);
        var cut = Ctx.Render<MyMessages>();
        cut.WaitForState(() => !cut.Markup.Contains("Loading messages"));
        Assert.That(cut.Markup, Does.Contain("Error"));
    }

    [Test]
    public void ShowsNewMessageButton()
    {
        MockHttp.When("https://localhost:5001/api/my/messages/threads")
            .Respond("application/json", "[]");
        var cut = Ctx.Render<MyMessages>();
        Assert.That(cut.Markup, Does.Contain("+ New Message"));
    }

    [Test]
    public void ShowsNewMessageFormOnClick()
    {
        MockHttp.When("https://localhost:5001/api/my/messages/threads")
            .Respond("application/json", "[]");
        var cut = Ctx.Render<MyMessages>();
        cut.WaitForState(() => !cut.Markup.Contains("Loading messages"));
        cut.Find(".btn-primary").Click();
        Assert.That(cut.Markup, Does.Contain("New Message to Care Team"));
        Assert.That(cut.Markup, Does.Contain("Subject"));
        Assert.That(cut.Markup, Does.Contain("Category"));
        Assert.That(cut.Markup, Does.Contain("Send Message"));
    }

    [Test]
    public void ShowsValidationErrorOnEmptySubmit()
    {
        MockHttp.When("https://localhost:5001/api/my/messages/threads")
            .Respond("application/json", "[]");
        var cut = Ctx.Render<MyMessages>();
        cut.WaitForState(() => !cut.Markup.Contains("Loading messages"));
        // Click "+ New Message" to show the form
        cut.Find(".btn-primary").Click();
        // Click "Send Message" without filling fields - it's the second .btn-primary
        var buttons = cut.FindAll(".btn-primary");
        buttons[1].Click();
        Assert.That(cut.Markup, Does.Contain("Subject and message are required."));
    }

    [Test]
    public void ViewsThreadDetail()
    {
        var threads = new[]
        {
            new
            {
                ThreadId = "T-1", Subject = "Lab Question", Category = "lab-results",
                Status = "open", LastMessageDate = DateTime.UtcNow,
                MessageCount = 2, HasUnreadPatient = false, HasUnreadProvider = false
            }
        };
        var threadDetail = new
        {
            ThreadId = "T-1",
            Subject = "Lab Question",
            Category = "lab-results",
            Status = "open",
            AssignedProviderName = "Dr. Jones",
            Messages = new[]
            {
                new { SenderType = "patient", SenderName = (string?)null, Body = "What do my results mean?", SentDate = new DateTime(2026, 3, 14, 10, 0, 0), IsRead = true },
                new { SenderType = "provider", SenderName = (string?)"Dr. Jones", Body = "Your results are normal.", SentDate = new DateTime(2026, 3, 15, 9, 0, 0), IsRead = false }
            }
        };
        MockHttp.When("https://localhost:5001/api/my/messages/threads")
            .Respond("application/json", JsonSerializer.Serialize(threads));
        MockHttp.When("https://localhost:5001/api/my/messages/threads/T-1")
            .Respond("application/json", JsonSerializer.Serialize(threadDetail));
        var cut = Ctx.Render<MyMessages>();
        cut.WaitForState(() => !cut.Markup.Contains("Loading messages"));
        cut.Find(".btn-sm").Click();
        cut.WaitForState(() => cut.Markup.Contains("Dr. Jones"));
        Assert.That(cut.Markup, Does.Contain("What do my results mean?"));
        Assert.That(cut.Markup, Does.Contain("Your results are normal."));
        Assert.That(cut.Markup, Does.Contain("Dr. Jones"));
    }

    [Test]
    public void ShowsUnreadBadge()
    {
        var threads = new[]
        {
            new
            {
                ThreadId = "T-1", Subject = "New Results", Category = "lab-results",
                Status = "open", LastMessageDate = DateTime.UtcNow,
                MessageCount = 1, HasUnreadPatient = true, HasUnreadProvider = false
            }
        };
        MockHttp.When("https://localhost:5001/api/my/messages/threads")
            .Respond("application/json", JsonSerializer.Serialize(threads));
        var cut = Ctx.Render<MyMessages>();
        cut.WaitForState(() => !cut.Markup.Contains("Loading messages"));
        Assert.That(cut.Markup, Does.Contain("badge-unread"));
    }

    [Test]
    public void ShowsTableHeaders()
    {
        var threads = new[]
        {
            new
            {
                ThreadId = "T-1", Subject = "Test", Category = "general",
                Status = "open", LastMessageDate = DateTime.UtcNow,
                MessageCount = 1, HasUnreadPatient = false, HasUnreadProvider = false
            }
        };
        MockHttp.When("https://localhost:5001/api/my/messages/threads")
            .Respond("application/json", JsonSerializer.Serialize(threads));
        var cut = Ctx.Render<MyMessages>();
        cut.WaitForState(() => !cut.Markup.Contains("Loading messages"));
        Assert.That(cut.Markup, Does.Contain("Subject"));
        Assert.That(cut.Markup, Does.Contain("Category"));
        Assert.That(cut.Markup, Does.Contain("Last Message"));
        Assert.That(cut.Markup, Does.Contain("Messages"));
        Assert.That(cut.Markup, Does.Contain("Status"));
    }
}
