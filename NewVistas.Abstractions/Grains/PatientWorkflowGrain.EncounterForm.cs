// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// Encounter Form Templates — Site Flavor Architecture (Option 4: Composition).
/// Checks the ENCOUNTER_FORM_TEMPLATES feature flag before delegating to
/// the optional encounter form grains. If the feature is not enabled,
/// the form grains are never activated and consume no resources.
///
/// Maps to IHS RPMS PCC encounter forms and VistA Reminder Dialogs.
/// </summary>
public partial class PatientWorkflowGrain
{
    private const string EncounterFormFeature = "ENCOUNTER_FORM_TEMPLATES";

    public async Task<EncounterFormInstanceState> CreateEncounterFormInstanceAsync(
        string templateId, string templateName,
        string? encounterId,
        string createdByProviderId, string createdByProviderName)
    {
        bool enabled = await GetSiteParams().IsFeatureEnabledAsync(EncounterFormFeature);
        if (!enabled)
            throw new InvalidOperationException(
                "Encounter form templates are not enabled for this site. Enable the ENCOUNTER_FORM_TEMPLATES feature in Site Parameters.");

        PatientState patient = await GetPatientGrain().GetPatientAsync();

        string instanceId = $"EF-INST:{Guid.NewGuid()}";
        IEncounterFormInstanceGrain grain =
            GrainFactory.GetGrain<IEncounterFormInstanceGrain>(instanceId);

        EncounterFormInstanceState result = await grain.CreateInstanceAsync(
            templateId, templateName,
            PatientId, patient.Name,
            encounterId,
            createdByProviderId, createdByProviderName);

        await LogAuditEventAsync(
            "ENCOUNTER_FORM", "CREATE_INSTANCE", "EncounterFormInstance", instanceId,
            createdByProviderId, createdByProviderName, null, null,
            $"Created encounter form instance from template {templateName}");

        return result;
    }

    public async Task<List<EncounterFormInstanceIndexEntry>> GetEncounterFormInstancesAsync()
    {
        bool enabled = await GetSiteParams().IsFeatureEnabledAsync(EncounterFormFeature);
        if (!enabled) return [];

        IEncounterFormInstanceIndexGrain index =
            GrainFactory.GetGrain<IEncounterFormInstanceIndexGrain>("EF-INST-IDX");
        return await index.GetByPatientAsync(PatientId);
    }

    public async Task<EncounterFormInstanceState> GetEncounterFormInstanceAsync(string instanceId)
    {
        bool enabled = await GetSiteParams().IsFeatureEnabledAsync(EncounterFormFeature);
        if (!enabled)
            throw new InvalidOperationException("Encounter form templates are not enabled for this site.");

        IEncounterFormInstanceGrain grain = GrainFactory.GetGrain<IEncounterFormInstanceGrain>(instanceId);
        return await grain.GetInstanceAsync();
    }

    public async Task SetEncounterFormFieldValuesAsync(string instanceId, Dictionary<string, string?> fieldValues)
    {
        bool enabled = await GetSiteParams().IsFeatureEnabledAsync(EncounterFormFeature);
        if (!enabled)
            throw new InvalidOperationException("Encounter form templates are not enabled for this site.");

        IEncounterFormInstanceGrain grain = GrainFactory.GetGrain<IEncounterFormInstanceGrain>(instanceId);
        await grain.SetFieldValuesAsync(fieldValues);
    }

    public async Task SubmitEncounterFormAsync(string instanceId, string submittedByName)
    {
        bool enabled = await GetSiteParams().IsFeatureEnabledAsync(EncounterFormFeature);
        if (!enabled)
            throw new InvalidOperationException("Encounter form templates are not enabled for this site.");

        IEncounterFormInstanceGrain grain = GrainFactory.GetGrain<IEncounterFormInstanceGrain>(instanceId);
        await grain.SubmitAsync(submittedByName);
    }

    public async Task VoidEncounterFormAsync(string instanceId, string voidedByName, string reason)
    {
        bool enabled = await GetSiteParams().IsFeatureEnabledAsync(EncounterFormFeature);
        if (!enabled)
            throw new InvalidOperationException("Encounter form templates are not enabled for this site.");

        IEncounterFormInstanceGrain grain = GrainFactory.GetGrain<IEncounterFormInstanceGrain>(instanceId);
        await grain.VoidAsync(voidedByName, reason);
    }
}
