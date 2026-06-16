// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// Anesthesia Tracking — Site Flavor Architecture (Option 4: Composition).
/// Structured anesthesia data capture during surgery.
/// </summary>
public partial class PatientWorkflowGrain
{
    private const string AnesthesiaTrackingFeature = "ANESTHESIA_TRACKING";

    public async Task<AnesthesiaRecordState> CreateAnesthesiaRecordAsync(
        string surgeryId, string procedureName,
        string anesthesiaType, string anesthesiologistId, string anesthesiologistName,
        string asaClassification, string? airwayClass, string? preOpNotes)
    {
        bool enabled = await GetSiteParams().IsFeatureEnabledAsync(AnesthesiaTrackingFeature);
        if (!enabled)
            throw new InvalidOperationException(
                "Anesthesia tracking is not enabled for this site. Enable the ANESTHESIA_TRACKING feature in Site Parameters.");

        PatientState patient = await GetPatientGrain().GetPatientAsync();

        string recordId = $"ANES:{Guid.NewGuid()}";
        IAnesthesiaRecordGrain grain = GrainFactory.GetGrain<IAnesthesiaRecordGrain>(recordId);

        AnesthesiaRecordState result = await grain.CreateRecordAsync(
            PatientId, patient.Name, surgeryId, procedureName,
            anesthesiaType, anesthesiologistId, anesthesiologistName,
            asaClassification, airwayClass, preOpNotes);

        await LogAuditEventAsync(
            "SURGERY", "CREATE_ANESTHESIA_RECORD", "AnesthesiaRecord", recordId,
            anesthesiologistId, anesthesiologistName, null, null,
            $"Anesthesia record for {procedureName}, {anesthesiaType}, ASA {asaClassification}");

        return result;
    }

    public async Task<List<AnesthesiaRecordIndexEntry>> GetAnesthesiaRecordsAsync()
    {
        bool enabled = await GetSiteParams().IsFeatureEnabledAsync(AnesthesiaTrackingFeature);
        if (!enabled) return [];

        IAnesthesiaRecordIndexGrain index =
            GrainFactory.GetGrain<IAnesthesiaRecordIndexGrain>("ANES-IDX");
        return await index.GetByPatientAsync(PatientId);
    }

    public async Task<AnesthesiaRecordState> GetAnesthesiaRecordAsync(string recordId)
    {
        bool enabled = await GetSiteParams().IsFeatureEnabledAsync(AnesthesiaTrackingFeature);
        if (!enabled)
            throw new InvalidOperationException("Anesthesia tracking is not enabled for this site.");

        IAnesthesiaRecordGrain grain = GrainFactory.GetGrain<IAnesthesiaRecordGrain>(recordId);
        return await grain.GetRecordAsync();
    }

    public async Task AddAnesthesiaAgentAsync(string recordId, AnesthesiaAgent agent)
    {
        bool enabled = await GetSiteParams().IsFeatureEnabledAsync(AnesthesiaTrackingFeature);
        if (!enabled)
            throw new InvalidOperationException("Anesthesia tracking is not enabled for this site.");

        IAnesthesiaRecordGrain grain = GrainFactory.GetGrain<IAnesthesiaRecordGrain>(recordId);
        await grain.AddAgentAsync(agent);
    }

    public async Task FinalizeAnesthesiaRecordAsync(string recordId, string finalizedByName)
    {
        bool enabled = await GetSiteParams().IsFeatureEnabledAsync(AnesthesiaTrackingFeature);
        if (!enabled)
            throw new InvalidOperationException("Anesthesia tracking is not enabled for this site.");

        IAnesthesiaRecordGrain grain = GrainFactory.GetGrain<IAnesthesiaRecordGrain>(recordId);
        await grain.FinalizeRecordAsync(finalizedByName);
    }
}
