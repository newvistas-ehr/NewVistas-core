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

        // Register services — must happen before first Render
        Ctx.Services.AddSingleton(grainService);
        Ctx.Services.AddSingleton(httpClient);
        Ctx.Services.AddSingleton(new PatientContextService());
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
