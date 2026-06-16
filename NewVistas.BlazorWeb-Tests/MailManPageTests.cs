// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Bunit;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.BlazorWeb.Components.Pages;
using NSubstitute;

namespace NewVistas.BlazorWeb_Tests;

[TestFixture]
public class MailManPageTests : BlazorTestBase
{
    [Test]
    public void MailMan_RendersPageTitle()
    {
        var cut = Ctx.Render<MailMan>();
        Assert.That(cut.Markup, Does.Contain("MailMan"));
    }

    [Test]
    public void MailMan_RendersLookupBar()
    {
        var cut = Ctx.Render<MailMan>();
        var input = cut.Find("input.lookup-input");
        Assert.That(input, Is.Not.Null);
    }

    [Test]
    public async Task MailMan_LoadsInboxFromGrain()
    {
        var mockMailbox = Substitute.For<IUserMailboxGrain>();
        mockMailbox.GetInboxAsync().Returns(new List<MailboxEntry>
        {
            new() { MessageId = "MSG-1", Subject = "Test Message", SenderName = "Admin", SentDateTime = DateTime.UtcNow }
        });
        mockMailbox.GetSentItemsAsync().Returns(new List<MailboxEntry>());
        mockMailbox.GetDeletedItemsAsync().Returns(new List<MailboxEntry>());
        mockMailbox.GetUnreadCountAsync().Returns(1);
        MockGrainFactory.GetGrain<IUserMailboxGrain>(Arg.Any<string>(), Arg.Any<string?>()).Returns(mockMailbox);

        var mockGroupIndex = Substitute.For<IMailGroupIndexGrain>();
        mockGroupIndex.GetAllGroupsAsync().Returns(new List<MailGroupIndexEntry>());
        MockGrainFactory.GetGrain<IMailGroupIndexGrain>(Arg.Any<string>(), Arg.Any<string?>()).Returns(mockGroupIndex);

        var cut = Ctx.Render<MailMan>();
        cut.Find("input.lookup-input").Input("USER-001");
        await cut.Find("button.btn-primary").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        Assert.That(cut.Markup, Does.Contain("Test Message"));
    }
}
