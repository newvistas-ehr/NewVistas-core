// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NUnit.Framework;
using Orleans.TestingHost;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.FunctionalTests;

[TestFixture]
public class PatientPortalWorkflowTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    // ─── §170.315(e)(3) Patient Health Information Capture ─────────────────────

    [Test]
    public async Task Submission_FullLifecycle_SubmitReviewAccept()
    {
        // Patient submits health information
        string submissionId = $"PATIENT-SUB:{Guid.NewGuid():N}";
        string patientId = $"PAT-FUNC-{Guid.NewGuid():N}";

        IPatientSubmissionGrain subGrain = _cluster.GrainFactory.GetGrain<IPatientSubmissionGrain>(submissionId);
        await subGrain.CreateSubmissionAsync(new PatientSubmissionState
        {
            SubmissionId = submissionId,
            PatientId = patientId,
            PatientName = "Jones, Mary",
            Demographics = new PatientSubmittedDemographics
            {
                StreetAddress = "456 Oak Ave",
                City = "Chicago",
                State = "IL",
                ZipCode = "60601",
                PhoneCell = "312-555-1234",
                Email = "mary.jones@example.com",
                PreferredLanguage = "English"
            },
            Allergies = new List<PatientSubmittedAllergy>
            {
                new() { Allergen = "Aspirin", ReactionDescription = "Stomach upset", Severity = "mild", AllergenType = "Drug" },
                new() { Allergen = "Latex", ReactionDescription = "Skin rash", Severity = "moderate", AllergenType = "Environmental" }
            },
            Medications = new List<PatientSubmittedMedication>
            {
                new() { MedicationName = "Lisinopril", Dosage = "10mg", Frequency = "Daily", Reason = "Blood pressure" }
            },
            HealthGoals = new List<string> { "Lower blood pressure to 120/80" }
        });

        // Add to patient index and queue
        var summary = new PatientSubmissionSummary
        {
            SubmissionId = submissionId,
            PatientId = patientId,
            PatientName = "Jones, Mary",
            SubmittedDate = DateTime.UtcNow,
            Status = "submitted",
            SectionCount = 4
        };

        IPatientSubmissionIndexGrain patientIndex = _cluster.GrainFactory.GetGrain<IPatientSubmissionIndexGrain>(
            $"PATIENT-SUB-IDX:{patientId}");
        await patientIndex.AddSubmissionAsync(summary);

        IPatientSubmissionQueueGrain queue = _cluster.GrainFactory.GetGrain<IPatientSubmissionQueueGrain>("PATIENT-SUB-QUEUE");
        await queue.AddSubmissionAsync(summary);

        // Verify in queue
        List<PatientSubmissionSummary> pending = await queue.GetPendingSubmissionsAsync();
        Assert.That(pending.Any(s => s.SubmissionId == submissionId), Is.True);

        // Clinician marks under review
        await subGrain.MarkUnderReviewAsync("DR-FUNC-001");
        await queue.UpdateStatusAsync(submissionId, "under-review");

        PatientSubmissionState sub = await subGrain.GetSubmissionAsync();
        Assert.That(sub.Status, Is.EqualTo("under-review"));

        // Clinician completes review — accepted
        await subGrain.CompleteReviewAsync(
            "accepted", "DR-FUNC-001", "All sections verified and accepted into chart.",
            new List<string> { "Demographics", "Allergies", "Medications", "HealthGoals" },
            new List<string>());
        await queue.UpdateStatusAsync(submissionId, "accepted");
        await patientIndex.UpdateStatusAsync(submissionId, "accepted");

        // Verify final state
        sub = await subGrain.GetSubmissionAsync();
        Assert.That(sub.Status, Is.EqualTo("accepted"));
        Assert.That(sub.ReviewedBy, Is.EqualTo("DR-FUNC-001"));
        Assert.That(sub.AcceptedSections, Has.Count.EqualTo(4));
        Assert.That(sub.ReviewedDate, Is.Not.Null);

        // Verify index updated
        List<PatientSubmissionSummary> patientSubs = await patientIndex.GetAllSubmissionsAsync();
        Assert.That(patientSubs[0].Status, Is.EqualTo("accepted"));
    }

    [Test]
    public async Task Submission_PartialReview_AcceptSomeRejectOthers()
    {
        string submissionId = $"PATIENT-SUB:{Guid.NewGuid():N}";
        string patientId = $"PAT-PARTIAL-{Guid.NewGuid():N}";

        IPatientSubmissionGrain subGrain = _cluster.GrainFactory.GetGrain<IPatientSubmissionGrain>(submissionId);
        await subGrain.CreateSubmissionAsync(new PatientSubmissionState
        {
            SubmissionId = submissionId,
            PatientId = patientId,
            Demographics = new PatientSubmittedDemographics { Email = "partial@example.com" },
            Medications = new List<PatientSubmittedMedication>
            {
                new() { MedicationName = "Unknown supplement", Dosage = "unknown" }
            },
            SocialHistory = new PatientSubmittedSocialHistory
            {
                SmokingStatus = "current", SmokingDetails = "1 pack/day"
            }
        });

        // Clinician partially accepts
        await subGrain.CompleteReviewAsync(
            "partial", "DR-PARTIAL",
            "Demographics and social history accepted. Medication unclear — needs follow-up.",
            new List<string> { "Demographics", "SocialHistory" },
            new List<string> { "Medications" });

        PatientSubmissionState result = await subGrain.GetSubmissionAsync();
        Assert.That(result.Status, Is.EqualTo("partial"));
        Assert.That(result.AcceptedSections, Contains.Item("Demographics"));
        Assert.That(result.AcceptedSections, Contains.Item("SocialHistory"));
        Assert.That(result.RejectedSections, Contains.Item("Medications"));
    }

    [Test]
    public async Task Submission_MultipleSubmissionsPerPatient()
    {
        string patientId = $"PAT-MULTI-{Guid.NewGuid():N}";
        IPatientSubmissionIndexGrain index = _cluster.GrainFactory.GetGrain<IPatientSubmissionIndexGrain>(
            $"PATIENT-SUB-IDX:{patientId}");

        // Submit 3 times
        for (int i = 0; i < 3; i++)
        {
            string subId = $"PATIENT-SUB:MULTI-{i}";
            IPatientSubmissionGrain grain = _cluster.GrainFactory.GetGrain<IPatientSubmissionGrain>(subId);
            await grain.CreateSubmissionAsync(new PatientSubmissionState
            {
                SubmissionId = subId,
                PatientId = patientId,
                HealthGoals = new List<string> { $"Goal {i}" }
            });

            await index.AddSubmissionAsync(new PatientSubmissionSummary
            {
                SubmissionId = subId,
                PatientId = patientId,
                Status = "submitted",
                SubmittedDate = DateTime.UtcNow.AddHours(i),
                SectionCount = 1
            });
        }

        List<PatientSubmissionSummary> all = await index.GetAllSubmissionsAsync();
        Assert.That(all, Has.Count.EqualTo(3));

        // Accept first, reject second
        await index.UpdateStatusAsync("PATIENT-SUB:MULTI-0", "accepted");
        await index.UpdateStatusAsync("PATIENT-SUB:MULTI-1", "rejected");

        List<PatientSubmissionSummary> submitted = await index.GetSubmissionsByStatusAsync("submitted");
        Assert.That(submitted, Has.Count.EqualTo(1));
        Assert.That(submitted[0].SubmissionId, Is.EqualTo("PATIENT-SUB:MULTI-2"));
    }

    // ─── §170.315(e)(2) Secure Messaging ──────────────────────────────────────

    [Test]
    public async Task SecureMessaging_FullConversation()
    {
        string threadId = $"SECURE-MSG-THREAD:{Guid.NewGuid():N}";
        string patientId = $"PAT-CHAT-{Guid.NewGuid():N}";

        // Create thread
        ISecureMessageThreadGrain thread = _cluster.GrainFactory.GetGrain<ISecureMessageThreadGrain>(threadId);
        await thread.CreateThreadAsync(patientId, "Wilson, Bob", "Medication refill request",
            "medication", "DR-CHAT-001", "Dr. Adams");

        // Add to indexes
        var summary = new SecureMessageThreadSummary
        {
            ThreadId = threadId,
            PatientId = patientId,
            PatientName = "Wilson, Bob",
            Subject = "Medication refill request",
            Category = "medication",
            Status = "open",
            LastMessageDate = DateTime.UtcNow,
            MessageCount = 0
        };

        ISecureMessageIndexGrain patientIndex = _cluster.GrainFactory.GetGrain<ISecureMessageIndexGrain>(
            $"SECURE-MSG-IDX:{patientId}");
        await patientIndex.AddThreadAsync(summary);

        ISecureMessageQueueGrain providerQueue = _cluster.GrainFactory.GetGrain<ISecureMessageQueueGrain>("SECURE-MSG-QUEUE");
        await providerQueue.AddThreadAsync(summary);

        // Patient sends initial message
        await thread.AddMessageAsync("patient", patientId, "Bob Wilson",
            "I need a refill on my Lisinopril 10mg. I have about 5 days left.");

        SecureMessageThreadState state = await thread.GetThreadAsync();
        Assert.That(state.Messages, Has.Count.EqualTo(1));
        Assert.That(state.HasUnreadProvider, Is.True);

        // Update provider queue
        summary.HasUnreadProvider = true;
        summary.MessageCount = 1;
        await providerQueue.UpdateThreadAsync(summary);

        // Provider reads and replies
        await thread.MarkReadAsync("provider");
        await thread.AddMessageAsync("provider", "DR-CHAT-001", "Dr. Adams",
            "I've submitted a refill for Lisinopril 10mg, 30-day supply. It should be ready at your pharmacy tomorrow.");

        state = await thread.GetThreadAsync();
        Assert.That(state.Messages, Has.Count.EqualTo(2));
        Assert.That(state.HasUnreadPatient, Is.True);
        Assert.That(state.HasUnreadProvider, Is.False);

        // Patient reads and thanks
        await thread.MarkReadAsync("patient");
        await thread.AddMessageAsync("patient", patientId, "Bob Wilson",
            "Thank you, Dr. Adams!");

        state = await thread.GetThreadAsync();
        Assert.That(state.Messages, Has.Count.EqualTo(3));

        // Provider closes thread
        await thread.CloseThreadAsync();
        state = await thread.GetThreadAsync();
        Assert.That(state.Status, Is.EqualTo("closed"));

        // Verify all messages preserved
        Assert.That(state.Messages[0].Body, Does.Contain("refill on my Lisinopril"));
        Assert.That(state.Messages[1].Body, Does.Contain("ready at your pharmacy"));
        Assert.That(state.Messages[2].Body, Does.Contain("Thank you"));
    }

    [Test]
    public async Task SecureMessaging_ProviderQueueFiltering()
    {
        ISecureMessageQueueGrain queue = _cluster.GrainFactory.GetGrain<ISecureMessageQueueGrain>(
            $"SECURE-MSG-QUEUE-FILTER-{Guid.NewGuid():N}");

        // Add threads with different states
        await queue.AddThreadAsync(new SecureMessageThreadSummary
        {
            ThreadId = "FILTER-1", PatientId = "P1", Subject = "Unread", Status = "open",
            LastMessageDate = DateTime.UtcNow, HasUnreadProvider = true, MessageCount = 1
        });
        await queue.AddThreadAsync(new SecureMessageThreadSummary
        {
            ThreadId = "FILTER-2", PatientId = "P2", Subject = "Read", Status = "open",
            LastMessageDate = DateTime.UtcNow, HasUnreadProvider = false, MessageCount = 3
        });
        await queue.AddThreadAsync(new SecureMessageThreadSummary
        {
            ThreadId = "FILTER-3", PatientId = "P3", Subject = "Also unread", Status = "open",
            LastMessageDate = DateTime.UtcNow.AddMinutes(-5), HasUnreadProvider = true, MessageCount = 2
        });

        // Unread queue shows only unread threads
        List<SecureMessageThreadSummary> unread = await queue.GetUnreadThreadsAsync();
        Assert.That(unread, Has.Count.EqualTo(2));

        // Active queue shows all open threads
        List<SecureMessageThreadSummary> active = await queue.GetAllActiveThreadsAsync();
        Assert.That(active, Has.Count.EqualTo(3));

        // Remove a thread
        await queue.RemoveThreadAsync("FILTER-2");
        active = await queue.GetAllActiveThreadsAsync();
        Assert.That(active, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task SecureMessaging_PatientMultipleThreads()
    {
        string patientId = $"PAT-MULTI-THREAD-{Guid.NewGuid():N}";
        ISecureMessageIndexGrain index = _cluster.GrainFactory.GetGrain<ISecureMessageIndexGrain>(
            $"SECURE-MSG-IDX:{patientId}");

        // Patient has threads in different categories
        await index.AddThreadAsync(new SecureMessageThreadSummary
        {
            ThreadId = "MT-1", PatientId = patientId, Subject = "Medication question",
            Category = "medication", Status = "open",
            LastMessageDate = DateTime.UtcNow.AddHours(-2), MessageCount = 2,
            HasUnreadPatient = true
        });
        await index.AddThreadAsync(new SecureMessageThreadSummary
        {
            ThreadId = "MT-2", PatientId = patientId, Subject = "Lab follow-up",
            Category = "lab-results", Status = "open",
            LastMessageDate = DateTime.UtcNow, MessageCount = 1,
            HasUnreadProvider = true
        });
        await index.AddThreadAsync(new SecureMessageThreadSummary
        {
            ThreadId = "MT-3", PatientId = patientId, Subject = "Old billing question",
            Category = "billing", Status = "closed",
            LastMessageDate = DateTime.UtcNow.AddDays(-30), MessageCount = 5
        });

        // All threads
        List<SecureMessageThreadSummary> all = await index.GetAllThreadsAsync();
        Assert.That(all, Has.Count.EqualTo(3));

        // Open threads
        List<SecureMessageThreadSummary> open = await index.GetOpenThreadsAsync();
        Assert.That(open, Has.Count.EqualTo(2));

        // Unread by patient
        List<SecureMessageThreadSummary> unreadByPatient = await index.GetUnreadByPatientAsync();
        Assert.That(unreadByPatient, Has.Count.EqualTo(1));
        Assert.That(unreadByPatient[0].ThreadId, Is.EqualTo("MT-1"));

        // Unread by provider
        List<SecureMessageThreadSummary> unreadByProvider = await index.GetUnreadByProviderAsync();
        Assert.That(unreadByProvider, Has.Count.EqualTo(1));
        Assert.That(unreadByProvider[0].ThreadId, Is.EqualTo("MT-2"));
    }
}
