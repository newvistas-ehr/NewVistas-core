// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NUnit.Framework;
using Orleans.TestingHost;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.FunctionalTests;

/// <summary>
/// Functional tests for VistA Consults — File #123.
/// Tests consult request/accept/schedule/complete workflows and Phase 4 depth
/// enhancements (tracking comments, accept/schedule details, consult type,
/// clinical history, follow-up, consulting provider, interfacility).
/// Calls <see cref="IConsultGrain"/> methods directly via TestCluster.
/// </summary>
[TestFixture]
public class ConsultsWorkflowTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private IConsultGrain NewConsult()
        => _cluster.GrainFactory.GetGrain<IConsultGrain>($"CONSULT-{Guid.NewGuid():N}");

    private async Task RequestStandardAsync(IConsultGrain grain,
        string toService = "CARDIOLOGY",
        string urgency = "ROUTINE",
        string? reason = "Chest pain evaluation")
    {
        await grain.RequestConsultAsync(
            "PAT-001", toService, $"SVC-{Guid.NewGuid():N}",
            "PRIMARY CARE", $"SVC-{Guid.NewGuid():N}",
            urgency,
            "PROV-001", "Dr. Adams",
            "PROV-002", "Dr. Rivera",
            reason, "Suspected CAD",
            null, "LOC-001", "Medicine Clinic");
    }

    // ─── 1. Request tests ───────────────────────────────────────────────────

    [Test]
    public async Task Consult_CanRequestConsult()
    {
        // Arrange
        IConsultGrain grain = NewConsult();

        // Act
        await RequestStandardAsync(grain);
        ConsultState state = await grain.GetConsultAsync();

        // Assert
        Assert.That(state.ConsultId, Is.Not.Null.And.Not.Empty);
        Assert.That(state.ToService, Is.EqualTo("CARDIOLOGY"));
        Assert.That(state.Status, Is.EqualTo("PENDING"));
        Assert.That(state.PatientId, Is.EqualTo("PAT-001"));
        Assert.That(state.Urgency, Is.EqualTo("ROUTINE"));
        Assert.That(state.ReasonForRequest, Is.EqualTo("Chest pain evaluation"));
    }

    // ─── 2. Accept ──────────────────────────────────────────────────────────

    [Test]
    public async Task Consult_CanAccept()
    {
        // Arrange
        IConsultGrain grain = NewConsult();
        await RequestStandardAsync(grain);

        // Act
        await grain.AcceptAsync();
        ConsultState state = await grain.GetConsultAsync();

        // Assert
        Assert.That(state.Status, Is.EqualTo("ACTIVE"));
    }

    // ─── 3. Schedule ────────────────────────────────────────────────────────

    [Test]
    public async Task Consult_CanSchedule()
    {
        // Arrange
        IConsultGrain grain = NewConsult();
        await RequestStandardAsync(grain);
        await grain.AcceptAsync();

        // Act
        await grain.ScheduleAsync();
        ConsultState state = await grain.GetConsultAsync();

        // Assert
        Assert.That(state.Status, Is.EqualTo("SCHEDULED"));
    }

    // ─── 4. Complete ────────────────────────────────────────────────────────

    [Test]
    public async Task Consult_CanComplete()
    {
        // Arrange
        IConsultGrain grain = NewConsult();
        await RequestStandardAsync(grain);
        await grain.AcceptAsync();
        await grain.ScheduleAsync();

        // Act
        DateTime completedAt = DateTime.UtcNow;
        await grain.CompleteAsync(completedAt, "DOC-001");
        ConsultState state = await grain.GetConsultAsync();

        // Assert
        Assert.That(state.Status, Is.EqualTo("COMPLETE"));
        Assert.That(state.CompletedDateTime, Is.Not.Null);
        Assert.That(state.ResultDocumentId, Is.EqualTo("DOC-001"));
    }

    // ─── 5. Cancel ──────────────────────────────────────────────────────────

    [Test]
    public async Task Consult_CanCancel()
    {
        // Arrange
        IConsultGrain grain = NewConsult();
        await RequestStandardAsync(grain);

        // Act
        await grain.CancelAsync("Patient declined consult");
        ConsultState state = await grain.GetConsultAsync();

        // Assert
        Assert.That(state.Status, Is.EqualTo("CANCELLED"));
        Assert.That(state.Comments, Is.EqualTo("Patient declined consult"));
    }

    // ─── 6. Discontinue ─────────────────────────────────────────────────────

    [Test]
    public async Task Consult_CanDiscontinue()
    {
        // Arrange
        IConsultGrain grain = NewConsult();
        await RequestStandardAsync(grain);

        // Act
        await grain.DiscontinueAsync("No longer clinically indicated");
        ConsultState state = await grain.GetConsultAsync();

        // Assert
        Assert.That(state.Status, Is.EqualTo("DISCONTINUED"));
        Assert.That(state.Comments, Is.EqualTo("No longer clinically indicated"));
    }

    // ─── 7. Add tracking comment ────────────────────────────────────────────

    [Test]
    public async Task Consult_CanAddTrackingComment()
    {
        // Arrange
        IConsultGrain grain = NewConsult();
        await RequestStandardAsync(grain);

        // Act
        await grain.AddTrackingCommentAsync("AUTH-001", "Dr. Smith", "Patient needs urgent eval", "FORWARDED");
        List<ConsultTrackingComment> comments = await grain.GetTrackingCommentsAsync();

        // Assert
        Assert.That(comments, Has.Count.EqualTo(1));
        Assert.That(comments[0].CommentText, Is.EqualTo("Patient needs urgent eval"));
        Assert.That(comments[0].ActionTaken, Is.EqualTo("FORWARDED"));
        Assert.That(comments[0].AuthorName, Is.EqualTo("Dr. Smith"));
    }

    // ─── 8. Multiple tracking comments ──────────────────────────────────────

    [Test]
    public async Task Consult_CanAddMultipleTrackingComments()
    {
        // Arrange
        IConsultGrain grain = NewConsult();
        await RequestStandardAsync(grain);

        // Act
        await grain.AddTrackingCommentAsync("AUTH-001", "Dr. Smith", "Forwarded to cardiology", "FORWARDED");
        await grain.AddTrackingCommentAsync("AUTH-002", "Dr. Jones", "Accepted for review", "ACCEPTED");
        await grain.AddTrackingCommentAsync("AUTH-003", "Dr. Lee", "Scheduled for next week", "SCHEDULED");
        List<ConsultTrackingComment> comments = await grain.GetTrackingCommentsAsync();

        // Assert
        Assert.That(comments, Has.Count.EqualTo(3));
        Assert.That(comments[0].CommentText, Is.EqualTo("Forwarded to cardiology"));
        Assert.That(comments[1].CommentText, Is.EqualTo("Accepted for review"));
        Assert.That(comments[2].CommentText, Is.EqualTo("Scheduled for next week"));
    }

    // ─── 9. Accept with details ─────────────────────────────────────────────

    [Test]
    public async Task Consult_CanAcceptWithDetails()
    {
        // Arrange
        IConsultGrain grain = NewConsult();
        await RequestStandardAsync(grain);

        // Act
        await grain.AcceptWithDetailsAsync("PROV-010", "Dr. Chen");
        ConsultState state = await grain.GetConsultAsync();

        // Assert
        Assert.That(state.Status, Is.EqualTo("ACTIVE"));
        Assert.That(state.AcceptedById, Is.EqualTo("PROV-010"));
        Assert.That(state.AcceptedByName, Is.EqualTo("Dr. Chen"));
        Assert.That(state.AcceptedDateTime, Is.Not.Null);
    }

    // ─── 10. Schedule with details ──────────────────────────────────────────

    [Test]
    public async Task Consult_CanScheduleWithDetails()
    {
        // Arrange
        IConsultGrain grain = NewConsult();
        await RequestStandardAsync(grain);
        await grain.AcceptAsync();
        DateTime scheduledAt = new DateTime(2026, 4, 15, 10, 30, 0, DateTimeKind.Utc);

        // Act
        await grain.ScheduleWithDetailsAsync(scheduledAt, "CLINIC-001", "Cardiology Clinic A");
        ConsultState state = await grain.GetConsultAsync();

        // Assert
        Assert.That(state.Status, Is.EqualTo("SCHEDULED"));
        Assert.That(state.ScheduledDateTime, Is.EqualTo(scheduledAt));
        Assert.That(state.ScheduledClinicId, Is.EqualTo("CLINIC-001"));
        Assert.That(state.ScheduledClinicName, Is.EqualTo("Cardiology Clinic A"));
    }

    // ─── 11. Set consult type ───────────────────────────────────────────────

    [Test]
    public async Task Consult_CanSetConsultType()
    {
        // Arrange
        IConsultGrain grain = NewConsult();
        await RequestStandardAsync(grain);

        // Act
        await grain.SetConsultTypeAsync("PROCEDURE");
        ConsultState state = await grain.GetConsultAsync();

        // Assert
        Assert.That(state.ConsultType, Is.EqualTo("PROCEDURE"));
    }

    // ─── 12. Set clinical history ───────────────────────────────────────────

    [Test]
    public async Task Consult_CanSetClinicalHistory()
    {
        // Arrange
        IConsultGrain grain = NewConsult();
        await RequestStandardAsync(grain);
        string history = "Patient has a 3-year history of progressive dyspnea on exertion with two episodes of syncope.";

        // Act
        await grain.SetClinicalHistoryAsync(history);
        ConsultState state = await grain.GetConsultAsync();

        // Assert
        Assert.That(state.ClinicalHistory, Is.EqualTo(history));
    }

    // ─── 13. Set follow-up recommendation ───────────────────────────────────

    [Test]
    public async Task Consult_CanSetFollowUpRecommendation()
    {
        // Arrange
        IConsultGrain grain = NewConsult();
        await RequestStandardAsync(grain);
        string recommendation = "Follow up in 6 weeks with repeat echocardiogram";

        // Act
        await grain.SetFollowUpRecommendationAsync(recommendation);
        ConsultState state = await grain.GetConsultAsync();

        // Assert
        Assert.That(state.FollowUpRecommendation, Is.EqualTo(recommendation));
    }

    // ─── 14. Set consulting provider ────────────────────────────────────────

    [Test]
    public async Task Consult_CanSetConsultingProvider()
    {
        // Arrange
        IConsultGrain grain = NewConsult();
        await RequestStandardAsync(grain);

        // Act
        await grain.SetConsultingProviderAsync("PROV-050", "Dr. Martinez");
        ConsultState state = await grain.GetConsultAsync();

        // Assert
        Assert.That(state.ConsultingProviderId, Is.EqualTo("PROV-050"));
        Assert.That(state.ConsultingProviderName, Is.EqualTo("Dr. Martinez"));
    }

    // ─── 15. Mark interfacility ─────────────────────────────────────────────

    [Test]
    public async Task Consult_CanMarkInterfacility()
    {
        // Arrange
        IConsultGrain grain = NewConsult();
        await RequestStandardAsync(grain);

        // Act
        await grain.MarkInterfacilityAsync("FAC-999", "VA Palo Alto HCS");
        ConsultState state = await grain.GetConsultAsync();

        // Assert
        Assert.That(state.IsInterfacility, Is.True);
        Assert.That(state.ExternalFacilityId, Is.EqualTo("FAC-999"));
        Assert.That(state.ExternalFacilityName, Is.EqualTo("VA Palo Alto HCS"));
    }

    // ─── 16. Full workflow ──────────────────────────────────────────────────

    [Test]
    public async Task Consult_FullWorkflow()
    {
        // Arrange
        IConsultGrain grain = NewConsult();
        await RequestStandardAsync(grain, toService: "GASTROENTEROLOGY", urgency: "URGENT",
            reason: "Persistent GI bleeding");

        // Act — accept with details
        await grain.AcceptWithDetailsAsync("PROV-020", "Dr. Garcia");
        ConsultState afterAccept = await grain.GetConsultAsync();
        Assert.That(afterAccept.Status, Is.EqualTo("ACTIVE"));
        Assert.That(afterAccept.AcceptedByName, Is.EqualTo("Dr. Garcia"));

        // Schedule with details
        DateTime scheduledAt = new DateTime(2026, 5, 1, 9, 0, 0, DateTimeKind.Utc);
        await grain.ScheduleWithDetailsAsync(scheduledAt, "CLINIC-GI", "GI Procedure Suite");
        ConsultState afterSchedule = await grain.GetConsultAsync();
        Assert.That(afterSchedule.Status, Is.EqualTo("SCHEDULED"));
        Assert.That(afterSchedule.ScheduledClinicName, Is.EqualTo("GI Procedure Suite"));

        // Set consult type
        await grain.SetConsultTypeAsync("PROCEDURE");

        // Set clinical history
        await grain.SetClinicalHistoryAsync("Two weeks of melena with Hgb drop from 14 to 9.2.");

        // Add tracking comment
        await grain.AddTrackingCommentAsync("PROV-020", "Dr. Garcia", "EGD scheduled, prep instructions given", "SCHEDULED");

        // Complete
        DateTime completedAt = DateTime.UtcNow;
        await grain.CompleteAsync(completedAt, "TIU-DOC-001");

        // Set follow-up recommendation
        await grain.SetFollowUpRecommendationAsync("Repeat EGD in 8 weeks; continue PPI therapy");

        // Assert final state
        ConsultState finalState = await grain.GetConsultAsync();
        Assert.That(finalState.Status, Is.EqualTo("COMPLETE"));
        Assert.That(finalState.ToService, Is.EqualTo("GASTROENTEROLOGY"));
        Assert.That(finalState.ConsultType, Is.EqualTo("PROCEDURE"));
        Assert.That(finalState.ClinicalHistory, Does.Contain("melena"));
        Assert.That(finalState.FollowUpRecommendation, Does.Contain("Repeat EGD"));
        Assert.That(finalState.CompletedDateTime, Is.Not.Null);
        Assert.That(finalState.ResultDocumentId, Is.EqualTo("TIU-DOC-001"));
        List<ConsultTrackingComment> comments = await grain.GetTrackingCommentsAsync();
        Assert.That(comments, Has.Count.EqualTo(1));
    }
}
