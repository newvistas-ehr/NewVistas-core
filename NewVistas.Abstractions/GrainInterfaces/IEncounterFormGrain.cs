// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Grain for managing encounter form templates — configurable data capture form definitions.
/// Enabled per site via ISiteParametersGrain.Features containing "ENCOUNTER_FORM_TEMPLATES".
/// Follows the Site Flavor Architecture (Option 4 — Composition).
///
/// Maps to IHS RPMS PCC encounter forms and VistA Reminder Dialogs which allow
/// sites to define custom data capture forms for clinical encounters.
/// Keyed by template ID (e.g., "EF-TPL:{guid}").
/// </summary>
public interface IEncounterFormTemplateGrain : IGrainWithStringKey
{
    Task<EncounterFormTemplateState> GetTemplateAsync();

    Task<EncounterFormTemplateState> CreateTemplateAsync(
        string name,
        string description,
        string formType,
        string? clinicId,
        List<EncounterFormFieldDefinition> fields,
        string createdByName);

    Task UpdateTemplateAsync(
        string name,
        string description,
        List<EncounterFormFieldDefinition> fields,
        string updatedByName);

    Task PublishAsync(string publishedByName);
    Task RetireAsync(string retiredByName);
    Task AddFieldAsync(EncounterFormFieldDefinition field, string updatedByName);
    Task RemoveFieldAsync(string fieldId, string updatedByName);
}

/// <summary>
/// Grain for a completed/in-progress encounter form instance — a patient's data
/// captured using a specific template.
/// Keyed by instance ID (e.g., "EF-INST:{guid}").
/// </summary>
public interface IEncounterFormInstanceGrain : IGrainWithStringKey
{
    Task<EncounterFormInstanceState> GetInstanceAsync();

    Task<EncounterFormInstanceState> CreateInstanceAsync(
        string templateId,
        string templateName,
        string patientId,
        string patientName,
        string? encounterId,
        string createdByProviderId,
        string createdByProviderName);

    Task SetFieldValueAsync(string fieldId, string? value);
    Task SetFieldValuesAsync(Dictionary<string, string?> fieldValues);
    Task SubmitAsync(string submittedByName);
    Task AmendAsync(string amendedByName, string reason);
    Task VoidAsync(string voidedByName, string reason);
}

/// <summary>
/// System-level index grain for encounter form templates.
/// Singleton keyed by "EF-TPL-IDX".
/// </summary>
public interface IEncounterFormTemplateIndexGrain : IGrainWithStringKey
{
    Task AddOrUpdateAsync(EncounterFormTemplateIndexEntry entry);
    Task RemoveAsync(string templateId);
    Task<List<EncounterFormTemplateIndexEntry>> GetAllAsync(int maxResults = 50);
    Task<List<EncounterFormTemplateIndexEntry>> GetByFormTypeAsync(string formType, int maxResults = 50);
    Task<List<EncounterFormTemplateIndexEntry>> GetPublishedAsync(int maxResults = 50);
    Task<List<EncounterFormTemplateIndexEntry>> SearchAsync(string? formType, string? status, string? clinicId, int maxResults = 50);
    Task<int> GetCountAsync();
}

/// <summary>
/// System-level index grain for encounter form instances.
/// Singleton keyed by "EF-INST-IDX".
/// </summary>
public interface IEncounterFormInstanceIndexGrain : IGrainWithStringKey
{
    Task AddOrUpdateAsync(EncounterFormInstanceIndexEntry entry);
    Task<List<EncounterFormInstanceIndexEntry>> GetByPatientAsync(string patientId, int maxResults = 50);
    Task<List<EncounterFormInstanceIndexEntry>> GetByTemplateAsync(string templateId, int maxResults = 50);
    Task<List<EncounterFormInstanceIndexEntry>> GetByStatusAsync(string status, int maxResults = 50);
    Task<List<EncounterFormInstanceIndexEntry>> SearchAsync(string? patientId, string? templateId, string? status, int maxResults = 50);
    Task<int> GetCountAsync();
}
