// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans.TestingHost;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.UnitTests;

/// <summary>
/// Unit tests for the Women's Health grain layer — VistA File #790.
/// Tests notification and index grains directly via Orleans TestCluster.
/// </summary>
[TestFixture]
public class WomensHealthGrainTests
{
    private TestCluster _cluster = default!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    // ── Notification grain — creation ─────────────────────────────────────────

    [Test]
    public async Task NotificationGrain_Mammography_PersistsAllFields()
    {
        string id = $"WH-NOTE:{Guid.NewGuid()}";
        IWomensHealthNotificationGrain grain =
            _cluster.GrainFactory.GetGrain<IWomensHealthNotificationGrain>(id);

        await grain.CreateAsync(
            patientId:           "PATIENT-001",
            notificationType:    WomensHealthNotificationType.Mammography,
            procedureDate:       new DateTime(2024, 10, 1, 9, 0, 0),
            providerId:          "PROV-001",
            providerName:        "Dr. Jane Doe",
            locationId:          "LOC-001",
            locationName:        "Women's Health Clinic",
            mammographyResult:   MammographyResult.ProbablyBenign,
            biRadsScore:         3,
            papSmearResult:      null,
            contraceptiveMethod: null,
            gestationalAgeWeeks: null,
            estimatedDueDate:    null,
            pregnancyOutcome:    null,
            followUpRequired:    true,
            nextDueDate:         new DateTime(2024, 12, 1),
            isRefusal:           false,
            notes:               "6-month follow-up recommended.");

        WomensHealthNotificationState state = await grain.GetAsync();

        Assert.That(state.PatientId,             Is.EqualTo("PATIENT-001"));
        Assert.That(state.NotificationType,      Is.EqualTo(WomensHealthNotificationType.Mammography));
        Assert.That(state.MammographyResult,     Is.EqualTo(MammographyResult.ProbablyBenign));
        Assert.That(state.BiRadsScore,           Is.EqualTo(3));
        Assert.That(state.ProviderName,          Is.EqualTo("Dr. Jane Doe"));
        Assert.That(state.FollowUpRequired,      Is.True);
        Assert.That(state.NextDueDate,           Is.Not.Null);
        Assert.That(state.IsRefusal,             Is.False);
        Assert.That(state.Status,                Is.EqualTo(WomensHealthNotificationStatus.FollowUpRequired));
        Assert.That(state.Notes,                 Does.Contain("6-month follow-up"));
    }

    [Test]
    public async Task NotificationGrain_PapSmear_PersistsCytologyResult()
    {
        string id = $"WH-NOTE:{Guid.NewGuid()}";
        IWomensHealthNotificationGrain grain =
            _cluster.GrainFactory.GetGrain<IWomensHealthNotificationGrain>(id);

        await grain.CreateAsync(
            "PATIENT-002",
            WomensHealthNotificationType.PapSmear,
            new DateTime(2024, 9, 15),
            null, "Dr. Smith", null, null,
            null, null,
            PapSmearResult.Ascus,
            null, null, null, null,
            followUpRequired: true,
            nextDueDate: new DateTime(2025, 3, 15),
            isRefusal: false,
            notes: "Repeat in 6 months.");

        WomensHealthNotificationState state = await grain.GetAsync();

        Assert.That(state.PapSmearResult,   Is.EqualTo(PapSmearResult.Ascus));
        Assert.That(state.FollowUpRequired, Is.True);
        Assert.That(state.Status,           Is.EqualTo(WomensHealthNotificationStatus.FollowUpRequired));
    }

    [Test]
    public async Task NotificationGrain_Pregnancy_PersistsObFields()
    {
        string id = $"WH-NOTE:{Guid.NewGuid()}";
        IWomensHealthNotificationGrain grain =
            _cluster.GrainFactory.GetGrain<IWomensHealthNotificationGrain>(id);

        DateTime edd = new DateTime(2025, 4, 10);
        await grain.CreateAsync(
            "PATIENT-003",
            WomensHealthNotificationType.Pregnancy,
            DateTime.UtcNow,
            null, null, null, null,
            null, null, null,
            contraceptiveMethod: null,
            gestationalAgeWeeks: 12,
            estimatedDueDate: edd,
            pregnancyOutcome: "ONGOING",
            followUpRequired: false,
            nextDueDate: null,
            isRefusal: false,
            notes: null);

        WomensHealthNotificationState state = await grain.GetAsync();

        Assert.That(state.GestationalAgeWeeks, Is.EqualTo(12));
        Assert.That(state.EstimatedDueDate,    Is.EqualTo(edd));
        Assert.That(state.PregnancyOutcome,    Is.EqualTo("ONGOING"));
        Assert.That(state.Status,              Is.EqualTo(WomensHealthNotificationStatus.Active));
    }

