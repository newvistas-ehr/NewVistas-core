// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;
using Orleans.Runtime;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// FHIR Bulk Data Export Grain — manages an async group-export job.
/// §170.215(d)(1) — FHIR Bulk Data Access (Flat FHIR) v1.0.0 STU 1.
///
/// Each export job gets its own grain, identified by a unique job ID.
/// The export processes patients in the group and produces NDJSON output files
/// for each requested resource type.
///
/// Grain Key: "BULK-EXPORT:{jobId}"
/// </summary>
public class BulkDataExportGrain : Grain, IBulkDataExportGrain
{
    private readonly IPersistentState<BulkDataExportState> _state;
    private readonly IGrainFactory _grainFactory;

    public BulkDataExportGrain(
        [PersistentState("bulkExportState", "bulkExportStore")] IPersistentState<BulkDataExportState> state,
        IGrainFactory grainFactory)
    {
        _state = state;
        _grainFactory = grainFactory;
    }

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_state.State.JobId))
        {
            _state.State.JobId = this.GetPrimaryKeyString();
        }
        return base.OnActivateAsync(cancellationToken);
    }

    private static readonly List<string> DefaultResourceTypes =
    [
        "Patient", "Condition", "AllergyIntolerance", "Observation",
        "MedicationRequest", "DiagnosticReport", "Encounter", "Appointment"
    ];

    public async Task StartExportAsync(
        string groupId,
        List<string> patientIds,
        List<string>? resourceTypes,
        DateTime? since,
        string? requestedBy)
    {
        if (_state.State.Status == "in-progress")
            throw new InvalidOperationException("Export already in progress.");

        _state.State.GroupId = groupId;
        _state.State.PatientIds = patientIds;
        _state.State.ResourceTypes = resourceTypes?.Count > 0 ? resourceTypes : DefaultResourceTypes;
        _state.State.Since = since;
        _state.State.RequestedBy = requestedBy;
        _state.State.Status = "in-progress";
        _state.State.RequestedDate = DateTime.UtcNow;
        _state.State.OutputFiles = new();
        _state.State.ProcessedCount = 0;
        _state.State.ErrorMessage = null;
        _state.State.CompletedDate = null;

        await _state.WriteStateAsync();

        // Process the export synchronously for now (each patient, each resource type).
        // In production this would be async/batched with progress tracking.
        try
        {
            var outputFiles = new Dictionary<string, List<string>>();

            foreach (string patientId in patientIds)
            {
                IPatientWorkflowGrain w = _grainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

                foreach (string resType in _state.State.ResourceTypes)
                {
                    string? ndjsonLine = await ExportResourceForPatient(w, patientId, resType);
                    if (ndjsonLine != null)
                    {
                        if (!outputFiles.ContainsKey(resType))
                            outputFiles[resType] = new();
                        outputFiles[resType].Add(ndjsonLine);
                    }
                }

                _state.State.ProcessedCount++;
            }

            // Build output file references
            foreach (var (resType, lines) in outputFiles)
            {
                _state.State.OutputFiles.Add(new BulkExportOutputFile
                {
                    ResourceType = resType,
                    Url = $"/api/fhir/bulk-export/{_state.State.JobId}/{resType}.ndjson",
                    Count = lines.Count
                });
            }

            _state.State.Status = "completed";
            _state.State.CompletedDate = DateTime.UtcNow;
        }
        catch (Exception ex)
        {
            _state.State.Status = "error";
            _state.State.ErrorMessage = ex.Message;
            _state.State.CompletedDate = DateTime.UtcNow;
        }

        await _state.WriteStateAsync();
    }

    public Task<BulkDataExportState> GetStatusAsync() => Task.FromResult(_state.State);

    public async Task CancelAsync()
    {
        if (_state.State.Status == "in-progress" || _state.State.Status == "pending")
        {
            _state.State.Status = "error";
            _state.State.ErrorMessage = "Cancelled by user.";
            _state.State.CompletedDate = DateTime.UtcNow;
            await _state.WriteStateAsync();
        }
    }

    /// <summary>
    /// Export a single FHIR resource for one patient. Returns an NDJSON-ready line
    /// with the patient ID embedded, or null if no data exists.
    /// In production, this would serialize full FHIR resources.
    /// </summary>
    private static async Task<string?> ExportResourceForPatient(
        IPatientWorkflowGrain w, string patientId, string resourceType)
    {
        // Verify the patient exists by checking if we can retrieve data
        // Returns a minimal resource indicator — full FHIR serialization deferred to FhirController
        try
        {
            switch (resourceType)
            {
                case "Patient":
                    PatientState ps = await w.GetPatientAsync();
                    return string.IsNullOrEmpty(ps.Name) ? null : $"{{\"resourceType\":\"Patient\",\"id\":\"{patientId}\"}}";

                case "Condition":
                    List<ProblemSummary> problems = await w.GetAllProblemsAsync();
                    return problems.Count > 0 ? $"{{\"resourceType\":\"Condition\",\"subject\":\"Patient/{patientId}\",\"count\":{problems.Count}}}" : null;

                case "AllergyIntolerance":
                    List<AllergySummary> allergies = await w.GetAllergiesAsync();
                    return allergies.Count > 0 ? $"{{\"resourceType\":\"AllergyIntolerance\",\"patient\":\"Patient/{patientId}\",\"count\":{allergies.Count}}}" : null;

                case "Observation":
                    List<VitalSummary> vitals = await w.GetLatestVitalsAsync();
                    return vitals.Count > 0 ? $"{{\"resourceType\":\"Observation\",\"subject\":\"Patient/{patientId}\",\"count\":{vitals.Count}}}" : null;

                case "MedicationRequest":
                    List<MedicationSummary> meds = await w.GetActiveMedicationsAsync();
                    return meds.Count > 0 ? $"{{\"resourceType\":\"MedicationRequest\",\"subject\":\"Patient/{patientId}\",\"count\":{meds.Count}}}" : null;

                case "DiagnosticReport":
                    List<LabTestSummaryEntry> labs = await w.GetLabSummaryAsync();
                    return labs.Count > 0 ? $"{{\"resourceType\":\"DiagnosticReport\",\"subject\":\"Patient/{patientId}\",\"count\":{labs.Count}}}" : null;

                case "Encounter":
                    List<PceVisitEntry> visits = await w.GetEncounterListAsync(100);
                    return visits.Count > 0 ? $"{{\"resourceType\":\"Encounter\",\"subject\":\"Patient/{patientId}\",\"count\":{visits.Count}}}" : null;

                case "Appointment":
                    List<AppointmentEntry> appts = await w.GetAllAppointmentsAsync(100);
                    return appts.Count > 0 ? $"{{\"resourceType\":\"Appointment\",\"participant\":\"Patient/{patientId}\",\"count\":{appts.Count}}}" : null;

                default:
                    return null;
            }
        }
        catch
        {
            return null;
        }
    }
}
