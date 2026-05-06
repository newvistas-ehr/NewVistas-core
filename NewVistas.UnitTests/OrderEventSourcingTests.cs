// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans.TestingHost;
using NewVistas.Abstractions.Events;
using NewVistas.Abstractions.Events.Clinical.Orders;
using NewVistas.Abstractions.EventSourcing;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.UnitTests;

/// <summary>
/// Event-sourcing tests for the ORDERS domain — every <see cref="OrderGrain"/>
/// command (create / sign / release / discontinue / hold) must emit a causal
/// envelope into the patient's clinical event stream and keep the hash chain
/// intact. Replay must reproduce the live order state from the chain alone.
/// </summary>
[TestFixture]
public class OrderEventSourcingTests
{
    private TestCluster _cluster = default!;

    [OneTimeSetUp]
    public void OneTimeSetup() => _cluster = SharedCluster.Instance;

    private IOrderGrain Order(string orderId) =>
        _cluster.GrainFactory.GetGrain<IOrderGrain>(orderId);

    private IPatientClinicalEventStreamGrain Stream(string patientId) =>
        _cluster.GrainFactory.GetGrain<IPatientClinicalEventStreamGrain>(patientId);

    private async Task<(string patientId, string orderId)> CreateOrderAsync()
    {
        string patientId = $"PAT-{Guid.NewGuid()}";
        string orderId = $"ORDER-{Guid.NewGuid()}";
        IOrderGrain order = Order(orderId);
        await order.CreateOrderAsync(
            patientId, "LAB", "CBC", "OI-001",
            "PROV-1", "Smith,Jane",
            DateTime.UtcNow, "LOC-1", "Clinic A",
            "ROUTINE", "fasting required", "annual physical",
            "NEW", "PROV-1");
        await WaitForStreamVersionAsync(patientId, expected: 1);
        return (patientId, orderId);
    }

    [Test]
    public async Task CreateOrder_EmitsOrderPlacedV1()
    {
        var (patientId, orderId) = await CreateOrderAsync();

        IReadOnlyList<EventEnvelope> events = await Stream(patientId).ReadAsync(0, 10);
        Assert.That(events, Has.Count.EqualTo(1));
        Assert.That(events[0].EventType, Is.EqualTo(nameof(OrderPlacedV1)));
        Assert.That(events[0].PatientId, Is.EqualTo(patientId));
        Assert.That(events[0].Domain, Is.EqualTo("ORDERS"));

        var payload = events[0].Payload as OrderPlacedV1;
        Assert.That(payload, Is.Not.Null);
        Assert.That(payload!.OrderId, Is.EqualTo(orderId));
        Assert.That(payload.Snapshot.OrderType, Is.EqualTo("LAB"));
        Assert.That(payload.Snapshot.OrderableItem, Is.EqualTo("CBC"));
        Assert.That(payload.Snapshot.Status, Is.EqualTo("Pending"));
    }

    [Test]
    public async Task CreateOrder_Idempotent_OnSecondCallSamePatient()
    {
        var (patientId, orderId) = await CreateOrderAsync();

        // Second create on the same grain key — should be a no-op.
        await Order(orderId).CreateOrderAsync(
            "OTHER-PAT", "RAD", "X-Ray", null,
            "PROV-2", "Other,Doc",
            DateTime.UtcNow, null, null, "STAT", null, null, null, null);

        await Task.Delay(150);

        Assert.That(await Stream(patientId).GetVersionAsync(), Is.EqualTo(1));
        OrderState live = await Order(orderId).GetOrderAsync();
        Assert.That(live.PatientId, Is.EqualTo(patientId));
        Assert.That(live.OrderType, Is.EqualTo("LAB"));
    }

    [Test]
    public async Task SignOrder_EmitsOrderSignedV1_ChainIntact()
    {
        var (patientId, orderId) = await CreateOrderAsync();

        DateTime signed = DateTime.UtcNow;
        await Order(orderId).SignOrderAsync("ELECTRONIC-SIG", signed);
        await WaitForStreamVersionAsync(patientId, expected: 2);

        IReadOnlyList<EventEnvelope> events = await Stream(patientId).ReadAsync(0, 10);
        Assert.That(events[1].EventType, Is.EqualTo(nameof(OrderSignedV1)));
        var payload = events[1].Payload as OrderSignedV1;
        Assert.That(payload, Is.Not.Null);
        Assert.That(payload!.OrderId, Is.EqualTo(orderId));
        Assert.That(payload.ElectronicSignature, Is.EqualTo("ELECTRONIC-SIG"));
        Assert.That(payload.SignatureDateTime, Is.EqualTo(signed));

        Assert.That(await Stream(patientId).VerifyChainAsync(), Is.True);
    }

