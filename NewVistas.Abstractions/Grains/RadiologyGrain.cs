// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;
using Orleans.Runtime;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// Radiology Grain implementation based on VistA RAD/NUC MED ORDERS file (#75.1)
/// </summary>
public class RadiologyGrain : Grain, IRadiologyGrain
{
    private readonly IPersistentState<RadiologyState> _state;

    public RadiologyGrain(
        [PersistentState("radiologyState", "radiologyStore")] IPersistentState<RadiologyState> state)
    {
        _state = state;
    }

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_state.State.RadiologyId))
        {
            _state.State.RadiologyId = this.GetPrimaryKeyString();
            _state.State.CreatedDate = DateTime.UtcNow;
            _state.State.LastModifiedDate = DateTime.UtcNow;
        }
        return base.OnActivateAsync(cancellationToken);
    }

    public Task<RadiologyState> GetRadiologyAsync() => Task.FromResult(_state.State);

    public async Task OrderStudyAsync(
        string patientId, string procedureName, string? procedureId,
        string? cptCode, string? imagingType,
        string? requestingProviderId, string? requestingProviderName,
        string? urgency, string? clinicalHistory, string? reasonForStudy,
        string? orderId, string? locationId, string? locationName)
    {
        _state.State.PatientId = patientId;
        _state.State.ProcedureName = procedureName;
        _state.State.ProcedureId = procedureId;
        _state.State.CptCode = cptCode;
        _state.State.ImagingType = imagingType;
        _state.State.RequestingProviderId = requestingProviderId;
        _state.State.RequestingProviderName = requestingProviderName;
        _state.State.Urgency = urgency;
        _state.State.ClinicalHistory = clinicalHistory;
        _state.State.ReasonForStudy = reasonForStudy;
        _state.State.OrderId = orderId;
        _state.State.LocationId = locationId;
        _state.State.LocationName = locationName;
        _state.State.OrderDateTime = DateTime.UtcNow;
        _state.State.Status = "PENDING";
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task RecordExamAsync(DateTime examDateTime)
    {
        _state.State.ExamDateTime = examDateTime;
        _state.State.Status = "EXAMINED";
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task RecordReportAsync(
        string reportText, string? impression, string? diagnosticCode,
        string? interpretingPhysicianId, string? interpretingPhysicianName,
        DateTime reportDateTime)
    {
        _state.State.ReportText = reportText;
        _state.State.Impression = impression;
        _state.State.DiagnosticCode = diagnosticCode;
        _state.State.InterpretingPhysicianId = interpretingPhysicianId;
        _state.State.InterpretingPhysicianName = interpretingPhysicianName;
        _state.State.ReportDateTime = reportDateTime;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task CompleteAsync()
    {
        _state.State.Status = "COMPLETE";
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task CancelAsync()
    {
        _state.State.Status = "CANCELLED";
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task RecordContrastAsync(string contrastAgent, string? route, double? volumeMl)
    {
        _state.State.ContrastAgent = contrastAgent;
        _state.State.ContrastRoute = route;
        _state.State.ContrastVolumeMl = volumeMl;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task RecordContrastReactionAsync(string reactionDetails)
    {
        _state.State.ContrastReactionOccurred = true;
        _state.State.ContrastReactionDetails = reactionDetails;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task RecordRadiationDoseAsync(double? doseMSv, double? ctdiVol, double? doseLengthProduct)
    {
        _state.State.RadiationDoseMSv = doseMSv;
        _state.State.CtdiVol = ctdiVol;
        _state.State.DoseLengthProduct = doseLengthProduct;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task SignReportAsync(string signedById, string signedByName, DateTime signedDateTime)
    {
        _state.State.SignedById = signedById;
        _state.State.SignedByName = signedByName;
        _state.State.SignedDateTime = signedDateTime;
        _state.State.ReportStatus = "FINAL";
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task FlagCriticalResultAsync()
    {
        _state.State.IsCriticalResult = true;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task RecordCriticalResultNotificationAsync(string notifiedTo, DateTime notifiedDateTime)
    {
        _state.State.CriticalResultNotifiedTo = notifiedTo;
        _state.State.CriticalResultNotifiedDateTime = notifiedDateTime;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task AcknowledgeCriticalResultAsync(string acknowledgedBy)
    {
        _state.State.CriticalResultAcknowledgedBy = acknowledgedBy;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task RecordTechnicianAsync(string technicianId, string technicianName)
    {
        _state.State.TechnicianId = technicianId;
        _state.State.TechnicianName = technicianName;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task SetBodyPartAndLateralityAsync(string? bodyPart, string? laterality)
    {
        _state.State.BodyPart = bodyPart;
        _state.State.Laterality = laterality;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task AddPriorStudyAsync(string priorStudyId)
    {
        if (!_state.State.PriorStudyIds.Contains(priorStudyId))
        {
            _state.State.PriorStudyIds.Add(priorStudyId);
        }
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task AmendReportAsync(string amendmentText)
    {
        _state.State.AmendmentText = amendmentText;
        _state.State.AmendmentDateTime = DateTime.UtcNow;
        _state.State.ReportStatus = "AMENDED";
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public Task<bool> IsCriticalResultAsync() => Task.FromResult(_state.State.IsCriticalResult);
}
