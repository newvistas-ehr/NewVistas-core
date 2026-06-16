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
/// Surgery Grain implementation based on VistA SURGERY file (#130)
/// </summary>
public class SurgeryGrain : Grain, ISurgeryGrain
{
    private readonly IPersistentState<SurgeryState> _state;

    public SurgeryGrain(
        [PersistentState("surgeryState", "surgeryStore")] IPersistentState<SurgeryState> state)
    {
        _state = state;
    }

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_state.State.SurgeryId))
        {
            _state.State.SurgeryId = this.GetPrimaryKeyString();
            _state.State.CreatedDate = DateTime.UtcNow;
            _state.State.LastModifiedDate = DateTime.UtcNow;
        }
        return base.OnActivateAsync(cancellationToken);
    }

    public Task<SurgeryState> GetSurgeryAsync() => Task.FromResult(_state.State);

    public async Task ScheduleSurgeryAsync(
        string patientId, string principalProcedure, string? principalProcedureCptCode,
        DateTime dateOfOperation, string? surgeonId, string? surgeonName,
        string? anesthesiaTechnique, string? surgicalSpecialty,
        string? preOpDiagnosis, string? locationId, string? locationName, string? comments)
    {
        _state.State.PatientId = patientId;
        _state.State.PrincipalProcedure = principalProcedure;
        _state.State.PrincipalProcedureCptCode = principalProcedureCptCode;
        _state.State.DateOfOperation = dateOfOperation;
        _state.State.SurgeonId = surgeonId;
        _state.State.SurgeonName = surgeonName;
        _state.State.AnesthesiaTechnique = anesthesiaTechnique;
        _state.State.SurgicalSpecialty = surgicalSpecialty;
        _state.State.PreOpDiagnosis = preOpDiagnosis;
        _state.State.LocationId = locationId;
        _state.State.LocationName = locationName;
        _state.State.Comments = comments;
        _state.State.Status = "SCHEDULED";
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task BeginOperationAsync(DateTime timeOperationBegan, string? anesthesiologistId, string? anesthesiologistName)
    {
        _state.State.TimeOperationBegan = timeOperationBegan;
        _state.State.AnesthesiologistId = anesthesiologistId;
        _state.State.AnesthesiologistName = anesthesiologistName;
        _state.State.Status = "IN PROGRESS";
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task EndOperationAsync(DateTime timeOperationEnded)
    {
        _state.State.TimeOperationEnded = timeOperationEnded;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task AddAssistantAsync(string assistantId, string assistantName)
    {
        _state.State.FirstAssistantId = assistantId;
        _state.State.FirstAssistantName = assistantName;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task AddOtherProcedureAsync(string procedure)
    {
        _state.State.OtherProcedures.Add(procedure);
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task RecordOperativeReportAsync(string operativeReport, string? postOpDiagnosis, string? woundClassification)
    {
        _state.State.OperativeReport = operativeReport;
        _state.State.PostOpDiagnosis = postOpDiagnosis;
        _state.State.WoundClassification = woundClassification;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task CompleteAsync()
    {
        _state.State.Status = "COMPLETED";
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task CancelAsync(string? comments)
    {
        _state.State.Status = "CANCELLED";
        _state.State.Comments = comments;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task RecordPreOpAssessmentAsync(int asaClassification, string? notes, string? providerId, string? providerName, DateTime assessmentDate)
    {
        _state.State.AsaClassification = asaClassification;
        _state.State.PreOpAssessmentNotes = notes;
        _state.State.PreOpAssessmentProviderId = providerId;
        _state.State.PreOpAssessmentProviderName = providerName;
        _state.State.PreOpAssessmentDate = assessmentDate;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task AddComplicationAsync(string complicationCode, string description, string? severity, DateTime occurrenceDate, string? treatmentAction)
    {
        _state.State.Complications.Add(new SurgicalComplication
        {
            ComplicationCode = complicationCode,
            Description = description,
            Severity = severity,
            OccurrenceDate = occurrenceDate,
            TreatmentAction = treatmentAction
        });
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task AddImplantAsync(string deviceName, string? manufacturer, string? serialNumber, string? lotNumber, string? bodySite)
    {
        _state.State.Implants.Add(new SurgicalImplant
        {
            DeviceName = deviceName,
            Manufacturer = manufacturer,
            SerialNumber = serialNumber,
            LotNumber = lotNumber,
            BodySite = bodySite
        });
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task AddSurgicalAssistantAsync(string assistantId, string assistantName, string? role)
    {
        _state.State.Assistants.Add(new SurgicalAssistant
        {
            AssistantId = assistantId,
            AssistantName = assistantName,
            Role = role
        });
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task RecordIntraOpDetailsAsync(int? estimatedBloodLoss, int? spongeCountCorrect, int? needleCountCorrect, int? instrumentCountCorrect, string? dispositionAfterSurgery)
    {
        _state.State.EstimatedBloodLoss = estimatedBloodLoss;
        _state.State.SpongeCountCorrect = spongeCountCorrect;
        _state.State.NeedleCountCorrect = needleCountCorrect;
        _state.State.InstrumentCountCorrect = instrumentCountCorrect;
        _state.State.DispositionAfterSurgery = dispositionAfterSurgery;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task AddAnesthesiaAgentAsync(string agent)
    {
        if (!_state.State.AnesthesiaAgents.Contains(agent))
        {
            _state.State.AnesthesiaAgents.Add(agent);
        }
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task AddSpecimenAsync(string specimenType, string? bodySite, string? accessionNumber, DateTime collectionDateTime)
    {
        _state.State.Specimens.Add(new SurgicalSpecimen
        {
            SpecimenType = specimenType,
            BodySite = bodySite,
            PathologyAccessionNumber = accessionNumber,
            CollectionDateTime = collectionDateTime
        });
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task AddBloodProductAsync(string bloodProduct)
    {
        _state.State.BloodProductsGiven.Add(bloodProduct);
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public Task<List<SurgicalComplication>> GetComplicationsAsync() => Task.FromResult(_state.State.Complications);
}
