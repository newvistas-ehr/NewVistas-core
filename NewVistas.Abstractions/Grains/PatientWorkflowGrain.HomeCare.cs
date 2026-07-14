// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// Home-Based Care (HBPC) orchestration. Patient-scoped operations route through the workflow
/// grain (the orchestration entry point); facility-wide caseload/visit-schedule reads are served
/// by the singleton census and visit-index grains. VistA File #750 / #750.1 (HBPC.m, HBVISIT.m).
/// </summary>
public partial class PatientWorkflowGrain
{
    // ─── Grain accessors ────────────────────────────────────────────────
    private IHomeCareEpisodeGrain HomeEpisode(string episodeId) => GrainFactory.GetGrain<IHomeCareEpisodeGrain>(episodeId);
    private IHomeVisitGrain HomeVisit(string visitId) => GrainFactory.GetGrain<IHomeVisitGrain>(visitId);
    private IHomeVisitIndexGrain HomeVisitIndex() => GrainFactory.GetGrain<IHomeVisitIndexGrain>("HHC-VISIT-INDEX");
    private IHomeCarePlanGrain HomePlan(string planId) => GrainFactory.GetGrain<IHomeCarePlanGrain>(planId);
    private IHomeCareAssessmentGrain HomeAssessment(string assessmentId) => GrainFactory.GetGrain<IHomeCareAssessmentGrain>(assessmentId);
    private IHomeCareCensusGrain HomeCensus() => GrainFactory.GetGrain<IHomeCareCensusGrain>("HHC-CENSUS:DEFAULT");

    private static HomeVisitIndexEntry BuildHomeVisitIndexEntry(HomeVisitState v) => new()
    {
        VisitId = v.VisitId,
        EpisodeId = v.EpisodeId,
        PatientId = v.PatientId,
        PatientName = v.PatientName,
        ScheduledDateTime = v.ScheduledDateTime,
        Discipline = v.Discipline,
        VisitType = v.VisitType,
        Status = v.Status,
        ClinicianId = v.ClinicianId,
        ClinicianName = v.ClinicianName,
        DurationMinutes = v.DurationMinutes
    };

    /// <summary>Rebuilds the census roster entry for an episode (caseload + workload roll-up).</summary>
    private async Task RefreshHomeCensusAsync(string episodeId)
    {
        HomeCareEpisodeState e = await HomeEpisode(episodeId).GetEpisodeAsync();
        HomeCareTeamMember? primary = e.Team.FirstOrDefault(m => m.IsPrimary) ?? e.Team.FirstOrDefault();
        int openProblems = 0;
        if (!string.IsNullOrEmpty(e.PlanOfCareId))
        {
            HomeCarePlanState plan = await HomePlan(e.PlanOfCareId).GetPlanAsync();
            openProblems = plan.Problems.Count(p => p.Status == CarePlanProblemStatus.Active);
        }
        await HomeCensus().UpsertEntryAsync(new HomeCareCensusEntry
        {
            EpisodeId = e.EpisodeId,
            PatientId = e.PatientId,
            PatientName = e.PatientName,
            ProgramType = e.ProgramType,
            LevelOfCare = e.LevelOfCare,
            Status = e.Status,
            PrimaryDiagnosisText = e.PrimaryDiagnosisText,
            PrimaryProviderId = primary?.ProviderId ?? string.Empty,
            PrimaryProviderName = primary?.Name ?? string.Empty,
            AdmissionDate = e.AdmissionDate,
            LastVisitDate = e.LastVisitDate,
            NextVisitDate = e.NextVisitDate,
            OpenProblemCount = openProblems,
            DeliveryModel = e.DeliveryModel,
            AgencyName = e.AgencyCoordination?.AgencyName ?? string.Empty
        });
    }

    private async Task<string> ResolvePatientNameAsync()
    {
        string name = (await GetPatientAsync()).Name;
        return string.IsNullOrEmpty(name) ? PatientId : name;
    }

    // ─── Episode lifecycle (writes) ─────────────────────────────────────

    public async Task<string> AdmitToHomeCareAsync(
        HomeCareProgramType programType,
        DateTime admissionDate,
        HomeCareAdmissionSource admissionSource,
        string referringProviderId,
        string referringProviderName,
        string primaryDiagnosisCode,
        string primaryDiagnosisText,
        HomeCareLevelOfCare levelOfCare,
        string clinicalNeedNarrative,
        string primaryCaregiver,
        string homeAddress,
        HomeCareDeliveryModel deliveryModel = HomeCareDeliveryModel.HospitalProvided)
    {
        string episodeId = $"HHC-EPISODE:{Guid.NewGuid()}";
        await HomeEpisode(episodeId).AdmitAsync(
            PatientId, await ResolvePatientNameAsync(), programType, admissionDate, admissionSource,
            referringProviderId, referringProviderName, primaryDiagnosisCode, primaryDiagnosisText,
            levelOfCare, clinicalNeedNarrative, primaryCaregiver, homeAddress, deliveryModel);
        await RefreshHomeCensusAsync(episodeId);
        return episodeId;
    }

