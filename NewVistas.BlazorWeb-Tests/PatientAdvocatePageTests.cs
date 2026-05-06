// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Bunit;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.BlazorWeb.Components.Pages;
using NSubstitute;

namespace NewVistas.BlazorWeb_Tests;

[TestFixture]
public class PatientAdvocatePageTests : BlazorTestBase
{
    private IComplaintIndexGrain _mockCmpIndex = null!;
    private ICongressionalInquiryIndexGrain _mockInqIndex = null!;

    public override void Setup()
    {
        base.Setup();
        _mockCmpIndex = Substitute.For<IComplaintIndexGrain>();
        _mockInqIndex = Substitute.For<ICongressionalInquiryIndexGrain>();
        MockGrainFactory.GetGrain<IComplaintIndexGrain>(Arg.Any<string>(), Arg.Any<string?>()).Returns(_mockCmpIndex);
        MockGrainFactory.GetGrain<ICongressionalInquiryIndexGrain>(Arg.Any<string>(), Arg.Any<string?>()).Returns(_mockInqIndex);
        _mockCmpIndex.GetAllComplaintsAsync().Returns(new List<ComplaintIndexEntry>());
        _mockInqIndex.GetAllInquiriesAsync().Returns(new List<CongressionalInquiryIndexEntry>());
    }

    [Test]
    public void PatientAdvocate_RendersTitle()
    {
        var cut = Ctx.Render<PatientAdvocate>();
        Assert.That(cut.Markup, Does.Contain("Patient Advocate"));
    }

    [Test]
    public void PatientAdvocate_RendersTabs()
    {
        var cut = Ctx.Render<PatientAdvocate>();
        Assert.That(cut.Markup, Does.Contain("Complaints"));
        Assert.That(cut.Markup, Does.Contain("Congressional Inquiries"));
        Assert.That(cut.Markup, Does.Contain("Advocate Dashboard"));
    }

    [Test]
    public async Task PatientAdvocate_LoadsComplaints()
    {
        var complaints = new List<ComplaintIndexEntry>
        {
            new() { ComplaintId = "C1", PatientId = "P1", PatientName = "Complainant",
                     ReceivedDate = DateTime.UtcNow, ComplaintType = ComplaintType.Formal,
                     Category = ComplaintCategory.QualityOfCare,
                     Priority = ComplaintPriority.Routine, Status = ComplaintStatus.Received }
        };
        _mockCmpIndex.GetAllComplaintsAsync().Returns(complaints);

        var cut = Ctx.Render<PatientAdvocate>();
        cut.WaitForState(() => cut.Markup.Contains("Complainant"));

        Assert.That(cut.Markup, Does.Contain("Complainant"));
    }

    [Test]
    public async Task PatientAdvocate_ShowsEmptyComplaints()
    {
        _mockCmpIndex.GetAllComplaintsAsync().Returns(new List<ComplaintIndexEntry>());

        var cut = Ctx.Render<PatientAdvocate>();
        cut.WaitForState(() => cut.Markup.Contains("No complaints found"));

        Assert.That(cut.Markup, Does.Contain("No complaints found"));
    }
}
