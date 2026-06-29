// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NUnit.Framework;
using Orleans.TestingHost;
using NewVistas.Abstractions.Clinical;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.FunctionalTests;

/// <summary>
/// Functional tests for the Phase-2 Medicare skilled home-health workflow (HOME_HEALTH_MEDICARE):
/// eligibility gates, 60-day certification / 30-day payment periods, OASIS capture + scrubbing,
/// PDGM grouping, EVV check-in/out, and NOA + per-period claim billing — all via
/// <see cref="IPatientWorkflowGrain"/>. The SharedCluster carries no authorization filter, so the
/// HBHC-MANAGER-gated methods are exercised directly.
/// </summary>
[TestFixture]
public class HomeCareMedicareWorkflowTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    private IPatientWorkflowGrain Workflow(string patientId)
        => _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

    // ── Helpers ─────────────────────────────────────────────────────────────────

    private static readonly DateTime AdmissionDate = new(2026, 1, 10);

    /// <summary>Admits a Medicare skilled home-health episode and returns the episode id.</summary>
    private static Task<string> AdmitMedicare(IPatientWorkflowGrain wf)
        => wf.AdmitToHomeCareAsync(
            programType: HomeCareProgramType.MedicareSkilledHomeHealth,
            admissionDate: AdmissionDate,
            admissionSource: HomeCareAdmissionSource.AcuteHospital,
            referringProviderId: "REF-001",
            referringProviderName: "Dr. Holt",
            primaryDiagnosisCode: "I50.9",
            primaryDiagnosisText: "Congestive heart failure, unspecified",
            levelOfCare: HomeCareLevelOfCare.Enhanced,
            clinicalNeedNarrative: "Post-acute CHF; homebound; skilled nursing for medication management.",
            primaryCaregiver: "Spouse — Helen",
            homeAddress: "14 Maple St, Lowell MA");

    /// <summary>A complete SOC OASIS functional item set (all M18xx + the primary-diagnosis item).</summary>
    private static Dictionary<string, string> CompleteOasisItems()
    {
        var items = OasisItems.FunctionalItems.ToDictionary(item => item, _ => "2");
        items[OasisItems.PrimaryDiagnosis] = "I50.9";
        return items;
    }

    // ── Eligibility (Medicare gates) ──────────────────────────────────────────────

    [Test]
    public async Task SetHomeCareEligibility_ReflectsHomeboundAndSkilledNeed()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);
        string episodeId = await AdmitMedicare(wf);

        await wf.SetHomeCareEligibilityAsync(
            episodeId, isHomebound: true,
            homeboundJustification: "Unable to leave home without taxing assistance.",
            skilledNeed: SkilledNeedType.SkilledNursing);

        HomeCareEpisodeState episode = await wf.GetHomeCareEpisodeAsync(episodeId);
        Assert.That(episode.Eligibility.IsHomebound, Is.True);
        Assert.That(episode.Eligibility.SkilledNeed, Is.EqualTo(SkilledNeedType.SkilledNursing));
        Assert.That(episode.Eligibility.HomeboundJustification, Does.Contain("taxing assistance"));
    }

    // ── Certification ─────────────────────────────────────────────────────────────

    [Test]
    public async Task CertifyHomeCareEpisode_OpensSixtyDayPeriod_WithTwoThirtyDayPaymentPeriods()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);
        string episodeId = await AdmitMedicare(wf);

        DateTime start = new(2026, 1, 10);
        string certId = await wf.CertifyHomeCareEpisodeAsync(
            episodeId, "MD-001", "Dr. Patel", start, faceToFaceDate: new DateTime(2026, 1, 5), isRecertification: false);

        Assert.That(certId, Is.Not.Null.And.Not.Empty);

        HomeCareEpisodeState episode = await wf.GetHomeCareEpisodeAsync(episodeId);
        Assert.That(episode.CertificationPeriods, Has.Count.EqualTo(1));

        CertificationPeriod period = episode.CertificationPeriods.Single();
        Assert.That(period.PeriodId, Is.EqualTo(certId));
        Assert.That(period.StartDate, Is.EqualTo(start));
        Assert.That(period.EndDate, Is.EqualTo(start.AddDays(59)));
        Assert.That(period.PaymentPeriods, Has.Count.EqualTo(2));

        Assert.That(period.PaymentPeriods[0].StartDate, Is.EqualTo(start));
        Assert.That(period.PaymentPeriods[0].EndDate, Is.EqualTo(start.AddDays(29)));
        Assert.That(period.PaymentPeriods[1].StartDate, Is.EqualTo(start.AddDays(30)));
        Assert.That(period.PaymentPeriods[1].EndDate, Is.EqualTo(start.AddDays(59)));
    }

    // ── OASIS capture + scrubbing ──────────────────────────────────────────────────

    [Test]
    public async Task RecordOasis_CompleteSoc_IsClean_AndStoredValidated()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);
        string episodeId = await AdmitMedicare(wf);

        OasisRecordResult result = await wf.RecordOasisAsync(
            episodeId, HomeCareAssessmentType.OasisStartOfCare, "OASIS-E2", CompleteOasisItems(),
            "RN-001", "Nurse Vega", new DateTime(2026, 1, 12));

        Assert.That(result.AssessmentId, Is.Not.Null.And.Not.Empty);
        Assert.That(result.Scrub.IsClean, Is.True);
        Assert.That(result.Scrub.Issues, Is.Empty);

        List<HomeCareAssessmentState> assessments = await wf.GetHomeCareAssessmentsForEpisodeAsync(episodeId);
        HomeCareAssessmentState recorded = assessments.Single(a => a.AssessmentId == result.AssessmentId);
        Assert.That(recorded.AssessmentType, Is.EqualTo(HomeCareAssessmentType.OasisStartOfCare));
        Assert.That(recorded.Oasis, Is.Not.Null);
        Assert.That(recorded.Oasis!.Validated, Is.True);
        Assert.That(recorded.Oasis.Version, Is.EqualTo("OASIS-E2"));
    }

    [Test]
    public async Task RecordOasis_MissingFunctionalItems_IsNotClean()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);
        string episodeId = await AdmitMedicare(wf);

        // Only the primary-diagnosis item present — all M18xx functional items missing.
        var incomplete = new Dictionary<string, string> { [OasisItems.PrimaryDiagnosis] = "I50.9" };

        OasisRecordResult result = await wf.RecordOasisAsync(
            episodeId, HomeCareAssessmentType.OasisStartOfCare, "OASIS-E2", incomplete,
            "RN-001", "Nurse Vega", new DateTime(2026, 1, 12));

        Assert.That(result.AssessmentId, Is.Not.Null.And.Not.Empty);
        Assert.That(result.Scrub.IsClean, Is.False);
        Assert.That(result.Scrub.Issues, Is.Not.Empty);

        List<HomeCareAssessmentState> assessments = await wf.GetHomeCareAssessmentsForEpisodeAsync(episodeId);
        HomeCareAssessmentState recorded = assessments.Single(a => a.AssessmentId == result.AssessmentId);
        Assert.That(recorded.Oasis, Is.Not.Null);
        Assert.That(recorded.Oasis!.Validated, Is.False);
    }

    // ── PDGM grouping ───────────────────────────────────────────────────────────────

    [Test]
    public async Task ComputePdgmGrouping_ProducesCaseMixGroup_AndStoresOnPaymentPeriod()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);
        string episodeId = await AdmitMedicare(wf);

        string certId = await wf.CertifyHomeCareEpisodeAsync(
            episodeId, "MD-001", "Dr. Patel", new DateTime(2026, 1, 10), null, false);

        // Record an OASIS so the grouper has functional items to work from.
        await wf.RecordOasisAsync(
            episodeId, HomeCareAssessmentType.OasisStartOfCare, "OASIS-E2", CompleteOasisItems(),
            "RN-001", "Nurse Vega", new DateTime(2026, 1, 12));

        HomeCareEpisodeState before = await wf.GetHomeCareEpisodeAsync(episodeId);
        string ppId = before.CertificationPeriods.Single().PaymentPeriods.First().PeriodId;

        PdgmGroupingResult grouping = await wf.ComputePdgmGroupingAsync(episodeId, certId, ppId);

        Assert.That(grouping.CaseMixGroup, Is.Not.Null.And.Not.Empty);
        // Institutional (acute hospital) + first (early) period → HIPPS starts with '3'.
        Assert.That(grouping.CaseMixGroup, Does.StartWith("3"));

        HomeCareEpisodeState after = await wf.GetHomeCareEpisodeAsync(episodeId);
        PaymentPeriod stored = after.CertificationPeriods.Single().PaymentPeriods.Single(p => p.PeriodId == ppId);
        Assert.That(stored.Grouping, Is.Not.Null);
        Assert.That(stored.Grouping!.CaseMixGroup, Is.EqualTo(grouping.CaseMixGroup));
    }

    // ── EVV (Electronic Visit Verification) ─────────────────────────────────────────

    [Test]
    public async Task CheckInThenCheckOutHomeVisit_RecordsEvvTimesAndMethod()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);
        string episodeId = await AdmitMedicare(wf);

        string visitId = await wf.ScheduleHomeVisitAsync(
            episodeId, HomeCareDiscipline.SkilledNursing, HomeVisitType.Routine,
            DateTime.UtcNow.AddDays(1), "RN-001", "Nurse Vega", "Skilled nursing visit");

        await wf.CheckInHomeVisitAsync(visitId, "42.33,-71.30", EvvMethod.Gps);
        await wf.CheckOutHomeVisitAsync(visitId, "42.33,-71.30");

        HomeVisitState visit = await wf.GetHomeVisitAsync(visitId);
        Assert.That(visit.CheckInTime, Is.Not.Null);
        Assert.That(visit.CheckOutTime, Is.Not.Null);
        Assert.That(visit.EvvMethod, Is.EqualTo(EvvMethod.Gps));
        Assert.That(visit.CheckInLocation, Is.EqualTo("42.33,-71.30"));
        Assert.That(visit.CheckOutTime, Is.GreaterThanOrEqualTo(visit.CheckInTime));
    }

    // ── Billing: Notice of Admission ─────────────────────────────────────────────────

    [Test]
    public async Task SubmitNoticeOfAdmission_OnTime_StatusSubmitted_NotLate()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);
        string episodeId = await AdmitMedicare(wf);

        // Admission 2026-01-10; submitted within 5 days → on time.
        await wf.SubmitHomeHealthNoticeOfAdmissionAsync(episodeId, new DateTime(2026, 1, 13));

        HomeHealthBillingState billing = await wf.GetHomeHealthBillingAsync(episodeId);
        Assert.That(billing.Noa.Status, Is.EqualTo(NoaStatus.Submitted));
        Assert.That(billing.Noa.SubmittedDate, Is.EqualTo(new DateTime(2026, 1, 13)));
        Assert.That(billing.Noa.IsLate, Is.False);
        Assert.That(billing.Noa.ControlNumber, Is.Not.Empty);
    }

    [Test]
    public async Task SubmitNoticeOfAdmission_Late_FlagsIsLate()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);
        string episodeId = await AdmitMedicare(wf);

        // Admission 2026-01-10; submitted more than 5 days later → late.
        await wf.SubmitHomeHealthNoticeOfAdmissionAsync(episodeId, new DateTime(2026, 1, 20));

        HomeHealthBillingState billing = await wf.GetHomeHealthBillingAsync(episodeId);
        Assert.That(billing.Noa.Status, Is.EqualTo(NoaStatus.Submitted));
        Assert.That(billing.Noa.IsLate, Is.True);
    }

    // ── Billing: claim generation + submission ───────────────────────────────────────

    [Test]
    public async Task GenerateThenSubmitHomeHealthClaim_CarriesHippsAndControlNumber()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);
        string episodeId = await AdmitMedicare(wf);

        string certId = await wf.CertifyHomeCareEpisodeAsync(
            episodeId, "MD-001", "Dr. Patel", new DateTime(2026, 1, 10), null, false);
        await wf.RecordOasisAsync(
            episodeId, HomeCareAssessmentType.OasisStartOfCare, "OASIS-E2", CompleteOasisItems(),
            "RN-001", "Nurse Vega", new DateTime(2026, 1, 12));

        HomeCareEpisodeState episode = await wf.GetHomeCareEpisodeAsync(episodeId);
        string ppId = episode.CertificationPeriods.Single().PaymentPeriods.First().PeriodId;

        PdgmGroupingResult grouping = await wf.ComputePdgmGroupingAsync(episodeId, certId, ppId);

        string claimId = await wf.GenerateHomeHealthClaimAsync(episodeId, certId, ppId);
        Assert.That(claimId, Is.Not.Null.And.Not.Empty);

        HomeHealthBillingState afterGenerate = await wf.GetHomeHealthBillingAsync(episodeId);
        HomeHealthClaim claim = afterGenerate.Claims.Single(c => c.ClaimId == claimId);
        Assert.That(claim.HippsCode, Is.EqualTo(grouping.CaseMixGroup));
        Assert.That(claim.PaymentPeriodId, Is.EqualTo(ppId));
        Assert.That(claim.Status, Is.EqualTo(HomeHealthClaimStatus.Draft));

        await wf.SubmitHomeHealthClaimAsync(episodeId, claimId, new DateTime(2026, 3, 15));

        HomeHealthBillingState afterSubmit = await wf.GetHomeHealthBillingAsync(episodeId);
        HomeHealthClaim submitted = afterSubmit.Claims.Single(c => c.ClaimId == claimId);
        Assert.That(submitted.Status, Is.EqualTo(HomeHealthClaimStatus.Submitted));
        Assert.That(submitted.SubmittedDate, Is.EqualTo(new DateTime(2026, 3, 15)));
        Assert.That(submitted.ControlNumber, Is.Not.Empty);
    }
}
