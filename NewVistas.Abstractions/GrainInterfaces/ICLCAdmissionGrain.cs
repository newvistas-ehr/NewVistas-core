// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Manages a single Community Living Center admission.
/// Key pattern: "CLC-ADMIT:{guid}".
/// VistA GEC File #25.1 (COMMUNITY LIVING CENTER). GECCLC.m
/// </summary>
public interface ICLCAdmissionGrain : IGrainWithStringKey
{
    Task AdmitPatientAsync(
        string patientId,
        string patientName,
        DateTime? patientDOB,
        DateTime admitDate,
        CLCAdmitSource admitSource,
        GECLevelOfCare levelOfCare,
        string ward,
        string bedRoom,
        string attendingPhysician,
        string primaryDiagnosis,
        string referringFacility,
        DateTime? anticipatedDischargeDate,
        string notes);

    Task UpdateLevelOfCareAsync(GECLevelOfCare levelOfCare);
    Task UpdateBedAssignmentAsync(string ward, string bedRoom);
    Task MarkOnLeaveAsync();
    Task ReturnFromLeaveAsync();
    Task DischargePatientAsync(CLCDischargeDestination destination, string dischargeNotes);
    Task MarkDeceasedAsync(string notes);
    Task<CLCAdmissionState> GetAdmissionAsync();
}
