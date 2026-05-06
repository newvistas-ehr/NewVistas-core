// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NUnit.Framework;
using Orleans.TestingHost;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.UnitTests;

[TestFixture]
public class PatientPortalGrainTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    // ─── Patient Submission Tests ──────────────────────────────────────────────

    [Test]
    public async Task PatientSubmissionGrain_CanCreateAndRetrieve()
    {
        string submissionId = $"PATIENT-SUB:{Guid.NewGuid():N}";
        IPatientSubmissionGrain grain = _cluster.GrainFactory.GetGrain<IPatientSubmissionGrain>(submissionId);

        await grain.CreateSubmissionAsync(new PatientSubmissionState
        {
            SubmissionId = submissionId,
            PatientId = "PAT-001",
            PatientName = "Smith, John",
            Demographics = new PatientSubmittedDemographics
            {
                StreetAddress = "123 Main St",
                City = "Springfield",
                State = "IL",
                ZipCode = "62701",
                Email = "john.smith@example.com"
            },
            HealthConcerns = new List<PatientSubmittedHealthConcern>
            {
                new() { Description = "Persistent headache", Severity = "moderate", IsOngoing = true }
            },
            HealthGoals = new List<string> { "Reduce blood pressure", "Lose 10 pounds" }
        });

        PatientSubmissionState result = await grain.GetSubmissionAsync();

        Assert.That(result.PatientId, Is.EqualTo("PAT-001"));
        Assert.That(result.PatientName, Is.EqualTo("Smith, John"));
        Assert.That(result.Status, Is.EqualTo("submitted"));
        Assert.That(result.Demographics, Is.Not.Null);
        Assert.That(result.Demographics!.City, Is.EqualTo("Springfield"));
        Assert.That(result.HealthConcerns, Has.Count.EqualTo(1));
        Assert.That(result.HealthGoals, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task PatientSubmissionGrain_CanSubmitAllSections()
    {
        string submissionId = $"PATIENT-SUB:{Guid.NewGuid():N}";
        IPatientSubmissionGrain grain = _cluster.GrainFactory.GetGrain<IPatientSubmissionGrain>(submissionId);

        await grain.CreateSubmissionAsync(new PatientSubmissionState
        {
            SubmissionId = submissionId,
            PatientId = "PAT-002",
            Demographics = new PatientSubmittedDemographics { Email = "test@example.com" },
            HealthConcerns = new List<PatientSubmittedHealthConcern>
            {
                new() { Description = "Back pain", Severity = "severe", OnsetDate = DateTime.UtcNow.AddMonths(-3), IsOngoing = true }
            },
            Medications = new List<PatientSubmittedMedication>
            {
                new() { MedicationName = "Ibuprofen", Dosage = "400mg", Frequency = "As needed", Reason = "Pain relief" }
            },
            Allergies = new List<PatientSubmittedAllergy>
            {
                new() { Allergen = "Penicillin", ReactionDescription = "Hives", Severity = "moderate", AllergenType = "Drug" }
            },
            SocialHistory = new PatientSubmittedSocialHistory
            {
                SmokingStatus = "former", AlcoholUse = "occasional", ExerciseFrequency = "3x/week"
            },
            FamilyHistory = new List<PatientSubmittedFamilyHistory>
            {
                new() { Relationship = "Father", Condition = "Heart Disease", AgeAtOnset = "55" }
            },
            AdvanceDirective = new PatientSubmittedAdvanceDirective
            {
                HasLivingWill = true, HasHealthcarePowerOfAttorney = true, HealthcareProxyName = "Jane Smith"
            },
            HealthGoals = new List<string> { "Manage pain without opioids" },
            PatientNotes = "I'd like to discuss alternative pain management."
        });

        PatientSubmissionState result = await grain.GetSubmissionAsync();

        Assert.That(result.Demographics, Is.Not.Null);
        Assert.That(result.HealthConcerns, Has.Count.EqualTo(1));
        Assert.That(result.Medications, Has.Count.EqualTo(1));
        Assert.That(result.Allergies, Has.Count.EqualTo(1));
        Assert.That(result.SocialHistory, Is.Not.Null);
        Assert.That(result.SocialHistory!.SmokingStatus, Is.EqualTo("former"));
        Assert.That(result.FamilyHistory, Has.Count.EqualTo(1));
        Assert.That(result.AdvanceDirective, Is.Not.Null);
        Assert.That(result.AdvanceDirective!.HasLivingWill, Is.True);
        Assert.That(result.PatientNotes, Is.EqualTo("I'd like to discuss alternative pain management."));
    }

    [Test]
    public async Task PatientSubmissionGrain_CanMarkUnderReview()
    {
        string submissionId = $"PATIENT-SUB:{Guid.NewGuid():N}";
        IPatientSubmissionGrain grain = _cluster.GrainFactory.GetGrain<IPatientSubmissionGrain>(submissionId);

        await grain.CreateSubmissionAsync(new PatientSubmissionState
        {
            SubmissionId = submissionId,
            PatientId = "PAT-003",
            HealthConcerns = new List<PatientSubmittedHealthConcern>
            {
                new() { Description = "Chest pain", Severity = "severe" }
            }
        });

        await grain.MarkUnderReviewAsync("DR-001");

        PatientSubmissionState result = await grain.GetSubmissionAsync();
        Assert.That(result.Status, Is.EqualTo("under-review"));
        Assert.That(result.ReviewedBy, Is.EqualTo("DR-001"));
    }

    [Test]
    public async Task PatientSubmissionGrain_CanCompleteReviewAccepted()
    {
        string submissionId = $"PATIENT-SUB:{Guid.NewGuid():N}";
        IPatientSubmissionGrain grain = _cluster.GrainFactory.GetGrain<IPatientSubmissionGrain>(submissionId);

        await grain.CreateSubmissionAsync(new PatientSubmissionState
        {
            SubmissionId = submissionId,
            PatientId = "PAT-004",
            Demographics = new PatientSubmittedDemographics { Email = "new@example.com" },
            Allergies = new List<PatientSubmittedAllergy>
            {
                new() { Allergen = "Sulfa", AllergenType = "Drug" }
            }
        });

        await grain.CompleteReviewAsync(
            "accepted", "DR-002", "All sections verified.",
            new List<string> { "Demographics", "Allergies" },
            new List<string>());

        PatientSubmissionState result = await grain.GetSubmissionAsync();
        Assert.That(result.Status, Is.EqualTo("accepted"));
        Assert.That(result.ReviewedBy, Is.EqualTo("DR-002"));
        Assert.That(result.ReviewNotes, Is.EqualTo("All sections verified."));
        Assert.That(result.AcceptedSections, Has.Count.EqualTo(2));
        Assert.That(result.RejectedSections, Has.Count.EqualTo(0));
        Assert.That(result.ReviewedDate, Is.Not.Null);
    }

    [Test]
    public async Task PatientSubmissionGrain_CanCompleteReviewPartial()
    {
        string submissionId = $"PATIENT-SUB:{Guid.NewGuid():N}";
        IPatientSubmissionGrain grain = _cluster.GrainFactory.GetGrain<IPatientSubmissionGrain>(submissionId);

        await grain.CreateSubmissionAsync(new PatientSubmissionState
        {
            SubmissionId = submissionId,
            PatientId = "PAT-005",
            Demographics = new PatientSubmittedDemographics { PhoneCell = "555-1234" },
            Medications = new List<PatientSubmittedMedication>
            {
                new() { MedicationName = "Unknown herb", Dosage = "???" }
            }
        });

        await grain.CompleteReviewAsync(
            "partial", "DR-003", "Demographics accepted; medication unclear.",
            new List<string> { "Demographics" },
            new List<string> { "Medications" });

        PatientSubmissionState result = await grain.GetSubmissionAsync();
        Assert.That(result.Status, Is.EqualTo("partial"));
        Assert.That(result.AcceptedSections, Contains.Item("Demographics"));
        Assert.That(result.RejectedSections, Contains.Item("Medications"));
    }

    // ─── Submission Index Tests ────────────────────────────────────────────────

    [Test]
    public async Task PatientSubmissionIndexGrain_TracksSubmissions()
    {
        string patientId = $"PAT-IDX-{Guid.NewGuid():N}";
        IPatientSubmissionIndexGrain index = _cluster.GrainFactory.GetGrain<IPatientSubmissionIndexGrain>(
            $"PATIENT-SUB-IDX:{patientId}");

        await index.AddSubmissionAsync(new PatientSubmissionSummary
        {
            SubmissionId = "SUB-1", PatientId = patientId, Status = "submitted",
            SubmittedDate = DateTime.UtcNow.AddDays(-2), SectionCount = 3
        });
        await index.AddSubmissionAsync(new PatientSubmissionSummary
        {
            SubmissionId = "SUB-2", PatientId = patientId, Status = "submitted",
            SubmittedDate = DateTime.UtcNow, SectionCount = 1
        });

        List<PatientSubmissionSummary> all = await index.GetAllSubmissionsAsync();
        Assert.That(all, Has.Count.EqualTo(2));
        Assert.That(all[0].SubmissionId, Is.EqualTo("SUB-2")); // most recent first
    }

    [Test]
    public async Task PatientSubmissionIndexGrain_CanFilterByStatus()
    {
        string patientId = $"PAT-IDX-{Guid.NewGuid():N}";
        IPatientSubmissionIndexGrain index = _cluster.GrainFactory.GetGrain<IPatientSubmissionIndexGrain>(
            $"PATIENT-SUB-IDX:{patientId}");

        await index.AddSubmissionAsync(new PatientSubmissionSummary
        {
            SubmissionId = "SUB-A", PatientId = patientId, Status = "submitted", SubmittedDate = DateTime.UtcNow
        });
        await index.AddSubmissionAsync(new PatientSubmissionSummary
        {
            SubmissionId = "SUB-B", PatientId = patientId, Status = "accepted", SubmittedDate = DateTime.UtcNow
        });

        List<PatientSubmissionSummary> submitted = await index.GetSubmissionsByStatusAsync("submitted");
        Assert.That(submitted, Has.Count.EqualTo(1));
        Assert.That(submitted[0].SubmissionId, Is.EqualTo("SUB-A"));
    }

    [Test]
    public async Task PatientSubmissionIndexGrain_CanUpdateStatus()
    {
        string patientId = $"PAT-IDX-{Guid.NewGuid():N}";
        IPatientSubmissionIndexGrain index = _cluster.GrainFactory.GetGrain<IPatientSubmissionIndexGrain>(
            $"PATIENT-SUB-IDX:{patientId}");

        await index.AddSubmissionAsync(new PatientSubmissionSummary
        {
            SubmissionId = "SUB-UPD", PatientId = patientId, Status = "submitted", SubmittedDate = DateTime.UtcNow
        });

        await index.UpdateStatusAsync("SUB-UPD", "under-review");

        List<PatientSubmissionSummary> all = await index.GetAllSubmissionsAsync();
        Assert.That(all[0].Status, Is.EqualTo("under-review"));
    }

    // ─── Submission Queue Tests ────────────────────────────────────────────────

    [Test]
    public async Task PatientSubmissionQueueGrain_TracksPendingSubmissions()
    {
        string queueId = $"PATIENT-SUB-QUEUE-{Guid.NewGuid():N}";
        IPatientSubmissionQueueGrain queue = _cluster.GrainFactory.GetGrain<IPatientSubmissionQueueGrain>(queueId);

        await queue.AddSubmissionAsync(new PatientSubmissionSummary
        {
            SubmissionId = "Q-1", PatientId = "PAT-Q1", Status = "submitted", SubmittedDate = DateTime.UtcNow.AddHours(-1)
        });
        await queue.AddSubmissionAsync(new PatientSubmissionSummary
        {
            SubmissionId = "Q-2", PatientId = "PAT-Q2", Status = "under-review", SubmittedDate = DateTime.UtcNow
        });
        await queue.AddSubmissionAsync(new PatientSubmissionSummary
        {
            SubmissionId = "Q-3", PatientId = "PAT-Q3", Status = "accepted", SubmittedDate = DateTime.UtcNow
        });

        List<PatientSubmissionSummary> pending = await queue.GetPendingSubmissionsAsync();
        Assert.That(pending, Has.Count.EqualTo(2)); // submitted + under-review
        Assert.That(pending[0].SubmissionId, Is.EqualTo("Q-1")); // oldest first
    }

    [Test]
    public async Task PatientSubmissionQueueGrain_CanRemoveSubmission()
    {
        string queueId = $"PATIENT-SUB-QUEUE-{Guid.NewGuid():N}";
        IPatientSubmissionQueueGrain queue = _cluster.GrainFactory.GetGrain<IPatientSubmissionQueueGrain>(queueId);

        await queue.AddSubmissionAsync(new PatientSubmissionSummary
        {
            SubmissionId = "Q-DEL", PatientId = "PAT-QD", Status = "submitted", SubmittedDate = DateTime.UtcNow
        });

        await queue.RemoveSubmissionAsync("Q-DEL");

        List<PatientSubmissionSummary> all = await queue.GetAllSubmissionsAsync();
        Assert.That(all, Has.Count.EqualTo(0));
    }

    // ─── Secure Message Thread Tests ───────────────────────────────────────────

    [Test]
    public async Task SecureMessageThreadGrain_CanCreateThread()
    {
        string threadId = $"SECURE-MSG-THREAD:{Guid.NewGuid():N}";
        ISecureMessageThreadGrain grain = _cluster.GrainFactory.GetGrain<ISecureMessageThreadGrain>(threadId);

        await grain.CreateThreadAsync("PAT-MSG-001", "Smith, John", "Medication question",
            "medication", "DR-001", "Dr. Jones");

        SecureMessageThreadState thread = await grain.GetThreadAsync();

        Assert.That(thread.PatientId, Is.EqualTo("PAT-MSG-001"));
        Assert.That(thread.PatientName, Is.EqualTo("Smith, John"));
        Assert.That(thread.Subject, Is.EqualTo("Medication question"));
        Assert.That(thread.Category, Is.EqualTo("medication"));
        Assert.That(thread.Status, Is.EqualTo("open"));
        Assert.That(thread.AssignedProviderId, Is.EqualTo("DR-001"));
        Assert.That(thread.Messages, Has.Count.EqualTo(0));
    }

    [Test]
    public async Task SecureMessageThreadGrain_CanAddMessages()
    {
        string threadId = $"SECURE-MSG-THREAD:{Guid.NewGuid():N}";
        ISecureMessageThreadGrain grain = _cluster.GrainFactory.GetGrain<ISecureMessageThreadGrain>(threadId);

        await grain.CreateThreadAsync("PAT-MSG-002", "Doe, Jane", "Lab results",
            "lab-results", null, null);

        await grain.AddMessageAsync("patient", "PAT-MSG-002", "Jane Doe",
            "When will my lab results be available?");

        await grain.AddMessageAsync("provider", "DR-002", "Dr. Smith",
            "Your results should be available by Friday.");

        SecureMessageThreadState thread = await grain.GetThreadAsync();

        Assert.That(thread.Messages, Has.Count.EqualTo(2));
        Assert.That(thread.Messages[0].SenderType, Is.EqualTo("patient"));
        Assert.That(thread.Messages[0].Body, Does.Contain("lab results"));
        Assert.That(thread.Messages[1].SenderType, Is.EqualTo("provider"));
        Assert.That(thread.HasUnreadPatient, Is.True);   // provider sent last
        Assert.That(thread.HasUnreadProvider, Is.True);  // patient's first message still unread
    }

    [Test]
    public async Task SecureMessageThreadGrain_UnreadTracking()
    {
        string threadId = $"SECURE-MSG-THREAD:{Guid.NewGuid():N}";
        ISecureMessageThreadGrain grain = _cluster.GrainFactory.GetGrain<ISecureMessageThreadGrain>(threadId);

        await grain.CreateThreadAsync("PAT-MSG-003", null, "Appointment",
            "appointment", null, null);

        // Patient sends message → provider has unread
        await grain.AddMessageAsync("patient", "PAT-MSG-003", null, "Can I reschedule?");
        SecureMessageThreadState thread = await grain.GetThreadAsync();
        Assert.That(thread.HasUnreadProvider, Is.True);
        Assert.That(thread.HasUnreadPatient, Is.False);

        // Provider marks as read
        await grain.MarkReadAsync("provider");
        thread = await grain.GetThreadAsync();
        Assert.That(thread.HasUnreadProvider, Is.False);
        Assert.That(thread.Messages[0].IsRead, Is.True);

        // Provider replies → patient has unread
        await grain.AddMessageAsync("provider", "DR-003", null, "Yes, please call to reschedule.");
        thread = await grain.GetThreadAsync();
        Assert.That(thread.HasUnreadPatient, Is.True);
        Assert.That(thread.HasUnreadProvider, Is.False);

        // Patient marks as read
        await grain.MarkReadAsync("patient");
        thread = await grain.GetThreadAsync();
        Assert.That(thread.HasUnreadPatient, Is.False);
    }

    [Test]
    public async Task SecureMessageThreadGrain_CanCloseAndReopen()
    {
        string threadId = $"SECURE-MSG-THREAD:{Guid.NewGuid():N}";
        ISecureMessageThreadGrain grain = _cluster.GrainFactory.GetGrain<ISecureMessageThreadGrain>(threadId);

        await grain.CreateThreadAsync("PAT-MSG-004", null, "Billing", "billing", null, null);

        await grain.CloseThreadAsync();
        SecureMessageThreadState thread = await grain.GetThreadAsync();
        Assert.That(thread.Status, Is.EqualTo("closed"));

        await grain.ReopenThreadAsync();
        thread = await grain.GetThreadAsync();
        Assert.That(thread.Status, Is.EqualTo("open"));
    }

    // ─── Secure Message Index Tests ────────────────────────────────────────────

    [Test]
    public async Task SecureMessageIndexGrain_TracksThreads()
    {
        string indexId = $"SECURE-MSG-IDX:{Guid.NewGuid():N}";
        ISecureMessageIndexGrain index = _cluster.GrainFactory.GetGrain<ISecureMessageIndexGrain>(indexId);

        await index.AddThreadAsync(new SecureMessageThreadSummary
        {
            ThreadId = "T-1", PatientId = "P1", Subject = "Question 1", Category = "general",
            Status = "open", LastMessageDate = DateTime.UtcNow.AddHours(-1), MessageCount = 2,
            HasUnreadProvider = true
        });
        await index.AddThreadAsync(new SecureMessageThreadSummary
        {
            ThreadId = "T-2", PatientId = "P1", Subject = "Question 2", Category = "medication",
            Status = "open", LastMessageDate = DateTime.UtcNow, MessageCount = 1,
            HasUnreadPatient = true
        });

        List<SecureMessageThreadSummary> all = await index.GetAllThreadsAsync();
        Assert.That(all, Has.Count.EqualTo(2));
        Assert.That(all[0].ThreadId, Is.EqualTo("T-2")); // most recent first

        List<SecureMessageThreadSummary> open = await index.GetOpenThreadsAsync();
        Assert.That(open, Has.Count.EqualTo(2));

        List<SecureMessageThreadSummary> unreadByProvider = await index.GetUnreadByProviderAsync();
        Assert.That(unreadByProvider, Has.Count.EqualTo(1));
        Assert.That(unreadByProvider[0].ThreadId, Is.EqualTo("T-1"));

        List<SecureMessageThreadSummary> unreadByPatient = await index.GetUnreadByPatientAsync();
        Assert.That(unreadByPatient, Has.Count.EqualTo(1));
        Assert.That(unreadByPatient[0].ThreadId, Is.EqualTo("T-2"));
    }

    // ─── Secure Message Queue Tests ────────────────────────────────────────────

    [Test]
    public async Task SecureMessageQueueGrain_TracksProviderQueue()
    {
        string queueId = $"SECURE-MSG-QUEUE-{Guid.NewGuid():N}";
        ISecureMessageQueueGrain queue = _cluster.GrainFactory.GetGrain<ISecureMessageQueueGrain>(queueId);

        await queue.AddThreadAsync(new SecureMessageThreadSummary
        {
            ThreadId = "TQ-1", PatientId = "PQ1", Subject = "Urgent", Status = "open",
            LastMessageDate = DateTime.UtcNow, HasUnreadProvider = true
        });
        await queue.AddThreadAsync(new SecureMessageThreadSummary
        {
            ThreadId = "TQ-2", PatientId = "PQ2", Subject = "Follow-up", Status = "open",
            LastMessageDate = DateTime.UtcNow, HasUnreadProvider = false
        });

        List<SecureMessageThreadSummary> unread = await queue.GetUnreadThreadsAsync();
        Assert.That(unread, Has.Count.EqualTo(1));
        Assert.That(unread[0].ThreadId, Is.EqualTo("TQ-1"));

        List<SecureMessageThreadSummary> active = await queue.GetAllActiveThreadsAsync();
        Assert.That(active, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task SecureMessageQueueGrain_CanRemoveThread()
    {
        string queueId = $"SECURE-MSG-QUEUE-{Guid.NewGuid():N}";
        ISecureMessageQueueGrain queue = _cluster.GrainFactory.GetGrain<ISecureMessageQueueGrain>(queueId);

        await queue.AddThreadAsync(new SecureMessageThreadSummary
        {
            ThreadId = "TQ-DEL", PatientId = "PQD", Subject = "Remove me", Status = "open",
            LastMessageDate = DateTime.UtcNow
        });

        await queue.RemoveThreadAsync("TQ-DEL");

        List<SecureMessageThreadSummary> active = await queue.GetAllActiveThreadsAsync();
        Assert.That(active, Has.Count.EqualTo(0));
    }
}
