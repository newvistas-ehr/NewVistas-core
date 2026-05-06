// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// ADT (Admission/Discharge/Transfer) Grain Interface based on VistA PATIENT MOVEMENT file (#405)
/// </summary>
public interface IAdtGrain : IGrainWithStringKey
{
    Task<GrainStates.AdtState> GetMovementAsync();

    Task RecordAdmissionAsync(
        string patientId,
        DateTime movementDateTime,
        string? wardLocationId,
        string? wardLocationName,
        string? roomBed,
        string? treatingSpecialtyId,
        string? treatingSpecialtyName,
        string? attendingPhysicianId,
        string? attendingPhysicianName,
        string? typeOfPatient,
        string? admissionDiagnosis,
        string? comments);

    Task RecordTransferAsync(
        string? wardLocationId,
        string? wardLocationName,
        string? roomBed,
        string? treatingSpecialtyId,
        string? treatingSpecialtyName,
        DateTime movementDateTime);

    /// <summary>
    /// Populates this grain as a new TRANSFER movement record.
    /// Carries admissionDateTime from the source grain so LengthOfStay can be computed at discharge.
    /// VistA PATIENT MOVEMENT (#405) — TransactionType = "TRANSFER"
    /// </summary>
    Task RecordAsTransferAsync(
        string patientId,
        DateTime admissionDateTime,
        DateTime transferDateTime,
        string? toWardLocationId,
        string? toWardLocationName,
        string? toRoomBed,
        string? toTreatingSpecialtyId,
        string? toTreatingSpecialtyName,
        string? attendingPhysicianId,
        string? attendingPhysicianName,
        string? comments);

    Task RecordDischargeAsync(
        DateTime dischargeDateTime,
        string? dischargeDiagnosis,
        string? disposition,
        string? comments);
}
