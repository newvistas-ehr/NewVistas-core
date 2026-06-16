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
/// Functional tests for Collection Letter Generation — PRCA dunning letters,
/// AR statements, and collection correspondence (RCCLLT*.m, RCCL*.m).
/// </summary>
[TestFixture]
public class CollectionLetterWorkflowTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    private ICollectionLetterGrain NewLetter()
        => _cluster.GrainFactory.GetGrain<ICollectionLetterGrain>($"AR-LETTER:{Guid.NewGuid()}");

    private ICollectionLetterIndexGrain GetIndex(string patientId)
        => _cluster.GrainFactory.GetGrain<ICollectionLetterIndexGrain>($"AR-LETTER-IDX:{patientId}");

    private IPatientWorkflowGrain GetWorkflow(string patientId)
        => _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

    // ─── Letter Generation ───────────────────────────────────────────────────

    [Test]
    public async Task GenerateLetter_SetsGeneratedStatus()
    {
        ICollectionLetterGrain letter = NewLetter();
        await letter.GenerateAsync(
            "PAT-CL-001", "TEST,PATIENT", "123 Main St",
            CollectionLetterType.FirstNotice,
            new List<CollectionLetterLineItem>
            {
                new() { ARAccountId = "AR-001", Description = "Outpatient Copay", OriginalAmount = 50m, CurrentBalance = 50m, DateEstablished = DateTime.UtcNow.AddDays(-30), DaysPastDue = 30 }
            },
            50m, null, 30, 1, "USR-001", "Test User", null);

        CollectionLetterState state = await letter.GetAsync();
        Assert.That(state.Status, Is.EqualTo(CollectionLetterStatus.Generated));
    }

    [Test]
    public async Task GenerateLetter_StoresPatientAndAmount()
    {
        ICollectionLetterGrain letter = NewLetter();
        await letter.GenerateAsync(
            "PAT-CL-002", "SMITH,JOHN", "456 Oak Ave",
            CollectionLetterType.SecondNotice,
            new List<CollectionLetterLineItem>
            {
                new() { ARAccountId = "AR-002", Description = "Pharmacy Copay", OriginalAmount = 24m, CurrentBalance = 24m, DateEstablished = DateTime.UtcNow.AddDays(-60), DaysPastDue = 60 },
                new() { ARAccountId = "AR-003", Description = "Outpatient Copay", OriginalAmount = 50m, CurrentBalance = 35m, DateEstablished = DateTime.UtcNow.AddDays(-45), DaysPastDue = 45 },
            },
            59m, null, 30, 2, "USR-001", "Test User", "Second notice");

        CollectionLetterState state = await letter.GetAsync();
        Assert.That(state.PatientName, Is.EqualTo("SMITH,JOHN"));
        Assert.That(state.TotalAmountDue, Is.EqualTo(59m));
        Assert.That(state.LineItems, Has.Count.EqualTo(2));
        Assert.That(state.DunningSequence, Is.EqualTo(2));
    }

    [Test]
    public async Task GenerateLetter_CreatesLetterBody()
    {
        ICollectionLetterGrain letter = NewLetter();
        await letter.GenerateAsync(
            "PAT-CL-003", "DOE,JANE", null,
            CollectionLetterType.ThirdNotice,
            new List<CollectionLetterLineItem>
            {
                new() { ARAccountId = "AR-004", Description = "Copay", OriginalAmount = 100m, CurrentBalance = 100m, DateEstablished = DateTime.UtcNow.AddDays(-90), DaysPastDue = 90 }
            },
            100m, null, 30, 3, null, null, null);

        CollectionLetterState state = await letter.GetAsync();
        Assert.That(state.LetterBody, Does.Contain("FINAL NOTICE"));
        Assert.That(state.LetterBody, Does.Contain("DOE,JANE"));
        Assert.That(state.LetterBody, Does.Contain("$100.00"));
    }

    // ─── Status Transitions ──────────────────────────────────────────────────

    [Test]
    public async Task MarkPrinted_SetsPrintedStatus()
    {
        ICollectionLetterGrain letter = NewLetter();
        await letter.GenerateAsync("PAT-CL-010", "TEST", null, CollectionLetterType.FirstNotice,
            new List<CollectionLetterLineItem>(), 50m, null, 30, 1, null, null, null);

        await letter.MarkPrintedAsync();

        CollectionLetterState state = await letter.GetAsync();
        Assert.That(state.Status, Is.EqualTo(CollectionLetterStatus.Printed));
        Assert.That(state.PrintedDate, Is.Not.Null);
    }

    [Test]
    public async Task MarkMailed_SetsMailedStatus()
    {
        ICollectionLetterGrain letter = NewLetter();
        await letter.GenerateAsync("PAT-CL-011", "TEST", null, CollectionLetterType.Statement,
            new List<CollectionLetterLineItem>(), 75m, null, 30, 1, null, null, null);
        await letter.MarkPrintedAsync();

        await letter.MarkMailedAsync();

        CollectionLetterState state = await letter.GetAsync();
        Assert.That(state.Status, Is.EqualTo(CollectionLetterStatus.Mailed));
        Assert.That(state.MailedDate, Is.Not.Null);
    }

    [Test]
    public async Task MarkReturned_SetsReturnedStatus()
    {
        ICollectionLetterGrain letter = NewLetter();
        await letter.GenerateAsync("PAT-CL-012", "TEST", null, CollectionLetterType.FirstNotice,
            new List<CollectionLetterLineItem>(), 25m, null, 30, 1, null, null, null);
        await letter.MarkMailedAsync();

        await letter.MarkReturnedAsync();

        CollectionLetterState state = await letter.GetAsync();
        Assert.That(state.Status, Is.EqualTo(CollectionLetterStatus.Returned));
        Assert.That(state.ReturnedDate, Is.Not.Null);
    }

    [Test]
    public async Task Cancel_SetsCancelledStatus()
    {
        ICollectionLetterGrain letter = NewLetter();
        await letter.GenerateAsync("PAT-CL-013", "TEST", null, CollectionLetterType.IntentToCollect,
            new List<CollectionLetterLineItem>(), 200m, null, 30, 4, null, null, null);

        await letter.CancelAsync("Patient paid in full");

        CollectionLetterState state = await letter.GetAsync();
        Assert.That(state.Status, Is.EqualTo(CollectionLetterStatus.Cancelled));
        Assert.That(state.Notes, Does.Contain("Patient paid in full"));
    }

    // ─── Letter Index ────────────────────────────────────────────────────────

    [Test]
    public async Task LetterIndex_AddOrUpdate_AppearsInGetAll()
    {
        string patientId = $"PAT-CL-IDX-{Guid.NewGuid()}";
        ICollectionLetterIndexGrain index = GetIndex(patientId);

        string letterId = Guid.NewGuid().ToString();
        await index.AddOrUpdateAsync(new CollectionLetterIndexEntry
        {
            LetterId = letterId, LetterType = CollectionLetterType.FirstNotice,
            Status = CollectionLetterStatus.Generated, TotalAmountDue = 50m,
            GeneratedDate = DateTime.UtcNow, DunningSequence = 1,
        });

        List<CollectionLetterIndexEntry> all = await index.GetAllAsync();
        Assert.That(all.Any(e => e.LetterId == letterId), Is.True);
    }

    [Test]
    public async Task LetterIndex_GetNextDunningSequence_Increments()
    {
        string patientId = $"PAT-CL-SEQ-{Guid.NewGuid()}";
        ICollectionLetterIndexGrain index = GetIndex(patientId);

        int first = await index.GetNextDunningSequenceAsync();
        Assert.That(first, Is.EqualTo(1));

        await index.AddOrUpdateAsync(new CollectionLetterIndexEntry
        {
            LetterId = "L1", LetterType = CollectionLetterType.FirstNotice,
            Status = CollectionLetterStatus.Generated, TotalAmountDue = 50m,
            GeneratedDate = DateTime.UtcNow, DunningSequence = 1,
        });

        int second = await index.GetNextDunningSequenceAsync();
        Assert.That(second, Is.EqualTo(2));
    }

    [Test]
    public async Task LetterIndex_GetByStatus_FiltersCorrectly()
    {
        string patientId = $"PAT-CL-STAT-{Guid.NewGuid()}";
        ICollectionLetterIndexGrain index = GetIndex(patientId);

        await index.AddOrUpdateAsync(new CollectionLetterIndexEntry
        {
            LetterId = "L-GEN", LetterType = CollectionLetterType.FirstNotice,
            Status = CollectionLetterStatus.Generated, TotalAmountDue = 50m,
            GeneratedDate = DateTime.UtcNow, DunningSequence = 1,
        });
        await index.AddOrUpdateAsync(new CollectionLetterIndexEntry
        {
            LetterId = "L-MAIL", LetterType = CollectionLetterType.SecondNotice,
            Status = CollectionLetterStatus.Mailed, TotalAmountDue = 75m,
            GeneratedDate = DateTime.UtcNow, DunningSequence = 2,
        });

        List<CollectionLetterIndexEntry> mailed = await index.GetByStatusAsync(CollectionLetterStatus.Mailed);
        Assert.That(mailed, Has.Count.EqualTo(1));
        Assert.That(mailed[0].LetterId, Is.EqualTo("L-MAIL"));
    }

    // ─── Letter Type Templates ───────────────────────────────────────────────

    [Test]
    public async Task IntentToCollect_ContainsCollectionWarning()
    {
        ICollectionLetterGrain letter = NewLetter();
        await letter.GenerateAsync("PAT-CL-ITC", "VETERAN,TEST", null,
            CollectionLetterType.IntentToCollect,
            new List<CollectionLetterLineItem>
            {
                new() { ARAccountId = "AR-X", Description = "Copay", OriginalAmount = 500m, CurrentBalance = 500m, DateEstablished = DateTime.UtcNow.AddDays(-120), DaysPastDue = 120 }
            },
            500m, null, 30, 4, null, null, null);

        CollectionLetterState state = await letter.GetAsync();
        Assert.That(state.LetterBody, Does.Contain("INTENT TO COLLECT"));
        Assert.That(state.LetterBody, Does.Contain("collection agency"));
    }

    [Test]
    public async Task TopPreReferral_ContainsTreasuryWarning()
    {
        ICollectionLetterGrain letter = NewLetter();
        await letter.GenerateAsync("PAT-CL-TOP", "VETERAN,TOP", null,
            CollectionLetterType.TopPreReferral,
            new List<CollectionLetterLineItem>
            {
                new() { ARAccountId = "AR-TOP", Description = "Copay", OriginalAmount = 1000m, CurrentBalance = 1000m, DateEstablished = DateTime.UtcNow.AddDays(-180), DaysPastDue = 180 }
            },
            1000m, null, 60, 5, null, null, null);

        CollectionLetterState state = await letter.GetAsync();
        Assert.That(state.LetterBody, Does.Contain("TREASURY OFFSET PROGRAM"));
        Assert.That(state.LetterBody, Does.Contain("tax refunds"));
    }

    // ─── Workflow Integration ────────────────────────────────────────────────

    [Test]
    public async Task Workflow_GenerateCollectionLetter_ReturnsLetterId()
    {
        string patientId = $"PAT-CL-WF-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = GetWorkflow(patientId);

        // Create an AR account first
        await wf.CreateARAccountAsync(null, "CopayOutpatient", 50m, DateTime.UtcNow.AddDays(-30));

        string letterId = await wf.GenerateCollectionLetterAsync(
            CollectionLetterType.FirstNotice, "USR-WF", "Workflow User", "Test");

        Assert.That(letterId, Is.Not.Null.And.Not.Empty);

        CollectionLetterState state = await wf.GetCollectionLetterAsync(letterId);
        Assert.That(state.Status, Is.EqualTo(CollectionLetterStatus.Generated));
        Assert.That(state.TotalAmountDue, Is.GreaterThan(0));
    }

    [Test]
    public async Task Workflow_GenerateLetter_UpdatesIndex()
    {
        string patientId = $"PAT-CL-WF2-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = GetWorkflow(patientId);

        await wf.CreateARAccountAsync(null, "CopayPharmacy", 24m, DateTime.UtcNow.AddDays(-15));

        await wf.GenerateCollectionLetterAsync(CollectionLetterType.Statement, "USR-WF", "WF", null);

        List<CollectionLetterIndexEntry> letters = await wf.GetCollectionLettersAsync();
        Assert.That(letters, Has.Count.EqualTo(1));
        Assert.That(letters[0].LetterType, Is.EqualTo(CollectionLetterType.Statement));
    }

    [Test]
    public async Task Workflow_MarkLetterPrinted_UpdatesIndexStatus()
    {
        string patientId = $"PAT-CL-WF3-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = GetWorkflow(patientId);

        await wf.CreateARAccountAsync(null, "CopayOutpatient", 100m, DateTime.UtcNow.AddDays(30));
        string letterId = await wf.GenerateCollectionLetterAsync(CollectionLetterType.FirstNotice, null, null, null);

        await wf.MarkCollectionLetterPrintedAsync(letterId);

        List<CollectionLetterIndexEntry> letters = await wf.GetCollectionLettersAsync();
        Assert.That(letters[0].Status, Is.EqualTo(CollectionLetterStatus.Printed));
    }

    [Test]
    public async Task Workflow_CancelLetter_UpdatesIndexStatus()
    {
        string patientId = $"PAT-CL-WF4-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = GetWorkflow(patientId);

        await wf.CreateARAccountAsync(null, "CopayOutpatient", 75m, DateTime.UtcNow.AddDays(30));
        string letterId = await wf.GenerateCollectionLetterAsync(CollectionLetterType.SecondNotice, null, null, null);

        await wf.CancelCollectionLetterAsync(letterId, "Balance paid");

        CollectionLetterState state = await wf.GetCollectionLetterAsync(letterId);
        Assert.That(state.Status, Is.EqualTo(CollectionLetterStatus.Cancelled));
    }
}
