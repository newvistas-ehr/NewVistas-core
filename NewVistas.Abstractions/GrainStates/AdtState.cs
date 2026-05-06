// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;
using NewVistas.Abstractions.EventSourcing;

namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// Represents the state of an ADT Episode grain based on VistA PATIENT MOVEMENT file (#405)
/// Admissions, Discharges, Transfers, and treating specialty history.
///
/// Inherits the clinical event-sourcing outbox from <see cref="EventSourcedStateBase"/>
/// so that causal events for this movement are persisted atomically with the
/// projection in a single <c>WriteStateAsync</c>.
/// </summary>
[GenerateSerializer]
public class AdtState : EventSourcedStateBase
{
    [Id(0)]
    public string MovementId { get; set; } = string.Empty;

    [Id(1)]
    public string PatientId { get; set; } = string.Empty;

    /// <summary>
    /// Transaction Type (.02) � ADMISSION, DISCHARGE, TRANSFER, TREATING SPECIALTY
    /// </summary>
    [Id(2)]
    public string TransactionType { get; set; } = string.Empty;

    /// <summary>
    /// Movement Date/Time (.01)
    /// </summary>
    [Id(3)]
    public DateTime MovementDateTime { get; set; }

    /// <summary>
    /// Ward Location (.06) � pointer to WARD LOCATION file (#42)
    /// </summary>
    [Id(4)]
    public string? WardLocationId { get; set; }

    [Id(5)]
    public string? WardLocationName { get; set; }

    /// <summary>
    /// Room-Bed (.04)
    /// </summary>
    [Id(6)]
    public string? RoomBed { get; set; }

    /// <summary>
    /// Treating Specialty (.05) � pointer to FACILITY TREATING SPECIALTY file (#45.7)
    /// </summary>
    [Id(7)]
    public string? TreatingSpecialtyId { get; set; }

    [Id(8)]
    public string? TreatingSpecialtyName { get; set; }

    /// <summary>
    /// Attending Physician (.18)
    /// </summary>
    [Id(9)]
    public string? AttendingPhysicianId { get; set; }

    [Id(10)]
    public string? AttendingPhysicianName { get; set; }

    /// <summary>
    /// Admission Date/Time � for tracking the full episode
    /// </summary>
    [Id(11)]
    public DateTime? AdmissionDateTime { get; set; }

    /// <summary>
    /// Discharge Date/Time
    /// </summary>
    [Id(12)]
    public DateTime? DischargeDateTime { get; set; }

    /// <summary>
    /// Type of Patient (.14) � e.g., INPATIENT, OBS PATIENT
    /// </summary>
    [Id(13)]
    public string? TypeOfPatient { get; set; }

    /// <summary>
    /// Admission Diagnosis
    /// </summary>
    [Id(14)]
    public string? AdmissionDiagnosis { get; set; }

    /// <summary>
    /// Discharge Diagnosis
    /// </summary>
    [Id(15)]
    public string? DischargeDiagnosis { get; set; }

    /// <summary>
    /// Disposition � REGULAR, AMA, DEATH, TRANSFER, etc.
    /// </summary>
    [Id(16)]
    public string? Disposition { get; set; }

    /// <summary>
    /// Length of Stay (days)
    /// </summary>
    [Id(17)]
    public int? LengthOfStay { get; set; }

    [Id(18)]
    public string? Comments { get; set; }

    [Id(19)]
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    [Id(20)]
    public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Deep copy. Used at event/state boundaries so that mutating the live
    /// movement on the grain does not retroactively mutate the historical
    /// snapshot stored on a clinical event payload.
    /// </summary>
    public AdtState Clone() => new()
    {
        MovementId = MovementId,
        PatientId = PatientId,
        TransactionType = TransactionType,
        MovementDateTime = MovementDateTime,
        WardLocationId = WardLocationId,
        WardLocationName = WardLocationName,
        RoomBed = RoomBed,
        TreatingSpecialtyId = TreatingSpecialtyId,
        TreatingSpecialtyName = TreatingSpecialtyName,
        AttendingPhysicianId = AttendingPhysicianId,
        AttendingPhysicianName = AttendingPhysicianName,
        AdmissionDateTime = AdmissionDateTime,
        DischargeDateTime = DischargeDateTime,
        TypeOfPatient = TypeOfPatient,
        AdmissionDiagnosis = AdmissionDiagnosis,
        DischargeDiagnosis = DischargeDiagnosis,
        Disposition = Disposition,
        LengthOfStay = LengthOfStay,
        Comments = Comments,
        CreatedDate = CreatedDate,
        LastModifiedDate = LastModifiedDate
    };
}
