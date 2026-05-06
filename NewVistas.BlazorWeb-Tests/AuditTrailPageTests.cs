// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Bunit;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.BlazorWeb.Components.Pages;
using NSubstitute;

namespace NewVistas.BlazorWeb_Tests;

[TestFixture]
public class AuditTrailPageTests : BlazorTestBase
{
    [Test]
    public void AuditTrail_RendersPageTitle()
    {
        var cut = Ctx.Render<AuditTrail>();
        Assert.That(cut.Markup, Does.Contain("Audit Trail"));
    }

    [Test]
    public void AuditTrail_RendersLookupBar()
    {
        var cut = Ctx.Render<AuditTrail>();
        var input = cut.Find("input.lookup-input");
        Assert.That(input, Is.Not.Null);
    }

    [Test]
    public async Task AuditTrail_LoadsEventsFromGrain()
    {
        var events = new List<AuditEventSummary>
        {
            new() { EventId = "EVT-001", Domain = "ORDERS", Action = "CREATE",
                     EntityType = "ORDER", EntityId = "ORD-001",
                     UserName = "Smith", Timestamp = DateTime.UtcNow }
        };
        MockWorkflowGrain.GetRecentAuditEventsAsync(200).Returns(events);

        var cut = Ctx.Render<AuditTrail>();
        cut.Find("input.lookup-input").Input("PAT-001");
        await cut.Find("button.btn-primary").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        Assert.That(cut.Markup, Does.Contain("ORDERS"));
        Assert.That(cut.Markup, Does.Contain("CREATE"));
    }

    [Test]
    public async Task AuditTrail_ShowsErrorOnFailure()
    {
        MockWorkflowGrain.GetRecentAuditEventsAsync(200).Returns<List<AuditEventSummary>>(
            _ => throw new Exception("Grain unavailable"));

        var cut = Ctx.Render<AuditTrail>();
        cut.Find("input.lookup-input").Input("PAT-002");
        await cut.Find("button.btn-primary").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        Assert.That(cut.Markup, Does.Contain("Error"));
    }
}