    public async Task AssignHomeCareTeamMemberAsync(
        string episodeId, string providerId, string name, HomeCareDiscipline discipline, string roleTitle, bool isPrimary)
    {
        await HomeEpisode(episodeId).AddTeamMemberAsync(new HomeCareTeamMember
        {
            ProviderId = providerId,
            Name = name,
            Discipline = discipline,
            RoleTitle = roleTitle,
            IsPrimary = isPrimary,
            AssignedDate = DateTime.UtcNow
        });
        await RefreshHomeCensusAsync(episodeId);
    }

    public async Task RemoveHomeCareTeamMemberAsync(string episodeId, string providerId)
    {
        await HomeEpisode(episodeId).RemoveTeamMemberAsync(providerId);
        await RefreshHomeCensusAsync(episodeId);
    }

    public async Task UpdateHomeCareLevelOfCareAsync(string episodeId, HomeCareLevelOfCare levelOfCare)
    {
        await HomeEpisode(episodeId).UpdateLevelOfCareAsync(levelOfCare);
        await RefreshHomeCensusAsync(episodeId);
    }

    public async Task AddHomeCareSecondaryDiagnosisAsync(string episodeId, string diagnosis)
    {
        await HomeEpisode(episodeId).AddSecondaryDiagnosisAsync(diagnosis);
    }

    public async Task PutHomeCareEpisodeOnHoldAsync(string episodeId, string reason)
    {
        await HomeEpisode(episodeId).PutOnHoldAsync(reason);
        await RefreshHomeCensusAsync(episodeId);
    }

    public async Task ReactivateHomeCareEpisodeAsync(string episodeId)
    {
        await HomeEpisode(episodeId).ReactivateAsync();
        await RefreshHomeCensusAsync(episodeId);
    }

    public async Task DischargeFromHomeCareAsync(string episodeId, DateTime dischargeDate, HomeCareDischargeReason reason, string notes)
    {
        await HomeEpisode(episodeId).DischargeAsync(dischargeDate, reason, notes);
        await RefreshHomeCensusAsync(episodeId);
    }

    // ─── Plan of care (writes) ──────────────────────────────────────────

    public async Task<string> CreateHomeCarePlanAsync(string episodeId, string establishedById, string establishedByName)
    {
        string planId = $"HHC-POC:{Guid.NewGuid()}";
        await HomePlan(planId).CreateAsync(episodeId, PatientId, establishedById, establishedByName);
        await HomeEpisode(episodeId).SetPlanOfCareIdAsync(planId);
        await RefreshHomeCensusAsync(episodeId);
        return planId;
    }

    public async Task AddHomeCarePlanProblemAsync(
        string planId, string problem, string relatedTo, List<string> goals, List<string> interventions, HomeCareDiscipline responsibleDiscipline)
    {
        await HomePlan(planId).AddProblemAsync(new CarePlanProblem
        {
            Problem = problem,
            RelatedTo = relatedTo,
            Goals = goals ?? new(),
            Interventions = interventions ?? new(),
            ResponsibleDiscipline = responsibleDiscipline
        });
        HomeCarePlanState plan = await HomePlan(planId).GetPlanAsync();
        await RefreshHomeCensusAsync(plan.EpisodeId);
    }

    public async Task ResolveHomeCarePlanProblemAsync(string planId, string problemId)
    {
        await HomePlan(planId).ResolveProblemAsync(problemId);
        HomeCarePlanState plan = await HomePlan(planId).GetPlanAsync();
        await RefreshHomeCensusAsync(plan.EpisodeId);
    }

    public async Task ReviewHomeCarePlanAsync(string planId, DateTime reviewDate, DateTime? nextReviewDue)
    {
        await HomePlan(planId).ReviewAsync(reviewDate, nextReviewDue);
    }

    // ─── Visits (writes) ────────────────────────────────────────────────

    public async Task<string> ScheduleHomeVisitAsync(
        string episodeId,
        HomeCareDiscipline discipline,
        HomeVisitType visitType,
        DateTime scheduledDateTime,
        string clinicianId,
        string clinicianName,
        string reason)
    {
        string visitId = $"HHC-VISIT:{Guid.NewGuid()}";
        await HomeVisit(visitId).ScheduleAsync(
            episodeId, PatientId, await ResolvePatientNameAsync(), discipline, visitType,
            scheduledDateTime, clinicianId, clinicianName, reason);
        await HomeEpisode(episodeId).AddVisitIdAsync(visitId);
        await HomeEpisode(episodeId).RecordVisitDatesAsync(null, scheduledDateTime);
        await HomeVisitIndex().UpsertVisitAsync(BuildHomeVisitIndexEntry(await HomeVisit(visitId).GetVisitAsync()));
        await RefreshHomeCensusAsync(episodeId);
        return visitId;
    }

