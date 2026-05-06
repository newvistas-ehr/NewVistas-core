// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.BlazorWeb.Components.Pages;
using NSubstitute;

namespace NewVistas.BlazorWeb_Tests;

[TestFixture]
public class SecurityKeyManagementPageTests : BlazorTestBase
{
    [Test]
    public void SecurityKeyManagement_RendersPageTitle()
    {
        var cut = Ctx.Render<SecurityKeyManagement>();
        Assert.That(cut.Markup, Does.Contain("Security Key Management"));
    }

    [Test]
    public void SecurityKeyManagement_RendersLookupBar()
    {
        var cut = Ctx.Render<SecurityKeyManagement>();
        var input = cut.Find("input.lookup-input");
        Assert.That(input, Is.Not.Null);
    }

    [Test]
    public async Task SecurityKeyManagement_LoadsUserFromGrain()
    {
        var mockGrain = Substitute.For<IAccessControlGrain>();
        mockGrain.GetAccessControlStateAsync().Returns(new AccessControlState
        {
            UserId = "USER-001",
            SecurityKeys = new HashSet<string> { "ORES", "PROVIDER" },
            HasActiveSession = true
        });
        mockGrain.GetKeyAuditLogAsync().Returns(new List<SecurityKeyAuditEntry>());
        MockGrainFactory.GetGrain<IAccessControlGrain>(Arg.Any<string>(), Arg.Any<string?>()).Returns(mockGrain);

        var cut = Ctx.Render<SecurityKeyManagement>();
        cut.Find("input.lookup-input").Input("USER-001");
        await cut.Find("button.btn-primary").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        Assert.That(cut.Markup, Does.Contain("SESSION ACTIVE"));
    }

    [Test]
    public async Task SecurityKeyManagement_ShowsErrorOnGrainFailure()
    {
        var mockGrain = Substitute.For<IAccessControlGrain>();
        mockGrain.GetAccessControlStateAsync().Returns<AccessControlState>(x => throw new Exception("Connection failed"));
        MockGrainFactory.GetGrain<IAccessControlGrain>(Arg.Any<string>(), Arg.Any<string?>()).Returns(mockGrain);

        var cut = Ctx.Render<SecurityKeyManagement>();
        cut.Find("input.lookup-input").Input("USER-001");
        await cut.Find("button.btn-primary").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        Assert.That(cut.Markup, Does.Contain("Error loading user"));
    }
}
