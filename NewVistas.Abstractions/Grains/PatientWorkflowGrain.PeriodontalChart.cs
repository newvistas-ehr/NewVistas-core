// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// Periodontal Charting — Site Flavor Architecture (Option 4: Composition).
/// Structured dental assessment with 6-point probing depths per tooth.
/// </summary>
public partial class PatientWorkflowGrain
{
    private const string PeriodontalChartingFeature = "PERIODONTAL_CHARTING";

    public async Task<PeriodontalChartState> CreatePeriodontalChartAsync(
        string providerId, string providerName, string? notes)
    {
        bool enabled = await GetSiteParams().IsFeatureEnabledAsync(PeriodontalChartingFeature);
        if (!enabled)
            throw new InvalidOperationException(
                "Periodontal charting is not enabled for this site. Enable the PERIODONTAL_CHARTING feature in Site Parameters.");

        PatientState patient = await GetPatientGrain().GetPatientAsync();

        string chartId = $"PERIO:{Guid.NewGuid()}";
        IPeriodontalChartGrain grain = GrainFactory.GetGrain<IPeriodontalChartGrain>(chartId);

        PeriodontalChartState result = await grain.CreateChartAsync(
            PatientId, patient.Name, providerId, providerName, notes);

        await LogAuditEventAsync(
            "DENTAL", "CREATE_PERIO_CHART", "PeriodontalChart", chartId,
            providerId, providerName, null, null,
            $"Created periodontal chart for {patient.Name}");

        return result;
    }

    public async Task<List<PeriodontalChartIndexEntry>> GetPeriodontalChartsAsync()
    {
        bool enabled = await GetSiteParams().IsFeatureEnabledAsync(PeriodontalChartingFeature);
        if (!enabled) return [];

        IPeriodontalChartIndexGrain index =
            GrainFactory.GetGrain<IPeriodontalChartIndexGrain>("PERIO-IDX");
        return await index.GetByPatientAsync(PatientId);
    }

    public async Task<PeriodontalChartState> GetPeriodontalChartAsync(string chartId)
    {
        bool enabled = await GetSiteParams().IsFeatureEnabledAsync(PeriodontalChartingFeature);
        if (!enabled)
            throw new InvalidOperationException("Periodontal charting is not enabled for this site.");

        IPeriodontalChartGrain grain = GrainFactory.GetGrain<IPeriodontalChartGrain>(chartId);
        return await grain.GetChartAsync();
    }

    public async Task RecordPeriodontalToothDataAsync(string chartId, int toothNumber, PeriodontalToothData data)
    {
        bool enabled = await GetSiteParams().IsFeatureEnabledAsync(PeriodontalChartingFeature);
        if (!enabled)
            throw new InvalidOperationException("Periodontal charting is not enabled for this site.");

        IPeriodontalChartGrain grain = GrainFactory.GetGrain<IPeriodontalChartGrain>(chartId);
        await grain.RecordToothDataAsync(toothNumber, data);
    }

    public async Task FinalizePeriodontalChartAsync(string chartId, string finalizedByName)
    {
        bool enabled = await GetSiteParams().IsFeatureEnabledAsync(PeriodontalChartingFeature);
        if (!enabled)
            throw new InvalidOperationException("Periodontal charting is not enabled for this site.");

        IPeriodontalChartGrain grain = GrainFactory.GetGrain<IPeriodontalChartGrain>(chartId);
        await grain.FinalizeChartAsync(finalizedByName);
    }
}
