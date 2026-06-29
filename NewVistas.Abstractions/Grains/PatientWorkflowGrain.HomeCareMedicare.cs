// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.Clinical;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// Medicare skilled home-health orchestration (Phase 2 / HOME_HEALTH_MEDICARE): eligibility gates,
/// 60-day certification periods (with 30-day payment periods), OASIS capture + scrubbing, PDGM
/// grouping, EVV, and NOA/claim billing. Layers on the same episode/visit/assessment grains as
/// HBPC (Phase 1); writes gated by HBHC MANAGER, reads open.
/// </summary>
public partial class PatientWorkflowGrain
{
    private IHomeHealthBillingGrain HomeBilling(string episodeId) =>
        GrainFactory.GetGrain<IHomeHealthBillingGrain>($"HHC-BILLING:{episodeId}");

    // ─── Eligibility (Medicare gates) ───────────────────────────────────

    public async Task SetHomeCareEligibilityAsync(string episodeId, bool isHomebound, string homeboundJustification, SkilledNeedType skilledNeed)
    {
        HomeCareEpisodeState e = await HomeEpisode(episodeId).GetEpisodeAsync();
        HomeCareEligibility elig = e.Eligibility;
        elig.IsHomebound = isHomebound;
        elig.HomeboundJustification = homeboundJustification;
        elig.SkilledNeed = skilledNeed;
        await HomeEpisode(episodeId).UpdateEligibilityAsync(elig);
    }

    // ─── Certification ──────────────────────────────────────────────────

    /// <summary>
    /// Opens a 60-day certification period (with its two 30-day payment periods) on the episode.
    /// Returns the certification-period id.
    /// </summary>
    public async Task<string> CertifyHomeCareEpisodeAsync(
        string episodeId, string certifyingProviderId, string certifyingProviderName, DateTime periodStart, DateTime? faceToFaceDate, bool isRecertification)
    {
        string certId = $"HHC-CERT:{Guid.NewGuid()}";
        var period = new CertificationPeriod
        {
            PeriodId = certId,
            StartDate = periodStart,
            EndDate = periodStart.AddDays(59),
            IsRecertification = isRecertification,
            CertifyingProviderId = certifyingProviderId,
            FaceToFaceEncounterDate = faceToFaceDate,
            PaymentPeriods =
            {
                new PaymentPeriod { PeriodId = $"HHC-PAY:{Guid.NewGuid()}", StartDate = periodStart, EndDate = periodStart.AddDays(29) },
                new PaymentPeriod { PeriodId = $"HHC-PAY:{Guid.NewGuid()}", StartDate = periodStart.AddDays(30), EndDate = periodStart.AddDays(59) }
            }
        };
        await HomeEpisode(episodeId).OpenCertificationPeriodAsync(period);
        return certId;
    }

    // ─── OASIS ──────────────────────────────────────────────────────────

    /// <summary>
    /// Records an OASIS assessment, scrubs it (deterministic validation), stores the Validated
    /// flag, and returns the new assessment id with the scrub result.
    /// </summary>
    public async Task<OasisRecordResult> RecordOasisAsync(
        string episodeId, HomeCareAssessmentType assessmentType, string oasisVersion, Dictionary<string, string> items, string assessorId, string assessorName, DateTime assessmentDate)
    {
        var oasis = new OasisDataSet { Version = oasisVersion, Items = items ?? new() };
        OasisScrubResult scrub = OasisScrubber.Scrub(oasis, assessmentType);
        oasis.Validated = scrub.IsClean;

        string assessmentId = $"HHC-ASSESS:{Guid.NewGuid()}";
        await HomeAssessment(assessmentId).RecordOasisAsync(episodeId, PatientId, assessmentType, assessorId, assessorName, assessmentDate, oasis);
        await HomeEpisode(episodeId).AddAssessmentIdAsync(assessmentId);
        return new OasisRecordResult { AssessmentId = assessmentId, Scrub = scrub };
    }

    // ─── PDGM grouping ──────────────────────────────────────────────────

