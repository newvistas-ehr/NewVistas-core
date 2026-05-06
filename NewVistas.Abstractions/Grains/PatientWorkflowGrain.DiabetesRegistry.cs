// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// Diabetes Registry workflow methods — Site Flavor Architecture (Option 4:
/// Composition). Feature-gated by <c>DIABETES_REGISTRY</c>; when off, the
/// grain methods reject mutating calls with a clear error and return empty
/// snapshots from read calls so non-tribal sites incur no surprises.
/// </summary>
public partial class PatientWorkflowGrain
{
    private const string DiabetesRegistryFeature = "DIABETES_REGISTRY";

    private IDiabetesRegistryGrain DiabetesRegistry() =>
        GrainFactory.GetGrain<IDiabetesRegistryGrain>($"DM-REG:{PatientId}");

    private IDiabetesRegistryIndexGrain DiabetesRegistryIndex() =>
        GrainFactory.GetGrain<IDiabetesRegistryIndexGrain>("DM-REGISTRY-IDX");

    public async Task<DiabetesRegistrySnapshot> GetDiabetesRegistrySnapshotAsync()
    {
        bool enabled = await GetSiteParams().IsFeatureEnabledAsync(DiabetesRegistryFeature);
        if (!enabled)
            return new DiabetesRegistrySnapshot { Icn = PatientId };
        return await DiabetesRegistry().GetSnapshotAsync();
    }

    public async Task<DiabetesPreVisitPlan> GetDiabetesPreVisitPlanAsync(DateTime visitDate)
    {
        bool enabled = await GetSiteParams().IsFeatureEnabledAsync(DiabetesRegistryFeature);
        if (!enabled)
            return new DiabetesPreVisitPlan { Icn = PatientId, VisitDate = visitDate };
        return await DiabetesRegistry().GetPreVisitPlanAsync(visitDate);
    }

    public async Task EnrollInDiabetesRegistryAsync(string diabetesType, DateTime enrollmentDate)
    {
        await RequireDiabetesRegistryFeatureAsync();
        await DiabetesRegistry().EnrollAsync(diabetesType, enrollmentDate);
        await DiabetesRegistryIndex().AddOrUpdateAsync(PatientId, enrollmentDate);
    }

    public async Task RecordDiabetesHbA1cAsync(decimal value, DateTime dateOfTest)
    {
        await RequireDiabetesRegistryFeatureAsync();
        await DiabetesRegistry().RecordHbA1cAsync(value, dateOfTest);
    }

    public async Task RecordDiabetesFootExamAsync(DateTime dateOfExam, string? providerName)
    {
        await RequireDiabetesRegistryFeatureAsync();
        await DiabetesRegistry().RecordFootExamAsync(dateOfExam, providerName);
    }

    public async Task RecordDiabetesEyeExamAsync(DateTime dateOfExam, string? providerName)
    {
        await RequireDiabetesRegistryFeatureAsync();
        await DiabetesRegistry().RecordEyeExamAsync(dateOfExam, providerName);
    }

    public async Task RecordDiabetesEgfrAsync(decimal eGfrValue, DateTime dateOfTest)
    {
        await RequireDiabetesRegistryFeatureAsync();
        await DiabetesRegistry().RecordEgfrAsync(eGfrValue, dateOfTest);
    }

    public async Task RecordDiabetesAcrAsync(decimal acrValue, DateTime dateOfTest)
    {
        await RequireDiabetesRegistryFeatureAsync();
        await DiabetesRegistry().RecordAcrAsync(acrValue, dateOfTest);
    }

    private async Task RequireDiabetesRegistryFeatureAsync()
    {
        bool enabled = await GetSiteParams().IsFeatureEnabledAsync(DiabetesRegistryFeature);
        if (!enabled)
            throw new InvalidOperationException(
                "Diabetes registry is not enabled for this site. Enable the DIABETES_REGISTRY feature in Site Parameters.");
    }
}
