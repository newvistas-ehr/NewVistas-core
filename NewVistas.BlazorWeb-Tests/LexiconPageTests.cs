// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Bunit;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.BlazorWeb.Components.Pages;
using NSubstitute;

namespace NewVistas.BlazorWeb_Tests;

[TestFixture]
public class LexiconPageTests : BlazorTestBase
{
    [Test]
    public void Lexicon_RendersPageTitle()
    {
        var cut = Ctx.Render<Lexicon>();
        Assert.That(cut.Markup, Does.Contain("Lexicon Utility"));
    }

    [Test]
    public void Lexicon_RendersSearchTab()
    {
        var cut = Ctx.Render<Lexicon>();
        Assert.That(cut.Markup, Does.Contain("Search"));
    }

    [Test]
    public async Task Lexicon_ShowsErrorOnEmptySearch()
    {
        var cut = Ctx.Render<Lexicon>();
        // The search action button is the last "Search" button (after the toolbar input/select)
        var searchButton = cut.FindAll("button").Last(b => b.TextContent.Trim() == "Search");
        await searchButton.ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());
        Assert.That(cut.Markup, Does.Contain("Enter a search term"));
    }
}
