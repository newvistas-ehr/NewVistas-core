// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans.TestingHost;
using NewVistas.Abstractions.Events;
using NewVistas.Abstractions.Events.Clinical.Labs;
using NewVistas.Abstractions.EventSourcing;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.UnitTests;

/// <summary>
/// Event-sourcing tests for the LABS domain — every <see cref="LabTestGrain"/>
/// command (order / collect / record / verify) must emit a causal envelope
/// into the patient's clinical event stream and keep the hash chain intact
/// across the lab-order lifecycle.
/// </summary>
[TestFixture]
public class LabEventSourcingTests
{
    private TestCluster _cluster = default!;

    [OneTimeSetUp]
    public void OneTimeSetup() => _cluster = SharedCluster.Instance;

    private ILabTestGrain Lab(string labTestId) =>
        _cluster.GrainFactory.GetGrain<ILabTestGrain>(labTestId);

    private IPatientClinicalEventStreamGrain Stream(string patientId) =>
        _cluster.GrainFactory.GetGrain<IPatientClinicalEventStreamGrain>(patientId);

    private async Task<(string patientId, string labTestId)> OrderLabAsync()
    {
        string patientId = $"PAT-{Guid.NewGuid()}";
        string labTestId = $"LAB-{Guid.NewGuid()}";
        await Lab(labTestId).OrderLabTestAsync(
            patientId, "TEST-CBC", "Complete Blood Count", "85025",
            "ORDER-1", "PROV-1", "Smith,Jane",
            "BLOOD", "HEMATOLOGY");
        await WaitForStreamVersionAsync(patientId, expected: 1);
        return (patientId, labTestId);
    }

    [Test]
    public async Task OrderLab_EmitsLabOrderedV1()
    {
        var (patientId, labTestId) = await OrderLabAsync();

        IReadOnlyList<EventEnvelope> events = await Stream(patientId).ReadAsync(0, 10);
        Assert.That(events, Has.Count.EqualTo(1));
        Assert.That(events[0].EventType, Is.EqualTo(nameof(LabOrderedV1)));
        Assert.That(events[0].Domain, Is.EqualTo("LABS"));

        var payload = events[0].Payload as LabOrderedV1;
        Assert.That(payload, Is.Not.Null);
        Assert.That(payload!.LabTestId, Is.EqualTo(labTestId));
        Assert.That(payload.Snapshot.TestName, Is.EqualTo("Complete Blood Count"));
        Assert.That(payload.Snapshot.Status, Is.EqualTo("Ordered"));
    }

    [Test]
    public async Task OrderLab_Idempotent_OnSecondCall()
    {
        var (patientId, labTestId) = await OrderLabAsync();

        await Lab(labTestId).OrderLabTestAsync(
            "OTHER-PAT", "TEST-X", "X", null, null, null, null, null, null);
        await Task.Delay(150);

        Assert.That(await Stream(patientId).GetVersionAsync(), Is.EqualTo(1));
    }

    [Test]
    public async Task CollectSpecimen_EmitsSpecimenCollectedV1()
    {
        var (patientId, labTestId) = await OrderLabAsync();

        DateTime collected = DateTime.UtcNow;
        await Lab(labTestId).CollectSpecimenAsync(collected, "LAVENDER", "MAIN-LAB");
        await WaitForStreamVersionAsync(patientId, expected: 2);

        IReadOnlyList<EventEnvelope> events = await Stream(patientId).ReadAsync(0, 10);
        Assert.That(events[1].EventType, Is.EqualTo(nameof(SpecimenCollectedV1)));
        var payload = events[1].Payload as SpecimenCollectedV1;
        Assert.That(payload, Is.Not.Null);
        Assert.That(payload!.CollectionDateTime, Is.EqualTo(collected));
        Assert.That(payload.CollectionSample, Is.EqualTo("LAVENDER"));

        LabTestState live = await Lab(labTestId).GetLabTestAsync();
        Assert.That(live.Status, Is.EqualTo("Collected"));
        Assert.That(await Stream(patientId).VerifyChainAsync(), Is.True);
    }

    [Test]
    public async Task CollectSpecimen_Idempotent_OnSecondCall()
    {
        var (patientId, labTestId) = await OrderLabAsync();
        await Lab(labTestId).CollectSpecimenAsync(DateTime.UtcNow, "LAVENDER", "LAB-A");
        await WaitForStreamVersionAsync(patientId, expected: 2);

        await Lab(labTestId).CollectSpecimenAsync(DateTime.UtcNow, "OTHER", "LAB-B");
        await Task.Delay(150);

        Assert.That(await Stream(patientId).GetVersionAsync(), Is.EqualTo(2));
        LabTestState live = await Lab(labTestId).GetLabTestAsync();
        Assert.That(live.CollectionSample, Is.EqualTo("LAVENDER"));
    }

    [Test]
    public async Task RecordResult_EmitsLabResultRecordedV1()
    {
        var (patientId, labTestId) = await OrderLabAsync();
        await Lab(labTestId).CollectSpecimenAsync(DateTime.UtcNow, "LAVENDER", "LAB-A");

        DateTime resultedAt = DateTime.UtcNow;
        await Lab(labTestId).RecordResultAsync(
            resultedAt, "7.5", "K/cmm", "4.0", "11.0", "Normal");
        await WaitForStreamVersionAsync(patientId, expected: 3);

        IReadOnlyList<EventEnvelope> events = await Stream(patientId).ReadAsync(0, 10);
        Assert.That(events[2].EventType, Is.EqualTo(nameof(LabResultRecordedV1)));
        var payload = events[2].Payload as LabResultRecordedV1;
        Assert.That(payload, Is.Not.Null);
        Assert.That(payload!.ResultValue, Is.EqualTo("7.5"));
        Assert.That(payload.AbnormalFlag, Is.EqualTo("Normal"));
    }