    [Test]
    public async Task NotificationGrain_Contraception_PersistsMethod()
    {
        string id = $"WH-NOTE:{Guid.NewGuid()}";
        IWomensHealthNotificationGrain grain =
            _cluster.GrainFactory.GetGrain<IWomensHealthNotificationGrain>(id);

        await grain.CreateAsync(
            "PATIENT-004",
            WomensHealthNotificationType.Contraception,
            DateTime.UtcNow,
            null, null, null, null,
            null, null, null,
            contraceptiveMethod: "IUD",
            null, null, null,
            followUpRequired: false,
            nextDueDate: null,
            isRefusal: false,
            notes: null);

        WomensHealthNotificationState state = await grain.GetAsync();

        Assert.That(state.ContraceptiveMethod, Is.EqualTo("IUD"));
        Assert.That(state.Status,              Is.EqualTo(WomensHealthNotificationStatus.Active));
    }

    [Test]
    public async Task NotificationGrain_Refusal_SetsRefusalFlag()
    {
        string id = $"WH-NOTE:{Guid.NewGuid()}";
        IWomensHealthNotificationGrain grain =
            _cluster.GrainFactory.GetGrain<IWomensHealthNotificationGrain>(id);

        await grain.CreateAsync(
            "PATIENT-005",
            WomensHealthNotificationType.Mammography,
            DateTime.UtcNow,
            null, null, null, null,
            null, null, null, null, null, null, null,
            followUpRequired: false,
            nextDueDate: null,
            isRefusal: true,
            notes: "Patient declined mammography.");

        WomensHealthNotificationState state = await grain.GetAsync();

        Assert.That(state.IsRefusal, Is.True);
        Assert.That(state.Notes, Does.Contain("declined"));
    }

    // ── Notification grain — lifecycle ────────────────────────────────────────

    [Test]
    public async Task NotificationGrain_Complete_SetsStatusAndClearsFollowUp()
    {
        string id = $"WH-NOTE:{Guid.NewGuid()}";
        IWomensHealthNotificationGrain grain =
            _cluster.GrainFactory.GetGrain<IWomensHealthNotificationGrain>(id);

        await grain.CreateAsync(
            "PATIENT-006",
            WomensHealthNotificationType.PapSmear,
            DateTime.UtcNow,
            null, null, null, null,
            null, null, PapSmearResult.Lsil,
            null, null, null, null,
            followUpRequired: true,
            nextDueDate: DateTime.UtcNow.AddMonths(6),
            isRefusal: false,
            notes: null);

        Assert.That((await grain.GetAsync()).Status, Is.EqualTo(WomensHealthNotificationStatus.FollowUpRequired));

        DateTime completedOn = new DateTime(2024, 11, 20);
        await grain.CompleteAsync(completedOn, "Colposcopy completed — benign.");

        WomensHealthNotificationState state = await grain.GetAsync();

        Assert.That(state.Status,                Is.EqualTo(WomensHealthNotificationStatus.Completed));
        Assert.That(state.FollowUpRequired,      Is.False);
        Assert.That(state.FollowUpCompletedDate, Is.EqualTo(completedOn));
        Assert.That(state.Notes,                 Does.Contain("Colposcopy"));
    }

    [Test]
    public async Task NotificationGrain_Cancel_SetsStatusCancelled()
    {
        string id = $"WH-NOTE:{Guid.NewGuid()}";
        IWomensHealthNotificationGrain grain =
            _cluster.GrainFactory.GetGrain<IWomensHealthNotificationGrain>(id);

        await grain.CreateAsync(
            "PATIENT-007",
            WomensHealthNotificationType.Mammography,
            DateTime.UtcNow,
            null, null, null, null,
            null, null, null, null, null, null, null,
            followUpRequired: false,
            nextDueDate: null,
            isRefusal: false,
            notes: null);

        await grain.CancelAsync();

        WomensHealthNotificationState state = await grain.GetAsync();
        Assert.That(state.Status, Is.EqualTo(WomensHealthNotificationStatus.Cancelled));
    }

