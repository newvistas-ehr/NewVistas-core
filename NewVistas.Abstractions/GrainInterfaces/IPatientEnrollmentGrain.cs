// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Manages a patient's VA healthcare enrollment record (VistA File #27.11 PATIENT ENROLLMENT).
/// Key: <c>"ENROLLMENT:{patientId}"</c>
/// MUMPS references: DGENELA.m, DGENELB.m, DGENUPL.m
/// </summary>
public interface IPatientEnrollmentGrain : IGrainWithStringKey
{
    /// <summary>Returns the current enrollment record.</summary>
    Task<PatientEnrollmentState> GetAsync();

    /// <summary>
    /// Initializes the enrollment record for a newly enrolling patient.
    /// No-op if already initialized.
    /// </summary>
    Task InitializeAsync(
        string patientId,
        string? enrollmentSource,
        string? enrollmentSiteId,
        string? enrollmentSiteName);

    /// <summary>Updates the enrollment status and records the status change.</summary>
    Task UpdateStatusAsync(EnrollmentStatus status, string changedByUserId, string? notes);

    /// <summary>Sets the patient's enrollment priority group and copay exemption flags.</summary>
    Task SetPriorityGroupAsync(
        string priorityGroup,
        string? prioritySubgroup,
        bool meansTestRequired,
        bool copayExempt,
        string? copayExemptionReason);

    /// <summary>Sets application, effective, and/or termination dates.</summary>
    Task SetDatesAsync(DateTime? applicationDate, DateTime? effectiveDate, DateTime? terminationDate);
}
