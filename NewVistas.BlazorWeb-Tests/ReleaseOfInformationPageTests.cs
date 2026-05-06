// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Bunit;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.BlazorWeb.Components.Pages;
using NSubstitute;

namespace NewVistas.BlazorWeb_Tests;

[TestFixture]
public class ReleaseOfInformationPageTests : BlazorTestBase
{
    private IROIRequestIndexGrain _mockRoiIndex = null!;

    public override void Setup()
    {
        base.Setup();
        _mockRoiIndex = Substitute.For<IROIRequestIndexGrain>();
        MockGrainFactory.GetGrain<IROIRequestIndexGrain>(Arg.Any<string>(), Arg.Any<string?>()).Returns(_mockRoiIndex);
        _mockRoiIndex.GetAllRequestsAsync().Returns(new List<ROIRequestIndexEntry>());
    }

    [Test]
    public void ReleaseOfInformation_RendersTitle()
    {
        var cut = Ctx.Render<ReleaseOfInformation>();
        Assert.That(cut.Markup, Does.Contain("Release of Information"));
    }

    [Test]
    public void ReleaseOfInformation_RendersTabs()
    {
        var cut = Ctx.Render<ReleaseOfInformation>();
        Assert.That(cut.Markup, Does.Contain("Record Requests"));
        Assert.That(cut.Markup, Does.Contain("HIPAA Disclosures"));
        Assert.That(cut.Markup, Does.Contain("Accounting of Disclosures"));
        Assert.That(cut.Markup, Does.Contain("Dashboard"));
    }

    [Test]
    public async Task ReleaseOfInformation_LoadsRequests()
    {
        var requests = new List<ROIRequestIndexEntry>
        {
            new() { RequestId = "R1", PatientId = "P1", PatientName = "ROI Patient",
                     ReceivedDate = DateTime.UtcNow, RequestType = ROIRequestType.MedicalRecords,
                     RequesterType = RequesterType.Patient, RequesterName = "Self",
                     Status = ROIRequestStatus.Received, DueDate = DateTime.UtcNow.AddDays(30),
                     Priority = ROIRequestPriority.Routine }
        };
        _mockRoiIndex.GetAllRequestsAsync().Returns(requests);

        var cut = Ctx.Render<ReleaseOfInformation>();
        cut.WaitForState(() => cut.Markup.Contains("ROI Patient"));

        Assert.That(cut.Markup, Does.Contain("ROI Patient"));
    }

    [Test]
    public async Task ReleaseOfInformation_ShowsEmptyRequests()
    {
        _mockRoiIndex.GetAllRequestsAsync().Returns(new List<ROIRequestIndexEntry>());

        var cut = Ctx.Render<ReleaseOfInformation>();
        cut.WaitForState(() => cut.Markup.Contains("No requests found"));

        Assert.That(cut.Markup, Does.Contain("No requests found"));
    }
}
