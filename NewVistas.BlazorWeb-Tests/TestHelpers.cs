// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Bunit;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.BlazorWeb.Services;
using NewVistas.ImageStorage;
using NSubstitute;
using Orleans;
using System.Security.Claims;

namespace NewVistas.BlazorWeb_Tests;

/// <summary>
/// Base class for bUnit Blazor component tests. Uses composition (owns a BunitContext)
/// rather than inheritance, because BunitContext locks its service container after first
/// render and cannot be reset between tests.
/// </summary>
public abstract class BlazorTestBase
{
    protected BunitContext Ctx { get; private set; } = null!;
    protected IGrainFactory MockGrainFactory { get; private set; } = null!;
    protected IPatientWorkflowGrain MockWorkflowGrain { get; private set; } = null!;

    /// <summary>Grain service wired to the mock factory — exposed so tests can initialize
    /// <see cref="SecurityContext"/> from a mock ACL grain or call grains directly.</summary>
    protected OrleansGrainService GrainService { get; private set; } = null!;

    /// <summary>Per-circuit security context. Uninitialized by default (no keys, IsInitialized
    /// false), so key-gated pages fall through to the grain as before; initialize it to test
    /// key gating.</summary>
    protected UserSecurityContext SecurityContext { get; private set; } = null!;

    /// <summary>
    /// The shared patient context for the circuit. Patient-scoped pages now show the
    /// patient selected here (via the &lt;PatientBar&gt;) and auto-load it on render —
    /// there is no per-page ID box. Call <see cref="SelectPatient"/> before rendering.
    /// </summary>
    protected PatientContextService PatientContext { get; private set; } = null!;

    /// <summary>
    /// Select the active patient before rendering a page, mimicking a prior visit to
    /// Patient Lookup. The page's &lt;PatientBar&gt; adopts this patient and auto-loads it.
    /// </summary>
    protected void SelectPatient(string patientId, string patientName = "TEST,PATIENT")
        => PatientContext.SetPatient(patientId, patientName);

    [SetUp]
    public virtual void Setup()
    {
        Ctx = new BunitContext();

        MockGrainFactory = Substitute.For<IGrainFactory>();
        MockWorkflowGrain = Substitute.For<IPatientWorkflowGrain>();

        // Default: any patient ID returns the mock workflow grain
        MockGrainFactory
            .GetGrain<IPatientWorkflowGrain>(Arg.Any<string>(), Arg.Any<string?>())
            .Returns(MockWorkflowGrain);

        // Create a test HttpClient (used only by JwtAuthenticationStateProvider constructor)
        var httpClient = new HttpClient { BaseAddress = new Uri("https://localhost:7127") };

        // Create auth provider and grain service with mock factory
        var authProvider = new JwtAuthenticationStateProvider(httpClient);
        var grainService = new OrleansGrainService(MockGrainFactory, authProvider);
        GrainService = grainService;

        // Register services — must happen before first Render
        PatientContext = new PatientContextService();
        SecurityContext = new UserSecurityContext();
        Ctx.Services.AddSingleton(grainService);
        Ctx.Services.AddSingleton(httpClient);
        Ctx.Services.AddSingleton(PatientContext);
        Ctx.Services.AddSingleton(SecurityContext);
        Ctx.Services.AddSingleton(Substitute.For<IImageBlobStorageService>());
        Ctx.Services.AddSingleton(Substitute.For<IImageIngestionService>());

        // Provide fake AuthenticationStateProvider so [Authorize] doesn't block rendering
        var authState = Task.FromResult(
            new AuthenticationState(
                new ClaimsPrincipal(new ClaimsIdentity(
                    [new Claim(ClaimTypes.Name, "TestUser")], "TestAuth"))));
        var mockAuthProvider = Substitute.For<AuthenticationStateProvider>();
        mockAuthProvider.GetAuthenticationStateAsync().Returns(authState);
        Ctx.Services.AddSingleton<AuthenticationStateProvider>(mockAuthProvider);
        Ctx.Services.AddAuthorizationCore();
        Ctx.Services.AddCascadingAuthenticationState();
    }

    [TearDown]
    public virtual void TearDown()
    {
        Ctx?.Dispose();
    }
}
