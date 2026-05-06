// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Grain representing a patient's transplant registration record.
/// Grain key: "TX-PATIENT:{patientId}"
/// </summary>
public interface ITransplantPatientGrain : IGrainWithStringKey
{
    /// <summary>Returns the full transplant patient state.</summary>
    Task<TransplantPatientState> GetPatientAsync();

    /// <summary>Registers a patient on the transplant waiting list.</summary>
    Task RegisterPatientAsync(
        string patientId,
        string patientName,
        DateTime? dateOfBirth,
        TransplantOrganType organType,
        TransplantPriority priority,
        BloodType bloodType,
        string? hlaTyping,
        decimal? panelReactiveAntibodyPct,
        string primaryDiagnosis,
        string? diagnosisCode,
        decimal? weightKg,
        decimal? heightCm,
        decimal? meldScore,
        string locationId,
        string locationName,
        string? referringProviderId,
        string? referringProviderName,
        string? notes);

    /// <summary>Updates the patient's waitlist status (e.g., Listed, OnHold, Removed).</summary>
    Task UpdateStatusAsync(TransplantStatus status, string? reason);

    /// <summary>Updates the patient's urgency priority tier.</summary>
    Task UpdatePriorityAsync(TransplantPriority priority);

    /// <summary>Updates the patient's calculated MELD/PELD score.</summary>
    Task UpdateMeldScoreAsync(decimal meldScore);

    /// <summary>Records that the transplant has been performed.</summary>
    Task RecordTransplantAsync(string donorId, string surgeonId, string surgeonName, DateTime transplantDate);
}
