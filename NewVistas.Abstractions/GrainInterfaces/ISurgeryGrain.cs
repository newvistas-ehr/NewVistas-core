// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Surgery Grain Interface based on VistA SURGERY file (#130)
/// </summary>
public interface ISurgeryGrain : IGrainWithStringKey
{
    Task<GrainStates.SurgeryState> GetSurgeryAsync();

    Task ScheduleSurgeryAsync(
        string patientId,
        string principalProcedure,
        string? principalProcedureCptCode,
        DateTime dateOfOperation,
        string? surgeonId,
        string? surgeonName,
        string? anesthesiaTechnique,
        string? surgicalSpecialty,
        string? preOpDiagnosis,
        string? locationId,
        string? locationName,
        string? comments);

    Task BeginOperationAsync(DateTime timeOperationBegan, string? anesthesiologistId, string? anesthesiologistName);
    Task EndOperationAsync(DateTime timeOperationEnded);
    Task AddAssistantAsync(string assistantId, string assistantName);
    Task AddOtherProcedureAsync(string procedure);
    Task RecordOperativeReportAsync(string operativeReport, string? postOpDiagnosis, string? woundClassification);
    Task CompleteAsync();
    Task CancelAsync(string? comments);

    Task RecordPreOpAssessmentAsync(int asaClassification, string? notes, string? providerId, string? providerName, DateTime assessmentDate);
    Task AddComplicationAsync(string complicationCode, string description, string? severity, DateTime occurrenceDate, string? treatmentAction);
    Task AddImplantAsync(string deviceName, string? manufacturer, string? serialNumber, string? lotNumber, string? bodySite);
    Task AddSurgicalAssistantAsync(string assistantId, string assistantName, string? role);
    Task RecordIntraOpDetailsAsync(int? estimatedBloodLoss, int? spongeCountCorrect, int? needleCountCorrect, int? instrumentCountCorrect, string? dispositionAfterSurgery);
    Task AddAnesthesiaAgentAsync(string agent);
    Task AddSpecimenAsync(string specimenType, string? bodySite, string? accessionNumber, DateTime collectionDateTime);
    Task AddBloodProductAsync(string bloodProduct);
    Task<List<GrainStates.SurgicalComplication>> GetComplicationsAsync();
}
