// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Manages a single home health visit record.
/// Key pattern: "HHC-VISIT:{guid}".
/// VistA File #750.1 (HOME HEALTH VISIT). HBVISIT.m
/// </summary>
public interface IHHCVisitGrain : IGrainWithStringKey
{
    Task ScheduleVisitAsync(
        string patientId,
        string patientName,
        DateTime visitDate,
        HHCVisitDiscipline discipline,
        HHCVisitType visitType,
        string clinicianId,
        string clinicianName,
        string notes);

    Task CompleteVisitAsync(
        int durationMinutes,
        string vitalSigns,
        List<string> interventions,
        string patientResponse,
        string goalsProgress,
        DateTime? nextVisitDate,
        string notes);

    Task CancelVisitAsync(string cancellationReason);
    Task MarkNoAnswerAsync();
    Task MarkPatientRefusedAsync(string notes);
    Task<HHCVisitState> GetVisitAsync();
}
