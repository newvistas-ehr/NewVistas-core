// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Grain for a mass casualty incident (MCI) event — disaster operations coordination.
/// Enabled per site via ISiteParametersGrain.Features containing "MASS_CASUALTY".
/// Follows the Site Flavor Architecture (Option 4 — Composition).
///
/// Maps to VA/DoD emergency management protocols and IHS RPMS disaster response.
/// Manages the incident lifecycle, triage tracking, and resource coordination
/// during mass casualty events affecting the ED.
/// Keyed by incident ID (e.g., "MCI:{guid}").
/// </summary>
public interface IMassCasualtyIncidentGrain : IGrainWithStringKey
{
    Task<MassCasualtyIncidentState> GetIncidentAsync();

    Task<MassCasualtyIncidentState> ActivateAsync(
        string incidentName,
        string incidentType,
        string severity,
        string activatedByName,
        string? description,
        int? estimatedCasualties);

    Task UpdateSeverityAsync(string severity, string updatedByName);
    Task UpdateEstimatedCasualtiesAsync(int estimatedCasualties, string updatedByName);
    Task AddStatusUpdateAsync(string message, string authorName);
    Task DeactivateAsync(string deactivatedByName, string? afterActionNotes);
}

/// <summary>
/// Grain for tracking a single casualty/patient within a mass casualty incident.
/// Keyed by "MCI-CASUALTY:{guid}".
/// </summary>
public interface IMassCasualtyCasualtyGrain : IGrainWithStringKey
{
    Task<MassCasualtyCasualtyState> GetCasualtyAsync();

    Task<MassCasualtyCasualtyState> RegisterCasualtyAsync(
        string incidentId,
        string triageTag,
        string triageCategory,
        string? patientId,
        string? patientName,
        string? chiefInjury,
        string? arrivalMode,
        string registeredByName);

    Task UpdateTriageCategoryAsync(string triageCategory, string updatedByName);
    Task AssignToAreaAsync(string treatmentArea, string assignedByName);
    Task UpdateDispositionAsync(string disposition, string updatedByName);
    Task LinkPatientAsync(string patientId, string patientName);
    Task AddNoteAsync(string note, string authorName);
}

/// <summary>
/// System-level index for mass casualty incidents. Singleton "MCI-IDX".
/// </summary>
public interface IMassCasualtyIncidentIndexGrain : IGrainWithStringKey
{
    Task AddOrUpdateAsync(MassCasualtyIncidentIndexEntry entry);
    Task<List<MassCasualtyIncidentIndexEntry>> GetAllAsync(int maxResults = 50);
    Task<List<MassCasualtyIncidentIndexEntry>> GetActiveAsync();
    Task<List<MassCasualtyIncidentIndexEntry>> GetByStatusAsync(string status, int maxResults = 50);
    Task<int> GetCountAsync();
}

/// <summary>
/// System-level index for casualties within incidents. Singleton "MCI-CASUALTY-IDX".
/// </summary>
public interface IMassCasualtyCasualtyIndexGrain : IGrainWithStringKey
{
    Task AddOrUpdateAsync(MassCasualtyCasualtyIndexEntry entry);
    Task<List<MassCasualtyCasualtyIndexEntry>> GetByIncidentAsync(string incidentId);
    Task<List<MassCasualtyCasualtyIndexEntry>> GetByTriageCategoryAsync(string incidentId, string triageCategory);
    Task<List<MassCasualtyCasualtyIndexEntry>> GetByTreatmentAreaAsync(string incidentId, string treatmentArea);
    Task<List<MassCasualtyCasualtyIndexEntry>> SearchAsync(string? incidentId, string? triageCategory, string? disposition, int maxResults = 100);
    Task<int> GetCountByIncidentAsync(string incidentId);
}