    /// <summary>
    /// Computes the PDGM case-mix grouping for a 30-day payment period from the episode's
    /// admission source/timing, principal + secondary diagnoses, the latest OASIS functional
    /// items, and the period's completed-visit count; stores it on the payment period.
    /// </summary>
    public async Task<PdgmGroupingResult> ComputePdgmGroupingAsync(string episodeId, string certificationPeriodId, string paymentPeriodId)
    {
        HomeCareEpisodeState e = await HomeEpisode(episodeId).GetEpisodeAsync();
        CertificationPeriod? cert = e.CertificationPeriods.FirstOrDefault(c => c.PeriodId == certificationPeriodId);
        PaymentPeriod? pp = cert?.PaymentPeriods.FirstOrDefault(p => p.PeriodId == paymentPeriodId);
        if (pp is null) return new PdgmGroupingResult();

        // Timing: early = the chronologically first payment period across all certification periods.
        List<PaymentPeriod> allPeriods = e.CertificationPeriods.SelectMany(c => c.PaymentPeriods).OrderBy(p => p.StartDate).ToList();
        bool isEarly = allPeriods.Count > 0 && allPeriods[0].PeriodId == paymentPeriodId;

        // Latest OASIS functional items.
        Dictionary<string, string> oasisItems = new();
        DateTime latest = DateTime.MinValue;
        foreach (string aid in e.AssessmentIds)
        {
            HomeCareAssessmentState a = await HomeAssessment(aid).GetAssessmentAsync();
            if (a.Oasis is not null && a.AssessmentDate >= latest)
            {
                latest = a.AssessmentDate;
                oasisItems = a.Oasis.Items;
            }
        }

        // Completed visits within the payment period.
        List<HomeVisitIndexEntry> visits = await HomeVisitIndex().GetVisitsByEpisodeAsync(episodeId);
        int visitCount = visits.Count(v => v.Status == HomeVisitStatus.Completed
                                           && v.ScheduledDateTime >= pp.StartDate && v.ScheduledDateTime <= pp.EndDate);

        PdgmGroupingResult result = HomeHealthGrouper.Group(new HomeHealthGroupingInput
        {
            AdmissionSource = e.AdmissionSource,
            IsEarlyPeriod = isEarly,
            PrimaryDiagnosisCode = e.PrimaryDiagnosisCode,
            SecondaryDiagnoses = e.SecondaryDiagnoses,
            OasisItems = oasisItems,
            VisitCount = visitCount
        });

        await HomeEpisode(episodeId).SetPaymentPeriodGroupingAsync(certificationPeriodId, paymentPeriodId, result);
        return result;
    }

    // ─── EVV ────────────────────────────────────────────────────────────

    public async Task CheckInHomeVisitAsync(string visitId, string location, EvvMethod method)
    {
        await HomeVisit(visitId).CheckInAsync(DateTime.UtcNow, location, method);
        await HomeVisitIndex().UpsertVisitAsync(BuildHomeVisitIndexEntry(await HomeVisit(visitId).GetVisitAsync()));
    }

    public async Task CheckOutHomeVisitAsync(string visitId, string location)
    {
        await HomeVisit(visitId).CheckOutAsync(DateTime.UtcNow, location);
        await HomeVisitIndex().UpsertVisitAsync(BuildHomeVisitIndexEntry(await HomeVisit(visitId).GetVisitAsync()));
    }

    // ─── Billing (NOA + claims) ─────────────────────────────────────────

    public async Task SubmitHomeHealthNoticeOfAdmissionAsync(string episodeId, DateTime submittedDate)
    {
        HomeCareEpisodeState e = await HomeEpisode(episodeId).GetEpisodeAsync();
        await HomeBilling(episodeId).SubmitNoticeOfAdmissionAsync(e.PatientId, e.AdmissionDate, submittedDate);
    }

    /// <summary>Generates a claim for a payment period from its computed PDGM grouping. Returns the claim id.</summary>
    public async Task<string> GenerateHomeHealthClaimAsync(string episodeId, string certificationPeriodId, string paymentPeriodId)
    {
        HomeCareEpisodeState e = await HomeEpisode(episodeId).GetEpisodeAsync();
        CertificationPeriod? cert = e.CertificationPeriods.FirstOrDefault(c => c.PeriodId == certificationPeriodId);
        PaymentPeriod? pp = cert?.PaymentPeriods.FirstOrDefault(p => p.PeriodId == paymentPeriodId);
        PdgmGroupingResult? g = pp?.Grouping;
        string hipps = g?.CaseMixGroup ?? string.Empty;
        bool isLupa = g?.IsLupa ?? false;
        return await HomeBilling(episodeId).GenerateClaimAsync(certificationPeriodId, paymentPeriodId, hipps, isLupa);
    }

    public Task SubmitHomeHealthClaimAsync(string episodeId, string claimId, DateTime submittedDate) =>
        HomeBilling(episodeId).SubmitClaimAsync(claimId, submittedDate);

    public Task<HomeHealthBillingState> GetHomeHealthBillingAsync(string episodeId) =>
        HomeBilling(episodeId).GetBillingAsync();
}
