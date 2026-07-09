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
/// ADR-003 inter-facility Transfer Center: request → accept(reserve bed) →
/// complete(discharge at sender + admission at receiver), decline, cancel-releases-
/// reservation, the lost-reservation re-reserve path, receiving-side control, and
/// the MPI/treating-facility/ADR-002 completion side-effects.
/// </summary>
[TestFixture]
public class InterfacilityTransferWorkflowTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup() => _cluster = SharedCluster.Instance;

    private IPatientWorkflowGrain Workflow(string patientId)
        => _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

    private IInpatientUnitGrain Unit(string inst, string unit)
        => _cluster.GrainFactory.GetGrain<IInpatientUnitGrain>($"UNIT:{inst}:{unit}");

    private ITransferCenterGrain Center(string inst)
        => _cluster.GrainFactory.GetGrain<ITransferCenterGrain>($"TRANSFER-CENTER:{inst}");

    /// <summary>Two institutions (sender + receiver), a unit + beds at each, and a patient admitted at the sender.</summary>
    private async Task<(string PatientId, string SenderId, string ReceiverId, string AdmissionId)> BuildScenarioAsync(
        bool receiverAcceptsTransfers = true)
    {
        string suffix = Guid.NewGuid().ToString("N")[..8];
        string senderId = $"SEND-{suffix}";
        string receiverId = $"RECV-{suffix}";

        await _cluster.GrainFactory.GetGrain<IInstitutionGrain>($"INST:{senderId}")
            .RegisterAsync($"SENDER HOSPITAL {suffix}", InstitutionType.Hospital, null,
                "TEST-HS", "TEST HEALTH SYSTEM", null, "Burlington", "MA", null, null,
                new[] { InstitutionCapabilities.Icu }, null);
        await _cluster.GrainFactory.GetGrain<IInstitutionGrain>($"INST:{receiverId}")
            .RegisterAsync($"RECEIVER HOSPITAL {suffix}", InstitutionType.Hospital, null,
                "TEST-HS", "TEST HEALTH SYSTEM", null, "Lawrence", "MA", null, null,
                new[] { InstitutionCapabilities.Telemetry }, null);
        if (!receiverAcceptsTransfers)
            await _cluster.GrainFactory.GetGrain<IInstitutionGrain>($"INST:{receiverId}")
                .SetAcceptsInboundTransfersAsync(false);

        IInpatientUnitGrain senderIcu = Unit(senderId, "ICU");
        await senderIcu.ConfigureUnitAsync("Sender ICU", "ICU", "CRITICAL CARE");
        await senderIcu.AddBedAsync("ICU-1", null, BedType.Icu);

        IInpatientUnitGrain receiverTele = Unit(receiverId, "TELE");
        await receiverTele.ConfigureUnitAsync("Receiver Telemetry", "Telemetry", "CARDIOLOGY");
        await receiverTele.AddBedAsync("T-1", null, BedType.Telemetry);
        await receiverTele.AddBedAsync("T-2", null, BedType.Telemetry);

        string patientId = $"XFER-PT-{suffix}";
        IPatientWorkflowGrain wf = Workflow(patientId);
        await wf.UpdateDemographicsAsync("TRANSFER,TESTPATIENT", "M", new DateTime(1960, 1, 1), "666777888");
        string admissionId = await wf.RecordAdmissionAsync(
            DateTime.UtcNow.AddDays(-1), senderId, "ICU", "ICU-1",
            "CRITICAL CARE", "DR-SENDER", "Dr Sender", "NSTEMI", null);

        return (patientId, senderId, receiverId, admissionId);
    }

    private Task<string> RequestAsync(string patientId, string senderId, string receiverId, string admissionId)
        => Workflow(patientId).RequestInterfacilityTransferAsync(
            senderId, "ICU", admissionId, "DR-SENDER", "Dr Sender",
            receiverId, "TELEMETRY", BedType.Telemetry, BedIsolationType.None,
            "URGENT", "Stable NSTEMI needing telemetry.", "Step-down closer to home.");

    // ─────────────────────────── Lifecycle ───────────────────────────

    [Test]
    public async Task Transfer_HappyPath_RequestAcceptComplete()
    {
        var (patientId, senderId, receiverId, admissionId) = await BuildScenarioAsync();

        // Request → REQUESTED, visible in both centers.
        string transferId = await RequestAsync(patientId, senderId, receiverId, admissionId);
        TransferRequestState xfer = await Workflow(patientId).GetInterfacilityTransferAsync(transferId);
        Assert.That(xfer.Status, Is.EqualTo(TransferRequestStatus.Requested));
        Assert.That((await Center(receiverId).GetIncomingAsync()).Select(e => e.TransferId), Contains.Item(transferId));
        Assert.That((await Center(senderId).GetOutgoingAsync()).Select(e => e.TransferId), Contains.Item(transferId));
        Assert.That(await Center(receiverId).GetPendingIncomingCountAsync(), Is.GreaterThanOrEqualTo(1));

        // Accept → bed reserved for the patient, status ACCEPTED.
        await Workflow(patientId).AcceptInterfacilityTransferAsync(transferId, receiverId, "TELE", "T-1");
        xfer = await Workflow(patientId).GetInterfacilityTransferAsync(transferId);
        Assert.That(xfer.Status, Is.EqualTo(TransferRequestStatus.Accepted));
        Assert.That(xfer.ReservedBedId, Is.EqualTo("T-1"));
        InpatientBed reserved = (await Unit(receiverId, "TELE").GetAsync()).Beds.Single(b => b.BedId == "T-1");
        Assert.That(reserved.State, Is.EqualTo(BedLifecycleState.Reserved));
        Assert.That(reserved.ReservedForPatientId, Is.EqualTo(patientId));

        // Complete → discharge at sender (disposition TRANSFER), admission at receiver,
        // receiver bed Occupied, sender bed Dirty, receiver census contains the patient.
        DateTime arrival = DateTime.UtcNow;
        string admissionAdtId = await Workflow(patientId).CompleteInterfacilityTransferAsync(
            transferId, arrival, "DR-RECEIVER", "Dr Receiver", "NSTEMI, stable");
        xfer = await Workflow(patientId).GetInterfacilityTransferAsync(transferId);
        Assert.That(xfer.Status, Is.EqualTo(TransferRequestStatus.Completed));
        Assert.That(xfer.AdmissionAdtId, Is.EqualTo(admissionAdtId));

        AdtState discharge = await _cluster.GrainFactory.GetGrain<IAdtGrain>(admissionId).GetMovementAsync();
        Assert.That(discharge.Disposition, Is.EqualTo("TRANSFER"));
        Assert.That(discharge.DischargeDateTime, Is.Not.Null);

        AdtState admission = await _cluster.GrainFactory.GetGrain<IAdtGrain>(admissionAdtId).GetMovementAsync();
        Assert.That(admission.InstitutionId, Is.EqualTo(receiverId));
        Assert.That(admission.WardLocationId, Is.EqualTo("TELE"));
        Assert.That(admission.RoomBed, Is.EqualTo("T-1"));

        InpatientUnitState receiverUnit = await Unit(receiverId, "TELE").GetAsync();
        Assert.That(receiverUnit.Beds.Single(b => b.BedId == "T-1").State, Is.EqualTo(BedLifecycleState.Occupied));
        Assert.That(receiverUnit.Beds.Single(b => b.BedId == "T-1").PatientId, Is.EqualTo(patientId));
        List<UnitCensusEntry> receiverCensus = await Unit(receiverId, "TELE").GetCensusAsync();
        Assert.That(receiverCensus.Select(c => c.PatientId), Contains.Item(patientId));

        InpatientUnitState senderUnit = await Unit(senderId, "ICU").GetAsync();
        Assert.That(senderUnit.Beds.Single(b => b.BedId == "ICU-1").State, Is.EqualTo(BedLifecycleState.Dirty));
        Assert.That((await Unit(senderId, "ICU").GetCensusAsync()), Is.Empty);
    }

    [Test]
    public async Task Transfer_Completion_SideEffects_TreatingFacility_And_AttendingRelationship()
    {
        var (patientId, senderId, receiverId, admissionId) = await BuildScenarioAsync();
        string transferId = await RequestAsync(patientId, senderId, receiverId, admissionId);
        await Workflow(patientId).AcceptInterfacilityTransferAsync(transferId, receiverId, "TELE", "T-1");
        await Workflow(patientId).CompleteInterfacilityTransferAsync(
            transferId, DateTime.UtcNow, "DR-RECEIVER", "Dr Receiver", null);

        // File #391.91: the receiving institution is now an active INPATIENT treating facility.
        List<TreatingFacilityEntry> treating = await _cluster.GrainFactory
            .GetGrain<ITreatingFacilityListGrain>($"TREATING-FAC:{patientId}").GetActiveFacilitiesAsync();
        Assert.That(treating.Select(t => t.FacilityId), Contains.Item(receiverId));
        Assert.That(treating.Single(t => t.FacilityId == receiverId).RelationshipType, Is.EqualTo("INPATIENT"));

        // ADR-002: the receiving attending holds a treatment relationship (no break-the-glass).
        PatientAccessDecision d = await Workflow(patientId).AccessPatientAsync("DR-RECEIVER", "Dr Receiver",
            breakTheGlassAttested: false, justification: null);
        Assert.That(d.Granted, Is.True);
        Assert.That(d.WasBreakTheGlass, Is.False);
    }

    [Test]
    public async Task Transfer_Decline_IsTerminal()
    {
        var (patientId, senderId, receiverId, admissionId) = await BuildScenarioAsync();
        string transferId = await RequestAsync(patientId, senderId, receiverId, admissionId);

        await Workflow(patientId).DeclineInterfacilityTransferAsync(transferId, receiverId, "No telemetry capacity tonight.");

        TransferRequestState xfer = await Workflow(patientId).GetInterfacilityTransferAsync(transferId);
        Assert.That(xfer.Status, Is.EqualTo(TransferRequestStatus.Declined));
        Assert.That(xfer.DeclineReason, Does.Contain("capacity"));
        Assert.That(xfer.Timeline.Last().Status, Is.EqualTo(TransferRequestStatus.Declined));

        // A declined request cannot be accepted.
        Assert.ThrowsAsync<InvalidOperationException>(() =>
            Workflow(patientId).AcceptInterfacilityTransferAsync(transferId, receiverId, "TELE", "T-1"));

        // No reservation was ever made.
        Assert.That((await Unit(receiverId, "TELE").GetAsync()).Beds.All(b => b.State == BedLifecycleState.Available));
    }

    [Test]
    public async Task Transfer_CancelAfterAccept_ReleasesReservation()
    {
        var (patientId, senderId, receiverId, admissionId) = await BuildScenarioAsync();
        string transferId = await RequestAsync(patientId, senderId, receiverId, admissionId);
        await Workflow(patientId).AcceptInterfacilityTransferAsync(transferId, receiverId, "TELE", "T-1");
        Assert.That((await Unit(receiverId, "TELE").GetAsync()).Beds.Single(b => b.BedId == "T-1").State,
            Is.EqualTo(BedLifecycleState.Reserved));

        await Workflow(patientId).CancelInterfacilityTransferAsync(transferId, "Patient improved; staying at sender.");

        TransferRequestState xfer = await Workflow(patientId).GetInterfacilityTransferAsync(transferId);
        Assert.That(xfer.Status, Is.EqualTo(TransferRequestStatus.Cancelled));
        Assert.That((await Unit(receiverId, "TELE").GetAsync()).Beds.Single(b => b.BedId == "T-1").State,
            Is.EqualTo(BedLifecycleState.Available));
    }

    [Test]
    public async Task Transfer_LostReservation_ReassignBed_ThenCompleteSucceeds()
    {
        var (patientId, senderId, receiverId, admissionId) = await BuildScenarioAsync();
        string transferId = await RequestAsync(patientId, senderId, receiverId, admissionId);
        await Workflow(patientId).AcceptInterfacilityTransferAsync(transferId, receiverId, "TELE", "T-1");

        // Another patient takes the reserved bed with a bed-control override.
        await Unit(receiverId, "TELE").AdmitPatientAsync(new UnitAdmissionRequest
        {
            PatientId = $"USURPER-{Guid.NewGuid():N}",
            PatientName = "USURPER,URGENT",
            MovementId = $"ADT-{Guid.NewGuid()}",
            BedId = "T-1",
            AdmitDate = DateTime.UtcNow,
            OverrideReservation = true
        });

        // Completion fails; the transfer stays ACCEPTED (re-reserve path open).
        Assert.ThrowsAsync<InvalidOperationException>(() =>
            Workflow(patientId).CompleteInterfacilityTransferAsync(transferId, DateTime.UtcNow, null, null, null));
        TransferRequestState xfer = await Workflow(patientId).GetInterfacilityTransferAsync(transferId);
        Assert.That(xfer.Status, Is.EqualTo(TransferRequestStatus.Accepted));

        // Re-reserve onto T-2 and complete.
        await Workflow(patientId).ReassignTransferBedAsync(transferId, "TELE", "T-2");
        string admissionAdtId = await Workflow(patientId).CompleteInterfacilityTransferAsync(
            transferId, DateTime.UtcNow, "DR-RECEIVER", "Dr Receiver", null);

        Assert.That(admissionAdtId, Is.Not.Empty);
        InpatientUnitState unit = await Unit(receiverId, "TELE").GetAsync();
        Assert.That(unit.Beds.Single(b => b.BedId == "T-2").PatientId, Is.EqualTo(patientId));
    }

    // ─────────────────────────── Guards + idempotency ───────────────────────────

    [Test]
    public async Task Transfer_Accept_FromWrongInstitution_Rejected()
    {
        var (patientId, senderId, receiverId, admissionId) = await BuildScenarioAsync();
        string transferId = await RequestAsync(patientId, senderId, receiverId, admissionId);

        // The SENDER (or anyone but the receiver) cannot accept.
        Assert.ThrowsAsync<InvalidOperationException>(() =>
            Workflow(patientId).AcceptInterfacilityTransferAsync(transferId, senderId, "TELE", "T-1"));

        Assert.That((await Workflow(patientId).GetInterfacilityTransferAsync(transferId)).Status,
            Is.EqualTo(TransferRequestStatus.Requested));
    }

    [Test]
    public async Task Transfer_ReceiverNotAcceptingTransfers_RequestRejected()
    {
        var (patientId, senderId, receiverId, admissionId) = await BuildScenarioAsync(receiverAcceptsTransfers: false);

        Assert.ThrowsAsync<InvalidOperationException>(() =>
            RequestAsync(patientId, senderId, receiverId, admissionId));
    }

    [Test]
    public async Task Transfer_SameInstitution_Rejected()
    {
        var (patientId, senderId, _, admissionId) = await BuildScenarioAsync();

        Assert.ThrowsAsync<InvalidOperationException>(() =>
            Workflow(patientId).RequestInterfacilityTransferAsync(
                senderId, "ICU", admissionId, null, null, senderId,
                null, null, BedIsolationType.None, "ROUTINE", null, null));
    }

    [Test]
    public async Task Transfer_Idempotency_DoubleAcceptSameBed_And_DoubleComplete()
    {
        var (patientId, senderId, receiverId, admissionId) = await BuildScenarioAsync();
        string transferId = await RequestAsync(patientId, senderId, receiverId, admissionId);

        await Workflow(patientId).AcceptInterfacilityTransferAsync(transferId, receiverId, "TELE", "T-1");
        await Workflow(patientId).AcceptInterfacilityTransferAsync(transferId, receiverId, "TELE", "T-1"); // no-op

        string first = await Workflow(patientId).CompleteInterfacilityTransferAsync(
            transferId, DateTime.UtcNow, "DR-RECEIVER", "Dr Receiver", null);
        string second = await Workflow(patientId).CompleteInterfacilityTransferAsync(
            transferId, DateTime.UtcNow, "DR-RECEIVER", "Dr Receiver", null); // idempotent

        Assert.That(second, Is.EqualTo(first));
        // Exactly one admission movement at the receiver (no double placement).
        List<AdtSummary> movements = await Workflow(patientId).GetAdtMovementsAsync();
        Assert.That(movements.Count(m => m.MovementId == first), Is.EqualTo(1));
    }
}
