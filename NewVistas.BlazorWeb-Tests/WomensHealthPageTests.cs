// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.BlazorWeb.Components.Pages;
using NSubstitute;

namespace NewVistas.BlazorWeb_Tests;

[TestFixture]
public class WomensHealthPageTests : BlazorTestBase
{
    [Test]
    public void WomensHealth_RendersPageTitle()
    {
        var cut = Ctx.Render<WomensHealth>();

        Assert.That(cut.Markup, Does.Contain("Women's Health"));
    }

    [Test]
    public void WomensHealth_RendersLookupBar()
    {
        var cut = Ctx.Render<WomensHealth>();

        var input = cut.Find("input.lookup-input");
        Assert.That(input, Is.Not.Null);
        Assert.That(input.GetAttribute("placeholder"), Is.EqualTo("Patient ID"));
    }

    [Test]
    public void WomensHealth_LoadButton_DisabledWhenEmpty()
    {
        var cut = Ctx.Render<WomensHealth>();

        var button = cut.Find("button.btn-primary");
        Assert.That(button.HasAttribute("disabled"), Is.True);
    }

    [Test]
    public async Task WomensHealth_LoadsDataFromGrain()
    {
        var notifications = new List<WomensHealthIndexEntry>
        {
            new()
            {
                NotificationId = "WH-001",
                PatientId = "PAT-001",
                NotificationType = WomensHealthNotificationType.Mammography,
                ProcedureDate = DateTime.Today,
                Status = WomensHealthNotificationStatus.Active,
                ProviderName = "Dr. Smith",
                FollowUpRequired = true,
                NextDueDate = DateTime.Today.AddMonths(6)
            }
        };
        MockWorkflowGrain.GetWomensHealthNotificationsAsync().Returns(notifications);

        var cut = Ctx.Render<WomensHealth>();

        cut.Find("input.lookup-input").Input("PAT-001");
        await cut.Find("button.btn-primary").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        await MockWorkflowGrain.Received(1).GetWomensHealthNotificationsAsync();
        Assert.That(cut.Markup, Does.Contain("Dr. Smith"));
        Assert.That(cut.Markup, Does.Contain("Mammography"));
    }

    [Test]
    public async Task WomensHealth_ShowsEmptyState()
    {
        MockWorkflowGrain.GetWomensHealthNotificationsAsync().Returns(new List<WomensHealthIndexEntry>());

        var cut = Ctx.Render<WomensHealth>();

        cut.Find("input.lookup-input").Input("PAT-002");
        await cut.Find("button.btn-primary").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        Assert.That(cut.Markup, Does.Contain("No Women's Health notifications found"));
    }

    [Test]
    public async Task WomensHealth_ShowsErrorOnGrainFailure()
    {
        MockWorkflowGrain.GetWomensHealthNotificationsAsync().Returns<List<WomensHealthIndexEntry>>(
            _ => throw new Exception("Silo unreachable"));

        var cut = Ctx.Render<WomensHealth>();

        cut.Find("input.lookup-input").Input("PAT-003");
        await cut.Find("button.btn-primary").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        Assert.That(cut.Markup, Does.Contain("Silo unreachable"));
    }
}
