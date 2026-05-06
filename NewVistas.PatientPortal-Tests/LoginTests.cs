// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using System.Net;
using System.Text.Json;
using Bunit;
using Bunit.TestDoubles;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using NewVistas.PatientPortal.Components.Pages;
using RichardSzalay.MockHttp;

namespace NewVistas.PatientPortal_Tests;

[TestFixture]
public class LoginTests : PortalTestBase
{
    [Test]
    public void RendersLoginForm()
    {
        var cut = Ctx.Render<Login>();
        Assert.That(cut.Markup, Does.Contain("MyHealth Portal"));
        Assert.That(cut.Markup, Does.Contain("Patient Sign-In"));
        Assert.That(cut.Markup, Does.Contain("Sign In"));
    }

    [Test]
    public void ShowsRegisterLink()
    {
        var cut = Ctx.Render<Login>();
        Assert.That(cut.Markup, Does.Contain("Register here"));
        Assert.That(cut.Markup, Does.Contain("href=\"/register\""));
    }

    [Test]
    public void ShowsValidationErrorWhenFieldsEmpty()
    {
        var cut = Ctx.Render<Login>();
        cut.Find(".btn-login").Click();
        Assert.That(cut.Markup, Does.Contain("Please enter your Patient ID and password."));
    }

    [Test]
    public void ShowsErrorOnInvalidCredentials()
    {
        MockHttp.When(HttpMethod.Post, "https://localhost:5001/api/patient-auth/login")
            .Respond(HttpStatusCode.Unauthorized);

        var cut = Ctx.Render<Login>();
        cut.Find("input[type=\"text\"]").Input("PATIENT-001");
        cut.Find("input[type=\"password\"]").Input("wrongpassword");
        cut.Find(".btn-login").Click();
        cut.WaitForState(() => cut.Markup.Contains("Invalid credentials"));
        Assert.That(cut.Markup, Does.Contain("Invalid credentials"));
    }

    [Test]
    public void SuccessfulLoginNavigatesToHome()
    {
        MockHttp.When(HttpMethod.Post, "https://localhost:5001/api/patient-auth/login")
            .Respond("application/json", JsonSerializer.Serialize(new
            {
                Token = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiJQQVRJRU5ULTAwMSIsIm5hbWUiOiJUZXN0IFBhdGllbnQiLCJleHAiOjk5OTk5OTk5OTl9.dummy",
                PatientId = "PATIENT-001",
                DisplayName = "Test Patient"
            }));

        var nav = Ctx.Services.GetRequiredService<NavigationManager>() as BunitNavigationManager ?? throw new InvalidOperationException();
        var cut = Ctx.Render<Login>();
        cut.Find("input[type=\"text\"]").Input("PATIENT-001");
        cut.Find("input[type=\"password\"]").Input("password123");
        cut.Find(".btn-login").Click();
        cut.WaitForState(() => nav.Uri != nav.BaseUri + "login");
        Assert.That(nav.Uri, Does.EndWith("/"));
    }

    [Test]
    public void HasPasswordInput()
    {
        var cut = Ctx.Render<Login>();
        var passwordInput = cut.Find("input[type=\"password\"]");
        Assert.That(passwordInput, Is.Not.Null);
    }

    [Test]
    public void HasPatientIdInput()
    {
        var cut = Ctx.Render<Login>();
        var inputs = cut.FindAll("input[type=\"text\"]");
        Assert.That(inputs.Count, Is.GreaterThanOrEqualTo(1));
    }
}
