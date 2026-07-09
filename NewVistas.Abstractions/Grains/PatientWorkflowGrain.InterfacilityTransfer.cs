// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// Inter-facility transfer orchestration (the transfer center) — "Lahey Burlington
/// places a patient into Lawrence General." The sending facility REQUESTS; the
/// receiving facility controls its own beds (ACCEPT reserves a specific bed);
/// arrival COMPLETES: admission at the receiver, discharge at the sender — both
/// direct partial-class calls on this ONE ICN-keyed grain (ADR-001), so no
/// cross-grain saga is needed.
/// </summary>
public partial class PatientWorkflowGrain
{
    private ITransferRequestGrain Xfer(string transferId)
        => GrainFactory.GetGrain<ITransferRequestGrain>(transferId);

    private ITransferCenterGrain Center(string institutionId)
        => GrainFactory.GetGrain<ITransferCenterGrain>($"TRANSFER-CENTER:{institutionId}");

    public async Task<string> RequestInterfacilityTransferAsync(
        string sendingInstitutionId, string? sendingUnitId, string? sendingAdmissionId,
        string? sendingAttendingId, string? sendingAttendingName,
        string receivingInstitutionId,
        string? requestedLevelOfCare, BedType? requestedBedType, BedIsolationType isolationRequired,
        string urgency, string? clinicalSummary, string? reasonForTransfer)
    {
        if (string.IsNullOrWhiteSpace(sendingInstitutionId) || string.IsNullOrWhiteSpace(receivingInstitutionId))
            throw new InvalidOperationException("Sending and receiving institutions are required.");
        if (sendingInstitutionId == receivingInstitutionId)
            throw new InvalidOperationException("An inter-facility transfer needs two different institutions (use ADT transfer within a facility).");

        IInstitutionIndexGrain index = GrainFactory.GetGrain<IInstitutionIndexGrain>("INSTITUTION-INDEX");
        List<InstitutionIndexEntry> institutions = await index.GetAllAsync();
        InstitutionIndexEntry? sender = institutions.FirstOrDefault(i => i.InstitutionId == sendingInstitutionId);
        InstitutionIndexEntry? receiver = institutions.FirstOrDefault(i => i.InstitutionId == receivingInstitutionId)
            ?? throw new InvalidOperationException($"Unknown receiving institution '{receivingInstitutionId}'.");
        if (!receiver.AcceptsInboundTransfers)
            throw new InvalidOperationException($"{receiver.Name} is not accepting inbound transfers.");

        PatientState patient = await GetPatientGrain().GetPatientAsync();

        var transferId = $"XFER-{Guid.NewGuid()}";
        await Xfer(transferId).CreateAsync(
            PatientId, patient.Name,
            sendingInstitutionId, sender?.Name,
            sendingUnitId, sendingAdmissionId,
            sendingAttendingId, sendingAttendingName,
            receivingInstitutionId, receiver.Name,
            requestedLevelOfCare, requestedBedType, isolationRequired,
            urgency, clinicalSummary, reasonForTransfer);

        await SyncCentersAsync(transferId);
        return transferId;
    }

    public async Task AcceptInterfacilityTransferAsync(string transferId, string actingInstitutionId,
        string unitId, string bedId)
    {
        TransferRequestState xfer = await Xfer(transferId).GetAsync();
        if (string.IsNullOrEmpty(xfer.PatientId))
            throw new InvalidOperationException($"Transfer {transferId} does not exist.");

        // Receiving-side control (v1): the accept must come FROM the receiving
        // institution. Full per-facility RBAC is future work (ADR-003).
        if (actingInstitutionId != xfer.ReceivingInstitutionId)
            throw new InvalidOperationException(
                $"Only {xfer.ReceivingInstitutionName ?? xfer.ReceivingInstitutionId} can accept this transfer.");

        // Reserve FIRST, then flip status — a failed reservation leaves the request
        // REQUESTED and no orphan reservation exists on any race. The mirror-image
        // race (status went terminal between our read and the flip) is compensated
        // by releasing the fresh reservation.
        await Unit(xfer.ReceivingInstitutionId, unitId)
            .ReserveBedAsync(bedId, PatientId, xfer.PatientName ?? PatientId, null);
        try
        {
            await Xfer(transferId).AcceptAsync(unitId, bedId, null);
        }
        catch
        {
            await Unit(xfer.ReceivingInstitutionId, unitId).ClearReservationAsync(bedId);
            throw;
        }

        await SyncCentersAsync(transferId);
    }