    [Test]
    public async Task VerifyResult_EmitsLabResultVerifiedV1_StatusCompleted()
    {
        var (patientId, labTestId) = await OrderLabAsync();
        await Lab(labTestId).CollectSpecimenAsync(DateTime.UtcNow, "LAVENDER", "LAB-A");
        await Lab(labTestId).RecordResultAsync(
            DateTime.UtcNow, "7.5", "K/cmm", "4.0", "11.0", "Normal");

        DateTime verifiedAt = DateTime.UtcNow;
        await Lab(labTestId).VerifyResultAsync("PROV-2", "Brown,Bob", verifiedAt);
        await WaitForStreamVersionAsync(patientId, expected: 4);

        IReadOnlyList<EventEnvelope> events = await Stream(patientId).ReadAsync(0, 10);
        Assert.That(events[3].EventType, Is.EqualTo(nameof(LabResultVerifiedV1)));
        var payload = events[3].Payload as LabResultVerifiedV1;
        Assert.That(payload, Is.Not.Null);
        Assert.That(payload!.VerifyingProviderId, Is.EqualTo("PROV-2"));
        Assert.That(payload.VerifiedDateTime, Is.EqualTo(verifiedAt));

        LabTestState live = await Lab(labTestId).GetLabTestAsync();
        Assert.That(live.Status, Is.EqualTo("Completed"));
        Assert.That(await Stream(patientId).VerifyChainAsync(), Is.True);
    }

    [Test]
    public async Task VerifyResult_Idempotent_OnSecondVerify()
    {
        var (patientId, labTestId) = await OrderLabAsync();
        await Lab(labTestId).CollectSpecimenAsync(DateTime.UtcNow, null, null);
        await Lab(labTestId).RecordResultAsync(DateTime.UtcNow, "7.5", null, null, null, null);
        await Lab(labTestId).VerifyResultAsync("PROV-2", "Brown,Bob", DateTime.UtcNow);
        await WaitForStreamVersionAsync(patientId, expected: 4);

        await Lab(labTestId).VerifyResultAsync("PROV-3", "Other,Doc", DateTime.UtcNow);
        await Task.Delay(150);

        Assert.That(await Stream(patientId).GetVersionAsync(), Is.EqualTo(4));
        LabTestState live = await Lab(labTestId).GetLabTestAsync();
        Assert.That(live.VerifyingProviderId, Is.EqualTo("PROV-2"));
    }

    [Test]
    public async Task ReplayUntilAsync_AfterFullLifecycle_ReproducesLiveState()
    {
        var (patientId, labTestId) = await OrderLabAsync();
        await Lab(labTestId).CollectSpecimenAsync(DateTime.UtcNow, "LAVENDER", "LAB-A");
        await Lab(labTestId).RecordResultAsync(
            DateTime.UtcNow, "9.2", "K/cmm", "4.0", "11.0", "Normal");
        await Lab(labTestId).VerifyResultAsync("PROV-2", "Brown,Bob", DateTime.UtcNow);
        await WaitForStreamVersionAsync(patientId, expected: 4);

        PatientStateSnapshot replayed =
            await Stream(patientId).ReplayUntilAsync(DateTime.UtcNow);
        Assert.That(replayed.LabTests, Has.Count.EqualTo(1));

        LabTestState fromChain = replayed.LabTests[0];
        Assert.That(fromChain.LabTestId, Is.EqualTo(labTestId));
        Assert.That(fromChain.TestName, Is.EqualTo("Complete Blood Count"));
        Assert.That(fromChain.ResultValue, Is.EqualTo("9.2"));
        Assert.That(fromChain.AbnormalFlag, Is.EqualTo("Normal"));
        Assert.That(fromChain.Status, Is.EqualTo("Completed"));
        Assert.That(fromChain.VerifyingProviderName, Is.EqualTo("Brown,Bob"));
    }

    [Test]
    public async Task ReplayUntilAsync_PointInTime_BeforeVerification_ShowsPending()
    {
        var (patientId, labTestId) = await OrderLabAsync();
        await Lab(labTestId).CollectSpecimenAsync(DateTime.UtcNow, "LAVENDER", "LAB-A");
        await Lab(labTestId).RecordResultAsync(
            DateTime.UtcNow, "9.2", "K/cmm", "4.0", "11.0", "Normal");

        await Task.Delay(150);
        DateTime tBeforeVerify = DateTime.UtcNow;
        await Task.Delay(150);

        await Lab(labTestId).VerifyResultAsync("PROV-2", "Brown,Bob", DateTime.UtcNow);
        await WaitForStreamVersionAsync(patientId, expected: 4);

        PatientStateSnapshot before =
            await Stream(patientId).ReplayUntilAsync(tBeforeVerify);
        Assert.That(before.LabTests[0].Status, Is.EqualTo("Pending"));
        Assert.That(before.LabTests[0].VerifyingProviderId, Is.Null);

        PatientStateSnapshot after =
            await Stream(patientId).ReplayUntilAsync(DateTime.UtcNow);
        Assert.That(after.LabTests[0].Status, Is.EqualTo("Completed"));
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
