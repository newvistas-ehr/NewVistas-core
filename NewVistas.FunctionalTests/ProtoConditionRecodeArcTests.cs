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
/// The Patient-0 arc: an illness that matches nothing → a drafted cluster → promotion to a real
/// code → a recode that supersedes the working diagnosis without counting anyone wrong
/// (ADR-004 ↔ ADR-006).
/// </summary>
[TestFixture]
public class ProtoConditionRecodeArcTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup() => _cluster = SharedCluster.Instance;

    private IPatientWorkflowGrain Wf(string pid) => _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(pid);
    private IPatientGrain Pat(string pid) => _cluster.GrainFactory.GetGrain<IPatientGrain>(pid);
    private IProtoConditionGrain Proto(string id) => _cluster.GrainFactory.GetGrain<IProtoConditionGrain>($"PROTO:{id}");

    // ── The lab matcher fix ─────────────────────────────────────────────────

    [Test]
    public void LabAbsent_IsSatisfiedWhenNoResultOnFile()
    {
        // Previously unreachable: EvalLab returned early on a missing lab, so "we never tested"
        // and "we tested and it was negative" both scored zero and the record could not tell
        // them apart. That distinction is the whole signature of an undiagnosed illness.
        var proto = new ProtoConditionState
        {
            MatchThreshold = 0.5,
            Features =
            {
                new ProtoFeature
                {
                    FeatureId = "no-flu-test", Kind = ProtoFeatureKind.LabResult,
                    Code = "12345-6", Operator = ProtoFeatureOperator.Absent,
                    Rule = ProtoFeatureRule.Weighted, Weight = 1.0, Display = "Influenza PCR"
                }
            }
        };

        var noLab = new PatientFeatureSnapshot { PatientId = "P1", AssembledAt = DateTime.UtcNow };
        ProtoMatchResult r1 = ProtoConditionMatcher.Evaluate(proto, noLab);
        Assert.That(r1.Contributions.Single().Satisfied, Is.True, "no result on file must satisfy Absent");
        Assert.That(r1.Contributions.Single().Assessed, Is.True, "we checked the record — that is an assessment");

        var withLab = new PatientFeatureSnapshot
        {
            PatientId = "P1",
            AssembledAt = DateTime.UtcNow,
            Labs = { new SnapshotLab { Loinc = "12345-6", Value = "Negative", ResultedDate = DateTime.UtcNow } }
        };
        ProtoMatchResult r2 = ProtoConditionMatcher.Evaluate(proto, withLab);
        Assert.That(r2.Contributions.Single().Satisfied, Is.False, "a result on file must NOT satisfy Absent");
    }

    [Test]
    public void LabAbnormal_UsesTheLabsOwnFlag()
    {
        var proto = new ProtoConditionState
        {
            MatchThreshold = 0.5,
            Features =
            {
                new ProtoFeature
                {
                    FeatureId = "abn", Kind = ProtoFeatureKind.LabResult, Code = "777-7",
                    Operator = ProtoFeatureOperator.Abnormal,
                    Rule = ProtoFeatureRule.Weighted, Weight = 1.0, Display = "CRP"
                }
            }
        };

        var normal = new PatientFeatureSnapshot
        {
            PatientId = "P1", AssembledAt = DateTime.UtcNow,
            Labs = { new SnapshotLab { Loinc = "777-7", Value = "3", ResultedDate = DateTime.UtcNow, AbnormalFlag = LabAbnormalFlag.Normal } }
        };
        var high = new PatientFeatureSnapshot
        {
            PatientId = "P1", AssembledAt = DateTime.UtcNow,
            Labs = { new SnapshotLab { Loinc = "777-7", Value = "180", ResultedDate = DateTime.UtcNow, AbnormalFlag = LabAbnormalFlag.High } }
        };

        Assert.That(ProtoConditionMatcher.Evaluate(proto, normal).Contributions.Single().Satisfied, Is.False);
        Assert.That(ProtoConditionMatcher.Evaluate(proto, high).Contributions.Single().Satisfied, Is.True);
    }

    // ── Propose from a patient ──────────────────────────────────────────────

    [Test]
    public async Task ProposeFromPatient_CreatesADraftThatIsInvisibleToCharts()
    {
        string pid = $"PZERO-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Wf(pid);

        await wf.RecordSymptomObservationsAsync(new List<SymptomObservation>
        {
            new() { Code = SymptomCatalog.All.First().Code, Presence = SymptomPresence.Present, RecordedDate = DateTime.UtcNow }
        });

        string protoId = await wf.ProposeProtoConditionFromPatientAsync("Unexplained respiratory illness", "DOCTOR1");
        Assert.That(protoId, Is.Not.Empty);

        ProtoConditionState state = await Proto(protoId).GetAsync();
        Assert.That(state.Status, Is.EqualTo(ProtoConditionStatus.Draft), "a proposal must never be born Active");
        Assert.That(state.Origin, Is.EqualTo(ProtoConditionOrigin.DraftedFromPatient));
        Assert.That(state.DraftedFromPatientId, Is.EqualTo(pid));
        Assert.That(state.Description, Does.Contain("over-fitted"), "the n=1 warning must be on the draft");
        Assert.That(state.Features, Is.Not.Empty, "the draft should be seeded from the snapshot");
        Assert.That(state.Members.Any(m => m.PatientId == pid), Is.True, "the index case should be suggested in");

        // Invisible until an epidemiologist activates it — this is the whole "assemble, never
        // publish" line, and it is enforced by the existing active-index filter.
        List<ProtoConditionSummary> active = await _cluster.GrainFactory
            .GetGrain<IProtoConditionIndexGrain>("PROTOCONDITION-INDEX").GetActiveAsync();
        Assert.That(active.Any(s => s.ProtoConditionId == protoId), Is.False);

        CoverSheetState cover = await wf.GetCoverSheetAsync();
        Assert.That(cover.PrecautionBanners.Any(b => b.ProtoConditionId == protoId), Is.False,
            "a draft must not banner on any chart");
    }

    // ── Promotion → recode ──────────────────────────────────────────────────

    [Test]
    public async Task Promotion_RecodesWithSupersessionAndMovesNoErrorNumerator()
    {
        string pid = $"PZERO-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Wf(pid);

        // A working diagnosis, explicitly held as provisional — and asserted on the same
        // coded signal the cluster tracks. Supersession requires that overlap: certainty
        // alone must not retire a diagnosis (an unrelated "provisional anemia" would qualify
        // otherwise), so the arrangement mirrors a real chart where the provisional code and
        // the cluster grew from the same symptom.
        string sharedSymptomCode = SymptomCatalog.All.First().Code;
        string provisionalId = await wf.AddProblemAsync(
            "Viral pneumonia, unspecified", "J12.9", "A", "ACUTE", null, "PROV-1", "Dr. A", null, null, false, null);
        await Pat(pid).AssessProblemAsync(new ProblemAssessmentCommand
        {
            ProblemId = provisionalId,
            VerificationStatus = ProblemVerificationStatus.Provisional,
            Evidence = new List<EvidenceRef>
            {
                new() { Kind = EvidenceKind.Symptom, Code = sharedSymptomCode,
                        CodeSystem = "SNOMED", Display = "Presenting symptom",
                        Polarity = EvidencePolarity.Supports }
            }
        });

        string protoId = Guid.NewGuid().ToString();
        await Proto(protoId).CreateAsync("Novel respiratory cluster", "test", "EPI1");
        await Proto(protoId).AddOrUpdateFeatureAsync(new ProtoFeature
        {
            FeatureId = "F-SYMPTOM-1",
            Kind = ProtoFeatureKind.Symptom,
            Display = "Presenting symptom",
            Code = sharedSymptomCode,
            Operator = ProtoFeatureOperator.Present
        }, "EPI1");
        await Proto(protoId).ActivateAsync("EPI1");
        await wf.SuggestForProtoConditionAsync(protoId, "DOCTOR1");
        await wf.ConfirmProtoMembershipAsync(protoId, "EPI1");

        DiagnosisOutcomeState before = await SiteWideAsync();

        await Proto(protoId).PromoteAsync(
            "COVID-19", new List<string> { "U07.1" }, "840539006",
            new DateTime(2019, 12, 16), new List<string> { "US" }, "code issued", "EPI1");

        // The banner must SURVIVE while the recode is pending — otherwise an infection-control
        // patient has neither a precaution nor a coded diagnosis during the window.
        CoverSheetState midway = await wf.GetCoverSheetAsync();
        Assert.That(midway.PrecautionBanners.Any(b => b.ProtoConditionId == protoId), Is.True,
            "the precaution banner must not vanish between promotion and recode");

        string codedId = await wf.MigratePromotedProtoProblemAsync(protoId, "EPI1");

        // One active problem, and it is the coded one.
        List<ProblemSummary> active = await wf.GetActiveProblemsAsync();
        Assert.That(active.Any(p => p.ProblemId == codedId), Is.True);
        Assert.That(active.Any(p => p.ProblemId == provisionalId), Is.False,
            "the superseded working diagnosis must leave the active list");

        // Linked both ways, with structured provenance rather than a prose comment.
        ProblemEntry? old = await Pat(pid).GetProblemAsync(provisionalId);
        ProblemEntry? coded = await Pat(pid).GetProblemAsync(codedId);
        Assert.That(old!.SupersededByProblemId, Is.EqualTo(codedId));
        Assert.That(old.LastRevisionReason, Is.EqualTo(RevisionReason.Recode));
        Assert.That(coded!.SupersedesProblemId, Is.EqualTo(provisionalId));
        Assert.That(coded.Evidence.Any(e => e.Kind == EvidenceKind.ProtoCondition
                                            && e.SourceId == $"PROTO:{protoId}"), Is.True,
            "the cluster must be cited structurally, not in an English sentence");

        // Once recoded the banner stands down — the coded diagnosis carries the signal.
        CoverSheetState after = await wf.GetCoverSheetAsync();
        Assert.That(after.PrecautionBanners.Any(b => b.ProtoConditionId == protoId), Is.False);

        // A code-set change must move NOTHING that is reported back to clinicians.
        DiagnosisOutcomeState now = await SiteWideAsync();
        Assert.That(now.RevisedCount, Is.EqualTo(before.RevisedCount), "a recode is not a misdiagnosis");
        Assert.That(now.AdjudicatedCount, Is.EqualTo(before.AdjudicatedCount),
            "a recode must not enter the denominator either");
        Assert.That(now.RecodedCount, Is.GreaterThan(before.RecodedCount));
    }

    [Test]
    public async Task Promotion_AbstainsFromSupersedingWhenAmbiguous()
    {
        string pid = $"PZERO-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Wf(pid);

        // TWO provisional problems — the rule must refuse to guess which one the code replaces.
        foreach ((string dx, string code) in new[] { ("Viral pneumonia", "J12.9"), ("Fever of unknown origin", "R50.9") })
        {
            string id = await wf.AddProblemAsync(dx, code, "A", "ACUTE", null, "PROV-1", "Dr. A", null, null, false, null);
            await Pat(pid).AssessProblemAsync(new ProblemAssessmentCommand
            {
                ProblemId = id, VerificationStatus = ProblemVerificationStatus.Provisional
            });
        }

        string protoId = Guid.NewGuid().ToString();
        await Proto(protoId).CreateAsync("Ambiguous cluster", "test", "EPI1");
        await Proto(protoId).ActivateAsync("EPI1");
        await wf.SuggestForProtoConditionAsync(protoId, "DOCTOR1");
        await wf.ConfirmProtoMembershipAsync(protoId, "EPI1");
        await Proto(protoId).PromoteAsync("COVID-19", new List<string> { "U07.1" }, null, null, new List<string>(), "", "EPI1");

        string codedId = await wf.MigratePromotedProtoProblemAsync(protoId, "EPI1");

        // Three active problems: both provisionals survive untouched, plus the coded one.
        // Retiring the wrong active diagnosis is worse than leaving a human to reconcile.
        List<ProblemSummary> active = await wf.GetActiveProblemsAsync();
        Assert.That(active, Has.Count.EqualTo(3));
        Assert.That(active.Any(p => p.ProblemId == codedId), Is.True);

        ProtoConditionState st = await Proto(protoId).GetAsync();
        ProtoMigrationEntry entry = st.MigrationLog.Single(m => m.PatientId == pid);
        Assert.That(entry.Status, Is.EqualTo(ProtoMigrationStatus.Migrated));
        Assert.That(entry.Reason, Does.Contain("unambiguous"), "the abstention must be recorded, not silent");
    }

    [Test]
    public async Task Promotion_DoesNotSupersedeALegacyUnspecifiedProblem()
    {
        string pid = $"PZERO-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Wf(pid);

        // Unspecified is the legacy default on every imported row. It says nothing about whether
        // the clinician held the diagnosis provisionally, so it must never be superseded on.
        string legacyId = await wf.AddProblemAsync(
            "Hypertension", "I10", "C", "CHRONIC", null, "PROV-1", "Dr. A", null, null, false, null);

        string protoId = Guid.NewGuid().ToString();
        await Proto(protoId).CreateAsync("Cluster", "test", "EPI1");
        await Proto(protoId).ActivateAsync("EPI1");
        await wf.SuggestForProtoConditionAsync(protoId, "DOCTOR1");
        await wf.ConfirmProtoMembershipAsync(protoId, "EPI1");
        await Proto(protoId).PromoteAsync("COVID-19", new List<string> { "U07.1" }, null, null, new List<string>(), "", "EPI1");

        await wf.MigratePromotedProtoProblemAsync(protoId, "EPI1");

        ProblemEntry? legacy = await Pat(pid).GetProblemAsync(legacyId);
        Assert.That(legacy!.SupersededByProblemId, Is.Null, "an unrelated legacy problem must be left alone");
        Assert.That(legacy.Status, Is.EqualTo("ACTIVE"));
    }

    [Test]
    public async Task Promotion_NeverSupersedesAnUnrelatedWorkingDiagnosis()
    {
        string pid = $"PZERO-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Wf(pid);

        // The bug this guards against: a patient in a respiratory cluster whose ONLY working
        // hypothesis on the chart is something unrelated. Certainty alone must not select it.
        string anemiaId = await wf.AddProblemAsync(
            "Iron deficiency anemia", "D50.9", "A", "CHRONIC", null, "PROV-1", "Dr. A", null, null, false, null);
        await Pat(pid).AssessProblemAsync(new ProblemAssessmentCommand
        {
            ProblemId = anemiaId,
            VerificationStatus = ProblemVerificationStatus.Provisional,
            Evidence = new List<EvidenceRef>
            {
                // Evidence exists, but cites a signal the cluster does not track.
                new() { Kind = EvidenceKind.LabResult, Code = "718-7", CodeSystem = "LOINC",
                        Display = "Hemoglobin", Polarity = EvidencePolarity.Supports }
            }
        });

        string sharedSymptomCode = SymptomCatalog.All.First().Code;
        string protoId = Guid.NewGuid().ToString();
        await Proto(protoId).CreateAsync("Novel respiratory cluster", "test", "EPI1");
        await Proto(protoId).AddOrUpdateFeatureAsync(new ProtoFeature
        {
            FeatureId = "F-SYMPTOM-1", Kind = ProtoFeatureKind.Symptom,
            Display = "Presenting symptom", Code = sharedSymptomCode,
            Operator = ProtoFeatureOperator.Present
        }, "EPI1");
        await Proto(protoId).ActivateAsync("EPI1");
        await wf.SuggestForProtoConditionAsync(protoId, "DOCTOR1");
        await wf.ConfirmProtoMembershipAsync(protoId, "EPI1");
        await Proto(protoId).PromoteAsync(
            "COVID-19", new List<string> { "U07.1" }, "840539006",
            new DateTime(2019, 12, 16), new List<string> { "US" }, "code issued", "EPI1");

        await wf.MigratePromotedProtoProblemAsync(protoId, "EPI1");

        // The coded diagnosis lands; the unrelated anemia is untouched — two rows for a
        // human beats a machine retiring the wrong one.
        List<ProblemSummary> active = await wf.GetActiveProblemsAsync();
        Assert.That(active.Any(p => p.DiagnosisCode == "U07.1"), Is.True);
        Assert.That(active.Any(p => p.ProblemId == anemiaId), Is.True,
            "an unrelated provisional diagnosis must never be superseded by a cluster promotion");
        ProblemEntry? anemia = await wf.GetProblemWithEvidenceAsync(anemiaId);
        Assert.That(anemia!.SupersededByProblemId, Is.Null);
    }

    private async Task<DiagnosisOutcomeState> SiteWideAsync()
    {
        var shard = _cluster.GrainFactory.GetGrain<IDiagnosisOutcomeIndexGrain>(
            NewVistas.Abstractions.Grains.DiagnosisOutcomeIndexGrain.KeyFor(
                DiagnosisCodeGranularity.All, "ALL", DateTime.UtcNow.Year));
        return await shard.GetStateAsync();
    }
}
