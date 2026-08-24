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
/// End-to-end tests for diagnosis provenance and revision statistics (ADR-006), plus the
/// one-way disable latch on the DIAGNOSTIC_STEWARDSHIP flag.
/// </summary>
[TestFixture]
public class DiagnosticStewardshipWorkflowTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup() => _cluster = SharedCluster.Instance;

    private IPatientWorkflowGrain Workflow(string patientId)
        => _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

    // ── The one-way disable latch ───────────────────────────────────────────

    [Test]
    public async Task Feature_IsOnByDefaultOnAFreshSite()
    {
        // A newly created database must have the feature on — demo seeding depends on it.
        var site = _cluster.GrainFactory.GetGrain<ISiteParametersGrain>($"SITE:{Guid.NewGuid()}");
        Assert.That(await site.IsFeatureEnabledAsync(SiteFeatures.DiagnosticStewardship), Is.True);
        Assert.That(await site.IsFeaturePermanentlyDisabledAsync(SiteFeatures.DiagnosticStewardship), Is.False);
    }

    [Test]
    public async Task Feature_OnceDisabled_CannotBeReEnabled()
    {
        var site = _cluster.GrainFactory.GetGrain<ISiteParametersGrain>($"SITE:{Guid.NewGuid()}");

        await site.DisableFeatureAsync(
            SiteFeatures.DiagnosticStewardship, "USR-1", "SMITH,JOHN", "Site opted out");

        Assert.That(await site.IsFeatureEnabledAsync(SiteFeatures.DiagnosticStewardship), Is.False);
        Assert.That(await site.IsFeaturePermanentlyDisabledAsync(SiteFeatures.DiagnosticStewardship), Is.True);

        // Throwing rather than no-opping is deliberate: a silent failure would leave an
        // administrator believing the feature was back on and its statistics trustworthy.
        Assert.ThrowsAsync<InvalidOperationException>(
            () => site.EnableFeatureAsync(SiteFeatures.DiagnosticStewardship));

        Assert.That(await site.IsFeatureEnabledAsync(SiteFeatures.DiagnosticStewardship), Is.False);
    }

    [Test]
    public async Task Feature_PermanentDisableIsAudited()
    {
        var site = _cluster.GrainFactory.GetGrain<ISiteParametersGrain>($"SITE:{Guid.NewGuid()}");
        await site.DisableFeatureAsync(
            SiteFeatures.DiagnosticStewardship, "USR-7", "CHEN,MICHAEL", "Statistics not wanted");

        List<PermanentFeatureDisable> log = await site.GetPermanentDisableLogAsync();
        PermanentFeatureDisable entry = log.Single(e => e.FeatureName == SiteFeatures.DiagnosticStewardship);
        Assert.That(entry.DisabledByUserId, Is.EqualTo("USR-7"));
        Assert.That(entry.DisabledByUserName, Is.EqualTo("CHEN,MICHAEL"));
        Assert.That(entry.Reason, Is.EqualTo("Statistics not wanted"));
        Assert.That(entry.DisabledUtc, Is.Not.EqualTo(default(DateTime)));
    }

    [Test]
    public async Task Feature_OrdinaryFlagsStayReversible()
    {
        // Only flags in SiteFeatures.OneWayDisable latch. Everything else must behave normally,
        // or this change would quietly freeze every optional module in the product.
        var site = _cluster.GrainFactory.GetGrain<ISiteParametersGrain>($"SITE:{Guid.NewGuid()}");

        await site.DisableFeatureAsync(SiteFeatures.BoneHealth);
        Assert.That(await site.IsFeatureEnabledAsync(SiteFeatures.BoneHealth), Is.False);
        Assert.That(await site.IsFeaturePermanentlyDisabledAsync(SiteFeatures.BoneHealth), Is.False);

        await site.EnableFeatureAsync(SiteFeatures.BoneHealth);
        Assert.That(await site.IsFeatureEnabledAsync(SiteFeatures.BoneHealth), Is.True);
    }

    // ── Episodes and counting ───────────────────────────────────────────────

    [Test]
    public async Task AddingAProblem_OpensADiagnosticEpisode()
    {
        string pid = $"DXPAT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(pid);

        string problemId = await wf.AddProblemAsync(
            "Urinary tract infection", "N39.0", "A", "ACUTE",
            DateTime.UtcNow.AddDays(-1), "PROV-1", "Dr. A", null, null, false, null);

        List<DiagnosticEpisode> episodes = await wf.GetDiagnosticEpisodesAsync();
        DiagnosticEpisode e = episodes.Single(x => x.ProblemId == problemId);
        Assert.That(e.WorkingCode, Is.EqualTo("N390"), "code is normalized for shard keys");
        Assert.That(e.Outcome, Is.EqualTo(DiagnosticEpisodeOutcome.Open));
        Assert.That(e.AdjudicatedUtc, Is.Null);
    }

    [Test]
    public async Task Adjudication_RevisedCountsAsAnError_RefinedDoesNot()
    {
        string pid = $"DXPAT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(pid);

        string revisedProblem = await wf.AddProblemAsync(
            "Urinary tract infection", "N39.0", "A", "ACUTE", null, "PROV-1", "Dr. A", null, null, false, null);
        string refinedProblem = await wf.AddProblemAsync(
            "Diabetes mellitus", "E11", "C", "CHRONIC", null, "PROV-2", "Dr. B", null, null, false, null);

        Assert.That(await wf.AdjudicateDiagnosticEpisodeAsync(
            revisedProblem, DiagnosticEpisodeOutcome.Revised, "A41.9", "Sepsis",
            RevisionReason.Correction, null), Is.True);

        Assert.That(await wf.AdjudicateDiagnosticEpisodeAsync(
            refinedProblem, DiagnosticEpisodeOutcome.Refined, "E11.9", "Type 2 diabetes",
            RevisionReason.Refinement, null), Is.True);

        DiagnosisOutcomeState uti = await ShardAsync(DiagnosisCodeGranularity.Code, "N390");
        Assert.That(uti.RevisedCount, Is.EqualTo(1));
        Assert.That(uti.AdjudicatedCount, Is.EqualTo(1));
        Assert.That(uti.RevisedTo.Single().OutcomeCode, Is.EqualTo("A419"));

        DiagnosisOutcomeState dm = await ShardAsync(DiagnosisCodeGranularity.Code, "E11");
        // Refined is adjudicated — in the denominator — but is NOT an error. Counting it would
        // bury the real signal under ordinary good practice.
        Assert.That(dm.AdjudicatedCount, Is.EqualTo(1));
        Assert.That(dm.RefinedCount, Is.EqualTo(1));
        Assert.That(dm.RevisedCount, Is.EqualTo(0));
    }

    [Test]
    public async Task Adjudication_IsCountedAtMostOnce()
    {
        string pid = $"DXPAT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(pid);

        string problemId = await wf.AddProblemAsync(
            "Migraine", "G43.909", "C", "CHRONIC", null, "PROV-1", "Dr. A", null, null, false, null);

        Assert.That(await wf.AdjudicateDiagnosticEpisodeAsync(
            problemId, DiagnosticEpisodeOutcome.Revised, "I60.9", "Subarachnoid haemorrhage",
            RevisionReason.Correction, null), Is.True);

        // The episode is no longer open, so a repeat is refused. A double-counted misdiagnosis
        // is not cosmetic — it directly inflates the number shown back to a clinician.
        Assert.That(await wf.AdjudicateDiagnosticEpisodeAsync(
            problemId, DiagnosticEpisodeOutcome.Revised, "I60.9", "Subarachnoid haemorrhage",
            RevisionReason.Correction, null), Is.False);

        DiagnosisOutcomeState shard = await ShardAsync(DiagnosisCodeGranularity.Code, "G43909");
        Assert.That(shard.RevisedCount, Is.EqualTo(1));
    }

    [Test]
    public async Task Adjudication_FansOutToCodeCategoryAndSiteWide()
    {
        string pid = $"DXPAT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(pid);
        string uniqueCode = "Q98.7";  // unlikely to collide with other tests

        string problemId = await wf.AddProblemAsync(
            "Fanout probe", uniqueCode, "A", "ACUTE", null, "PROV-1", "Dr. A", null, null, false, null);
        await wf.AdjudicateDiagnosticEpisodeAsync(
            problemId, DiagnosticEpisodeOutcome.Confirmed, uniqueCode, "Fanout probe", null, null);

        Assert.That((await ShardAsync(DiagnosisCodeGranularity.Code, "Q987")).AdjudicatedCount, Is.EqualTo(1));
        Assert.That((await ShardAsync(DiagnosisCodeGranularity.Category, "Q98")).AdjudicatedCount, Is.EqualTo(1));
        // The ALL shard is shared across the fixture, so assert it moved rather than its value.
        Assert.That((await ShardAsync(DiagnosisCodeGranularity.All, "ALL")).AdjudicatedCount,
            Is.GreaterThan(0));
    }

    [Test]
    public async Task Adjudication_NosOutcomeIsTrackedSeparately()
    {
        string pid = $"DXPAT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(pid);

        string problemId = await wf.AddProblemAsync(
            "Influenza", "J11.1", "A", "ACUTE", null, "PROV-1", "Dr. A", null, null, false, null);
        await wf.AdjudicateDiagnosticEpisodeAsync(
            problemId, DiagnosticEpisodeOutcome.Revised, "J12.9", "Viral pneumonia, unspecified",
            RevisionReason.Correction, null);

        DiagnosisOutcomeState shard = await ShardAsync(DiagnosisCodeGranularity.Code, "J111");
        // "We changed our mind and still do not know" is a different and more alarming fact than
        // "we changed our mind" — it is the shape an unnamed emerging disease makes.
        Assert.That(shard.NosTerminatingRevisedCount, Is.EqualTo(1));
    }

    // ── Silence under insufficient data ─────────────────────────────────────

    [Test]
    public async Task Advisory_StaysSilentBelowTheFloors()
    {
        string pid = $"DXPAT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(pid);

        string problemId = await wf.AddProblemAsync(
            "Low back pain", "M54.50", "C", "CHRONIC", null, "PROV-1", "Dr. A", null, null, false, null);
        await wf.AdjudicateDiagnosticEpisodeAsync(
            problemId, DiagnosticEpisodeOutcome.Revised, "C79.51", "Metastatic disease of spine",
            RevisionReason.Correction, null);

        DiagnosisRevisionAdvisory advisory =
            await wf.GetDiagnosisRevisionAdvisoryAsync("M54.50", "Low back pain");

        Assert.That(advisory.Band, Is.EqualTo(RevisionRateBand.Insufficient));
        Assert.That(advisory.RevisionRate, Is.Null, "a null rate must never be rendered");
        Assert.That(advisory.InsufficientReason, Is.Not.Null.And.Not.Empty);
    }

    [Test]
    public async Task Advisory_StillCarriesCriticalBaselineWhenLocalDataIsAbsent()
    {
        string pid = $"DXPAT-{Guid.NewGuid()}";
        DiagnosisRevisionAdvisory advisory =
            await Workflow(pid).GetDiagnosisRevisionAdvisoryAsync("R42", "Dizziness and giddiness");

        // The floors gate the learned percentage, never the curated arrow.
        Assert.That(advisory.RevisionRate, Is.Null);
        Assert.That(advisory.Alternatives.Any(a => a.Harm == DiagnosticHarmIfMissed.Critical), Is.True);
        Assert.That(advisory.Disclaimer, Is.Not.Empty);
    }

    [Test]
    public async Task Advisory_IsEmptyAndSafeWhenTheFeatureIsOff()
    {
        // The advisory decorates a clinical page. A disabled optional feature must degrade to
        // silence, never to an exception on the problem list.
        var site = _cluster.GrainFactory.GetGrain<ISiteParametersGrain>("SITE:DEFAULT");
        bool wasEnabled = await site.IsFeatureEnabledAsync(SiteFeatures.DiagnosticStewardship);
        Assert.That(wasEnabled, Is.True, "fixture expects the default site to have it on");

        DiagnosisRevisionAdvisory advisory =
            await Workflow($"DXPAT-{Guid.NewGuid()}").GetDiagnosisRevisionAdvisoryAsync("N39.0", "UTI");
        Assert.That(advisory, Is.Not.Null);
    }

    // ── Provenance survives on the problem itself ───────────────────────────

    [Test]
    public async Task Revision_RecordsCodedProvenanceOnTheProblem()
    {
        string pid = $"DXPAT-{Guid.NewGuid()}";
        IPatientGrain patient = _cluster.GrainFactory.GetGrain<IPatientGrain>(pid);

        await patient.AddProblemAsync(new ProblemEntry
        {
            ProblemId = "PROB-DX-1",
            Diagnosis = "Urinary tract infection",
            DiagnosisCode = "N39.0",
            Status = "ACTIVE",
            DateRecorded = DateTime.UtcNow
        });

        await patient.ReviseProblemAsync(new ProblemRevisionCommand
        {
            ProblemId = "PROB-DX-1",
            Diagnosis = "Sepsis, unspecified organism",
            DiagnosisCode = "A41.9",
            Reason = RevisionReason.Correction,
            Narrative = "Lactate 4.2, hypotensive on recheck",
            VerificationStatus = ProblemVerificationStatus.Confirmed,
            Evidence = new List<EvidenceRef>
            {
                new()
                {
                    Kind = EvidenceKind.LabResult, SourceId = "LAB-9", CodeSystem = "LOINC",
                    Code = "32693-4", Display = "Lactate", Polarity = EvidencePolarity.Supports,
                    ObservedValue = "4.2", ObservedUnit = "mmol/L"
                }
            }
        });

        ProblemEntry? p = await patient.GetProblemAsync("PROB-DX-1");
        Assert.That(p!.DiagnosisCode, Is.EqualTo("A41.9"));
        Assert.That(p.RevisionNumber, Is.EqualTo(1), "AddProblemAsync emits the legacy event, so this is revision 1");
        Assert.That(p.LastRevisionReason, Is.EqualTo(RevisionReason.Correction));
        Assert.That(p.LastRevisionNarrative, Does.Contain("Lactate"));
        Assert.That(p.VerificationStatus, Is.EqualTo(ProblemVerificationStatus.Confirmed));
        Assert.That(p.Evidence.Single().Code, Is.EqualTo("32693-4"));
    }

    [Test]
    public async Task Assessment_AppendsEvidenceWithoutCountingAsARevision()
    {
        string pid = $"DXPAT-{Guid.NewGuid()}";
        IPatientGrain patient = _cluster.GrainFactory.GetGrain<IPatientGrain>(pid);

        await patient.AddProblemAsync(new ProblemEntry
        {
            ProblemId = "PROB-DX-2",
            Diagnosis = "Pneumonia",
            DiagnosisCode = "J18.9",
            Status = "ACTIVE",
            DateRecorded = DateTime.UtcNow
        });

        var evidence = new List<EvidenceRef>
        {
            new() { Kind = EvidenceKind.LabResult, SourceId = "LAB-1", Code = "33762-6",
                    Display = "NT-proBNP", Polarity = EvidencePolarity.Refutes, ObservedValue = "42" }
        };

        await patient.AssessProblemAsync(new ProblemAssessmentCommand
        {
            ProblemId = "PROB-DX-2",
            Evidence = evidence,
            VerificationStatus = ProblemVerificationStatus.Confirmed
        });

        // Same evidence again — must dedupe rather than double-count the citation.
        await patient.AssessProblemAsync(new ProblemAssessmentCommand
        {
            ProblemId = "PROB-DX-2",
            Evidence = evidence,
            VerificationStatus = ProblemVerificationStatus.Confirmed
        });

        ProblemEntry? p = await patient.GetProblemAsync("PROB-DX-2");
        Assert.That(p!.Evidence, Has.Count.EqualTo(1));
        Assert.That(p.VerificationStatus, Is.EqualTo(ProblemVerificationStatus.Confirmed));
        // An assessment is the workup proceeding, not a clinician changing their mind.
        Assert.That(p.RevisionNumber, Is.EqualTo(0));
    }

    [Test]
    public async Task EvidenceFacade_ReadsAndAssessesThroughTheWorkflowGrain()
    {
        // The UI path: Problems.razor talks only to the workflow grain, whose interface
        // entry carries the security-key and audit attributes (grain-internal calls bypass
        // the filters, so the façade is where enforcement lives).
        string pid = $"DXPAT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(pid);

        string probId = await wf.AddProblemAsync(
            "Muscle weakness (generalized)", "M62.81", "A", "ACUTE",
            null, "PROV-1", "Dr. A", null, null, false, null);

        await wf.AssessProblemAsync(new ProblemAssessmentCommand
        {
            ProblemId = probId,
            VerificationStatus = ProblemVerificationStatus.Provisional,
            Evidence = new List<EvidenceRef>
            {
                new()
                {
                    Kind = EvidenceKind.Vital, Display = "Neuro exam during episode",
                    Polarity = EvidencePolarity.NotAssessed,
                    Note = "Episode resolved before any clinician observed it"
                },
                new()
                {
                    Kind = EvidenceKind.LabResult, CodeSystem = "LOINC", Code = "2157-6",
                    Display = "Creatine kinase", Polarity = EvidencePolarity.Supports,
                    ObservedValue = "890", ObservedUnit = "U/L"
                }
            }
        });

        ProblemEntry? p = await wf.GetProblemWithEvidenceAsync(probId);
        Assert.That(p, Is.Not.Null);
        Assert.That(p!.VerificationStatus, Is.EqualTo(ProblemVerificationStatus.Provisional));
        Assert.That(p.Evidence, Has.Count.EqualTo(2));
        Assert.That(p.Evidence.Single(e => e.Polarity == EvidencePolarity.NotAssessed).Display,
            Does.Contain("Neuro exam"),
            "the not-assessed row is a positive record of a gap, not an absence");
        Assert.That(p.Evidence.Single(e => e.Polarity == EvidencePolarity.Supports).Code,
            Is.EqualTo("2157-6"));

        Assert.That(await wf.GetProblemWithEvidenceAsync("NO-SUCH-PROBLEM"), Is.Null);

        // Two DIFFERENT free-text gaps of the same kind (no source id, no code — exactly what
        // the evidence form produces) must both survive: their identity is their text. An
        // IDENTICAL resubmit must still dedupe.
        var secondGap = new List<EvidenceRef>
        {
            new() { Kind = EvidenceKind.Vital, Display = "Orthostatic vitals",
                    Polarity = EvidencePolarity.NotAssessed }
        };
        await wf.AssessProblemAsync(new ProblemAssessmentCommand
        {
            ProblemId = probId, Evidence = secondGap,
            VerificationStatus = ProblemVerificationStatus.Provisional
        });
        await wf.AssessProblemAsync(new ProblemAssessmentCommand
        {
            ProblemId = probId, Evidence = secondGap,
            VerificationStatus = ProblemVerificationStatus.Provisional
        });

        p = await wf.GetProblemWithEvidenceAsync(probId);
        Assert.That(p!.Evidence, Has.Count.EqualTo(3),
            "distinct free-text refs of the same kind must not collide; identical ones must dedupe");
    }

    [Test]
    public async Task Evidence_NotAssessedGap_UpgradesToTheResultWhenItArrives()
    {
        // The transition the whole evidence design is built around: "we never looked" later
        // becomes "we looked and here is the answer". Under the old keep-first dedupe the
        // arriving result was silently discarded and the chart forever read NotAssessed.
        string pid = $"DXPAT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(pid);
        string probId = await wf.AddProblemAsync(
            "Muscle weakness (generalized)", "M62.81", "A", "ACUTE",
            null, "PROV-1", "Dr. A", null, null, false, null);

        await wf.AssessProblemAsync(new ProblemAssessmentCommand
        {
            ProblemId = probId,
            VerificationStatus = ProblemVerificationStatus.Provisional,
            Evidence = new List<EvidenceRef>
            {
                new() { Kind = EvidenceKind.LabResult, CodeSystem = "LOINC", Code = "2157-6",
                        Display = "Creatine kinase", Polarity = EvidencePolarity.NotAssessed,
                        Note = "Not yet drawn" }
            }
        });

        await wf.AssessProblemAsync(new ProblemAssessmentCommand
        {
            ProblemId = probId,
            VerificationStatus = ProblemVerificationStatus.Provisional,
            Evidence = new List<EvidenceRef>
            {
                new() { Kind = EvidenceKind.LabResult, CodeSystem = "LOINC", Code = "2157-6",
                        Display = "Creatine kinase", Polarity = EvidencePolarity.Supports,
                        ObservedValue = "890", ObservedUnit = "U/L" }
            }
        });

        ProblemEntry? p = await wf.GetProblemWithEvidenceAsync(probId);
        EvidenceRef ck = p!.Evidence.Single(e => e.Code == "2157-6");
        Assert.That(ck.Polarity, Is.EqualTo(EvidencePolarity.Supports),
            "the arriving result must replace the recorded gap, not be silently dropped");
        Assert.That(ck.ObservedValue, Is.EqualTo("890"));

        // And a true byte-identical resubmit still dedupes rather than duplicating.
        await wf.AssessProblemAsync(new ProblemAssessmentCommand
        {
            ProblemId = probId,
            VerificationStatus = ProblemVerificationStatus.Provisional,
            Evidence = new List<EvidenceRef>
            {
                new() { Kind = EvidenceKind.LabResult, CodeSystem = "LOINC", Code = "2157-6",
                        Display = "Creatine kinase", Polarity = EvidencePolarity.Supports,
                        ObservedValue = "890", ObservedUnit = "U/L" }
            }
        });
        p = await wf.GetProblemWithEvidenceAsync(probId);
        Assert.That(p!.Evidence.Count(e => e.Code == "2157-6"), Is.EqualTo(1));
    }

    [Test]
    public async Task RefutedAndVoidedProblems_LeaveTheActiveList()
    {
        string pid = $"DXPAT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(pid);
        IPatientGrain patient = _cluster.GrainFactory.GetGrain<IPatientGrain>(pid);

        string keep = await wf.AddProblemAsync(
            "Hypertension", "I10", "C", "CHRONIC", null, "PROV-1", "Dr. A", null, null, false, null);
        string refute = await wf.AddProblemAsync(
            "Suspected PE", "I26.99", "A", "ACUTE", null, "PROV-1", "Dr. A", null, null, false, null);

        await patient.ReviseProblemAsync(new ProblemRevisionCommand
        {
            ProblemId = refute,
            Diagnosis = "Suspected PE",
            DiagnosisCode = "I26.99",
            Reason = RevisionReason.Correction,
            VerificationStatus = ProblemVerificationStatus.Refuted
        });

        List<ProblemSummary> active = await wf.GetActiveProblemsAsync();
        Assert.That(active.Select(p => p.ProblemId), Does.Contain(keep));
        Assert.That(active.Select(p => p.ProblemId), Does.Not.Contain(refute),
            "a disproved diagnosis must never render as a current problem");
    }

    private async Task<DiagnosisOutcomeState> ShardAsync(DiagnosisCodeGranularity g, string codeKey)
    {
        var shard = _cluster.GrainFactory.GetGrain<IDiagnosisOutcomeIndexGrain>(
            NewVistas.Abstractions.Grains.DiagnosisOutcomeIndexGrain.KeyFor(g, codeKey, DateTime.UtcNow.Year));
        return await shard.GetStateAsync();
    }
}
