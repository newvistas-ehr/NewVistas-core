// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Bunit;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.BlazorWeb.Components.Pages;
using NSubstitute;

namespace NewVistas.BlazorWeb_Tests;

[TestFixture]
public class BloodBankPageTests : BlazorTestBase
{
    [Test]
    public void BloodBank_RendersPageTitle()
    {
        var cut = Ctx.Render<BloodBank>();
        Assert.That(cut.Markup, Does.Contain("Blood Bank"));
    }

    [Test]
    public void BloodBank_RendersLookupBar()
    {
        var cut = Ctx.Render<BloodBank>();
        var input = cut.Find("input.lookup-input");
        Assert.That(input, Is.Not.Null);
    }

    [Test]
    public async Task BloodBank_LoadsPatientDataFromGrain()
    {
        var state = new BloodBankPatientState
        {
            PatientId = "PAT-001",
            AboType = AboBloodType.O,
            RhType = RhBloodType.Positive,
            AntibodyScreenResult = AntibodyScreenResult.Negative,
            TransfusionCount = 3
        };
        MockWorkflowGrain.GetBloodBankPatientAsync().Returns(state);
        MockWorkflowGrain.GetCrossmatchesAsync().Returns(new List<CrossmatchIndexEntry>());
        MockWorkflowGrain.GetTransfusionHistoryAsync().Returns(new List<TransfusionIndexEntry>());

        var cut = Ctx.Render<BloodBank>();
        cut.Find("input.lookup-input").Input("PAT-001");
        await cut.Find("button.btn-primary").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        Assert.That(cut.Markup, Does.Contain("O"));
        Assert.That(cut.Markup, Does.Contain("Positive"));
    }

    [Test]
    public async Task BloodBank_ShowsErrorOnGrainFailure()
    {
        MockWorkflowGrain.GetBloodBankPatientAsync().Returns<BloodBankPatientState>(
            _ => throw new Exception("Connection refused"));

        var cut = Ctx.Render<BloodBank>();
        cut.Find("input.lookup-input").Input("PAT-002");
        await cut.Find("button.btn-primary").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        Assert.That(cut.Markup, Does.Contain("Connection refused"));
    }
}
