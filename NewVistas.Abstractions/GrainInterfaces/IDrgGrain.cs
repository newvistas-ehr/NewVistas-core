// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// DRG Assignment Grain — manages DRG grouping for a single admission.
/// Key: "DRG:{admissionId}"
/// Maps to VistA ICD DRG Grouper.
/// </summary>
public interface IDrgAssignmentGrain : IGrainWithStringKey
{
    Task<DrgAssignmentState> GetAssignmentAsync();

    Task AssignDrgAsync(
        string patientId, string patientName,
        string principalDiagnosis, string principalDiagnosisDescription,
        List<string> secondaryDiagnoses, List<string> procedures,
        int patientAge, string patientSex,
        string dischargeStatus, int actualLOS);

    Task ReviewAsync(string reviewerId);
    Task FinalizeAsync();
    Task ReassignDrgAsync(string newDrgCode, string newDrgDescription, decimal newRelativeWeight, string reason);
}

/// <summary>
/// DRG Index Grain — facility-level coding worklist.
/// Key: "DRG-INDEX:{facilityId}"
/// </summary>
public interface IDrgIndexGrain : IGrainWithStringKey
{
    Task<List<DrgAssignmentEntry>> GetAllAssignmentsAsync();
    Task<List<DrgAssignmentEntry>> GetAssignmentsByStatusAsync(string status);
    Task AddOrUpdateAsync(DrgAssignmentEntry entry);
    Task RemoveAsync(string admissionId);
}
