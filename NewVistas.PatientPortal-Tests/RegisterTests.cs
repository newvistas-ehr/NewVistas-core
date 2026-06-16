// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
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
public class RegisterTests : PortalTestBase
{
    /// <summary>
    /// Helper to fill Register form fields. Re-queries elements after each input
    /// because oninput triggers a re-render that invalidates element references.
    /// </summary>
    private void FillForm(IRenderedComponent<Register> cut,
        string? patientId = null, string? email = null, string? displayName = null,
        string? password = null, string? confirmPassword = null)
    {
        if (patientId != null)
            cut.FindAll("input")[0].Input(patientId);
        if (email != null)
            cut.FindAll("input")[1].Input(email);
        if (displayName != null)
            cut.FindAll("input")[2].Input(displayName);
        if (password != null)
            cut.FindAll("input")[3].Input(password);
        if (confirmPassword != null)
            cut.FindAll("input")[4].Input(confirmPassword);
    }

    [Test]
    public void RendersRegistrationForm()
    {
        var cut = Ctx.Render<Register>();
        Assert.That(cut.Markup, Does.Contain("Create Your Account"));
        Assert.That(cut.Markup, Does.Contain("MyHealth Patient Portal"));
        Assert.That(cut.Markup, Does.Contain("Create Account"));
    }

    [Test]
    public void ShowsSignInLink()
    {
        var cut = Ctx.Render<Register>();
        Assert.That(cut.Markup, Does.Contain("Already have an account?"));
        Assert.That(cut.Markup, Does.Contain("href=\"/login\""));
    }

    [Test]
    public void ShowsValidationErrorWhenFieldsEmpty()
    {
        var cut = Ctx.Render<Register>();
        cut.Find(".btn-register").Click();
        Assert.That(cut.Markup, Does.Contain("Patient ID, Email, and Password are required."));
    }

    [Test]
    public void ShowsPasswordMismatchError()
    {
        var cut = Ctx.Render<Register>();
        FillForm(cut,
            patientId: "PATIENT-001",
            email: "test@example.com",
            password: "password123",
            confirmPassword: "different123");
        cut.Find(".btn-register").Click();
        Assert.That(cut.Markup, Does.Contain("Passwords do not match."));
    }

    [Test]
    public void ShowsPasswordLengthError()
    {
        var cut = Ctx.Render<Register>();
        FillForm(cut,
            patientId: "PATIENT-001",
            email: "test@example.com",
            password: "short",
            confirmPassword: "short");
        cut.Find(".btn-register").Click();
        Assert.That(cut.Markup, Does.Contain("Password must be at least 8 characters."));
    }

    [Test]
    public void SuccessfulRegistrationNavigatesToHome()
    {
        // Register endpoint
        MockHttp.When(HttpMethod.Post, "https://localhost:5001/api/patient-auth/register")
            .Respond(HttpStatusCode.OK);
        // Login endpoint (called after registration)
        MockHttp.When(HttpMethod.Post, "https://localhost:5001/api/patient-auth/login")
            .Respond("application/json", JsonSerializer.Serialize(new
            {
                Token = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiJQQVRJRU5ULTAwMSIsIm5hbWUiOiJUZXN0IFBhdGllbnQiLCJleHAiOjk5OTk5OTk5OTl9.dummy",
                PatientId = "PATIENT-001",
                DisplayName = "Test Patient"
            }));

        var nav = Ctx.Services.GetRequiredService<NavigationManager>() as BunitNavigationManager ?? throw new InvalidOperationException();
        var cut = Ctx.Render<Register>();
        FillForm(cut,
            patientId: "PATIENT-001",
            email: "test@example.com",
            displayName: "Test Patient",
            password: "password123",
            confirmPassword: "password123");
        cut.Find(".btn-register").Click();
        cut.WaitForState(() => nav.Uri != nav.BaseUri + "register");
        Assert.That(nav.Uri, Does.EndWith("/"));
    }

    [Test]
    public void ShowsErrorOnRegistrationFailure()
    {
        MockHttp.When(HttpMethod.Post, "https://localhost:5001/api/patient-auth/register")
            .Respond(HttpStatusCode.BadRequest, "text/plain", "Patient ID already registered");
        // Login endpoint (called after failed registration auto-login)
        MockHttp.When(HttpMethod.Post, "https://localhost:5001/api/patient-auth/login")
            .Respond(HttpStatusCode.Unauthorized);

        var cut = Ctx.Render<Register>();
        FillForm(cut,
            patientId: "PATIENT-001",
            email: "test@example.com",
            password: "password123",
            confirmPassword: "password123");
        cut.Find(".btn-register").Click();
        cut.WaitForState(() => cut.Markup.Contains("Registration failed"));
        Assert.That(cut.Markup, Does.Contain("Registration failed"));
    }

    [Test]
    public void HasRequiredFormFields()
    {
        var cut = Ctx.Render<Register>();
        Assert.That(cut.Markup, Does.Contain("Patient ID"));
        Assert.That(cut.Markup, Does.Contain("Email Address"));
        Assert.That(cut.Markup, Does.Contain("Display Name"));
        Assert.That(cut.Markup, Does.Contain("Password"));
        Assert.That(cut.Markup, Does.Contain("Confirm Password"));
    }
}
