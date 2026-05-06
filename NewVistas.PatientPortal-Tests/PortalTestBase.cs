// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Bunit;
using Bunit.TestDoubles;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using NewVistas.PatientPortal.Services;
using RichardSzalay.MockHttp;
using System.Security.Claims;

namespace NewVistas.PatientPortal_Tests;

public abstract class PortalTestBase
{
    protected BunitContext Ctx { get; private set; } = null!;
    protected MockHttpMessageHandler MockHttp { get; private set; } = null!;

    [SetUp]
    public virtual void Setup()
    {
        Ctx = new BunitContext();
        MockHttp = new MockHttpMessageHandler();

        var httpClient = MockHttp.ToHttpClient();
        httpClient.BaseAddress = new Uri("https://localhost:5001/");

        // Register HttpClient
        Ctx.Services.AddSingleton(httpClient);

        // Register PatientAuthStateProvider
        var authProvider = new PatientAuthStateProvider(httpClient);
        Ctx.Services.AddSingleton(authProvider);
        Ctx.Services.AddSingleton<AuthenticationStateProvider>(authProvider);

        // Use bUnit's built-in fake auth
        var authContext = Ctx.AddAuthorization();
        authContext.SetAuthorized("PATIENT-001");
        authContext.SetClaims(
            new Claim(ClaimTypes.Name, "Test Patient"),
            new Claim("patient_id", "PATIENT-001"));
    }

    [TearDown]
    public virtual void TearDown()
    {
        Ctx?.Dispose();
        MockHttp?.Dispose();
    }
}
