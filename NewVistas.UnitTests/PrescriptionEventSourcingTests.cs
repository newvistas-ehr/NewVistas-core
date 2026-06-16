// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans.TestingHost;
using NewVistas.Abstractions.Events;
using NewVistas.Abstractions.Events.Clinical.Prescriptions;
using NewVistas.Abstractions.EventSourcing;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.UnitTests;

/// <summary>
/// Event-sourcing tests for the PRESCRIPTIONS domain. PharmacyGrain emits
/// causal envelopes for create / fill / refill / verify / discontinue into the
/// patient's clinical event stream. Hash chain stays intact across the
/// prescription lifecycle and replay reproduces the live state.
/// </summary>
[TestFixture]
public class PrescriptionEventSourcingTests
{
    private TestCluster _cluster = default!;

    [OneTimeSetUp]
    public void OneTimeSetup() => _cluster = SharedCluster.Instance;

    private IPharmacyGrain Rx(string rxId) =>
        _cluster.GrainFactory.GetGrain<IPharmacyGrain>(rxId);

    private IPatientClinicalEventStreamGrain Stream(string patientId) =>
        _cluster.GrainFactory.GetGrain<IPatientClinicalEventStreamGrain>(patientId);

    private async Task<(string patientId, string rxId)> CreateRxAsync(
        int? daysSupply = 30, int? quantity = 60, int? refills = 3)
    {
        string patientId = $"PAT-{Guid.NewGuid()}";
        string rxId = $"RX-{Guid.NewGuid()}";
        await Rx(rxId).CreatePrescriptionAsync(
            patientId, "LISINOPRIL 10MG TAB", "DRUG-001",
            "10 mg", "ORAL", "QD", "Take 1 tablet by mouth daily",
            daysSupply, quantity, refills,
            "PROV-1", "Smith,Jane",
            "PHARM-A", "Main Pharmacy",
            null, null);
        await WaitForStreamVersionAsync(patientId, expected: 1);
        return (patientId, rxId);
    }

    [Test]
    public async Task CreatePrescription_EmitsPrescriptionCreatedV1()
    {
        var (patientId, rxId) = await CreateRxAsync();

        IReadOnlyList<EventEnvelope> events = await Stream(patientId).ReadAsync(0, 10);
        Assert.That(events, Has.Count.EqualTo(1));
        Assert.That(events[0].EventType, Is.EqualTo(nameof(PrescriptionCreatedV1)));
        Assert.That(events[0].Domain, Is.EqualTo("PRESCRIPTIONS"));

        var payload = events[0].Payload as PrescriptionCreatedV1;
        Assert.That(payload, Is.Not.Null);
        Assert.That(payload!.PrescriptionId, Is.EqualTo(rxId));
        Assert.That(payload.Snapshot.DrugName, Is.EqualTo("LISINOPRIL 10MG TAB"));
        Assert.That(payload.Snapshot.Status, Is.EqualTo("ACTIVE"));
        Assert.That(payload.Snapshot.RefillsRemaining, Is.EqualTo(3));
    }

    [Test]
    public async Task CreatePrescription_Idempotent_SecondCallNoOp()
    {
        var (patientId, rxId) = await CreateRxAsync();

        // Second call with totally different params — should be a no-op.
        await Rx(rxId).CreatePrescriptionAsync(
            "OTHER-PAT", "OTHER-DRUG", null, null, null, null, null,
            7, 14, 0, null, null, null, null, null, null);
        await Task.Delay(150);

        Assert.That(await Stream(patientId).GetVersionAsync(), Is.EqualTo(1));
        PharmacyState live = await Rx(rxId).GetPrescriptionAsync();
        Assert.That(live.PatientId, Is.EqualTo(patientId));
        Assert.That(live.DrugName, Is.EqualTo("LISINOPRIL 10MG TAB"));
    }

    [Test]
    public async Task VerifyPrescription_EmitsPrescriptionVerifiedV1_ChainIntact()
    {
        var (patientId, rxId) = await CreateRxAsync();

        await Rx(rxId).VerifyAsync("PHARM-1");
        await WaitForStreamVersionAsync(patientId, expected: 2);

        IReadOnlyList<EventEnvelope> events = await Stream(patientId).ReadAsync(0, 10);
        Assert.That(events[1].EventType, Is.EqualTo(nameof(PrescriptionVerifiedV1)));
        var payload = events[1].Payload as PrescriptionVerifiedV1;
        Assert.That(payload, Is.Not.Null);
        Assert.That(payload!.PharmacistId, Is.EqualTo("PHARM-1"));

        Assert.That(await Stream(patientId).VerifyChainAsync(), Is.True);
    }

