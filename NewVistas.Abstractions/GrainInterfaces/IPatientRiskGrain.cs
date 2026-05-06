// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Per-patient grain tracking suicide risk level, high-risk flag, and follow-up contacts.
/// Key pattern: "SP-RISK:{patientId}"
/// </summary>
public interface IPatientRiskGrain : IGrainWithStringKey
{
    Task<PatientRiskState> GetRiskStateAsync();

    Task SetRiskLevelAsync(RiskLevel level, string patientId, string patientName, string providerId, string providerName);

    Task SetHighRiskFlagAsync(bool flagged);

    Task AddFollowUpContactAsync(FollowUpContact contact);
}
