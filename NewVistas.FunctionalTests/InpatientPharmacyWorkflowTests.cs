// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NUnit.Framework;
using Orleans.TestingHost;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.FunctionalTests;

[TestFixture]
public class InpatientPharmacyWorkflowTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    // Helper — creates an order grain with common defaults
    private async Task<IInpatientOrderGrain> CreateOrder(
        string orderId, string patientId = "P-001",
        string orderType = "UNIT_DOSE", string drugName = "METOPROLOL 25MG TAB",
        string? schedule = "BID", string priority = "ROUTINE")
    {
        IInpatientOrderGrain grain = _cluster.GrainFactory.GetGrain<IInpatientOrderGrain>(orderId);
        await grain.CreateOrderAsync(
            patientId, "WARD-4B", "4 NORTH", "4B-12",
            orderType, drugName, null,
            "25MG", "MG", "ORAL", schedule, priority,
            DateTime.UtcNow, DateTime.UtcNow.AddDays(5), 5, 1,
            "PROV-001", "DR. SMITH", null,
            null, null, null);
        return grain;
    }

    // -------------------------------------------------------------------
    // 1. Create — default field values
    // -------------------------------------------------------------------
    [Test]
    public async Task InpatientOrderGrain_Create_InitialisesDefaultFields()
    {
        string orderId = $"PSJ-{Guid.NewGuid()}";
        IInpatientOrderGrain grain = await CreateOrder(orderId);

        InpatientOrderState state = await grain.GetOrderAsync();
        Assert.That(state.OrderId, Is.EqualTo(orderId));
        Assert.That(state.PatientId, Is.EqualTo("P-001"));
        Assert.That(state.Priority, Is.EqualTo("ROUTINE"));
        Assert.That(state.IsVerified, Is.False);
        Assert.That(state.IvAdditives, Is.Empty);
        Assert.That(state.BcmaAdministrationIds, Is.Empty);
    }

    // -------------------------------------------------------------------
    // 2. Create — status is PENDING
    // -------------------------------------------------------------------
    [Test]
    public async Task InpatientOrderGrain_Create_SetsStatusToPending()
    {
        string orderId = $"PSJ-{Guid.NewGuid()}";
        IInpatientOrderGrain grain = await CreateOrder(orderId);

        InpatientOrderState state = await grain.GetOrderAsync();
        Assert.That(state.Status, Is.EqualTo("PENDING"));
    }

    // -------------------------------------------------------------------
    // 3. Verify — sets ACTIVE + verified fields
    // -------------------------------------------------------------------
    [Test]
    public async Task InpatientOrderGrain_Verify_SetsActiveStatusAndVerifiedFields()
    {
        string orderId = $"PSJ-{Guid.NewGuid()}";
        IInpatientOrderGrain grain = await CreateOrder(orderId);

        await grain.VerifyAsync("RPH-007", "Jane Doe, RPh");

        InpatientOrderState state = await grain.GetOrderAsync();
        Assert.That(state.Status, Is.EqualTo("ACTIVE"));
        Assert.That(state.IsVerified, Is.True);
        Assert.That(state.VerifiedBy, Is.EqualTo("RPH-007"));
        Assert.That(state.PharmacistName, Is.EqualTo("Jane Doe, RPh"));
        Assert.That(state.VerifiedDate, Is.Not.Null);
    }

    // -------------------------------------------------------------------
    // 4. Discontinue — uses DiscontinueReason, not Comments
    // -------------------------------------------------------------------
    [Test]
    public async Task InpatientOrderGrain_Discontinue_UsesDiscontinueReasonField()
    {
        string orderId = $"PSJ-{Guid.NewGuid()}";
        IInpatientOrderGrain grain = await CreateOrder(orderId);
        await grain.VerifyAsync("RPH-001", null);

        await grain.DiscontinueAsync("Therapeutic duplication detected");

        InpatientOrderState state = await grain.GetOrderAsync();
        Assert.That(state.Status, Is.EqualTo("DISCONTINUED"));
        Assert.That(state.DiscontinueReason, Is.EqualTo("Therapeutic duplication detected"));
        Assert.That(state.Comments, Is.Null); // Comments field untouched
    }

    // -------------------------------------------------------------------
    // 5. Hold — uses HoldReason, not Comments
    // -------------------------------------------------------------------
    [Test]
    public async Task InpatientOrderGrain_Hold_UsesHoldReasonField()
    {
        string orderId = $"PSJ-{Guid.NewGuid()}";
        IInpatientOrderGrain grain = await CreateOrder(orderId);
        await grain.VerifyAsync("RPH-001", null);

        await grain.HoldAsync("Patient NPO for procedure");

        InpatientOrderState state = await grain.GetOrderAsync();
        Assert.That(state.Status, Is.EqualTo("HOLD"));
        Assert.That(state.HoldReason, Is.EqualTo("Patient NPO for procedure"));
        Assert.That(state.Comments, Is.Null);
    }

    // -------------------------------------------------------------------
    // 6. Resume — sets ACTIVE
    // -------------------------------------------------------------------
    [Test]
    public async Task InpatientOrderGrain_Resume_SetsStatusToActive()
    {
        string orderId = $"PSJ-{Guid.NewGuid()}";
        IInpatientOrderGrain grain = await CreateOrder(orderId);
        await grain.VerifyAsync("RPH-001", null);
        await grain.HoldAsync("Labs pending");

        await grain.ResumeAsync();

        InpatientOrderState state = await grain.GetOrderAsync();
        Assert.That(state.Status, Is.EqualTo("ACTIVE"));
    }

    // -------------------------------------------------------------------
    // 7. Expire — sets EXPIRED
    // -------------------------------------------------------------------
    [Test]
    public async Task InpatientOrderGrain_Expire_SetsExpiredStatus()
    {
        string orderId = $"PSJ-{Guid.NewGuid()}";
        IInpatientOrderGrain grain = await CreateOrder(orderId);
        await grain.VerifyAsync("RPH-001", null);

        await grain.ExpireAsync();

        InpatientOrderState state = await grain.GetOrderAsync();
        Assert.That(state.Status, Is.EqualTo("EXPIRED"));
    }

    // -------------------------------------------------------------------
    // 8. AddIvAdditive — appends to list
    // -------------------------------------------------------------------
    [Test]
    public async Task InpatientOrderGrain_AddIvAdditive_AppendsToList()
    {
        string orderId = $"PSJ-{Guid.NewGuid()}";
        IInpatientOrderGrain grain = await CreateOrder(orderId, orderType: "IV", drugName: "VANCOMYCIN 1GM/NS 250ML");

        await grain.AddIvAdditiveAsync(new IvAdditive
            { DrugName = "VANCOMYCIN", Dose = "1", DoseUnit = "GM", IsBase = true });
        await grain.AddIvAdditiveAsync(new IvAdditive
            { DrugName = "POTASSIUM CHLORIDE", Dose = "20", DoseUnit = "mEq", IsBase = false });

        InpatientOrderState state = await grain.GetOrderAsync();
        Assert.That(state.IvAdditives, Has.Count.EqualTo(2));
        Assert.That(state.IvAdditives[0].DrugName, Is.EqualTo("VANCOMYCIN"));
        Assert.That(state.IvAdditives[1].IsBase, Is.False);
    }

    // -------------------------------------------------------------------
    // 9. AddScheduledTime — appends and sorts
    // -------------------------------------------------------------------
    [Test]
    public async Task InpatientOrderGrain_AddScheduledTime_AppendsToList()
    {
        string orderId = $"PSJ-{Guid.NewGuid()}";
        IInpatientOrderGrain grain = await CreateOrder(orderId);

        DateTime t1 = DateTime.UtcNow.AddHours(12);
        DateTime t2 = DateTime.UtcNow.AddHours(6);

        await grain.AddScheduledTimeAsync(t1);
        await grain.AddScheduledTimeAsync(t2);

        InpatientOrderState state = await grain.GetOrderAsync();
        Assert.That(state.ScheduledAdminTimes, Has.Count.EqualTo(2));
        Assert.That(state.ScheduledAdminTimes[0], Is.LessThan(state.ScheduledAdminTimes[1])); // sorted
    }

    // -------------------------------------------------------------------
    // 10. RecordAdministration — adds BCMA ID + sets LastAdminDate
    // -------------------------------------------------------------------
    [Test]
    public async Task InpatientOrderGrain_RecordAdministration_AddsBcmaIdAndUpdatesLastAdmin()
    {
        string orderId = $"PSJ-{Guid.NewGuid()}";
        IInpatientOrderGrain grain = await CreateOrder(orderId);
        await grain.VerifyAsync("RPH-001", null);

        DateTime adminDate = DateTime.UtcNow;
        await grain.RecordAdministrationAsync("BCMA-001", adminDate);
        await grain.RecordAdministrationAsync("BCMA-001", adminDate); // duplicate should not be added twice

        InpatientOrderState state = await grain.GetOrderAsync();
        Assert.That(state.BcmaAdministrationIds, Has.Count.EqualTo(1));
        Assert.That(state.BcmaAdministrationIds[0], Is.EqualTo("BCMA-001"));
        Assert.That(state.LastAdminDate, Is.Not.Null);
    }

    // -------------------------------------------------------------------
    // 11. Profile — AddOrder appears in GetAll
    // -------------------------------------------------------------------
    [Test]
    public async Task ProfileGrain_AddOrder_AppearsInGetAll()
    {
        string patientId = $"P-{Guid.NewGuid()}";
        IPatientInpatientProfileGrain profile =
            _cluster.GrainFactory.GetGrain<IPatientInpatientProfileGrain>($"PSJ-PROFILE:{patientId}");

        InpatientOrderIndexEntry entry = new()
        {
            OrderId = $"PSJ-{Guid.NewGuid()}",
            DrugName = "ASPIRIN 81MG",
            OrderType = "UNIT_DOSE",
            Status = "ACTIVE",
            Priority = "ROUTINE"
        };

        await profile.AddOrUpdateOrderAsync(entry);
        List<InpatientOrderIndexEntry> all = await profile.GetAllOrdersAsync();

        Assert.That(all, Has.Count.EqualTo(1));
        Assert.That(all[0].DrugName, Is.EqualTo("ASPIRIN 81MG"));
    }

    // -------------------------------------------------------------------
    // 12. Profile — GetActive filters correctly
    // -------------------------------------------------------------------
    [Test]
    public async Task ProfileGrain_GetActive_FiltersToActiveOnly()
    {
        string patientId = $"P-{Guid.NewGuid()}";
        IPatientInpatientProfileGrain profile =
            _cluster.GrainFactory.GetGrain<IPatientInpatientProfileGrain>($"PSJ-PROFILE:{patientId}");

        await profile.AddOrUpdateOrderAsync(new InpatientOrderIndexEntry
            { OrderId = "O-1", DrugName = "DRUG-A", OrderType = "UNIT_DOSE", Status = "ACTIVE", Priority = "ROUTINE" });
        await profile.AddOrUpdateOrderAsync(new InpatientOrderIndexEntry
            { OrderId = "O-2", DrugName = "DRUG-B", OrderType = "IV", Status = "DISCONTINUED", Priority = "ROUTINE" });
        await profile.AddOrUpdateOrderAsync(new InpatientOrderIndexEntry
            { OrderId = "O-3", DrugName = "DRUG-C", OrderType = "LVP", Status = "ACTIVE", Priority = "STAT" });

        List<InpatientOrderIndexEntry> active = await profile.GetActiveOrdersAsync();
        Assert.That(active, Has.Count.EqualTo(2));
        Assert.That(active.Select(o => o.OrderId), Does.Not.Contain("O-2"));
    }

    // -------------------------------------------------------------------
    // 13. Profile — GetPendingVerification filters unverified PENDING
    // -------------------------------------------------------------------
    [Test]
    public async Task ProfileGrain_GetPending_FiltersToUnverified()
    {
        string patientId = $"P-{Guid.NewGuid()}";
        IPatientInpatientProfileGrain profile =
            _cluster.GrainFactory.GetGrain<IPatientInpatientProfileGrain>($"PSJ-PROFILE:{patientId}");

        await profile.AddOrUpdateOrderAsync(new InpatientOrderIndexEntry
            { OrderId = "P-1", DrugName = "X", OrderType = "UNIT_DOSE", Status = "PENDING", IsVerified = false, Priority = "ROUTINE" });
        await profile.AddOrUpdateOrderAsync(new InpatientOrderIndexEntry
            { OrderId = "P-2", DrugName = "Y", OrderType = "IV", Status = "ACTIVE", IsVerified = true, Priority = "ROUTINE" });
        await profile.AddOrUpdateOrderAsync(new InpatientOrderIndexEntry
            { OrderId = "P-3", DrugName = "Z", OrderType = "LVP", Status = "PENDING", IsVerified = false, Priority = "STAT" });

        List<InpatientOrderIndexEntry> pending = await profile.GetPendingVerificationAsync();
        Assert.That(pending, Has.Count.EqualTo(2));
        Assert.That(pending.Select(o => o.OrderId), Does.Not.Contain("P-2"));
    }

    // -------------------------------------------------------------------
    // 14. Profile — GetByType filters to order type
    // -------------------------------------------------------------------
    [Test]
    public async Task ProfileGrain_GetByType_FiltersToOrderType()
    {
        string patientId = $"P-{Guid.NewGuid()}";
        IPatientInpatientProfileGrain profile =
            _cluster.GrainFactory.GetGrain<IPatientInpatientProfileGrain>($"PSJ-PROFILE:{patientId}");

        await profile.AddOrUpdateOrderAsync(new InpatientOrderIndexEntry
            { OrderId = "T-1", DrugName = "UD-DRUG", OrderType = "UNIT_DOSE", Status = "ACTIVE", Priority = "ROUTINE" });
        await profile.AddOrUpdateOrderAsync(new InpatientOrderIndexEntry
            { OrderId = "T-2", DrugName = "IV-DRUG", OrderType = "IV", Status = "ACTIVE", Priority = "STAT" });
        await profile.AddOrUpdateOrderAsync(new InpatientOrderIndexEntry
            { OrderId = "T-3", DrugName = "LVP-DRUG", OrderType = "LVP", Status = "ACTIVE", Priority = "ROUTINE" });

        List<InpatientOrderIndexEntry> ivOnly = await profile.GetByTypeAsync("IV");
        Assert.That(ivOnly, Has.Count.EqualTo(1));
        Assert.That(ivOnly[0].OrderId, Is.EqualTo("T-2"));
    }

    // -------------------------------------------------------------------
    // 15. Profile — AddOrUpdate replaces existing
    // -------------------------------------------------------------------
    [Test]
    public async Task ProfileGrain_AddOrUpdate_ReplacesExistingEntry()
    {
        string patientId = $"P-{Guid.NewGuid()}";
        IPatientInpatientProfileGrain profile =
            _cluster.GrainFactory.GetGrain<IPatientInpatientProfileGrain>($"PSJ-PROFILE:{patientId}");

        string orderId = $"O-{Guid.NewGuid()}";

        await profile.AddOrUpdateOrderAsync(new InpatientOrderIndexEntry
            { OrderId = orderId, DrugName = "DRUG", OrderType = "UNIT_DOSE", Status = "PENDING", Priority = "ROUTINE" });
        await profile.AddOrUpdateOrderAsync(new InpatientOrderIndexEntry
            { OrderId = orderId, DrugName = "DRUG", OrderType = "UNIT_DOSE", Status = "ACTIVE", Priority = "ROUTINE", IsVerified = true });

        List<InpatientOrderIndexEntry> all = await profile.GetAllOrdersAsync();
        Assert.That(all, Has.Count.EqualTo(1));
        Assert.That(all[0].Status, Is.EqualTo("ACTIVE"));
        Assert.That(all[0].IsVerified, Is.True);
    }

    // -------------------------------------------------------------------
    // 16. End-to-end: Create → Verify → Add IV Additive → Record Admin
    // -------------------------------------------------------------------
    [Test]
    public async Task Workflow_CreateVerifyAddAdditiveRecordAdmin_EndToEnd()
    {
        string patientId = $"P-{Guid.NewGuid()}";
        string orderId = $"PSJ-{Guid.NewGuid()}";

        IInpatientOrderGrain orderGrain = _cluster.GrainFactory.GetGrain<IInpatientOrderGrain>(orderId);
        IPatientInpatientProfileGrain profileGrain =
            _cluster.GrainFactory.GetGrain<IPatientInpatientProfileGrain>($"PSJ-PROFILE:{patientId}");

        // Create IV order
        await orderGrain.CreateOrderAsync(
            patientId, "WARD-4B", "4 NORTH", "4B-12", "IV",
            "VANCOMYCIN 1GM/NS 250ML", "50-VANC",
            "1", "GM", "IV", "Q12H", "STAT",
            DateTime.UtcNow, DateTime.UtcNow.AddDays(7), 7, 1,
            "PROV-001", "DR. SMITH", null,
            "NS", 250, "over 60 min");

        InpatientOrderState state = await orderGrain.GetOrderAsync();
        await profileGrain.AddOrUpdateOrderAsync(BuildEntry(orderId, state));

        // Verify
        await orderGrain.VerifyAsync("RPH-007", "Jane Doe, RPh");
        state = await orderGrain.GetOrderAsync();
        await profileGrain.AddOrUpdateOrderAsync(BuildEntry(orderId, state));

        Assert.That(state.Status, Is.EqualTo("ACTIVE"));
        Assert.That(state.IsVerified, Is.True);

        // Add additive
        await orderGrain.AddIvAdditiveAsync(new IvAdditive
            { DrugName = "VANCOMYCIN", Dose = "1", DoseUnit = "GM", IsBase = true });
        state = await orderGrain.GetOrderAsync();

        Assert.That(state.IvAdditives, Has.Count.EqualTo(1));

        // Schedule a time
        await orderGrain.AddScheduledTimeAsync(DateTime.UtcNow.AddHours(12));
        state = await orderGrain.GetOrderAsync();
        Assert.That(state.ScheduledAdminTimes, Has.Count.EqualTo(1));

        // Record administration
        await orderGrain.RecordAdministrationAsync("BCMA-ADMIN-001", DateTime.UtcNow);
        state = await orderGrain.GetOrderAsync();
        await profileGrain.AddOrUpdateOrderAsync(BuildEntry(orderId, state));

        Assert.That(state.BcmaAdministrationIds, Has.Count.EqualTo(1));
        Assert.That(state.LastAdminDate, Is.Not.Null);

        // Verify profile reflects final state
        List<InpatientOrderIndexEntry> active = await profileGrain.GetActiveOrdersAsync();
        Assert.That(active, Has.Count.EqualTo(1));
        Assert.That(active[0].IsVerified, Is.True);
        Assert.That(active[0].LastAdminDate, Is.Not.Null);
    }

    private static InpatientOrderIndexEntry BuildEntry(string orderId, InpatientOrderState s) =>
        new()
        {
            OrderId = orderId,
            DrugName = s.DrugName,
            DrugId = s.DrugId,
            OrderType = s.OrderType,
            Status = s.Status,
            Priority = s.Priority,
            Schedule = s.Schedule,
            Dosage = s.Dosage,
            StartDate = s.StartDate,
            StopDate = s.StopDate,
            IsVerified = s.IsVerified,
            ProviderName = s.ProviderName,
            WardName = s.WardName,
            RoomBed = s.RoomBed,
            LastAdminDate = s.LastAdminDate
        };
}
