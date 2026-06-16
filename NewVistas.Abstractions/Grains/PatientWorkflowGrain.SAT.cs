// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// Substance Abuse Treatment — Site Flavor Architecture (Option 4: Composition).
/// Checks the SUBSTANCE_ABUSE_TREATMENT feature flag before delegating to
/// the optional SA grains (RPMS CDMIS — File #9002170-9002174).
/// If the feature is not enabled, the SA grains are never activated
/// and consume no resources.
/// </summary>
public partial class PatientWorkflowGrain
{
    private const string SubstanceAbuseTreatmentFeature = "SUBSTANCE_ABUSE_TREATMENT";

    private ISATreatmentEpisodeGrain SAEpisode(string episodeId)
        => GrainFactory.GetGrain<ISATreatmentEpisodeGrain>(episodeId);

    private ISATreatmentEpisodeIndexGrain SAEpisodeIndex()
        => GrainFactory.GetGrain<ISATreatmentEpisodeIndexGrain>($"SA-EPISODE-IDX:{PatientId}");

    private ISAVisitGrain SAVisit(string visitId)
        => GrainFactory.GetGrain<ISAVisitGrain>(visitId);

    private ISAVisitIndexGrain SAVisitIndex(string episodeId)
        => GrainFactory.GetGrain<ISAVisitIndexGrain>($"SA-VISIT-IDX:{episodeId}");

    private async Task EnsureSATreatmentEnabledAsync()
    {
        bool enabled = await GetSiteParams().IsFeatureEnabledAsync(SubstanceAbuseTreatmentFeature);
        if (!enabled)
            throw new InvalidOperationException(
                "Substance abuse treatment tracking is not enabled for this site. " +
                "Enable the SUBSTANCE_ABUSE_TREATMENT feature in Site Parameters.");
    }

    public async Task<string> CreateSATreatmentEpisodeAsync(
        SATreatmentModality modality,
        SubstanceType primarySubstance,
        List<SubstanceType>? secondarySubstances,
        DateTime intakeDate,
        DateTime? lastUseDate, DateTime? sobrietyDate,
        string? programName, List<string>? treatmentGoals,
        string? providerId, string? providerName,
        string? locationId, string? locationName,
        string? notes)
    {
        await EnsureSATreatmentEnabledAsync();

        string episodeId = $"SA-EPISODE:{Guid.NewGuid()}";
        await SAEpisode(episodeId).CreateAsync(
            PatientId, modality, primarySubstance, secondarySubstances,
            intakeDate, lastUseDate, sobrietyDate,
            programName, treatmentGoals,
            providerId, providerName, locationId, locationName, notes);

        await SAEpisodeIndex().AddEntryAsync(new SATreatmentEpisodeIndexEntry
        {
            EpisodeId        = episodeId,
            PatientId        = PatientId,
            Status           = SATreatmentStatus.Active,
            Modality         = modality,
            PrimarySubstance = primarySubstance,
            IntakeDate       = intakeDate,
            ProviderName     = providerName,
            ProgramName      = programName,
        });
        return episodeId;
    }

    public async Task<SATreatmentEpisodeState> GetSATreatmentEpisodeAsync(string episodeId)
    {
        await EnsureSATreatmentEnabledAsync();
        return await SAEpisode(episodeId).GetAsync();
    }

    public async Task<List<SATreatmentEpisodeIndexEntry>> GetSATreatmentEpisodesAsync()
    {
        bool enabled = await GetSiteParams().IsFeatureEnabledAsync(SubstanceAbuseTreatmentFeature);
        if (!enabled) return [];
        return await SAEpisodeIndex().GetAllAsync();
    }

    public async Task<SATreatmentEpisodeIndexEntry?> GetActiveSATreatmentAsync()
    {
        bool enabled = await GetSiteParams().IsFeatureEnabledAsync(SubstanceAbuseTreatmentFeature);
        if (!enabled) return null;
        return await SAEpisodeIndex().GetActiveAsync();
    }

    public async Task AddSAMATEntryAsync(string episodeId, MATEntry entry)
    {
        await EnsureSATreatmentEnabledAsync();
        await SAEpisode(episodeId).AddMATEntryAsync(entry);
    }

    public async Task StopSAMATEntryAsync(string episodeId, string entryId, DateTime endDate)
    {
        await EnsureSATreatmentEnabledAsync();
        await SAEpisode(episodeId).StopMATEntryAsync(entryId, endDate);
    }

    public async Task AddSATreatmentGoalAsync(string episodeId, string goal)
    {
        await EnsureSATreatmentEnabledAsync();
        await SAEpisode(episodeId).AddTreatmentGoalAsync(goal);
    }

    public async Task DischargeSATreatmentAsync(string episodeId, DateTime dischargeDate,
        SADischargeDisposition disposition, string? notes)
    {
        await EnsureSATreatmentEnabledAsync();
        await SAEpisode(episodeId).DischargeAsync(dischargeDate, disposition, notes);
        await SAEpisodeIndex().UpdateEntryAsync(episodeId, SATreatmentStatus.Discharged, dischargeDate);
    }

    public async Task ReopenSATreatmentAsync(string episodeId, string? notes)
    {
        await EnsureSATreatmentEnabledAsync();
        await SAEpisode(episodeId).ReopenAsync(notes);
        await SAEpisodeIndex().UpdateEntryAsync(episodeId, SATreatmentStatus.Reopened, null);
    }

    public async Task<string> CreateSAVisitAsync(
        string episodeId, DateTime visitDate,
        SAVisitType visitType, int? durationMinutes,
        string? udsResult, List<string>? udsSubstancesDetected,
        int? daysSinceLastUse, int? cravingLevel,
        string? providerId, string? providerName, string? notes)
    {
        await EnsureSATreatmentEnabledAsync();

        string visitId = $"SA-VISIT:{Guid.NewGuid()}";
        await SAVisit(visitId).CreateAsync(
            episodeId, PatientId, visitDate, visitType, durationMinutes,
            udsResult, udsSubstancesDetected, daysSinceLastUse, cravingLevel,
            providerId, providerName, notes);

        await SAVisitIndex(episodeId).AddEntryAsync(new SAVisitIndexEntry
        {
            VisitId         = visitId,
            EpisodeId       = episodeId,
            VisitDate       = visitDate,
            VisitType       = visitType,
            DurationMinutes = durationMinutes,
            ProviderName    = providerName,
            UdsResult       = udsResult,
        });
        return visitId;
    }

    public async Task<SAVisitState> GetSAVisitAsync(string visitId)
    {
        await EnsureSATreatmentEnabledAsync();
        return await SAVisit(visitId).GetAsync();
    }

    public async Task<List<SAVisitIndexEntry>> GetSAVisitsAsync(string episodeId)
    {
        bool enabled = await GetSiteParams().IsFeatureEnabledAsync(SubstanceAbuseTreatmentFeature);
        if (!enabled) return [];
        return await SAVisitIndex(episodeId).GetAllAsync();
    }

    public async Task<int> GetSAVisitCountAsync(string episodeId)
    {
        bool enabled = await GetSiteParams().IsFeatureEnabledAsync(SubstanceAbuseTreatmentFeature);
        if (!enabled) return 0;
        return await SAVisitIndex(episodeId).GetVisitCountAsync();
    }
}