    public async Task StartHomeVisitAsync(string visitId)
    {
        await HomeVisit(visitId).StartAsync();
        await HomeVisitIndex().UpsertVisitAsync(BuildHomeVisitIndexEntry(await HomeVisit(visitId).GetVisitAsync()));
    }

    public async Task CompleteHomeVisitAsync(
        string visitId, int durationMinutes, string vitalSigns, List<string> interventions, string summary, string noteId, DateTime? nextVisitDate)
    {
        await HomeVisit(visitId).CompleteAsync(durationMinutes, vitalSigns, interventions ?? new(), summary, noteId, nextVisitDate);
        HomeVisitState v = await HomeVisit(visitId).GetVisitAsync();
        await HomeVisitIndex().UpsertVisitAsync(BuildHomeVisitIndexEntry(v));
        await HomeEpisode(v.EpisodeId).RecordVisitDatesAsync(v.ScheduledDateTime, nextVisitDate);
        await RefreshHomeCensusAsync(v.EpisodeId);
    }

    public async Task CancelHomeVisitAsync(string visitId, HomeVisitStatus status, string reason)
    {
        await HomeVisit(visitId).CancelAsync(status, reason);
        await HomeVisitIndex().UpsertVisitAsync(BuildHomeVisitIndexEntry(await HomeVisit(visitId).GetVisitAsync()));
    }

    // ─── Assessment (writes) ────────────────────────────────────────────

    public async Task<string> RecordHomeCareAssessmentAsync(
        string episodeId, string assessorId, string assessorName, DateTime assessmentDate, HbpcComprehensiveAssessment assessment)
    {
        string assessmentId = $"HHC-ASSESS:{Guid.NewGuid()}";
        await HomeAssessment(assessmentId).RecordComprehensiveAsync(episodeId, PatientId, assessorId, assessorName, assessmentDate, assessment);
        await HomeEpisode(episodeId).AddAssessmentIdAsync(assessmentId);
        return assessmentId;
    }

    // ─── Reads (open) ───────────────────────────────────────────────────

    public Task<HomeCareEpisodeState> GetHomeCareEpisodeAsync(string episodeId) => HomeEpisode(episodeId).GetEpisodeAsync();

    /// <summary>The current Active home-care episode for this patient, or null.</summary>
    public async Task<HomeCareEpisodeState?> GetActiveHomeCareEpisodeAsync()
    {
        List<HomeCareCensusEntry> active = await HomeCensus().GetActiveAsync();
        HomeCareCensusEntry? mine = active.FirstOrDefault(e => e.PatientId == PatientId && e.Status == HomeCareEpisodeStatus.Active);
        return mine is null ? null : await HomeEpisode(mine.EpisodeId).GetEpisodeAsync();
    }

    /// <summary>All home-care episodes (any status) for this patient, from the census roster.</summary>
    public async Task<List<HomeCareCensusEntry>> GetHomeCareEpisodesForPatientAsync()
    {
        List<HomeCareCensusEntry> all = await HomeCensus().GetAllAsync();
        return all.Where(e => e.PatientId == PatientId).OrderByDescending(e => e.AdmissionDate).ToList();
    }

    public Task<List<HomeVisitIndexEntry>> GetHomeVisitsForEpisodeAsync(string episodeId) => HomeVisitIndex().GetVisitsByEpisodeAsync(episodeId);

    public Task<HomeVisitState> GetHomeVisitAsync(string visitId) => HomeVisit(visitId).GetVisitAsync();

    public Task<HomeCarePlanState> GetHomeCarePlanAsync(string planId) => HomePlan(planId).GetPlanAsync();

    public Task<HomeCareAssessmentState> GetHomeCareAssessmentAsync(string assessmentId) => HomeAssessment(assessmentId).GetAssessmentAsync();

    public async Task<List<HomeCareAssessmentState>> GetHomeCareAssessmentsForEpisodeAsync(string episodeId)
    {
        HomeCareEpisodeState e = await HomeEpisode(episodeId).GetEpisodeAsync();
        var result = new List<HomeCareAssessmentState>();
        foreach (string id in e.AssessmentIds)
            result.Add(await HomeAssessment(id).GetAssessmentAsync());
        return result.OrderByDescending(a => a.AssessmentDate).ToList();
    }
}
