// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Per-patient polytrauma record grain — key: "PT-RECORD:{patientId}"
/// Tracks injuries, ISS, TBI status, and VA Polytrauma System of Care enrollment.
/// </summary>
public interface IPolytraumaRecordGrain : IGrainWithStringKey
{
    Task<PolytraumaRecordState> GetRecordAsync();

    Task RegisterPatientAsync(
        string patientId,
        string patientName,
        DateTime? dateOfBirth,
        TraumaMechanism traumaMechanism,
        DateTime? traumaDate,
        string traumaLocation,
        string polytraumaNetworkSite,
        string referralSource,
        string primaryTeamId,
        string primaryTeamName,
        string caseManagerId,
        string caseManagerName,
        string? notes);

    Task AddInjuryAsync(PolytraumaInjury injury);

    Task UpdateStatusAsync(PolytraumaStatus status, DateTime? deactivationDate);

    Task UpdateTBIStatusAsync(bool hasTBI, TBISeverity? severity);

    Task UpdateIssScoreAsync(int issScore);
}
