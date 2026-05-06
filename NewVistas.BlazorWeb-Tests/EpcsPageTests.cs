// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Bunit;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.BlazorWeb.Components.Pages;
using NSubstitute;

namespace NewVistas.BlazorWeb_Tests;

[TestFixture]
public class EpcsPageTests : BlazorTestBase
{
    public override void Setup()
    {
        base.Setup();
        MockGrainFactory.GetGrain<IEpcsProviderIndexGrain>(Arg.Any<string>(), Arg.Any<string?>())
            .Returns(Substitute.For<IEpcsProviderIndexGrain>());
        MockGrainFactory.GetGrain<IEpcsProviderCredentialGrain>(Arg.Any<string>(), Arg.Any<string?>())
            .Returns(Substitute.For<IEpcsProviderCredentialGrain>());
    }

    [Test]
    public void Epcs_RendersPageTitle()
    {
        var cut = Ctx.Render<Epcs>();
        Assert.That(cut.Markup, Does.Contain("Electronic Prescribing of Controlled Substances"));
    }

    [Test]
    public async Task Epcs_LoadsPrescriptionsFromWorkflowGrain()
    {
        var rxList = new List<EpcsPrescriptionIndexEntry>
        {
            new() { EpcsId = "EPCS-001", PatientId = "P-001", DrugName = "Adderall 20mg", DeaSchedule = "II", TransactionType = EpcsScriptTransactionType.NewRx, Status = EpcsTransmissionStatus.Draft, CreatedDate = DateTime.UtcNow, IsSigned = false }
        };
        MockWorkflowGrain.GetEpcsPrescriptionsAsync().Returns(rxList);

        var cut = Ctx.Render<Epcs>();
        cut.Find("input.form-control").Change("P-001");
        await cut.Find("button.btn-primary").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        Assert.That(cut.Markup, Does.Contain("Adderall 20mg"));
    }

    [Test]
    public async Task Epcs_ShowsErrorOnGrainFailure()
    {
        MockWorkflowGrain.GetEpcsPrescriptionsAsync().Returns<List<EpcsPrescriptionIndexEntry>>(
            _ => throw new Exception("Silo unavailable"));

        var cut = Ctx.Render<Epcs>();
        cut.Find("input.form-control").Change("P-002");
        await cut.Find("button.btn-primary").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        Assert.That(cut.Markup, Does.Contain("Silo unavailable"));
    }
}
