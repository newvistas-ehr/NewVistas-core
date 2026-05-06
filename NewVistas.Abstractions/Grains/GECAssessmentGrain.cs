// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

public class GECAssessmentGrain : Grain, IGECAssessmentGrain
{
    private readonly IPersistentState<GECAssessmentState> _state;

    public GECAssessmentGrain(
        [PersistentState("gecAssessmentState", "gecAssessmentStore")] IPersistentState<GECAssessmentState> state)
    {
        _state = state;
    }

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_state.State.AssessmentId))
            _state.State.AssessmentId = this.GetPrimaryKeyString();
        return base.OnActivateAsync(cancellationToken);
    }

    public async Task CreateAssessmentAsync(
        string patientId,
        string patientName,
        GECAssessmentType assessmentType,
        DateTime assessmentDate,
        DateTime periodStart,
        DateTime periodEnd,
        GECLevelOfCare levelOfCare,
        string completedBy,
        string completedByTitle)
    {
        _state.State.PatientId = patientId;
        _state.State.PatientName = patientName;
        _state.State.AssessmentType = assessmentType;
        _state.State.AssessmentDate = assessmentDate;
        _state.State.PeriodStart = periodStart;
        _state.State.PeriodEnd = periodEnd;
        _state.State.LevelOfCare = levelOfCare;
        _state.State.CompletedBy = completedBy;
        _state.State.CompletedByTitle = completedByTitle;
        _state.State.Status = GECAssessmentStatus.Draft;
        _state.State.RUGCategory = GECRUGCategory.NotAssigned;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task RecordADLScoresAsync(
        int bedMobility,
        int transfer,
        int walking,
        int dressing,
        int eating,
        int toiletUse,
        int personalHygiene)
    {
        _state.State.ADLBedMobility = bedMobility;
        _state.State.ADLTransfer = transfer;
        _state.State.ADLWalking = walking;
        _state.State.ADLDressing = dressing;
        _state.State.ADLEating = eating;
        _state.State.ADLToiletUse = toiletUse;
        _state.State.ADLPersonalHygiene = personalHygiene;
        _state.State.ADLTotalScore = bedMobility + transfer + walking + dressing + eating + toiletUse + personalHygiene;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task RecordCognitiveMoodAsync(int? bimsScore, int? phq9Score)
    {
        _state.State.BIMSScore = bimsScore;
        _state.State.PHQ9Score = phq9Score;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task RecordClinicalIndicatorsAsync(
        bool painPresent,
        string painFrequency,
        int pressureUlcerCount,
        int fallsLast30Days,
        bool nutritionConcern,
        bool behaviorSymptoms)
    {
        _state.State.PainPresent = painPresent;
        _state.State.PainFrequency = painFrequency;
        _state.State.PressureUlcerCount = pressureUlcerCount;
        _state.State.FallsLast30Days = fallsLast30Days;
        _state.State.NutritionConcern = nutritionConcern;
        _state.State.BehaviorSymptoms = behaviorSymptoms;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task SetRUGCategoryAsync(GECRUGCategory rugCategory)
    {
        _state.State.RUGCategory = rugCategory;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task AddNotesAsync(string notes)
    {
        _state.State.Notes = notes;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task SubmitAssessmentAsync(string submittedBy)
    {
        _state.State.Status = GECAssessmentStatus.Submitted;
        _state.State.SubmittedDate = DateTime.UtcNow;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public Task<GECAssessmentState> GetAssessmentAsync() => Task.FromResult(_state.State);
}