    [Test]
    public async Task NotificationGrain_SetFollowUpRequired_UpdatesStatusAndDate()
    {
        string id = $"WH-NOTE:{Guid.NewGuid()}";
        IWomensHealthNotificationGrain grain =
            _cluster.GrainFactory.GetGrain<IWomensHealthNotificationGrain>(id);

        await grain.CreateAsync(
            "PATIENT-008",
            WomensHealthNotificationType.Mammography,
            DateTime.UtcNow,
            null, null, null, null,
            MammographyResult.Normal, 1,
            null, null, null, null, null,
            followUpRequired: false,
            nextDueDate: null,
            isRefusal: false,
            notes: null);

        DateTime due = new DateTime(2025, 10, 1);
        await grain.SetFollowUpRequiredAsync(true, due);

        WomensHealthNotificationState state = await grain.GetAsync();
        Assert.That(state.FollowUpRequired, Is.True);
        Assert.That(state.Status,           Is.EqualTo(WomensHealthNotificationStatus.FollowUpRequired));
        Assert.That(state.NextDueDate,      Is.EqualTo(due));

        await grain.SetFollowUpRequiredAsync(false, null);
        state = await grain.GetAsync();
        Assert.That(state.FollowUpRequired, Is.False);
        Assert.That(state.Status,           Is.EqualTo(WomensHealthNotificationStatus.Active));
    }

    // ── Index grain ───────────────────────────────────────────────────────────

    [Test]
    public async Task IndexGrain_AddAndGetAll_ReturnsMostRecentFirst()
    {
        string indexKey = $"WH-IDX:PATIENT-{Guid.NewGuid()}";
        IWomensHealthIndexGrain index =
            _cluster.GrainFactory.GetGrain<IWomensHealthIndexGrain>(indexKey);

        string id1 = $"WH-NOTE:{Guid.NewGuid()}";
        string id2 = $"WH-NOTE:{Guid.NewGuid()}";

        await index.AddEntryAsync(new WomensHealthIndexEntry
        {
            NotificationId   = id1,
            PatientId        = "P-001",
            NotificationType = WomensHealthNotificationType.Mammography,
            ProcedureDate    = new DateTime(2024, 1, 10),
            Status           = WomensHealthNotificationStatus.Completed,
            FollowUpRequired = false,
        });

        await index.AddEntryAsync(new WomensHealthIndexEntry
        {
            NotificationId   = id2,
            PatientId        = "P-001",
            NotificationType = WomensHealthNotificationType.PapSmear,
            ProcedureDate    = new DateTime(2024, 6, 5),
            Status           = WomensHealthNotificationStatus.FollowUpRequired,
            FollowUpRequired = true,
            NextDueDate      = new DateTime(2024, 12, 5),
        });

        List<WomensHealthIndexEntry> all = await index.GetAllAsync();

        // Insert(0) so newest is first
        Assert.That(all, Has.Count.EqualTo(2));
        Assert.That(all[0].NotificationId, Is.EqualTo(id2));
        Assert.That(all[1].NotificationId, Is.EqualTo(id1));
    }

    [Test]
    public async Task IndexGrain_GetByType_FiltersCorrectly()
    {
        string indexKey = $"WH-IDX:PATIENT-{Guid.NewGuid()}";
        IWomensHealthIndexGrain index =
            _cluster.GrainFactory.GetGrain<IWomensHealthIndexGrain>(indexKey);

        await index.AddEntryAsync(new WomensHealthIndexEntry
        {
            NotificationId   = $"WH-NOTE:{Guid.NewGuid()}",
            PatientId        = "P-002",
            NotificationType = WomensHealthNotificationType.Mammography,
            ProcedureDate    = DateTime.UtcNow,
            Status           = WomensHealthNotificationStatus.Completed,
        });

        await index.AddEntryAsync(new WomensHealthIndexEntry
        {
            NotificationId   = $"WH-NOTE:{Guid.NewGuid()}",
            PatientId        = "P-002",
            NotificationType = WomensHealthNotificationType.Pregnancy,
            ProcedureDate    = DateTime.UtcNow,
            Status           = WomensHealthNotificationStatus.Active,
        });

        List<WomensHealthIndexEntry> mammo =
            await index.GetByTypeAsync(WomensHealthNotificationType.Mammography);

        Assert.That(mammo, Has.Count.EqualTo(1));
        Assert.That(mammo[0].NotificationType, Is.EqualTo(WomensHealthNotificationType.Mammography));
    }

