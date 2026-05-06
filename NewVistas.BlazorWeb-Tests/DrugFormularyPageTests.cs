// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Bunit;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.BlazorWeb.Components.Pages;
using NSubstitute;

namespace NewVistas.BlazorWeb_Tests;

[TestFixture]
public class DrugFormularyPageTests : BlazorTestBase
{
    private IVaProductIndexGrain _mockProductIndex = null!;
    private IDrugClassIndexGrain _mockClassIndex = null!;

    public override void Setup()
    {
        base.Setup();
        _mockProductIndex = Substitute.For<IVaProductIndexGrain>();
        _mockClassIndex = Substitute.For<IDrugClassIndexGrain>();

        MockGrainFactory.GetGrain<IVaProductIndexGrain>(Arg.Any<string>(), Arg.Any<string?>()).Returns(_mockProductIndex);
        MockGrainFactory.GetGrain<IDrugClassIndexGrain>(Arg.Any<string>(), Arg.Any<string?>()).Returns(_mockClassIndex);
        MockGrainFactory.GetGrain<IVaProductGrain>(Arg.Any<string>(), Arg.Any<string?>()).Returns(Substitute.For<IVaProductGrain>());

        _mockProductIndex.GetStatusAsync().Returns(new NdfProductIndexStatus { IsLoaded = false, TotalProducts = 0 });
    }

    [Test]
    public void DrugFormulary_RendersPageTitle()
    {
        var cut = Ctx.Render<DrugFormulary>();
        Assert.That(cut.Markup, Does.Contain("Drug Formulary"));
    }

    [Test]
    public void DrugFormulary_ShowsNotLoadedBanner()
    {
        var cut = Ctx.Render<DrugFormulary>();
        Assert.That(cut.Markup, Does.Contain("Drug Formulary data has not been loaded"));
    }

    [Test]
    public async Task DrugFormulary_ShowsErrorOnSearchFailure()
    {
        _mockProductIndex.GetStatusAsync().Returns(new NdfProductIndexStatus { IsLoaded = true, TotalProducts = 100 });
        _mockProductIndex.SearchAsync(Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<string?>(), Arg.Any<bool>(), Arg.Any<int>())
            .Returns<List<VaProductIndexEntry>>(_ => throw new Exception("Index error"));

        var cut = Ctx.Render<DrugFormulary>();
        cut.Find("input.search-input").Input("aspirin");
        await cut.Find("button.btn-primary").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        Assert.That(cut.Markup, Does.Contain("Search error"));
    }
}
