// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NUnit.Framework;
using Orleans.TestingHost;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.FunctionalTests;

/// <summary>
/// Functional tests for the Home-Based Care (HBPC) module — VistA Files #750 / #750.1.
/// Tests end-to-end workflows via <see cref="IPatientWorkflowGrain"/>.
/// The facility census and visit-index are shared singletons across the whole test run,
/// so assertions about them use membership/contains for this test's unique ids — never
/// exact totals.
/// </summary>
[TestFixture]
public class HomeCareWorkflowTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    private IPatientWorkflowGrain Workflow(string patientId)
        => _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

    private IHomeCareCensusGrain Census()
        => _cluster.GrainFactory.GetGrain<IHomeCareCensusGrain>("HHC-CENSUS:DEFAULT");

    private IHomeVisitIndexGrain VisitIndex()
        => _cluster.GrainFactory.GetGrain<IHomeVisitIndexGrain>("HHC-VISIT-INDEX");

    // ── Helpers ─────────────────────────────────────────────────────────────────

    private Task<string> Admit(IPatientWorkflowGrain wf)
        => wf.AdmitToHomeCareAsync(
            programType: HomeCareProgramType.HomeBasedPrimaryCare,
            admissionDate: new DateTime(2026, 1, 10),
            admissionSource: HomeCareAdmissionSource.AcuteHospital,
            referringProviderId: "REF-001",
            referringProviderName: "Dr. Holt",
            primaryDiagnosisCode: "I50.9",
            primaryDiagnosisText: "Congestive heart failure, unspecified",
            levelOfCare: HomeCareLevelOfCare.Enhanced,
            clinicalNeedNarrative: "Frail elder with advanced CHF; multiple recent admissions; homebound.",
            primaryCaregiver: "Spouse — Helen",
            homeAddress: "14 Maple St, Lowell MA");

    private static HbpcComprehensiveAssessment SampleAssessment() => new()
    {
        FunctionalStatus = "Needs assist with bathing and transfers.",
        InstrumentalAdls = "Spouse manages meds and meals.",
        HomeSafety = "Loose rugs removed; grab bars installed.",
        CaregiverSupport = "Devoted spouse, some caregiver fatigue.",
        CognitiveMentalStatus = "Alert, oriented x3.",
        Nutrition = "Stable weight; low-sodium diet.",
        MedicationReconciliation = "Reconciled; no discrepancies.",
        FallRisk = "Moderate — prior fall 3 months ago.",
        Summary = "Stable on current plan; continue weekly RN visits."
    };

    // ── Admission / episode read-back ───────────────────────────────────────────

    [Test]
    public async Task AdmitToHomeCare_ReturnsEpisodeId_AndReflectsAdmissionDetails()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        string episodeId = await Admit(wf);

        Assert.That(episodeId, Is.Not.Null.And.Not.Empty);

        HomeCareEpisodeState episode = await wf.GetHomeCareEpisodeAsync(episodeId);
        Assert.That(episode.PatientId, Is.EqualTo(patientId));
        Assert.That(episode.ProgramType, Is.EqualTo(HomeCareProgramType.HomeBasedPrimaryCare));
        Assert.That(episode.AdmissionSource, Is.EqualTo(HomeCareAdmissionSource.AcuteHospital));
        Assert.That(episode.LevelOfCare, Is.EqualTo(HomeCareLevelOfCare.Enhanced));
        Assert.That(episode.PrimaryDiagnosisCode, Is.EqualTo("I50.9"));
        Assert.That(episode.Eligibility.ClinicalNeedNarrative, Does.Contain("CHF"));
        Assert.That(episode.Status, Is.EqualTo(HomeCareEpisodeStatus.Active));
    }

    [Test]
    public async Task GetActiveHomeCareEpisode_ReturnsTheOpenEpisode()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        string episodeId = await Admit(wf);

        HomeCareEpisodeState? active = await wf.GetActiveHomeCareEpisodeAsync();
        Assert.That(active, Is.Not.Null);
        Assert.That(active!.EpisodeId, Is.EqualTo(episodeId));
        Assert.That(active.Status, Is.EqualTo(HomeCareEpisodeStatus.Active));
    }

    [Test]
    public async Task GetHomeCareEpisodesForPatient_ContainsTheEpisode()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        string episodeId = await Admit(wf);

        List<HomeCareCensusEntry> episodes = await wf.GetHomeCareEpisodesForPatientAsync();
        Assert.That(episodes.Select(e => e.EpisodeId), Contains.Item(episodeId));
    }

    // ── Interdisciplinary team ──────────────────────────────────────────────────

    [Test]
    public async Task AssignHomeCareTeamMember_AddsMembers()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);
        string episodeId = await Admit(wf);

        await wf.AssignHomeCareTeamMemberAsync(episodeId, "MD-001", "Dr. Patel", HomeCareDiscipline.Physician, "Medical Director", true);
        await wf.AssignHomeCareTeamMemberAsync(episodeId, "RN-001", "Nurse Vega", HomeCareDiscipline.SkilledNursing, "Primary RN", false);

        HomeCareEpisodeState episode = await wf.GetHomeCareEpisodeAsync(episodeId);
        Assert.That(episode.Team, Has.Count.EqualTo(2));
        Assert.That(episode.Team.Select(m => m.ProviderId), Contains.Item("MD-001"));
        Assert.That(episode.Team.Select(m => m.ProviderId), Contains.Item("RN-001"));
    }

    [Test]
    public async Task AssignHomeCareTeamMember_SecondPrimary_FlipsFirstOff()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);
        string episodeId = await Admit(wf);

        await wf.AssignHomeCareTeamMemberAsync(episodeId, "MD-001", "Dr. Patel", HomeCareDiscipline.Physician, "Medical Director", true);
        await wf.AssignHomeCareTeamMemberAsync(episodeId, "NP-001", "NP Reyes", HomeCareDiscipline.NursePractitioner, "Lead NP", true);

        HomeCareEpisodeState episode = await wf.GetHomeCareEpisodeAsync(episodeId);
        Assert.That(episode.Team.Count(m => m.IsPrimary), Is.EqualTo(1));
        Assert.That(episode.Team.Single(m => m.IsPrimary).ProviderId, Is.EqualTo("NP-001"));
    }

    [Test]
    public async Task RemoveHomeCareTeamMember_RemovesByProviderId()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);
        string episodeId = await Admit(wf);

        await wf.AssignHomeCareTeamMemberAsync(episodeId, "MD-001", "Dr. Patel", HomeCareDiscipline.Physician, "Medical Director", true);
        await wf.AssignHomeCareTeamMemberAsync(episodeId, "RN-001", "Nurse Vega", HomeCareDiscipline.SkilledNursing, "Primary RN", false);

        await wf.RemoveHomeCareTeamMemberAsync(episodeId, "RN-001");

        HomeCareEpisodeState episode = await wf.GetHomeCareEpisodeAsync(episodeId);
        Assert.That(episode.Team, Has.Count.EqualTo(1));
        Assert.That(episode.Team.Select(m => m.ProviderId), Does.Not.Contain("RN-001"));
    }

    // ── Level of care / diagnoses ───────────────────────────────────────────────

    [Test]
    public async Task UpdateHomeCareLevelOfCare_ChangesTheLevel()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);
        string episodeId = await Admit(wf);

        await wf.UpdateHomeCareLevelOfCareAsync(episodeId, HomeCareLevelOfCare.Palliative);

        HomeCareEpisodeState episode = await wf.GetHomeCareEpisodeAsync(episodeId);
        Assert.That(episode.LevelOfCare, Is.EqualTo(HomeCareLevelOfCare.Palliative));
    }

    [Test]
    public async Task AddHomeCareSecondaryDiagnosis_AppendsWithDedup()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);
        string episodeId = await Admit(wf);

        await wf.AddHomeCareSecondaryDiagnosisAsync(episodeId, "E11.9 — Type 2 diabetes");
        await wf.AddHomeCareSecondaryDiagnosisAsync(episodeId, "N18.3 — CKD stage 3");
        await wf.AddHomeCareSecondaryDiagnosisAsync(episodeId, "E11.9 — Type 2 diabetes"); // duplicate

        HomeCareEpisodeState episode = await wf.GetHomeCareEpisodeAsync(episodeId);
        Assert.That(episode.SecondaryDiagnoses, Has.Count.EqualTo(2));
        Assert.That(episode.SecondaryDiagnoses, Contains.Item("N18.3 — CKD stage 3"));
    }

    // ── Plan of care ────────────────────────────────────────────────────────────

    [Test]
    public async Task CreateHomeCarePlan_ReturnsPlanId_AndLinksToEpisode()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);
        string episodeId = await Admit(wf);

        string planId = await wf.CreateHomeCarePlanAsync(episodeId, "MD-001", "Dr. Patel");

        Assert.That(planId, Is.Not.Null.And.Not.Empty);

        HomeCareEpisodeState episode = await wf.GetHomeCareEpisodeAsync(episodeId);
        Assert.That(episode.PlanOfCareId, Is.EqualTo(planId));

        HomeCarePlanState plan = await wf.GetHomeCarePlanAsync(planId);
        Assert.That(plan.EpisodeId, Is.EqualTo(episodeId));
        Assert.That(plan.PatientId, Is.EqualTo(patientId));
        Assert.That(plan.EstablishedByName, Is.EqualTo("Dr. Patel"));
    }

    [Test]
    public async Task AddHomeCarePlanProblem_AddsProblem()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);
        string episodeId = await Admit(wf);
        string planId = await wf.CreateHomeCarePlanAsync(episodeId, "MD-001", "Dr. Patel");

        await wf.AddHomeCarePlanProblemAsync(
            planId,
            problem: "Activity intolerance",
            relatedTo: "CHF / deconditioning",
            goals: new List<string> { "Walk 50 ft with rest breaks in 4 weeks" },
            interventions: new List<string> { "PT 2x/week", "Energy-conservation teaching" },
            responsibleDiscipline: HomeCareDiscipline.PhysicalTherapy);

        HomeCarePlanState plan = await wf.GetHomeCarePlanAsync(planId);
        Assert.That(plan.Problems, Has.Count.EqualTo(1));
        Assert.That(plan.Problems[0].Problem, Is.EqualTo("Activity intolerance"));
        Assert.That(plan.Problems[0].ResponsibleDiscipline, Is.EqualTo(HomeCareDiscipline.PhysicalTherapy));
        Assert.That(plan.Problems[0].Status, Is.EqualTo(CarePlanProblemStatus.Active));
        Assert.That(plan.Problems[0].Goals, Contains.Item("Walk 50 ft with rest breaks in 4 weeks"));
    }

    [Test]
    public async Task ResolveHomeCarePlanProblem_SetsProblemResolved()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);
        string episodeId = await Admit(wf);
        string planId = await wf.CreateHomeCarePlanAsync(episodeId, "MD-001", "Dr. Patel");

        await wf.AddHomeCarePlanProblemAsync(
            planId, "Wound care", "Stage 2 sacral ulcer",
            new List<string> { "Wound closure" }, new List<string> { "Daily dressing changes" },
            HomeCareDiscipline.SkilledNursing);

        HomeCarePlanState plan = await wf.GetHomeCarePlanAsync(planId);
        string problemId = plan.Problems[0].ProblemId;

        await wf.ResolveHomeCarePlanProblemAsync(planId, problemId);

        HomeCarePlanState resolved = await wf.GetHomeCarePlanAsync(planId);
        Assert.That(resolved.Problems.Single(p => p.ProblemId == problemId).Status,
            Is.EqualTo(CarePlanProblemStatus.Resolved));
    }

    // ── Visits ──────────────────────────────────────────────────────────────────

    [Test]
    public async Task ScheduleHomeVisit_ReturnsVisitId_AndAppearsForEpisode()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);
        string episodeId = await Admit(wf);

        string visitId = await wf.ScheduleHomeVisitAsync(
            episodeId, HomeCareDiscipline.SkilledNursing, HomeVisitType.Routine,
            DateTime.UtcNow.AddDays(2), "RN-001", "Nurse Vega", "Weekly CHF assessment");

        Assert.That(visitId, Is.Not.Null.And.Not.Empty);

        List<HomeVisitIndexEntry> visits = await wf.GetHomeVisitsForEpisodeAsync(episodeId);
        Assert.That(visits.Select(v => v.VisitId), Contains.Item(visitId));
        Assert.That(visits.Single(v => v.VisitId == visitId).Status, Is.EqualTo(HomeVisitStatus.Scheduled));
    }

    [Test]
    public async Task StartHomeVisit_SetsInProgress()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);
        string episodeId = await Admit(wf);
        string visitId = await wf.ScheduleHomeVisitAsync(
            episodeId, HomeCareDiscipline.SkilledNursing, HomeVisitType.Routine,
            DateTime.UtcNow.AddDays(1), "RN-001", "Nurse Vega", "Routine visit");

        await wf.StartHomeVisitAsync(visitId);

        HomeVisitState visit = await wf.GetHomeVisitAsync(visitId);
        Assert.That(visit.Status, Is.EqualTo(HomeVisitStatus.InProgress));
    }

    [Test]
    public async Task CompleteHomeVisit_SetsCompleted_AndUpdatesEpisodeLastVisitDate()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);
        string episodeId = await Admit(wf);
        DateTime scheduled = new DateTime(2026, 2, 1, 10, 0, 0);
        string visitId = await wf.ScheduleHomeVisitAsync(
            episodeId, HomeCareDiscipline.SkilledNursing, HomeVisitType.Routine,
            scheduled, "RN-001", "Nurse Vega", "Routine visit");
        await wf.StartHomeVisitAsync(visitId);

        await wf.CompleteHomeVisitAsync(
            visitId,
            durationMinutes: 45,
            vitalSigns: "BP 132/80, HR 74, O2 95%",
            interventions: new List<string> { "Weight check", "Med review" },
            summary: "Stable. Continue current plan.",
            noteId: "TIU-555",
            nextVisitDate: new DateTime(2026, 2, 8, 10, 0, 0));

        HomeVisitState visit = await wf.GetHomeVisitAsync(visitId);
        Assert.That(visit.Status, Is.EqualTo(HomeVisitStatus.Completed));
        Assert.That(visit.DurationMinutes, Is.EqualTo(45));

        HomeCareEpisodeState episode = await wf.GetHomeCareEpisodeAsync(episodeId);
        Assert.That(episode.LastVisitDate, Is.EqualTo(scheduled));
    }

    [Test]
    public async Task CancelHomeVisit_NoAnswer_SetsStatusAndReason()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);
        string episodeId = await Admit(wf);
        string visitId = await wf.ScheduleHomeVisitAsync(
            episodeId, HomeCareDiscipline.SkilledNursing, HomeVisitType.Routine,
            DateTime.UtcNow.AddDays(1), "RN-001", "Nurse Vega", "Routine visit");

        await wf.CancelHomeVisitAsync(visitId, HomeVisitStatus.NoAnswer, "No answer at door; left card.");

        HomeVisitState visit = await wf.GetHomeVisitAsync(visitId);
        Assert.That(visit.Status, Is.EqualTo(HomeVisitStatus.NoAnswer));
        Assert.That(visit.CancellationReason, Does.Contain("No answer"));
    }

    [Test]
    public async Task CancelHomeVisit_PatientRefused_SetsStatusAndReason()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);
        string episodeId = await Admit(wf);
        string visitId = await wf.ScheduleHomeVisitAsync(
            episodeId, HomeCareDiscipline.HomeHealthAide, HomeVisitType.Routine,
            DateTime.UtcNow.AddDays(1), "AIDE-001", "Aide Brooks", "Personal care");

        await wf.CancelHomeVisitAsync(visitId, HomeVisitStatus.PatientRefused, "Patient declined today.");

        HomeVisitState visit = await wf.GetHomeVisitAsync(visitId);
        Assert.That(visit.Status, Is.EqualTo(HomeVisitStatus.PatientRefused));
        Assert.That(visit.CancellationReason, Does.Contain("declined"));
    }

    // ── Comprehensive assessment ────────────────────────────────────────────────

    [Test]
    public async Task RecordHomeCareAssessment_ReturnsId_AndReturnsComprehensivePayload()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);
        string episodeId = await Admit(wf);

        string assessmentId = await wf.RecordHomeCareAssessmentAsync(
            episodeId, "RN-001", "Nurse Vega", new DateTime(2026, 1, 12), SampleAssessment());

        Assert.That(assessmentId, Is.Not.Null.And.Not.Empty);

        List<HomeCareAssessmentState> assessments = await wf.GetHomeCareAssessmentsForEpisodeAsync(episodeId);
        Assert.That(assessments.Select(a => a.AssessmentId), Contains.Item(assessmentId));

        HomeCareAssessmentState recorded = assessments.Single(a => a.AssessmentId == assessmentId);
        Assert.That(recorded.AssessmentType, Is.EqualTo(HomeCareAssessmentType.ComprehensiveHbpc));
        Assert.That(recorded.AssessorName, Is.EqualTo("Nurse Vega"));
        Assert.That(recorded.Comprehensive.FallRisk, Does.Contain("Moderate"));
        Assert.That(recorded.Comprehensive.Summary, Does.Contain("weekly RN visits"));
    }

    // ── Episode lifecycle: hold / reactivate / discharge ────────────────────────

    [Test]
    public async Task PutOnHoldThenReactivate_TogglesStatus()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);
        string episodeId = await Admit(wf);

        await wf.PutHomeCareEpisodeOnHoldAsync(episodeId, "Patient admitted to hospital.");

        HomeCareEpisodeState onHold = await wf.GetHomeCareEpisodeAsync(episodeId);
        Assert.That(onHold.Status, Is.EqualTo(HomeCareEpisodeStatus.OnHold));
        Assert.That(onHold.OnHoldReason, Does.Contain("hospital"));

        await wf.ReactivateHomeCareEpisodeAsync(episodeId);

        HomeCareEpisodeState reactivated = await wf.GetHomeCareEpisodeAsync(episodeId);
        Assert.That(reactivated.Status, Is.EqualTo(HomeCareEpisodeStatus.Active));
        Assert.That(reactivated.OnHoldReason, Is.Empty);
    }

    [Test]
    public async Task DischargeFromHomeCare_SetsDischargedWithDateAndReason()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);
        string episodeId = await Admit(wf);

        DateTime dischargeDate = new DateTime(2026, 6, 1);
        await wf.DischargeFromHomeCareAsync(
            episodeId, dischargeDate, HomeCareDischargeReason.GoalsMet, "Patient stable; transitioned to clinic follow-up.");

        HomeCareEpisodeState episode = await wf.GetHomeCareEpisodeAsync(episodeId);
        Assert.That(episode.Status, Is.EqualTo(HomeCareEpisodeStatus.Discharged));
        Assert.That(episode.DischargeDate, Is.EqualTo(dischargeDate));
        Assert.That(episode.DischargeReason, Is.EqualTo(HomeCareDischargeReason.GoalsMet));
    }

    // ── Facility census singleton (membership only — shared across the run) ──────

    [Test]
    public async Task Census_AfterAdmit_ContainsEpisode_AndWorkloadHasActive()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);
        string episodeId = await Admit(wf);

        List<HomeCareCensusEntry> activeEntries = await Census().GetActiveAsync();
        Assert.That(activeEntries.Select(e => e.EpisodeId), Contains.Item(episodeId));

        HomeCareWorkloadStats stats = await Census().GetWorkloadStatsAsync();
        Assert.That(stats.ActiveEpisodes, Is.GreaterThanOrEqualTo(1));
    }

    [Test]
    public async Task Census_AfterDischarge_NotActiveButStillInAll()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);
        string episodeId = await Admit(wf);

        await wf.DischargeFromHomeCareAsync(
            episodeId, new DateTime(2026, 6, 1), HomeCareDischargeReason.GoalsMet, "Goals met.");

        List<HomeCareCensusEntry> activeEntries = await Census().GetActiveAsync();
        Assert.That(activeEntries.Select(e => e.EpisodeId), Does.Not.Contain(episodeId));

        List<HomeCareCensusEntry> allEntries = await Census().GetAllAsync();
        Assert.That(allEntries.Select(e => e.EpisodeId), Contains.Item(episodeId));
    }

    // ── Facility visit index singleton (membership only — shared across the run) ─

    [Test]
    public async Task VisitIndex_ContainsScheduledFutureVisit_ByClinicianAndUpcoming()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);
        string episodeId = await Admit(wf);

        string clinicianId = $"RN-{Guid.NewGuid()}";
        string visitId = await wf.ScheduleHomeVisitAsync(
            episodeId, HomeCareDiscipline.SkilledNursing, HomeVisitType.Routine,
            DateTime.UtcNow.AddDays(3), clinicianId, "Nurse Vega", "Weekly visit");

        List<HomeVisitIndexEntry> byClinician = await VisitIndex().GetVisitsByClinicianAsync(clinicianId);
        Assert.That(byClinician.Select(v => v.VisitId), Contains.Item(visitId));

        List<HomeVisitIndexEntry> upcoming = await VisitIndex().GetUpcomingVisitsAsync(7);
        Assert.That(upcoming.Select(v => v.VisitId), Contains.Item(visitId));
    }
}