    [Test]
    public async Task SignOrder_Idempotent_OnSecondSign()
    {
        var (patientId, orderId) = await CreateOrderAsync();

        await Order(orderId).SignOrderAsync("SIG-1", DateTime.UtcNow);
        await WaitForStreamVersionAsync(patientId, expected: 2);

        await Order(orderId).SignOrderAsync("SIG-2", DateTime.UtcNow);
        await Task.Delay(150);

        Assert.That(await Stream(patientId).GetVersionAsync(), Is.EqualTo(2));
        OrderState live = await Order(orderId).GetOrderAsync();
        Assert.That(live.ElectronicSignature, Is.EqualTo("SIG-1"));
    }

    [Test]
    public async Task ReleaseOrder_EmitsOrderReleasedV1_StatusActive()
    {
        var (patientId, orderId) = await CreateOrderAsync();

        DateTime released = DateTime.UtcNow;
        await Order(orderId).ReleaseOrderAsync(released);
        await WaitForStreamVersionAsync(patientId, expected: 2);

        IReadOnlyList<EventEnvelope> events = await Stream(patientId).ReadAsync(0, 10);
        Assert.That(events[1].EventType, Is.EqualTo(nameof(OrderReleasedV1)));
        var payload = events[1].Payload as OrderReleasedV1;
        Assert.That(payload!.ReleaseDateTime, Is.EqualTo(released));

        OrderState live = await Order(orderId).GetOrderAsync();
        Assert.That(live.Status, Is.EqualTo("Active"));
        Assert.That(await Stream(patientId).VerifyChainAsync(), Is.True);
    }

    [Test]
    public async Task DiscontinueOrder_EmitsOrderDiscontinuedV1()
    {
        var (patientId, orderId) = await CreateOrderAsync();

        DateTime dc = DateTime.UtcNow;
        await Order(orderId).DiscontinueOrderAsync(dc, "Patient declined", "PROV-9");
        await WaitForStreamVersionAsync(patientId, expected: 2);

        IReadOnlyList<EventEnvelope> events = await Stream(patientId).ReadAsync(0, 10);
        var payload = events[1].Payload as OrderDiscontinuedV1;
        Assert.That(payload, Is.Not.Null);
        Assert.That(payload!.Reason, Is.EqualTo("Patient declined"));
        Assert.That(payload.DiscontinuedByProviderId, Is.EqualTo("PROV-9"));

        OrderState live = await Order(orderId).GetOrderAsync();
        Assert.That(live.Status, Is.EqualTo("Discontinued"));
    }

    [Test]
    public async Task DiscontinueOrder_Idempotent_OnAlreadyDiscontinued()
    {
        var (patientId, orderId) = await CreateOrderAsync();

        await Order(orderId).DiscontinueOrderAsync(DateTime.UtcNow, "first", "PROV-9");
        await WaitForStreamVersionAsync(patientId, expected: 2);

        await Order(orderId).DiscontinueOrderAsync(DateTime.UtcNow, "second", "PROV-9");
        await Task.Delay(150);

        Assert.That(await Stream(patientId).GetVersionAsync(), Is.EqualTo(2));
        OrderState live = await Order(orderId).GetOrderAsync();
        Assert.That(live.DiscontinuedReason, Is.EqualTo("first"));
    }

    [Test]
    public async Task HoldOrder_EmitsOrderHeldV1()
    {
        var (patientId, orderId) = await CreateOrderAsync();

        await Order(orderId).HoldOrderAsync();
        await WaitForStreamVersionAsync(patientId, expected: 2);

        IReadOnlyList<EventEnvelope> events = await Stream(patientId).ReadAsync(0, 10);
        Assert.That(events[1].EventType, Is.EqualTo(nameof(OrderHeldV1)));

        OrderState live = await Order(orderId).GetOrderAsync();
        Assert.That(live.Status, Is.EqualTo("Hold"));
    }

