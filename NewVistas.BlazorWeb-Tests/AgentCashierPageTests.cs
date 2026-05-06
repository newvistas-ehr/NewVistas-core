// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.BlazorWeb.Components.Pages;
using NSubstitute;

namespace NewVistas.BlazorWeb_Tests;

[TestFixture]
public class AgentCashierPageTests : BlazorTestBase
{
    [Test]
    public void AgentCashier_RendersPageTitle()
    {
        var cut = Ctx.Render<AgentCashier>();
        Assert.That(cut.Markup, Does.Contain("Agent Cashier"));
    }

    [Test]
    public void AgentCashier_RendersTabs()
    {
        var cut = Ctx.Render<AgentCashier>();
        Assert.That(cut.Markup, Does.Contain("Cashier Window"));
        Assert.That(cut.Markup, Does.Contain("Sessions"));
    }

    [Test]
    public async Task AgentCashier_ShowsErrorOnGrainFailure()
    {
        var mockIndex = Substitute.For<ICashierReceiptIndexGrain>();
        mockIndex.GetAllAsync().Returns<List<CashierReceiptIndexEntry>>(_ => throw new Exception("Index unavailable"));
        MockGrainFactory.GetGrain<ICashierReceiptIndexGrain>(Arg.Any<string>(), Arg.Any<string?>()).Returns(mockIndex);

        var cut = Ctx.Render<AgentCashier>();
        cut.Find("input.form-control").Change("PAT-001");
        await cut.Find("button.btn-primary").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        Assert.That(cut.Markup, Does.Contain("Error loading receipts"));
    }
}
