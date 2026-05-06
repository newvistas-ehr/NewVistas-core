// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Radiology Grain Interface based on VistA RAD/NUC MED ORDERS file (#75.1)
/// </summary>
public interface IRadiologyGrain : IGrainWithStringKey
{
    Task<GrainStates.RadiologyState> GetRadiologyAsync();

    Task OrderStudyAsync(
        string patientId,
        string procedureName,
        string? procedureId,
        string? cptCode,
        string? imagingType,
        string? requestingProviderId,
        string? requestingProviderName,
        string? urgency,
        string? clinicalHistory,
        string? reasonForStudy,
        string? orderId,
        string? locationId,
        string? locationName);

    Task RecordExamAsync(DateTime examDateTime);

    Task RecordReportAsync(
        string reportText,
        string? impression,
        string? diagnosticCode,
        string? interpretingPhysicianId,
        string? interpretingPhysicianName,
        DateTime reportDateTime);

    Task CompleteAsync();
    Task CancelAsync();

    Task RecordContrastAsync(string contrastAgent, string? route, double? volumeMl);
    Task RecordContrastReactionAsync(string reactionDetails);
    Task RecordRadiationDoseAsync(double? doseMSv, double? ctdiVol, double? doseLengthProduct);
    Task SignReportAsync(string signedById, string signedByName, DateTime signedDateTime);
    Task FlagCriticalResultAsync();
    Task RecordCriticalResultNotificationAsync(string notifiedTo, DateTime notifiedDateTime);
    Task AcknowledgeCriticalResultAsync(string acknowledgedBy);
    Task RecordTechnicianAsync(string technicianId, string technicianName);
    Task SetBodyPartAndLateralityAsync(string? bodyPart, string? laterality);
    Task AddPriorStudyAsync(string priorStudyId);
    Task AmendReportAsync(string amendmentText);
    Task<bool> IsCriticalResultAsync();
}