    [Test]
    public async Task SignThenRelease_TwoEventsInOrder_HashChainIntact()
    {
        var (patientId, orderId) = await CreateOrderAsync();

        DateTime t = DateTime.UtcNow;
        await Order(orderId).SignOrderAsync("SIG", t);
        await Order(orderId).ReleaseOrderAsync(t);
        await WaitForStreamVersionAsync(patientId, expected: 3);

        IReadOnlyList<EventEnvelope> events = await Stream(patientId).ReadAsync(0, 10);
        Assert.That(events, Has.Count.EqualTo(3));
        Assert.That(events[0].EventType, Is.EqualTo(nameof(OrderPlacedV1)));
        Assert.That(events[1].EventType, Is.EqualTo(nameof(OrderSignedV1)));
        Assert.That(events[2].EventType, Is.EqualTo(nameof(OrderReleasedV1)));

        Assert.That(await Stream(patientId).VerifyChainAsync(), Is.True);
    }

    [Test]
    public async Task ReplayUntilAsync_AfterFullLifecycle_MatchesLiveOrderState()
    {
        var (patientId, orderId) = await CreateOrderAsync();

        DateTime tSign = DateTime.UtcNow;
        await Order(orderId).SignOrderAsync("SIG", tSign);
        await Order(orderId).ReleaseOrderAsync(tSign);
        await Order(orderId).DiscontinueOrderAsync(
            tSign.AddMinutes(5), "Replaced by another order", "PROV-9");
        await WaitForStreamVersionAsync(patientId, expected: 4);

        PatientStateSnapshot replayed = await Stream(patientId).ReplayUntilAsync(DateTime.UtcNow);
        Assert.That(replayed.Orders, Has.Count.EqualTo(1));
        OrderState fromChain = replayed.Orders[0];
        Assert.That(fromChain.OrderId, Is.EqualTo(orderId));
        Assert.That(fromChain.OrderType, Is.EqualTo("LAB"));
        Assert.That(fromChain.ElectronicSignature, Is.EqualTo("SIG"));
        Assert.That(fromChain.Status, Is.EqualTo("Discontinued"));
        Assert.That(fromChain.DiscontinuedReason, Is.EqualTo("Replaced by another order"));
    }

    [Test]
    public async Task ReplayUntilAsync_PointInTime_BeforeDiscontinuation_ShowsActive()
    {
        // Real wall-clock gaps so envelope OccurredUtc values are distinct —
        // OccurredUtc is the recording instant (when the command ran), not
        // the historical timestamp passed in as a domain field.
        var (patientId, orderId) = await CreateOrderAsync();

        await Order(orderId).SignOrderAsync("SIG", DateTime.UtcNow);
        await Order(orderId).ReleaseOrderAsync(DateTime.UtcNow);

        // Capture a snapshot point in the chain — between release and DC.
        await Task.Delay(150);
        DateTime tBeforeDc = DateTime.UtcNow;
        await Task.Delay(150);

        await Order(orderId).DiscontinueOrderAsync(DateTime.UtcNow, "later", "PROV-9");
        await WaitForStreamVersionAsync(patientId, expected: 4);

        PatientStateSnapshot before =
            await Stream(patientId).ReplayUntilAsync(tBeforeDc);
        Assert.That(before.Orders, Has.Count.EqualTo(1));
        Assert.That(before.Orders[0].Status, Is.EqualTo("Active"));
        Assert.That(before.Orders[0].DiscontinuedReason, Is.Null);

        PatientStateSnapshot after =
            await Stream(patientId).ReplayUntilAsync(DateTime.UtcNow);
        Assert.That(after.Orders[0].Status, Is.EqualTo("Discontinued"));
    }

    private async Task WaitForStreamVersionAsync(
        string patientId, int expected, int timeoutMs = 5000)
    {
        DateTime deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        IPatientClinicalEventStreamGrain stream = Stream(patientId);
        while (DateTime.UtcNow < deadline)
        {
            int v = await stream.GetVersionAsync();
            if (v >= expected) return;
            await Task.Delay(50);
        }
        int finalVersion = await stream.GetVersionAsync();
        Assert.Fail(
            $"Stream for {patientId} did not reach version {expected} within {timeoutMs}ms (current={finalVersion}).");
    }
}