    public async Task ReassignTransferBedAsync(string transferId, string newUnitId, string newBedId)
    {
        TransferRequestState xfer = await Xfer(transferId).GetAsync();
        if (xfer.Status != TransferRequestStatus.Accepted)
            throw new InvalidOperationException($"Transfer {transferId} is {xfer.Status} — no reservation to move.");

        // Reserve the new bed first; then release the old one (best-effort, idempotent).
        await Unit(xfer.ReceivingInstitutionId, newUnitId)
            .ReserveBedAsync(newBedId, PatientId, xfer.PatientName ?? PatientId, null);
        if (!string.IsNullOrEmpty(xfer.ReservedUnitId) && !string.IsNullOrEmpty(xfer.ReservedBedId)
            && (xfer.ReservedUnitId != newUnitId || xfer.ReservedBedId != newBedId))
            await Unit(xfer.ReceivingInstitutionId, xfer.ReservedUnitId).ClearReservationAsync(xfer.ReservedBedId);

        await Xfer(transferId).ReassignBedAsync(newUnitId, newBedId, null);
        await SyncCentersAsync(transferId);
    }

    public async Task DeclineInterfacilityTransferAsync(string transferId, string actingInstitutionId, string reason)
    {
        TransferRequestState xfer = await Xfer(transferId).GetAsync();
        if (string.IsNullOrEmpty(xfer.PatientId))
            throw new InvalidOperationException($"Transfer {transferId} does not exist.");
        if (actingInstitutionId != xfer.ReceivingInstitutionId)
            throw new InvalidOperationException(
                $"Only {xfer.ReceivingInstitutionName ?? xfer.ReceivingInstitutionId} can decline this transfer.");

        await Xfer(transferId).DeclineAsync(reason);
        await SyncCentersAsync(transferId);
    }

    public async Task CancelInterfacilityTransferAsync(string transferId, string? reason)
    {
        TransferRequestState xfer = await Xfer(transferId).GetAsync();

        // Cancel-after-accept releases the reservation (decline can never hold one).
        if (xfer.Status == TransferRequestStatus.Accepted
            && !string.IsNullOrEmpty(xfer.ReservedUnitId) && !string.IsNullOrEmpty(xfer.ReservedBedId))
            await Unit(xfer.ReceivingInstitutionId, xfer.ReservedUnitId).ClearReservationAsync(xfer.ReservedBedId);

        await Xfer(transferId).CancelAsync(reason);
        await SyncCentersAsync(transferId);
    }

