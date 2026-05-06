// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Bunit;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.BlazorWeb.Components.Pages;
using NSubstitute;

namespace NewVistas.BlazorWeb_Tests;

[TestFixture]
public class Icd10BrowserPageTests : BlazorTestBase
{
    private IIcd10IndexGrain _mockIcd10Index = null!;

    public override void Setup()
    {
        base.Setup();
        _mockIcd10Index = Substitute.For<IIcd10IndexGrain>();
        MockGrainFactory.GetGrain<IIcd10IndexGrain>(Arg.Any<string>(), Arg.Any<string?>())
            .Returns(_mockIcd10Index);
        _mockIcd10Index.GetStatusAsync().Returns(new Icd10IndexStatus { IsLoaded = true, TotalCodes = 97000, BillableCodes = 72000 });
    }

    [Test]
    public void Icd10Browser_RendersPageTitle()
    {
        var cut = Ctx.Render<Icd10Browser>();
        Assert.That(cut.Markup, Does.Contain("ICD-10"));
    }

    [Test]
    public void Icd10Browser_RendersSearchInput()
    {
        var cut = Ctx.Render<Icd10Browser>();
        var input = cut.Find("input.search-input");
        Assert.That(input, Is.Not.Null);
    }

    [Test]
    public async Task Icd10Browser_SearchesFromGrain()
    {
        var results = new List<Icd10IndexEntry>
        {
            new() { Code = "E11.9", ShortDescription = "Type 2 diabetes mellitus without complications",
                     LongDescription = "Type 2 diabetes mellitus without complications", IsBillable = true, Chapter = "E" }
        };
        _mockIcd10Index.SearchAsync("diabetes", false, 100).Returns(results);

        var cut = Ctx.Render<Icd10Browser>();
        cut.Find("input.search-input").Input("diabetes");
        await cut.Find("button.btn-primary").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        Assert.That(cut.Markup, Does.Contain("E11.9"));
        Assert.That(cut.Markup, Does.Contain("diabetes"));
    }

    [Test]
    public async Task Icd10Browser_ShowsStatusInfo()
    {
        var cut = Ctx.Render<Icd10Browser>();
        // Status is loaded on init
        Assert.That(cut.Markup, Does.Contain("97,000").Or.Contain("97000"));
    }
}
