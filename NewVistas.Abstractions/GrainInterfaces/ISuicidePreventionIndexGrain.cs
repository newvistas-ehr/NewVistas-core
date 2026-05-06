// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Singleton site-wide index of all patients in the suicide prevention program.
/// Key: "SP-INDEX"
/// </summary>
public interface ISuicidePreventionIndexGrain : IGrainWithStringKey
{
    Task<List<PatientHighRiskSummary>> GetAllPatientsAsync();

    Task<List<PatientHighRiskSummary>> GetHighRiskPatientsAsync();

    Task UpsertPatientAsync(PatientHighRiskSummary summary);

    Task RemovePatientAsync(string patientId);
}