    /// <summary>
    /// Patient arrived. Sequence: admission at the RECEIVER first (occupies the
    /// reserved bed — auto-clears the reservation, fires the ADR-002 attending hook,
    /// updates the census), THEN discharge at the sender (disposition TRANSFER,
    /// releases the old bed → Dirty). Admission-first means a failed admission leaves
    /// the patient safely admitted at the sender; the brief dual-census window
    /// self-heals because the sender release is idempotent. Finally the MPI/treating-
    /// facility records gain the receiving institution.
    /// Returns the new admission movement id.
    /// </summary>
    public async Task<string> CompleteInterfacilityTransferAsync(string transferId, DateTime arrivalDateTime,
        string? receivingAttendingId, string? receivingAttendingName, string? admissionDiagnosis)
    {
        TransferRequestState xfer = await Xfer(transferId).GetAsync();
        if (xfer.Status == TransferRequestStatus.Completed)
            return xfer.AdmissionAdtId ?? string.Empty; // idempotent
        if (xfer.Status != TransferRequestStatus.Accepted
            || string.IsNullOrEmpty(xfer.ReservedUnitId) || string.IsNullOrEmpty(xfer.ReservedBedId))
            throw new InvalidOperationException(
                $"Transfer {transferId} is {xfer.Status} — it must be ACCEPTED with a reserved bed before completion.");

        // 1. Admission at the receiver. If the reserved bed was lost (out of service,
        //    reservation expired and taken), this throws and the transfer stays
        //    ACCEPTED — use ReassignTransferBedAsync to re-reserve, then retry.
        string admissionAdtId = await RecordAdmissionAsync(
            arrivalDateTime, xfer.ReceivingInstitutionId, xfer.ReservedUnitId, xfer.ReservedBedId,
            xfer.RequestedLevelOfCare,
            receivingAttendingId, receivingAttendingName,
            admissionDiagnosis ?? xfer.ReasonForTransfer,
            $"Inter-facility transfer {transferId} from {xfer.SendingInstitutionName ?? xfer.SendingInstitutionId}.");

        // 2. Discharge at the sender (idempotent on retry; releases the old bed → Dirty).
        string? dischargeAdtId = xfer.SendingAdmissionId;
        if (!string.IsNullOrEmpty(dischargeAdtId))
            await RecordDischargeAsync(dischargeAdtId, arrivalDateTime, null, "TRANSFER",
                $"Inter-facility transfer {transferId} to {xfer.ReceivingInstitutionName ?? xfer.ReceivingInstitutionId}.");

        // 2b. RecordDischargeAsync cleared the current-admission pointer — restore it
        //     to the receiving admission (the patient IS admitted, at the receiver).
        InpatientUnitState receivingUnit = await Unit(xfer.ReceivingInstitutionId, xfer.ReservedUnitId).GetAsync();
        await GetPatientGrain().UpdateCurrentAdmissionAsync(admissionAdtId, xfer.ReservedBedId, receivingUnit.Name);

        // 3. MPI + treating-facility side-effects: the receiving institution now
        //    treats this patient (File #985 correlation + File #391.91 entry).
        PatientState patient = await GetPatientGrain().GetPatientAsync();
        if (!string.IsNullOrEmpty(patient.Icn))
        {
            IMpiCorrelationGrain mpi = GrainFactory.GetGrain<IMpiCorrelationGrain>($"MPI:{patient.Icn}");
            List<MpiLocalCorrelation> correlations = await mpi.GetTreatingFacilitiesAsync();
            if (correlations.Any(c => c.FacilityId == xfer.ReceivingInstitutionId))
                await mpi.UpdateLastSeenAsync(xfer.ReceivingInstitutionId, arrivalDateTime);
            else
                await mpi.AddLocalCorrelationAsync(xfer.ReceivingInstitutionId,
                    xfer.ReceivingInstitutionName ?? xfer.ReceivingInstitutionId, PatientId, arrivalDateTime);
        }
        await GrainFactory.GetGrain<ITreatingFacilityListGrain>($"TREATING-FAC:{PatientId}")
            .AddOrUpdateFacilityAsync(new TreatingFacilityEntry
            {
                FacilityId = xfer.ReceivingInstitutionId,
                FacilityName = xfer.ReceivingInstitutionName ?? xfer.ReceivingInstitutionId,
                FacilityType = "HOSPITAL",
                LastActivityDate = arrivalDateTime,
                IsActive = true,
                RelationshipType = "INPATIENT"
            });

        // 4. Close out the request + refresh both queues.
        await Xfer(transferId).CompleteAsync(arrivalDateTime, dischargeAdtId ?? string.Empty, admissionAdtId,
            receivingAttendingId, receivingAttendingName);
        await SyncCentersAsync(transferId);

        return admissionAdtId;
    }

    public async Task<TransferRequestState> GetInterfacilityTransferAsync(string transferId)
        => await Xfer(transferId).GetAsync();

    /// <summary>Push the request's current entry to both institutions' transfer-center queues.</summary>
    private async Task SyncCentersAsync(string transferId)
    {
        TransferRequestState xfer = await Xfer(transferId).GetAsync();
        var entry = new TransferRequestEntry
        {
            TransferId = xfer.TransferId,
            PatientId = xfer.PatientId,
            PatientName = xfer.PatientName,
            SendingInstitutionId = xfer.SendingInstitutionId,
            SendingInstitutionName = xfer.SendingInstitutionName,
            ReceivingInstitutionId = xfer.ReceivingInstitutionId,
            ReceivingInstitutionName = xfer.ReceivingInstitutionName,
            Urgency = xfer.Urgency,
            RequestedLevelOfCare = xfer.RequestedLevelOfCare,
            Status = xfer.Status,
            ReservedBedId = xfer.ReservedBedId,
            RequestDateTime = xfer.RequestDateTime,
            LastModifiedDate = xfer.LastModifiedDate
        };
        await Center(xfer.SendingInstitutionId).AddOrUpdateAsync(entry);
        await Center(xfer.ReceivingInstitutionId).AddOrUpdateAsync(entry);
    }
}
