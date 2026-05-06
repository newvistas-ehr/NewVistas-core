// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans.Runtime;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

public class TransplantPatientGrain : Grain, ITransplantPatientGrain
{
    private readonly IPersistentState<TransplantPatientState> _state;

    public TransplantPatientGrain(
        [PersistentState("txPatientState", "txPatientStore")] IPersistentState<TransplantPatientState> state)
    {
        _state = state;
    }

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_state.State.PatientId))
            _state.State.PatientId = this.GetPrimaryKeyString();
        return base.OnActivateAsync(cancellationToken);
    }

    public Task<TransplantPatientState> GetPatientAsync() => Task.FromResult(_state.State);

    public async Task RegisterPatientAsync(
        string patientId,
        string patientName,
        DateTime? dateOfBirth,
        TransplantOrganType organType,
        TransplantPriority priority,
        BloodType bloodType,
        string? hlaTyping,
        decimal? panelReactiveAntibodyPct,
        string primaryDiagnosis,
        string? diagnosisCode,
        decimal? weightKg,
        decimal? heightCm,
        decimal? meldScore,
        string locationId,
        string locationName,
        string? referringProviderId,
        string? referringProviderName,
        string? notes)
    {
        _state.State.PatientName = patientName;
        _state.State.DateOfBirth = dateOfBirth;
        _state.State.OrganType = organType;
        _state.State.Priority = priority;
        _state.State.BloodType = bloodType;
        _state.State.HlaTyping = hlaTyping;
        _state.State.PanelReactiveAntibodyPct = panelReactiveAntibodyPct;
        _state.State.PrimaryDiagnosis = primaryDiagnosis;
        _state.State.DiagnosisCode = diagnosisCode;
        _state.State.WeightKg = weightKg;
        _state.State.HeightCm = heightCm;
        _state.State.CalculatedMeldScore = meldScore;
        _state.State.LocationId = locationId;
        _state.State.LocationName = locationName;
        _state.State.ReferringProviderId = referringProviderId;
        _state.State.ReferringProviderName = referringProviderName;
        _state.State.Notes = notes;
        _state.State.Status = TransplantStatus.PendingEvaluation;
        _state.State.ListedDate = DateTime.UtcNow;
        _state.State.CreatedDate = DateTime.UtcNow;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task UpdateStatusAsync(TransplantStatus status, string? reason)
    {
        _state.State.Status = status;
        if (status == TransplantStatus.Removed || status == TransplantStatus.Deceased)
        {
            _state.State.RemovedDate = DateTime.UtcNow;
            _state.State.RemovalReason = reason;
        }
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task UpdatePriorityAsync(TransplantPriority priority)
    {
        _state.State.Priority = priority;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task UpdateMeldScoreAsync(decimal meldScore)
    {
        _state.State.CalculatedMeldScore = meldScore;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task RecordTransplantAsync(string donorId, string surgeonId, string surgeonName, DateTime transplantDate)
    {
        _state.State.TransplantDonorId = donorId;
        _state.State.TransplantSurgeonId = surgeonId;
        _state.State.TransplantSurgeonName = surgeonName;
        _state.State.TransplantDate = transplantDate;
        _state.State.Status = TransplantStatus.Transplanted;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }
}
