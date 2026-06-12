// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Optional feature grain for structured anesthesia record tracking during surgery.
/// Enabled per site via ISiteParametersGrain.Features containing "ANESTHESIA_TRACKING".
/// Follows the Site Flavor Architecture (Option 4 — Composition).
///
/// VistA Surgery (File #130) captures basic anesthesia type but lacks structured
/// intraoperative anesthesia documentation. This grain provides a full anesthesia
/// record: pre-op assessment, agents/doses, airway management, intraop vitals,
/// events, and PACU handoff — the data an anesthesiologist needs to document.
///
/// Keyed by record ID (e.g., "ANES:{guid}").
/// </summary>
public interface IAnesthesiaRecordGrain : IGrainWithStringKey
{
    Task<AnesthesiaRecordState> GetRecordAsync();

    Task<AnesthesiaRecordState> CreateRecordAsync(
        string patientId,
        string patientName,
        string surgeryId,
        string procedureName,
        string anesthesiaType,
        string anesthesiologistId,
        string anesthesiologistName,
        string asaClassification,
        string? airwayClass,
        string? preOpNotes);

    Task AddAgentAsync(AnesthesiaAgent agent);
    Task RecordAirwayManagementAsync(string airwayDevice, string? airwaySize, string? airwayNotes, string performedByName);
    Task RecordVitalsAsync(AnesthesiaVitalEntry vitals);
    Task RecordEventAsync(string eventType, string description, string recordedByName);
    Task RecordInductionAsync(DateTime inductionTime, string inductionMethod, string performedByName);
    Task RecordEmergenceAsync(DateTime emergenceTime, string? emergenceNotes, string performedByName);
    Task RecordPacuHandoffAsync(string pacuNurse, int aldretScore, string? handoffNotes);
    Task FinalizeRecordAsync(string finalizedByName);
    Task AddendRecordAsync(string addendumNote, string addendedByName);
}

/// <summary>
/// System-level index for anesthesia records. Singleton "ANES-IDX".
/// </summary>
public interface IAnesthesiaRecordIndexGrain : IGrainWithStringKey
{
    Task AddOrUpdateAsync(AnesthesiaRecordIndexEntry entry);
    Task<List<AnesthesiaRecordIndexEntry>> GetByPatientAsync(string patientId, int maxResults = 50);
    Task<List<AnesthesiaRecordIndexEntry>> GetByAnesthesiologistAsync(string anesthesiologistId, int maxResults = 50);
    Task<List<AnesthesiaRecordIndexEntry>> GetByStatusAsync(string status, int maxResults = 50);
    Task<List<AnesthesiaRecordIndexEntry>> SearchAsync(string? patientId, string? anesthesiologistId, string? status, string? anesthesiaType, int maxResults = 50);
    Task<int> GetCountAsync();
}