    [Test]
    public async Task IndexGrain_GetFollowUpRequired_ReturnsOnlyPendingFollowUps()
    {
        string indexKey = $"WH-IDX:PATIENT-{Guid.NewGuid()}";
        IWomensHealthIndexGrain index =
            _cluster.GrainFactory.GetGrain<IWomensHealthIndexGrain>(indexKey);

        string id1 = $"WH-NOTE:{Guid.NewGuid()}";
        string id2 = $"WH-NOTE:{Guid.NewGuid()}";

        await index.AddEntryAsync(new WomensHealthIndexEntry
        {
            NotificationId   = id1,
            PatientId        = "P-003",
            NotificationType = WomensHealthNotificationType.PapSmear,
            ProcedureDate    = DateTime.UtcNow,
            Status           = WomensHealthNotificationStatus.FollowUpRequired,
            FollowUpRequired = true,
        });

        await index.AddEntryAsync(new WomensHealthIndexEntry
        {
            NotificationId   = id2,
            PatientId        = "P-003",
            NotificationType = WomensHealthNotificationType.Mammography,
            ProcedureDate    = DateTime.UtcNow,
            Status           = WomensHealthNotificationStatus.Completed,
            FollowUpRequired = false,
        });

        List<WomensHealthIndexEntry> followups = await index.GetFollowUpRequiredAsync();

        Assert.That(followups, Has.Count.EqualTo(1));
        Assert.That(followups[0].NotificationId, Is.EqualTo(id1));
    }

    [Test]
    public async Task IndexGrain_UpdateEntryStatus_CompletesFollowUp()
    {
        string indexKey = $"WH-IDX:PATIENT-{Guid.NewGuid()}";
        IWomensHealthIndexGrain index =
            _cluster.GrainFactory.GetGrain<IWomensHealthIndexGrain>(indexKey);

        string noteId = $"WH-NOTE:{Guid.NewGuid()}";
        await index.AddEntryAsync(new WomensHealthIndexEntry
        {
            NotificationId   = noteId,
            PatientId        = "P-004",
            NotificationType = WomensHealthNotificationType.Mammography,
            ProcedureDate    = DateTime.UtcNow,
            Status           = WomensHealthNotificationStatus.FollowUpRequired,
            FollowUpRequired = true,
        });

        await index.UpdateEntryStatusAsync(
            noteId,
            WomensHealthNotificationStatus.Completed,
            followUpRequired: false,
            nextDueDate: null);

        List<WomensHealthIndexEntry> all = await index.GetAllAsync();

        Assert.That(all[0].Status,           Is.EqualTo(WomensHealthNotificationStatus.Completed));
        Assert.That(all[0].FollowUpRequired, Is.False);

        List<WomensHealthIndexEntry> pendingFu = await index.GetFollowUpRequiredAsync();
        Assert.That(pendingFu, Is.Empty);
    }

    [Test]
    public async Task IndexGrain_MultipleTypes_AllStoredAndRetrieved()
    {
        string indexKey = $"WH-IDX:PATIENT-{Guid.NewGuid()}";
        IWomensHealthIndexGrain index =
            _cluster.GrainFactory.GetGrain<IWomensHealthIndexGrain>(indexKey);

        WomensHealthNotificationType[] types =
        {
            WomensHealthNotificationType.Mammography,
            WomensHealthNotificationType.PapSmear,
            WomensHealthNotificationType.Contraception,
            WomensHealthNotificationType.Pregnancy,
        };

        foreach (WomensHealthNotificationType t in types)
        {
            await index.AddEntryAsync(new WomensHealthIndexEntry
            {
                NotificationId   = $"WH-NOTE:{Guid.NewGuid()}",
                PatientId        = "P-005",
                NotificationType = t,
                ProcedureDate    = DateTime.UtcNow,
                Status           = WomensHealthNotificationStatus.Active,
            });
        }

        List<WomensHealthIndexEntry> all = await index.GetAllAsync();
        Assert.That(all, Has.Count.EqualTo(4));

        List<WomensHealthIndexEntry> pregnancies =
            await index.GetByTypeAsync(WomensHealthNotificationType.Pregnancy);
        Assert.That(pregnancies, Has.Count.EqualTo(1));
        Assert.That(pregnancies[0].NotificationType, Is.EqualTo(WomensHealthNotificationType.Pregnancy));
    }
}