    [Test]
    public async Task FillPrescription_EmitsPrescriptionFilledV1()
    {
        var (patientId, rxId) = await CreateRxAsync();
        await Rx(rxId).VerifyAsync("PHARM-1");
        await WaitForStreamVersionAsync(patientId, expected: 2);

        DateTime fill = DateTime.UtcNow;
        await Rx(rxId).FillPrescriptionAsync(fill);
        await WaitForStreamVersionAsync(patientId, expected: 3);

        IReadOnlyList<EventEnvelope> events = await Stream(patientId).ReadAsync(0, 10);
        Assert.That(events[2].EventType, Is.EqualTo(nameof(PrescriptionFilledV1)));
        var payload = events[2].Payload as PrescriptionFilledV1;
        Assert.That(payload, Is.Not.Null);
        Assert.That(payload!.FillDate, Is.EqualTo(fill));
        Assert.That(payload.Quantity, Is.EqualTo(60));
        Assert.That(payload.DaysSupply, Is.EqualTo(30));
    }

    [Test]
    public async Task RefillPrescription_EmitsPrescriptionRefilledV1_RefillsRemainingDecrement()
    {
        var (patientId, rxId) = await CreateRxAsync(daysSupply: 30, refills: 3);
        await Rx(rxId).VerifyAsync("PHARM-1");
        DateTime tFill = DateTime.UtcNow.AddDays(-25);  // 25 days ago — past 75% mark
        await Rx(rxId).FillPrescriptionAsync(tFill);
        await WaitForStreamVersionAsync(patientId, expected: 3);

        // Refill date now — > 75% of 30 days has passed since the original fill.
        DateTime tRefill = DateTime.UtcNow;
        await Rx(rxId).RefillAsync(tRefill);
        await WaitForStreamVersionAsync(patientId, expected: 4);

        IReadOnlyList<EventEnvelope> events = await Stream(patientId).ReadAsync(0, 10);
        Assert.That(events[3].EventType, Is.EqualTo(nameof(PrescriptionRefilledV1)));
        var payload = events[3].Payload as PrescriptionRefilledV1;
        Assert.That(payload, Is.Not.Null);
        Assert.That(payload!.FillNumber, Is.EqualTo(1));
        Assert.That(payload.RefillsRemainingAfter, Is.EqualTo(2));

        Assert.That(await Stream(patientId).VerifyChainAsync(), Is.True);
    }

    [Test]
    public async Task DiscontinuePrescription_EmitsPrescriptionDiscontinuedV1()
    {
        var (patientId, rxId) = await CreateRxAsync();

        await Rx(rxId).DiscontinueAsync("Patient transferred care");
        await WaitForStreamVersionAsync(patientId, expected: 2);

        IReadOnlyList<EventEnvelope> events = await Stream(patientId).ReadAsync(0, 10);
        Assert.That(events[1].EventType, Is.EqualTo(nameof(PrescriptionDiscontinuedV1)));
        var payload = events[1].Payload as PrescriptionDiscontinuedV1;
        Assert.That(payload!.Reason, Is.EqualTo("Patient transferred care"));

        PharmacyState live = await Rx(rxId).GetPrescriptionAsync();
        Assert.That(live.Status, Is.EqualTo("DISCONTINUED"));
    }

    [Test]
    public async Task ReplayUntilAsync_AfterFullLifecycle_ReproducesLiveState()
    {
        var (patientId, rxId) = await CreateRxAsync(daysSupply: 30, refills: 2);
        await Rx(rxId).VerifyAsync("PHARM-1");
        await Rx(rxId).FillPrescriptionAsync(DateTime.UtcNow.AddDays(-25));
        await Rx(rxId).RefillAsync(DateTime.UtcNow);
        await WaitForStreamVersionAsync(patientId, expected: 4);

        PatientStateSnapshot replayed =
            await Stream(patientId).ReplayUntilAsync(DateTime.UtcNow);
        Assert.That(replayed.Prescriptions, Has.Count.EqualTo(1));

        PharmacyState fromChain = replayed.Prescriptions[0];
        Assert.That(fromChain.PrescriptionId, Is.EqualTo(rxId));
        Assert.That(fromChain.DrugName, Is.EqualTo("LISINOPRIL 10MG TAB"));
        Assert.That(fromChain.IsVerified, Is.True);
        Assert.That(fromChain.RefillHistory, Has.Count.EqualTo(2));
        Assert.That(fromChain.RefillsRemaining, Is.EqualTo(1));
    }

    [Test]
    public async Task ReplayUntilAsync_PointInTime_BeforeDiscontinuation_ShowsActive()
    {
        var (patientId, rxId) = await CreateRxAsync();

        await Rx(rxId).VerifyAsync("PHARM-1");
        await Task.Delay(150);
        DateTime tBeforeDc = DateTime.UtcNow;
        await Task.Delay(150);
        await Rx(rxId).DiscontinueAsync("changed mind");
        await WaitForStreamVersionAsync(patientId, expected: 3);

        PatientStateSnapshot before =
            await Stream(patientId).ReplayUntilAsync(tBeforeDc);
        Assert.That(before.Prescriptions[0].Status, Is.EqualTo("ACTIVE"));
        Assert.That(before.Prescriptions[0].DiscontinueReason, Is.Null);

        PatientStateSnapshot after =
            await Stream(patientId).ReplayUntilAsync(DateTime.UtcNow);
        Assert.That(after.Prescriptions[0].Status, Is.EqualTo("DISCONTINUED"));
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
